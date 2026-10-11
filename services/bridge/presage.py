import json
import os
from pathlib import Path
import subprocess
import threading
import time
from policy import PulseState, InputError, strict_json


class Presage:
    def __init__(self, binary=None):
        self.binary = Path(binary or Path(__file__).resolve().parents[1]/"presage/build/BreathePresage.app/Contents/MacOS/breathe_presage")
        self.lock = threading.RLock()
        self.pulse = PulseState(time.time)
        self.process = None
        self.generation = 0
        self.message = "Stopped. Choose a USB camera and start measurement."
        self.validation = "No camera session"
        self.last_event = 0
        self.lease = 0
        self.selected_camera = ""
        self.camera_consent = False
        self.camera_fps = None

    def cameras(self):
        if not self.binary.exists():
            raise InputError("Presage helper has not been built. See the signing/setup guide.")
        try:
            run = subprocess.run([str(self.binary), "--list-cameras"], capture_output=True, timeout=8, text=True)
            for line in run.stdout.splitlines():
                if line.startswith('{"type":"cameras"'):
                    return strict_json(line)["cameras"]
        except (OSError, subprocess.TimeoutExpired, ValueError):
            pass
        raise InputError("Camera discovery failed. Check the Presage helper and macOS camera permissions.")

    def start(self, camera_id, consent, preserve_baseline=False):
        if consent is not True:
            raise InputError("Camera consent is required.")
        cameras = self.cameras()
        if camera_id not in [c["id"] for c in cameras]:
            raise InputError("Select an available camera; there is no automatic camera fallback.")
        if not os.getenv("PRESAGE_API_KEY"):
            raise InputError("Presage API key missing.")
        previous_baseline = self.pulse.baseline if preserve_baseline else None
        self.stop()
        with self.lock:
            self.selected_camera = camera_id
            self.camera_consent = True
            self.pulse.baseline = previous_baseline
            generation = self.generation
            self.message = "Starting Presage. macOS may request camera access."
            self.lease = time.monotonic() + 15
            try:
                # Restrict the child environment: never give Presage the other provider keys.
                env = {k:v for k,v in os.environ.items() if k not in {"GEMINI_API_KEY", "ELEVENLABS_API_KEY"}}
                self.process = subprocess.Popen([str(self.binary), "--camera", camera_id], stdout=subprocess.PIPE, stderr=subprocess.DEVNULL, text=True, env=env)
            except OSError:
                raise InputError("Could not launch the Presage helper.") from None
            threading.Thread(target=self.consume, args=(self.process, generation), daemon=True).start()

    def consume(self, process, generation):
        try:
            for line in process.stdout:
                if len(line) > 16384:
                    continue
                try:
                    event = strict_json(line)
                except ValueError:
                    continue
                with self.lock:
                    if generation != self.generation:
                        return
                    kind = event.get("type")
                    self.last_event = time.time()
                    if kind == "pulse":
                        self.pulse.ingest(event)
                    elif kind == "validation":
                        self.pulse.validation_ok = event.get("ok") is True
                        self.validation = str(event.get("hint", ""))[:200]
                        if not self.pulse.validation_ok:
                            self.pulse.valid = False
                    elif kind == "error":
                        self.message = str(event.get("message", "Presage failed"))[:200]
                        self.pulse.valid = False
                    elif kind == "fps":
                        fps = event.get("fps")
                        if type(fps) in (int, float) and 0 <= fps < 1000:
                            self.camera_fps = {"fps": round(float(fps), 1), "width": event.get("width"), "height": event.get("height")}
                    elif kind == "started":
                        self.message = "Camera running. Sit centered in steady light."
        finally:
            with self.lock:
                if generation == self.generation:
                    self.pulse.valid = False
                    if not self.message.startswith("Presage"):
                        self.message = "Presage stopped. Check signing, camera permission, key, and connection."

    def stop(self, preserve_baseline=False):
        with self.lock:
            baseline = self.pulse.baseline if preserve_baseline else None
            self.generation += 1
            process, self.process = self.process, None
            self.pulse.reset()
            self.pulse.baseline = baseline
            self.camera_fps = None
            self.message = "Stopped"
            self.validation = "No camera session"
        if process and process.poll() is None:
            process.terminate()
            try:
                process.wait(timeout=3)
            except subprocess.TimeoutExpired:
                process.kill(); process.wait(timeout=2)

    def resume(self):
        with self.lock:
            if self.process is not None and self.process.poll() is None:
                return
            camera, consent = self.selected_camera, self.camera_consent
        if not camera or not consent:
            raise InputError("Choose and start a camera in the Presage diagnostic window first.")
        self.start(camera, consent, preserve_baseline=True)

    def heartbeat(self):
        with self.lock:
            self.lease = time.monotonic() + 15

    def snapshot(self):
        with self.lock:
            alive = self.process is not None and self.process.poll() is None
            expired = alive and time.monotonic() > self.lease
        if expired:
            self.stop()
        with self.lock:
            state = self.pulse.snapshot()
            state.update(running=self.process is not None and self.process.poll() is None,
                         message=self.message, validation=self.validation,
                         helper_built=self.binary.exists(), camera=self.camera_fps)
            return state

    def calibrate(self):
        with self.lock:
            self.pulse.begin_calibration()
