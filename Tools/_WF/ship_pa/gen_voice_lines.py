#!/usr/bin/env python3
"""Renders the ship PA voice lines and runs them through the PA filter.

The line texts are read from Resources/Locale/en-US/_WF/ship-pa.ftl, so the voice always says what
the chat shows. Each line gets the attention chime in front of it unless --no-chime is given.

Engines (tool locations default to %LOCALAPPDATA%\\Wolfgate\\<tool>, overridable by env vars):
  espeak  formant synthesis (Dr. Sbaitso / DECtalk family). winget install --id eSpeak-NG.eSpeak-NG
  sapi    the voices built into Windows (Zira, Hazel, David). Output licensing is Microsoft's, so keep
          these to private servers.
  piper   Piper neural voices. PIPER_HOME holds piper/piper.exe (github.com/rhasspy/piper releases) and
          voices/<name>.onnx + .onnx.json (huggingface.co/rhasspy/piper-voices).
  kokoro  Kokoro-82M neural voices. KOKORO_HOME holds a venv with kokoro-onnx + soundfile, plus
          kokoro-v1.0.onnx and voices-v1.0.bin (github.com/thewh1teagle/kokoro-onnx releases). Voices
          are ids like af_heart or blends like af_heart:0.6,af_kore:0.4.

--vox synthesises every word on its own and splices them back together, which is how the Half-Life
announcer was built and gives that clipped intercom cadence.
--flat squeezes the pitch contour towards one note with Praat (PRAAT or Wolfgate\\praat\\Praat.exe),
which is the deadpan-announcer effect; --flat-factor 0.25 keeps a quarter of the movement.

Usage:
    python Tools/_WF/ship_pa/gen_voice_lines.py                                  # espeak "sbaitso" into Resources/Audio/_WF/ShipPa
    python Tools/_WF/ship_pa/gen_voice_lines.py --voice ljspeech --vox           # engine inferred from the voice name
    python Tools/_WF/ship_pa/gen_voice_lines.py --voice af_heart --flat --out samples --reel

ffmpeg must be on PATH.
"""

import argparse
import json
import os
import re
import shutil
import subprocess
import sys
import tempfile

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from pa_filter import filter_file  # noqa: E402

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
FTL = os.path.join(REPO, "Resources", "Locale", "en-US", "_WF", "ship-pa.ftl")
DEFAULT_OUT = os.path.join(REPO, "Resources", "Audio", "_WF", "ShipPa")
DEFAULT_CHIME = os.path.join(REPO, "Resources", "Audio", "Announcements", "attention.ogg")

LOCAL = os.path.join(os.environ.get("LOCALAPPDATA", ""), "Wolfgate")
PIPER_HOME = os.environ.get("PIPER_HOME") or os.path.join(LOCAL, "piper")
KOKORO_HOME = os.environ.get("KOKORO_HOME") or os.path.join(LOCAL, "kokoro")
PRAAT = os.environ.get("PRAAT") or os.path.join(LOCAL, "praat", "Praat.exe")
KOKORO_RENDER = os.path.join(HERE, "kokoro_render.py")
FLATTEN_SCRIPT = os.path.join(HERE, "flatten.praat")

# Output file name -> Fluent key holding the text.
LINES = {
    "code_green": "ship-alert-code-green-announcement",
    "code_yellow": "ship-alert-code-yellow-announcement",
    "code_red": "ship-alert-code-red-announcement",
    "code_black": "ship-alert-code-black-announcement",
    "general_quarters": "ship-pa-general-quarters-announcement",
    "general_quarters_secure": "ship-pa-general-quarters-secure",
}

# eSpeak NG is a formant synthesiser like the DOS-era engines, so the raw voice is already the
# effect; these just pick the flavour. -s words/min, -p pitch 0-99, -g word gap, -a amplitude.
ESPEAK_VOICES = {
    # The default voice, slowed down: closest to Dr. Sbaitso.
    "sbaitso": ["-v", "en-us", "-s", "150", "-p", "40", "-g", "4", "-a", "180"],
    # Female formant variant: same engine, higher pitch.
    "sbaitso-f": ["-v", "en-us+f3", "-s", "150", "-p", "60", "-g", "4", "-a", "180"],
    # Klatt variant: the DECtalk / Hawking lineage.
    "dectalk": ["-v", "en-us+klatt3", "-s", "160", "-p", "35", "-g", "4", "-a", "180"],
}

