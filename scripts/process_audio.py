#!/usr/bin/env python3
"""Normalize and trim the raw audio in the repo root into game-ready WAVs (copy-only: the originals are never modified).

Usage: python3 scripts/process_audio.py            # process everything listed below that exists
Needs ffmpeg + numpy. Safe to re-run; outputs are deterministic.

Loudness plan (integrated LUFS): music sits well below speech so Vale's voice is always on top.
  music  safe -26 / horror -25   laugh -20   jumpscare -12 (one deliberate loud cue)   sfx -16..-24
Voice (ElevenLabs) is levelled at runtime to about -16 LUFS equivalent (see VoiceLevel in LiveServices).
Looping clips are cross-faded at the seam (equal power, 60 ms) so the loop point does not click.
"""
import json
import pathlib
import re
import subprocess
import sys

import numpy as np

ROOT = pathlib.Path(__file__).resolve().parent.parent
MUSIC = ROOT / "game/Assets/_Breathe/Audio/Music"
SFX = ROOT / "game/Assets/_Breathe/Audio/SFX"
RATE = 48000

# name: (source, destination dir, lufs, true peak dBTP, channels, options)
JOBS = {
    "safe":       (MUSIC / "safe.mp3",   MUSIC, -26, -2.0, 2, {"trim_silence": True, "loop": True}),
    "horror":     (ROOT / "horror.mp3",  MUSIC, -25, -2.0, 2, {"loop": True}),
    "laugh":      (MUSIC / "laugh.m4a",  MUSIC, -20, -2.0, 2, {}),
    "jumpscare":  (ROOT / "jumpscare.mp3", SFX, -12, -1.0, 2, {}),
    "door-open":  (ROOT / "door-open.mp3", SFX, -20, -2.0, 1, {}),
    "door-break": (ROOT / "door-break.mp3", SFX, -17, -1.5, 1, {}),
    "knife-cut":  (ROOT / "knife-cut.mp3", SFX, -18, -1.5, 1, {}),
    "knife-swing": (ROOT / "knife-swing.mp3", SFX, -20, -2.0, 1, {}),
    "paper":      (ROOT / "paper.mp3",   SFX, -22, -2.0, 1, {"keep_seconds": .97, "fade_out": .09}),
    "walking":    (ROOT / "walking.mp3", SFX, -26, -2.0, 1, {"loop": True, "trim_silence": True}),
}


def decode(path, channels):
    raw = subprocess.run(["ffmpeg", "-v", "error", "-i", str(path), "-ac", str(channels), "-ar", str(RATE), "-f", "f32le", "-"],
                         capture_output=True, check=True).stdout
    return np.frombuffer(raw, dtype=np.float32).reshape(-1, channels).copy()


def trim_silence(x, threshold_db=-55.0):
    level = 10 ** (threshold_db / 20)
    loud = np.where(np.abs(x).max(axis=1) > level)[0]
    return x if len(loud) == 0 else x[loud[0]:loud[-1] + 1]


def crossfade_loop(x, seconds=.06):
    n = int(RATE * seconds)
    if len(x) <= 4 * n:
        return x
    t = np.linspace(0, 1, n, dtype=np.float32)[:, None]
    head = x[:n] * np.sqrt(t) + x[-n:] * np.sqrt(1 - t)
    return np.concatenate([head, x[n:-n]])


def write_wav(path, x):
    subprocess.run(["ffmpeg", "-v", "error", "-y", "-f", "f32le", "-ar", str(RATE), "-ac", str(x.shape[1]), "-i", "-",
                    "-c:a", "pcm_s16le", str(path)], input=x.astype("<f4").tobytes(), check=True)


def loudnorm(path_in, path_out, lufs, peak):
    flt = f"loudnorm=I={lufs}:TP={peak}:LRA=11"
    probe = subprocess.run(["ffmpeg", "-hide_banner", "-nostats", "-i", str(path_in), "-af", flt + ":print_format=json", "-f", "null", "-"],
                           capture_output=True, text=True).stderr
    m = json.loads(re.search(r"\{[^{}]*\"input_i\"[^{}]*\}", probe, re.S).group(0))
    second = (f"{flt}:measured_I={m['input_i']}:measured_TP={m['input_tp']}:measured_LRA={m['input_lra']}"
              f":measured_thresh={m['input_thresh']}:offset={m['target_offset']}:linear=true")
    subprocess.run(["ffmpeg", "-v", "error", "-y", "-i", str(path_in), "-af", second, "-ar", str(RATE), "-c:a", "pcm_s16le", str(path_out)], check=True)
    return m


def main():
    tmp = ROOT / "out" / "audio-tmp"
    tmp.mkdir(parents=True, exist_ok=True)
    for name, (src, dst_dir, lufs, peak, channels, opt) in JOBS.items():
        if not src.exists():
            print(f"skip {name}: {src} not found")
            continue
        x = decode(src, channels)
        if opt.get("trim_silence"):
            x = trim_silence(x)
        if "keep_seconds" in opt:
            x = x[: int(RATE * opt["keep_seconds"])]
        if "fade_out" in opt:
            n = int(RATE * opt["fade_out"])
            x[-n:] *= np.linspace(1, 0, n, dtype=np.float32)[:, None] ** 2
        if opt.get("loop"):
            x = crossfade_loop(x)
        dst_dir.mkdir(parents=True, exist_ok=True)
        stage = tmp / f"{name}.wav"
        write_wav(stage, x)
        out = dst_dir / f"{name}-norm.wav"
        info = loudnorm(stage, out, lufs, peak)
        print(f"{name:12s} {len(x) / RATE:7.2f}s  {float(info['input_i']):6.1f} -> {lufs} LUFS  -> {out.relative_to(ROOT)}")


if __name__ == "__main__":
    sys.exit(main())
