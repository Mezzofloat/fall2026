"""Loopback-only service and separate provider diagnostic windows. Python stdlib only."""
import argparse
import base64
import hmac
import json
import os
from pathlib import Path
import secrets
import threading
import time
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from urllib.parse import urlsplit
from policy import InputError, strict_json
from providers import Providers, ProviderError, load_env
from presage import Presage
import imu

ROOT = Path(__file__).resolve().parent


class Bridge:
    def __init__(self, token=None):
        self.token = token or secrets.token_urlsafe(32)
        self.providers = Providers()
        self.presage = Presage()
        self.imu, self.imu_reader = imu.from_env()
        self.slots = threading.BoundedSemaphore(3)
        self.last_request = {}
        self.lock = threading.Lock()
        self.mic_name = ""
        self.voice_id = ""

    def status(self):
        return {"schema": 1, "bridge": "ready", "presage": self.presage.snapshot(), "imu": self.imu.snapshot(),
                "providers": self.providers.status.copy(), "microphone": self.mic_name,
                "voice_id": self.voice_id or self.providers.voice,
                "keys_configured": {k: bool(os.getenv(v)) for k,v in {"presage":"PRESAGE_API_KEY", "elevenlabs":"ELEVENLABS_API_KEY", "gemini":"GEMINI_API_KEY"}.items()}}


class Server(ThreadingHTTPServer):
    daemon_threads = True
    allow_reuse_address = True

    def __init__(self, address, bridge):
        self.bridge = bridge
        super().__init__(address, Handler)


