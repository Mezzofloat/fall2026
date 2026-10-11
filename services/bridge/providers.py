"""Provider transport. Keys stay here; errors never contain provider bodies or URLs."""
import io
import json
import os
import re
import socket
import threading
import urllib.error
import urllib.request
import uuid
import wave
from policy import SYSTEM_PROMPT, ACT_INTENTS, TONES, VERDICTS, InputError, context, validate_reply, fallback, local_intent


class ProviderError(Exception):
    def __init__(self, provider, category, status=0):
        self.provider, self.category, self.status = provider, category, status
        super().__init__(f"{provider}: {category}" + (f" (HTTP {status})" if status else ""))


def load_env(path):
    if not path:
        return
    with open(path, encoding="utf-8") as source:
        for line in source:
            if "=" in line and not line.lstrip().startswith("#"):
                name, value = line.strip().split("=", 1)
                if name in {"PRESAGE_API_KEY", "ELEVENLABS_API_KEY", "GEMINI_API_KEY", "GEMINI_MODEL", "ELEVENLABS_TTS_MODEL", "ELEVENLABS_STT_MODEL", "ELEVENLABS_VOICE_ID", "IMU_HUB", "IMU_DEFAULT_HAND"}:
                    os.environ[name] = value.strip().strip('"').strip("'")