# Short names for Piper voice models; anything else is taken as a model name or .onnx path.
PIPER_VOICES = {
    "ljspeech": "en_US-ljspeech-medium",  # public domain, American
    "jenny": "en_GB-jenny_dioco-medium",  # Jenny (Dioco), commercial use allowed with attribution
    "amy": "en_US-amy-medium",
}

# Windows' own SAPI voices (Settings > Time & language > Speech to add more). Unit-selection voices,
# so the delivery is flat and slightly clipped on its own. Rate runs -10..10.
SAPI_VOICES = {
    "zira": ("Microsoft Zira Desktop", -2),
    "hazel": ("Microsoft Hazel Desktop", -2),
    "david": ("Microsoft David Desktop", -2),
}

# Pauses spliced in by --vox, in seconds.
VOX_WORD_GAP = 0.12
VOX_PAUSES = {",": 0.25, ";": 0.3, ":": 0.3, ".": 0.45, "!": 0.45, "?": 0.45}


def find_exe(explicit: str | None, name: str, fallback: str) -> str | None:
    candidates = [explicit, shutil.which(name), fallback]
    return next((c for c in candidates if c and os.path.isfile(c)), None)


def find_model(voice: str) -> str | None:
    name = PIPER_VOICES.get(voice, voice)
    candidates = [name, os.path.join(PIPER_HOME, "voices", name), os.path.join(PIPER_HOME, "voices", f"{name}.onnx")]
    return next((c for c in candidates if os.path.isfile(c)), None)


def infer_engine(voice: str) -> str:
    if voice in ESPEAK_VOICES:
        return "espeak"
    if voice in SAPI_VOICES:
        return "sapi"
    if voice[:3] in ("af_", "bf_", "am_", "bm_") or ":" in voice:
        return "kokoro"
    return "piper"


def read_lines() -> dict[str, str]:
    with open(FTL, encoding="utf-8") as f:
        entries = dict(re.findall(r"^([a-z0-9-]+)\s*=\s*(.+?)\s*$", f.read(), re.MULTILINE))

    missing = [key for key in LINES.values() if key not in entries]
    if missing:
        raise SystemExit(f"missing in {FTL}: {', '.join(missing)}")

    return {name: entries[key] for name, key in LINES.items()}


