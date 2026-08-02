#!/usr/bin/env python3
"""Build the final Ninth Bell cue set from auditable sources.

Three cues are curated from CC0 field recordings. The tinnitus cue is intentionally
project-authored synthesis because a controlled, seamless internal tone is the desired source.
Every remote source is SHA-256 pinned so a changed preview cannot silently enter the build.

Usage:
    python3 scripts/gen-bell-audio.py /path/to/Assets/Resources/Sfx

Set GM_AUDIO_SOURCE_CACHE to retain the pinned source downloads somewhere other than the
system temporary directory. ffmpeg and numpy are required.
"""

from __future__ import annotations

import hashlib
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import urllib.request

import numpy as np


SAMPLE_RATE = 48_000
SOURCE_CACHE = Path(
    os.environ.get(
        "GM_AUDIO_SOURCE_CACHE",
        Path(tempfile.gettempdir()) / "gm-ninth-bell-source",
    )
)

SOURCES = {
    "clock": {
        "url": "https://cdn.freesound.org/previews/438/438313_2524442-hq.mp3",
        "sha256": "20e7a20a9286aed44d0aa336865611c7d981a0cb2405c7670041442da34c3522",
    },
    "heartbeat": {
        "url": "https://cdn.freesound.org/previews/670/670465_3910073-hq.mp3",
        "sha256": "1d5ec4cddbefdbcf3f64cf72ec3980c9f1a0de2b21cb40744ff05d9a282ae867",
    },
    "whisper": {
        "url": "https://cdn.freesound.org/previews/517/517868_9506552-hq.mp3",
        "sha256": "5883a127c11f1b6a063147a5e4dc21047362e3eaeba274f8f8bebe3a41abae0b",
    },
}


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for block in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def source(name: str) -> Path:
    SOURCE_CACHE.mkdir(parents=True, exist_ok=True)
    spec = SOURCES[name]
    path = SOURCE_CACHE / f"{name}.mp3"
    if not path.exists() or sha256(path) != spec["sha256"]:
        partial = path.with_suffix(".download")
        if partial.exists():
            partial.unlink()
        print(f"[source] {name}: {spec['url']}")
        request = urllib.request.Request(
            spec["url"], headers={"User-Agent": "TheGamesMasterAudioCuration/1.0"}
        )
        with urllib.request.urlopen(request, timeout=30) as response, partial.open("wb") as out:
            out.write(response.read())
        if sha256(partial) != spec["sha256"]:
            partial.unlink(missing_ok=True)
            raise RuntimeError(f"Pinned source hash changed for {name}; refusing to build")
        partial.replace(path)
    return path


def ffmpeg(input_path: Path, output_path: Path, audio_filter: str) -> None:
    output_path.parent.mkdir(parents=True, exist_ok=True)
    command = [
        "ffmpeg",
        "-hide_banner",
        "-loglevel",
        "error",
        "-y",
        "-i",
        str(input_path),
        "-af",
        audio_filter,
        "-ar",
        str(SAMPLE_RATE),
        "-ac",
        "2",
        "-c:a",
        "vorbis",
        "-q:a",
        "5",
        "-strict",
        "-2",
        str(output_path),
    ]
    subprocess.run(command, check=True)


def write_tinnitus(output_path: Path) -> None:
    """Write an eight-second, periodic and therefore seam-safe internal tinnitus layer."""
    duration = 8.0
    frames = int(SAMPLE_RATE * duration)
    time = np.arange(frames, dtype=np.float64) / SAMPLE_RATE

    # Each carrier completes an integer number of cycles over eight seconds. Phase modulation and
    # the breathing envelope also complete whole cycles, so the loop wraps without a pitch jump.
    phase_drift = 1.35 * np.sin(2 * np.pi * 0.375 * time)
    left = (
        0.185 * np.sin(2 * np.pi * 3150 * time + phase_drift)
        + 0.070 * np.sin(2 * np.pi * 4780 * time + 0.62 * phase_drift + 0.7)
        + 0.035 * np.sin(2 * np.pi * 6280 * time - 0.45 * phase_drift + 1.1)
    )
    right = (
        0.175 * np.sin(2 * np.pi * 3150 * time + phase_drift + 0.08)
        + 0.076 * np.sin(2 * np.pi * 4780 * time + 0.58 * phase_drift + 0.82)
        + 0.031 * np.sin(2 * np.pi * 6280 * time - 0.42 * phase_drift + 1.0)
    )

    # A deterministic periodic high-band texture avoids a sterile oscillator while keeping the
    # cue free of the broadband engine character that was rejected in the exterior ambience.
    rng = np.random.default_rng(1907)
    bins = np.fft.rfftfreq(frames, d=1.0 / SAMPLE_RATE)
    spectrum = np.zeros(bins.size, dtype=np.complex128)
    band = (bins >= 2400) & (bins <= 8200)
    spectrum[band] = np.exp(1j * rng.uniform(0, 2 * np.pi, int(np.count_nonzero(band))))
    texture = np.fft.irfft(spectrum, n=frames)
    texture /= max(1e-12, np.max(np.abs(texture)))

    breath = 0.78 + 0.12 * np.cos(2 * np.pi * time / duration)
    stereo = np.column_stack(
        ((left + 0.012 * texture) * breath, (right + 0.010 * np.roll(texture, 19)) * breath)
    )
    stereo = np.clip(stereo, -1.0, 1.0)
    raw = (stereo * 32767).astype("<i2").tobytes()

    command = [
        "ffmpeg",
        "-hide_banner",
        "-loglevel",
        "error",
        "-y",
        "-f",
        "s16le",
        "-ar",
        str(SAMPLE_RATE),
        "-ac",
        "2",
        "-i",
        "pipe:0",
        "-c:a",
        "vorbis",
        "-q:a",
        "5",
        "-strict",
        "-2",
        str(output_path),
    ]
    subprocess.run(command, input=raw, check=True)