class Providers:
    def __init__(self):
        self.voice = os.getenv("ELEVENLABS_VOICE_ID", "")
        self.gemini_model = os.getenv("GEMINI_MODEL", "gemini-3.5-flash-lite")
        self.status = {"elevenlabs_stt": "Not tested", "elevenlabs_tts": "Not tested", "gemini": "Not tested"}
        self.locks = {k: threading.Lock() for k in self.status}

    def request(self, provider, url, data=None, headers=None, limit=4_000_000):
        key = os.getenv("GEMINI_API_KEY" if provider == "gemini" else "ELEVENLABS_API_KEY", "")
        if not key:
            raise ProviderError(provider, "API key missing")
        headers = dict(headers or {})
        headers["x-goog-api-key" if provider == "gemini" else "xi-api-key"] = key
        req = urllib.request.Request(url, data=data, headers=headers)
        try:
            with urllib.request.urlopen(req, timeout=25) as response:
                body = response.read(limit + 1)
                if len(body) > limit:
                    raise ProviderError(provider, "Response too large")
                return body
        except urllib.error.HTTPError as error:
            categories = {400: "Invalid request or key", 401: "Authentication failed", 403: "Permission denied", 404: "Model or voice unavailable", 429: "Quota or rate limit"}
            raise ProviderError(provider, categories.get(error.code, "Provider unavailable"), error.code) from None
        except (urllib.error.URLError, TimeoutError, socket.timeout, ConnectionError):
            raise ProviderError(provider, "Network unavailable or timed out") from None

    def voices(self):
        raw = json.loads(self.request("elevenlabs", "https://api.elevenlabs.io/v1/voices", limit=1_000_000))
        choices = [{"id": v["voice_id"], "name": v["name"]} for v in raw.get("voices", []) if re.fullmatch(r"[A-Za-z0-9_-]{1,80}", v.get("voice_id", ""))]
        if not self.voice and choices:
            self.voice = choices[0]["id"]
        return choices

    def transcribe(self, audio):
        # Only PCM WAV is accepted; diagnostic browsers and Unity use the same encoding.
        try:
            with wave.open(io.BytesIO(audio)) as wav:
                if wav.getnchannels() != 1 or wav.getsampwidth() != 2 or not 8000 <= wav.getframerate() <= 48000 or not .15 <= wav.getnframes()/wav.getframerate() <= 31:
                    raise InputError("Use mono PCM audio between 0.15 and 30 seconds.")
                raw = wav.readframes(wav.getnframes())
                if len(raw) != wav.getnframes()*2:
                    raise InputError("Audio is truncated.")
                # Silence does not count as an answer and should not consume an API request.
                import array
                samples = array.array("h", raw)
                if not samples or max(abs(v) for v in samples) < 90:
                    raise InputError("No speech detected. Check the selected microphone.")
        except (wave.Error, EOFError):
            raise InputError("Invalid WAV audio.") from None
        boundary = "breathe" + uuid.uuid4().hex
        parts = []
        for name, value in {"model_id": os.getenv("ELEVENLABS_STT_MODEL", "scribe_v2"), "tag_audio_events": "false", "diarize": "false"}.items():
            parts.append(f'--{boundary}\r\nContent-Disposition: form-data; name="{name}"\r\n\r\n{value}\r\n'.encode())
        parts.append(f'--{boundary}\r\nContent-Disposition: form-data; name="file"; filename="answer.wav"\r\nContent-Type: audio/wav\r\n\r\n'.encode()+audio+b"\r\n")
        parts.append(f"--{boundary}--\r\n".encode())
        with self.locks["elevenlabs_stt"]:
            result = json.loads(self.request("elevenlabs", "https://api.elevenlabs.io/v1/speech-to-text", b"".join(parts), {"Content-Type": "multipart/form-data; boundary="+boundary}))
        from policy import clean_transcript
        text = clean_transcript(result.get("text", ""))
        self.status["elevenlabs_stt"] = "Live transcription succeeded"
        return text

    # Tone is a closed enum chosen by game code/validated model output, never free text from the player.
    TONE_TAGS = {"calm": "", "sarcastic": "[sarcastic] ", "menacing": "[menacing, low voice] ", "furious": "[shouting angrily] "}
    TONE_STABILITY = {"calm": .6, "sarcastic": .4, "menacing": .3, "furious": .0}

    def tts_model(self):
        return os.getenv("ELEVENLABS_TTS_MODEL", "eleven_v4_turbo")

    def speak(self, text, voice=None, tone="calm"):
        if not isinstance(text, str) or not 1 <= len(text) <= 350 or any(ord(c) < 32 and c not in "\n\t" for c in text):
            raise InputError("Speech text must be 1–350 printable characters.")
        if tone not in TONES:
            raise InputError("Unknown speech tone.")
        with self.locks["elevenlabs_tts"]:
            if not self.voice and not voice:
                self.voices()
            voice = voice or self.voice
            if not voice or not re.fullmatch(r"[A-Za-z0-9_-]{1,80}", voice):
                raise InputError("Select an available ElevenLabs voice.")
            model = self.tts_model()
            tagged = model.startswith(("eleven_v3", "eleven_v4"))   # only these read audio tags as direction, not as words
            stability = self.TONE_STABILITY[tone]
            settings = {"stability": stability if tagged else min(.75, .35 + stability * .6), "similarity_boost": .7}
            if model == "eleven_multilingual_v2":
                settings["style"] = {"calm": 0, "sarcastic": .35, "menacing": .6, "furious": .9}[tone]
            payload = {"text": (self.TONE_TAGS[tone] if tagged else "") + text, "model_id": model, "voice_settings": settings}
            pcm = self.request("elevenlabs", f"https://api.elevenlabs.io/v1/text-to-speech/{voice}?output_format=pcm_24000", json.dumps(payload).encode(), {"Content-Type": "application/json"})
            if not pcm or len(pcm)%2:
                raise ProviderError("elevenlabs", "Invalid audio response")
        target = io.BytesIO()
        with wave.open(target, "wb") as wav:
            wav.setnchannels(1); wav.setsampwidth(2); wav.setframerate(24000); wav.writeframes(pcm)
        self.status["elevenlabs_tts"] = "Live speech generation succeeded (%s, %s)" % (model, tone)
        return target.getvalue()

    def dialogue(self, act, candidate, text, mood="safe", page=None):
        data = context(act, candidate, text, mood, page)
        urgent = local_intent(data["untrusted_transcript"])
        if urgent:
            return {"intent": urgent, "reply": "The interview is paused." if urgent == "pause" else "The interview is over.", "tone": "calm", "verdict": "none", "anger": .05, "fallback": False, "provider_status": "Local stop control"}
        schema = {"type": "OBJECT", "properties": {"intent": {"type": "STRING", "enum": sorted(ACT_INTENTS[act])}, "reply": {"type": "STRING"},
                                                 "tone": {"type": "STRING", "enum": list(TONES)}, "verdict": {"type": "STRING", "enum": list(VERDICTS)}}, "required": ["intent", "reply", "tone", "verdict"]}
        payload = {"systemInstruction": {"parts": [{"text": SYSTEM_PROMPT}]}, "contents": [{"role": "user", "parts": [{"text": json.dumps(data)}]}], "generationConfig": {"temperature": .55, "maxOutputTokens": 256, "responseMimeType": "application/json", "responseSchema": schema}}
        try:
            if not re.fullmatch(r"[A-Za-z0-9._-]+", self.gemini_model):
                raise InputError("Invalid configured model.")
            with self.locks["gemini"]:
                raw = json.loads(self.request("gemini", f"https://generativelanguage.googleapis.com/v1beta/models/{self.gemini_model}:generateContent", json.dumps(payload).encode(), {"Content-Type": "application/json"}, limit=100_000))
            candidates = raw.get("candidates", [])
            if not candidates or candidates[0].get("finishReason") != "STOP":
                raise InputError("Model response blocked or incomplete.")
            text = "".join(part.get("text", "") for part in candidates[0]["content"]["parts"] if not part.get("thought"))
            answer = validate_reply(text, act)
            answer.update(fallback=False, provider_status="Live Gemini reply")
            self.status["gemini"] = "Live structured dialogue succeeded"
            return answer
        except (ProviderError, InputError, ValueError, KeyError, TypeError, AttributeError, IndexError) as error:
            answer = fallback(act, data["untrusted_transcript"], mood)
            message = str(error) if isinstance(error, ProviderError) else "Invalid or incomplete model reply"
            self.status["gemini"] = message
            answer.update(fallback=True, provider_status=message)
            return answer