class Handler(BaseHTTPRequestHandler):
    server_version = "BreatheLocal/1"

    def log_message(self, *_):
        pass  # Never log audio, transcripts, URLs, keys, device IDs, or provider bodies.

    def setup(self):
        super().setup()
        self.connection.settimeout(35)

    def respond(self, code, body, content_type="application/json"):
        if not isinstance(body, bytes):
            body = json.dumps(body, allow_nan=False).encode()
        self.send_response(code)
        self.send_header("Content-Type", content_type)
        self.send_header("Content-Length", str(len(body)))
        self.send_header("Cache-Control", "no-store")
        self.send_header("X-Content-Type-Options", "nosniff")
        self.send_header("Referrer-Policy", "no-referrer")
        self.send_header("Content-Security-Policy", "default-src 'self'; script-src 'self'; style-src 'self'; connect-src 'self'; media-src 'self' blob:; frame-ancestors 'none'; base-uri 'none'")
        self.end_headers()
        try:
            self.wfile.write(body)
        except (BrokenPipeError, ConnectionResetError):
            pass

    def allowed_host(self):
        port = self.server.server_port
        return self.headers.get("Host") in {f"127.0.0.1:{port}", f"localhost:{port}"}

    def authorized(self):
        origin = self.headers.get("Origin")
        if origin and origin not in {f"http://127.0.0.1:{self.server.server_port}", f"http://localhost:{self.server.server_port}"}:
            return False
        value = self.headers.get("X-Breathe-Token", "")
        return self.allowed_host() and hmac.compare_digest(value, self.server.bridge.token)

    def do_OPTIONS(self):
        self.respond(403, {"error":"Cross-origin access is disabled."})

    def do_GET(self):
        if not self.allowed_host():
            self.respond(403, {"error":"Loopback host required."}); return
        path = urlsplit(self.path).path
        if path in {"/", "/presage", "/voice"}:
            self.respond(200, (ROOT/"diagnostics.html").read_bytes(), "text/html; charset=utf-8"); return
        if path in {"/diagnostics.js", "/diagnostics.css"}:
            mime = "application/javascript" if path.endswith(".js") else "text/css"
            self.respond(200, (ROOT/path[1:]).read_bytes(), mime); return
        # Same-origin bootstrap cannot be read by arbitrary websites (no CORS, strict Host).
        if path == "/bootstrap":
            origin = self.headers.get("Sec-Fetch-Site", "")
            if origin not in {"same-origin", "none", ""}:
                self.respond(403, {"error":"Same-origin required."}); return
            self.respond(200, {"token":self.server.bridge.token}); return
        if not self.authorized():
            self.respond(403, {"error":"Local session token required."}); return
        try:
            if path == "/status":
                self.respond(200, self.server.bridge.status())
            elif path == "/presage/cameras":
                self.respond(200, {"cameras":self.server.bridge.presage.cameras()})
            elif path == "/voices":
                self.respond(200, {"voices":self.server.bridge.providers.voices()})
            else:
                self.respond(404, {"error":"Unknown endpoint."})
        except (InputError, ProviderError) as error:
            self.respond(503, {"error":str(error)})
        except Exception:
            self.respond(500, {"error":"Local service failed. No sensitive details were logged."})

    def do_POST(self):
        if not self.authorized():
            self.respond(403, {"error":"Local session token required."}); return
        path = urlsplit(self.path).path
        try:
            length = int(self.headers.get("Content-Length", "-1"))
        except ValueError:
            length = -1
        limit = 3_000_000 if path == "/transcribe" else 16384
        if not 0 <= length <= limit or self.headers.get("Transfer-Encoding"):
            self.respond(413, {"error":"Request missing length or too large."}); return
        bridge = self.server.bridge
        provider_call = path in {"/transcribe", "/dialogue", "/tts"}
        if provider_call and not bridge.slots.acquire(blocking=False):
            self.respond(429, {"error":"Service busy; try again shortly."}); return
        try:
            if provider_call:
                with bridge.lock:
                    now = time.monotonic()
                    if now - bridge.last_request.get(path, -100) < .4:
                        self.respond(429, {"error":"Please wait before trying again."}); return
                    bridge.last_request[path] = now
            raw = self.rfile.read(length)
            if len(raw) != length:
                raise InputError("Truncated request.")
            data = None if path == "/transcribe" else strict_json(raw.decode() or "{}")
            if path != "/transcribe" and not isinstance(data, dict):
                raise InputError("Expected a JSON object.")
            if path == "/presage/start":
                bridge.presage.start(data.get("camera_id"), data.get("consent"))
                result = bridge.presage.snapshot()
            elif path == "/presage/stop":
                bridge.presage.stop(); result = bridge.presage.snapshot()
            elif path == "/presage/pause":
                bridge.presage.stop(preserve_baseline=True); result = bridge.presage.snapshot()
            elif path == "/presage/resume":
                bridge.presage.resume(); result = bridge.presage.snapshot()
            elif path == "/presage/heartbeat":
                bridge.presage.heartbeat(); result = {"ok":True}
            elif path == "/presage/calibrate":
                bridge.presage.calibrate(); result = bridge.presage.snapshot()
            elif path == "/imu/calibrate":
                bridge.imu.calibrate(); result = bridge.imu.snapshot()
            elif path == "/settings":
                mic = data.get("microphone", "")
                voice = data.get("voice_id", "")
                if not isinstance(mic,str) or len(mic)>200 or not isinstance(voice,str) or len(voice)>80:
                    raise InputError("Invalid device settings.")
                bridge.mic_name, bridge.voice_id = mic, voice
                result = {"ok":True}
            elif path == "/transcribe":
                result = {"text":bridge.providers.transcribe(raw)}
            elif path == "/dialogue":
                result = bridge.providers.dialogue(data.get("act"), data.get("candidate"), data.get("text"), data.get("mood", "safe"), None if (type(data.get("page")) is int and data.get("page") < 0) else data.get("page"))
            elif path == "/tts":
                result = {"wav_base64":base64.b64encode(bridge.providers.speak(data.get("text"), data.get("voice_id") or bridge.voice_id or None, data.get("tone", "calm"))).decode()}
            else:
                self.respond(404, {"error":"Unknown endpoint."}); return
            self.respond(200, result)
        except (InputError, ValueError, UnicodeError, TypeError):
            self.respond(400, {"error":"Invalid input, silent audio, or unavailable camera. Check the diagnostic panel."})
        except ProviderError as error:
            if path == "/transcribe": bridge.providers.status["elevenlabs_stt"] = str(error)
            if path == "/tts": bridge.providers.status["elevenlabs_tts"] = str(error)
            self.respond(503, {"error":str(error)})
        except Exception:
            self.respond(500, {"error":"Local service failed. No sensitive details were logged."})
        finally:
            if provider_call:
                bridge.slots.release()


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--env-file")
    parser.add_argument("--port", type=int, default=8765)
    parser.add_argument("--connection", default=str(Path.home()/"Documents/Codex/breathe-runtime/connection.json"))
    args = parser.parse_args()
    load_env(args.env_file)
    bridge = Bridge()
    server = Server(("127.0.0.1", args.port), bridge)
    connection = Path(args.connection)
    connection.parent.mkdir(parents=True, exist_ok=True)
    fd = os.open(connection, os.O_WRONLY|os.O_CREAT|os.O_TRUNC, 0o600)
    with os.fdopen(fd, "w") as target:
        json.dump({"url":f"http://127.0.0.1:{server.server_port}", "token":bridge.token}, target)
    print(f"BREATHE diagnostics: http://127.0.0.1:{server.server_port}/presage and /voice", flush=True)
    def watchdog():
        while True:
            time.sleep(2)
            bridge.presage.snapshot()
    threading.Thread(target=watchdog, daemon=True).start()
    if bridge.imu_reader:
        bridge.imu_reader.start()
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        pass
    finally:
        bridge.presage.stop()
        if bridge.imu_reader:
            bridge.imu_reader.stop()
        server.server_close()


if __name__ == "__main__":
    main()