def probe(path: Path) -> str:
    result = subprocess.run(
        [
            "ffprobe",
            "-v",
            "error",
            "-show_entries",
            "format=duration",
            "-of",
            "default=noprint_wrappers=1:nokey=1",
            str(path),
        ],
        check=True,
        capture_output=True,
        text=True,
    )
    return f"{float(result.stdout.strip()):.2f}s"


def main() -> int:
    if len(sys.argv) != 2:
        print("usage: python3 scripts/gen-bell-audio.py <out_dir>", file=sys.stderr)
        return 2
    out = Path(sys.argv[1]).expanduser().resolve()
    out.mkdir(parents=True, exist_ok=True)

    # chapel_bell is the licensed Horror Elements source already held by the project. Older intake
    # encoded it as Opus-in-Ogg, which ffprobe accepted but Unity's FMOD importer could not decode.
    # If that legacy file is present, normalize only its delivery codec before building the four
    # cues below. The original pack remains outside this repository under the Asset Store EULA.
    chapel = out / "chapel_bell.ogg"
    if chapel.exists():
        codec = subprocess.run(
            ["ffprobe", "-v", "error", "-select_streams", "a:0", "-show_entries",
             "stream=codec_name", "-of", "default=noprint_wrappers=1:nokey=1", str(chapel)],
            check=True, capture_output=True, text=True,
        ).stdout.strip()
        if codec != "vorbis":
            normalized = out / "chapel_bell.delivery.ogg"
            ffmpeg(chapel, normalized, "aresample=48000")
            normalized.replace(chapel)
            print(f"[curated] {chapel}  Unity-safe Vorbis delivery")

    # One clean strike from an archival grandfather-clock sequence, extended with a restrained
    # room tail. The source segment ends before the next physical strike, preserving the canonical
    # single ninth chime.
    ffmpeg(
        source("clock"),
        out / "clock_chime.ogg",
        "atrim=start=2.20:end=3.62,asetpts=PTS-STARTPTS,"
        "apad=pad_dur=5.0,"
        "aecho=0.8:0.65:43|71:0.18|0.13,"
        "aecho=0.8:0.65:113|167:0.16|0.12,"
        "aecho=0.8:0.65:241|359:0.14|0.10,"
        "aecho=0.8:0.65:503|719:0.11|0.08,"
        "aecho=0.8:0.65:727|1013:0.18|0.14,"
        "aecho=0.8:0.65:1499|2111:0.14|0.10,"
        "highpass=f=70,lowpass=f=12000,loudnorm=I=-20:TP=-2:LRA=7,"
        "atrim=start=0:end=6,afade=t=out:st=5.7:d=0.3",
    )

    # The source is an actual resting heartbeat. Exact beat-boundary trimming plus a tiny zero
    # crossing fade makes the seven-cycle loop natural and click-safe.
    ffmpeg(
        source("heartbeat"),
        out / "heartbeat.ogg",
        "atrim=start=1.092676:end=8.241474,asetpts=PTS-STARTPTS,"
        "highpass=f=25,lowpass=f=260,loudnorm=I=-20:TP=-3:LRA=6,"
        "afade=t=in:st=0:d=0.008,afade=t=out:st=7.140:d=0.008",
    )

    # Wordless human whisper, kept below dialogue intelligibility and made subtly binaural. It is
    # longer than the seven-second crossing use, so runtime never has to loop it.
    ffmpeg(
        source("whisper"),
        out / "whisper_bed.ogg",
        "atrim=start=0:end=7.75,asetpts=PTS-STARTPTS,atempo=0.88,"
        "highpass=f=180,lowpass=f=9500,loudnorm=I=-24:TP=-4:LRA=9,"
        "aecho=0.8:0.5:85|155:0.11|0.07,"
        "pan=stereo|c0=c0|c1=c0,apulsator=hz=0.08:amount=0.28:offset_l=0:offset_r=0.5,"
        "volume=8dB,"
        "afade=t=in:st=0:d=0.08,afade=t=out:st=8.75:d=0.20",
    )

    write_tinnitus(out / "ear_whine.ogg")

    for name in ("clock_chime", "heartbeat", "whisper_bed", "ear_whine"):
        path = out / f"{name}.ogg"
        print(f"[curated] {path}  {probe(path)}  sha256={sha256(path)[:16]}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