class Synth:
    """One configured engine; renders text to wav files. Kokoro queues its work and renders on flush()."""

    def __init__(self, args: argparse.Namespace):
        self.engine = args.engine
        self.length_scale = args.length_scale
        self.jobs: list[dict[str, str]] = []

        if self.engine == "espeak":
            if args.voice not in ESPEAK_VOICES:
                raise SystemExit(f"espeak voices: {', '.join(sorted(ESPEAK_VOICES))}")
            self.exe = find_exe(args.espeak, "espeak-ng", r"C:\Program Files\eSpeak NG\espeak-ng.exe")
            if self.exe is None:
                raise SystemExit("espeak-ng not found; winget install --id eSpeak-NG.eSpeak-NG")
            self.flags = ESPEAK_VOICES[args.voice]
        elif self.engine == "sapi":
            if args.voice not in SAPI_VOICES:
                raise SystemExit(f"sapi voices: {', '.join(sorted(SAPI_VOICES))}")
            self.sapi_voice, self.sapi_rate = SAPI_VOICES[args.voice]
        elif self.engine == "kokoro":
            self.exe = os.path.join(KOKORO_HOME, "venv", "Scripts", "python.exe")
            if not os.path.isfile(self.exe):
                raise SystemExit(f"no Kokoro venv at {self.exe}")
            self.voice = args.voice
        else:
            self.exe = find_exe(args.piper, "piper", os.path.join(PIPER_HOME, "piper", "piper.exe"))
            if self.exe is None:
                raise SystemExit(f"piper.exe not found under {PIPER_HOME}")
            self.model = find_model(args.voice)
            if self.model is None:
                raise SystemExit(f"no Piper model for '{args.voice}' under {os.path.join(PIPER_HOME, 'voices')}")
            self.speaker = args.speaker

    def render(self, text: str, wav: str, sentence_silence: float) -> None:
        if self.engine == "espeak":
            subprocess.run([self.exe, *self.flags, "-w", wav, text], check=True)
            return

        if self.engine == "sapi":
            script = (
                "Add-Type -AssemblyName System.Speech;"
                "$s = New-Object System.Speech.Synthesis.SpeechSynthesizer;"
                f"$s.SelectVoice('{self.sapi_voice}'); $s.Rate = {self.sapi_rate};"
                f"$s.SetOutputToWaveFile('{wav}'); $s.Speak('{text.replace(chr(39), chr(39) * 2)}'); $s.Dispose()"
            )
            subprocess.run(["powershell", "-NoProfile", "-NonInteractive", "-Command", script], check=True)
            return

        if self.engine == "kokoro":
            # Deferred so the 300 MB model loads once per run.
            self.jobs.append({"text": text, "out": wav})
            return

        cmd = [self.exe, "--model", self.model, "--output_file", wav, "--quiet",
               "--length_scale", str(self.length_scale), "--sentence_silence", str(sentence_silence)]
        if self.speaker is not None:
            cmd += ["--speaker", str(self.speaker)]

        # Piper looks for its espeak-ng-data next to the executable, so run it from there.
        subprocess.run(cmd, input=text.encode("utf-8"), cwd=os.path.dirname(self.exe), check=True)

    def flush(self, tmp: str) -> None:
        """Renders everything the Kokoro engine has queued."""
        if self.engine != "kokoro" or not self.jobs:
            return

        manifest = os.path.join(tmp, "kokoro_jobs.json")
        with open(manifest, "w", encoding="utf-8") as f:
            json.dump(self.jobs, f)

        # Kokoro's speed is the inverse of Piper's length scale.
        subprocess.run([self.exe, KOKORO_RENDER, "--jobs", manifest, "--voice", self.voice,
                        "--speed", str(1.0 / self.length_scale)], check=True)
        self.jobs.clear()


def splice(parts: list[tuple[str, float]], output: str) -> None:
    """Concatenates word wavs, padding each with its trailing pause."""
    cmd = ["ffmpeg", "-y", "-hide_banner", "-loglevel", "error"]
    for path, _ in parts:
        cmd += ["-i", path]
    pads = "".join(f"[{i}:a]apad=pad_dur={gap}[p{i}];" for i, (_, gap) in enumerate(parts))
    inputs = "".join(f"[p{i}]" for i in range(len(parts)))
    cmd += ["-filter_complex", f"{pads}{inputs}concat=n={len(parts)}:v=0:a=1[out]", "-map", "[out]",
            "-c:a", "pcm_s16le", output]
    subprocess.run(cmd, check=True)


def queue_vox(synth: Synth, name: str, text: str, tmp: str, word_gap: float) -> list[tuple[str, float]]:
    """Every word spoken as its own utterance; returns the parts to splice once they exist."""
    tokens = re.findall(r"[A-Za-z0-9'\-]+|[,;:.!?]", text)
    parts: list[tuple[str, float]] = []

    for i, token in enumerate(tokens):
        if token in VOX_PAUSES:
            if parts:
                path, gap = parts[-1]
                parts[-1] = (path, gap + VOX_PAUSES[token])
            continue

        path = os.path.join(tmp, f"{name}_vox_{i}.wav")
        synth.render(token, path, sentence_silence=0.0)
        parts.append((path, word_gap))

    if not parts:
        raise SystemExit(f"nothing to say in: {text}")

    # No pause after the last word; the PA filter's tail handles the ending.
    path, _ = parts[-1]
    parts[-1] = (path, 0.0)
    return parts


def flatten(wav: str, output: str, factor: float, target: float) -> None:
    """Runs the Praat pitch-range squeeze over a finished line."""
    if not os.path.isfile(PRAAT):
        raise SystemExit(f"Praat.exe not found at {PRAAT}; set PRAAT or drop the Windows build there")

    subprocess.run([PRAAT, "--FULL-TRUST", "--run", FLATTEN_SCRIPT, wav, output, str(factor), str(target)], check=True)


def build_reel(files: list[str], output: str) -> None:
    """Strings the lines together with a short pause, for listening to a whole flavour at once."""
    cmd = ["ffmpeg", "-y", "-hide_banner", "-loglevel", "error"]
    for path in files:
        cmd += ["-i", path]
    pads = "".join(f"[{i}:a]apad=pad_dur=0.7[p{i}];" for i in range(len(files)))
    inputs = "".join(f"[p{i}]" for i in range(len(files)))
    cmd += ["-filter_complex", f"{pads}{inputs}concat=n={len(files)}:v=0:a=1[out]", "-map", "[out]",
            "-c:a", "libvorbis", "-q:a", "4", output]
    subprocess.run(cmd, check=True)


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--engine", choices=["espeak", "sapi", "piper", "kokoro"], help="inferred from --voice when omitted")
    parser.add_argument("--voice", default="sbaitso",
                        help=f"espeak: {', '.join(sorted(ESPEAK_VOICES))}; sapi: {', '.join(sorted(SAPI_VOICES))}; "
                             f"piper: {', '.join(sorted(PIPER_VOICES))} or a model name/path; kokoro: af_heart etc. or a blend")
    parser.add_argument("--speaker", type=int, help="speaker id for multi-speaker Piper models")
    parser.add_argument("--length-scale", type=float, default=1.1, help="Piper/Kokoro speed; above 1 is slower")
    parser.add_argument("--vox", action="store_true", help="speak word by word and splice, like the Half-Life announcer")
    parser.add_argument("--vox-gap", type=float, default=VOX_WORD_GAP, help="seconds between spliced words")
    parser.add_argument("--flat", action="store_true", help="squeeze the pitch contour with Praat for a deadpan read")
    parser.add_argument("--flat-factor", type=float, default=0.25, help="share of pitch movement kept by --flat")
    parser.add_argument("--flat-pitch", type=float, default=0.0, help="Hz to centre --flat on; 0 keeps the voice's mean")
    parser.add_argument("--out", default=DEFAULT_OUT, help="destination folder for the .ogg files")
    parser.add_argument("--espeak", help="path to espeak-ng if it isn't on PATH")
    parser.add_argument("--piper", help="path to piper.exe if it isn't under PIPER_HOME")
    parser.add_argument("--no-chime", action="store_true", help="skip the attention chime prefix")
    parser.add_argument("--reel", action="store_true", help="also write <voice>_reel.ogg with every line in a row")
    args = parser.parse_args()

    if args.engine is None:
        args.engine = infer_engine(args.voice)

    synth = Synth(args)
    lines = read_lines()
    os.makedirs(args.out, exist_ok=True)
    chime = None if args.no_chime else DEFAULT_CHIME
    written = []

    with tempfile.TemporaryDirectory() as tmp:
        # Synthesis first (Kokoro batches it), then splicing, flattening and filtering per line.
        pending: dict[str, list[tuple[str, float]] | None] = {}

        for name, text in lines.items():
            if args.vox:
                pending[name] = queue_vox(synth, name, text, tmp, args.vox_gap)
            else:
                synth.render(text, os.path.join(tmp, f"{name}.wav"), sentence_silence=0.35)
                pending[name] = None

        synth.flush(tmp)

        for name, text in lines.items():
            wav = os.path.join(tmp, f"{name}.wav")

            if pending[name] is not None:
                splice(pending[name], wav)

            if args.flat:
                flat = os.path.join(tmp, f"{name}_flat.wav")
                flatten(wav, flat, args.flat_factor, args.flat_pitch)
                wav = flat

            ogg = os.path.join(args.out, f"{name}.ogg")
            filter_file(wav, ogg, chime=chime)
            written.append(ogg)
            print(f"{name}: {text}")

    if args.reel:
        label = args.voice.replace(":", "").replace(",", "+")
        label += ("-vox" if args.vox else "") + ("-flat" if args.flat else "")
        reel = os.path.join(args.out, f"{label}_reel.ogg")
        build_reel(written, reel)
        print(f"reel: {reel}")

    return 0


if __name__ == "__main__":
    sys.exit(main())
