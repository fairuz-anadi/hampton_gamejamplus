"""Synthesises the opening's placeholder audio (no recorded assets exist yet). Owner: Samprity.

    python -I Tools/make_intro_audio.py

Writes 44.1 kHz 16-bit WAVs into Assets/_Project/Audio/{Music,SFX}/. Replace any of them with a real recording of the
same name and the game picks it up; nothing else needs to change.
"""
import os
import numpy as np
from scipy.io import wavfile
from scipy.signal import butter, sosfilt

SR = 44100
ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "Assets", "_Project", "Audio")
rng = np.random.default_rng(7)


def lowpass(x, hz, order=2):
    return sosfilt(butter(order, hz, "low", fs=SR, output="sos"), x)


def bandpass(x, lo, hi, order=2):
    return sosfilt(butter(order, [lo, hi], "band", fs=SR, output="sos"), x)


def loopable(x, fade_s=1.5):
    """Crossfades the tail into the head so the clip loops without a click."""
    n = int(fade_s * SR)
    head, tail = x[:n].copy(), x[-n:]
    w = np.linspace(0, 1, n)
    if x.ndim == 2:
        w = w[:, None]
    x = x[:-n].copy()
    x[:n] = head * w + tail * (1 - w)
    return x


def save(rel, x, peak=0.85):
    x = np.asarray(x, dtype=np.float64)
    x = x / (np.max(np.abs(x)) + 1e-9) * peak
    path = os.path.join(ROOT, rel)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    wavfile.write(path, SR, (x * 32767).astype(np.int16))
    print("wrote", os.path.normpath(path), f"{len(x) / SR:.1f}s")


def t_axis(seconds):
    return np.arange(int(seconds * SR)) / SR


# Ocean: two layers of filtered noise breathing on slow, unrelated swells, stereo.
def ocean(seconds=24.0):
    t = t_axis(seconds + 1.5)
    out = []
    for ch in range(2):
        n = rng.standard_normal(len(t))
        body = lowpass(n, 500, 2)
        hiss = bandpass(rng.standard_normal(len(t)), 900, 5000, 2) * 0.35
        swell = 0.55 + 0.45 * np.sin(2 * np.pi * (0.11 + ch * 0.013) * t + ch) ** 2
        swell2 = 0.6 + 0.4 * np.sin(2 * np.pi * (0.067 + ch * 0.009) * t + 2 * ch)
        out.append((body + hiss * swell2) * swell)
    return loopable(np.stack(out, 1))


# The engine room: low thrum with a slow beat and a little mechanical knock.
def engine_hum(seconds=12.0):
    t = t_axis(seconds + 1.5)
    hum = 0.6 * np.sin(2 * np.pi * 46 * t) + 0.3 * np.sin(2 * np.pi * 92.5 * t) + 0.15 * np.sin(2 * np.pi * 139 * t)
    hum *= 0.8 + 0.2 * np.sin(2 * np.pi * 1.6 * t)
    rumble = lowpass(rng.standard_normal(len(t)), 120, 2) * 2.0
    knock = np.zeros(len(t))
    for k in np.arange(0, seconds + 1.5, 0.625):
        i = int(k * SR)
        n = min(int(0.08 * SR), len(t) - i)
        knock[i:i + n] += np.exp(-np.arange(n) / (0.012 * SR)) * np.sin(2 * np.pi * 70 * np.arange(n) / SR)
    x = hum + rumble + knock * 0.5
    return loopable(np.stack([x, x * 0.96], 1))


# A steamship's horn far off: two detuned low reeds, slow attack, long release, a hint of echo off the sea.
def horn(seconds=5.5):
    t = t_axis(seconds)
    env = np.clip(t / 0.35, 0, 1) * np.clip((3.6 - t) / 1.4, 0, 1)
    f = 110 * (1 + 0.004 * np.sin(2 * np.pi * 5 * t))
    phase = 2 * np.pi * np.cumsum(f) / SR
    tone = sum(np.sin(h * phase) / h ** 0.9 for h in range(1, 9))
    tone += 0.8 * sum(np.sin(h * phase * 1.26) / h for h in range(1, 6))  # a major third above, steam whistle chord
    x = lowpass(tone * env, 900, 2)
    echo = np.zeros_like(x)
    d = int(0.42 * SR)
    echo[d:] = x[:-d] * 0.35
    x = lowpass(x + echo, 1400, 1)
    return np.stack([x, np.roll(x, 300)], 1)


# Music: a slow, low, uneasy pad. D minor-ish, with a cold suspended note that never quite resolves.
def drone(seconds=64.0):
    t = t_axis(seconds)
    chords = [(73.42, 110.0, 146.83, 174.61), (65.41, 98.0, 130.81, 164.81), (58.27, 87.31, 116.54, 155.56), (73.42, 110.0, 146.83, 164.81)]
    seg = seconds / len(chords)
    left = np.zeros(len(t))
    right = np.zeros(len(t))
    for ci, chord in enumerate(chords):
        centre = (ci + 0.5) * seg
        w = np.exp(-((t - centre) / (seg * 0.62)) ** 2)  # soft crossfades between chords
        for k, f in enumerate(chord):
            det = 1 + 0.0025 * (k - 1.5)
            vib = 1 + 0.0015 * np.sin(2 * np.pi * (0.13 + 0.05 * k) * t)
            v = np.sin(2 * np.pi * f * det * vib * t) + 0.35 * np.sin(2 * np.pi * 2 * f * det * t + k)
            pan = 0.5 + 0.35 * np.sin(k * 1.7)
            left += v * w * (1 - pan)
            right += v * w * pan
    swell = np.clip(t / 8, 0, 1) * np.clip((seconds - t) / 6, 0, 1)
    air = lowpass(rng.standard_normal(len(t)), 300, 2) * 0.6
    x = np.stack([lowpass(left, 700, 2) + air, lowpass(right, 700, 2) + air], 1) * swell[:, None]
    return x


# The ship's speaker: a burst of radio static that opens up and then cuts.
def crackle(seconds=0.9):
    t = t_axis(seconds)
    n = bandpass(rng.standard_normal(len(t)), 600, 4200, 2)
    pops = (rng.random(len(t)) > 0.9985) * rng.standard_normal(len(t)) * 6
    env = np.clip(t / 0.03, 0, 1) * np.clip((seconds - t) / 0.25, 0, 1) * (0.6 + 0.4 * np.sin(2 * np.pi * 13 * t))
    x = (n + pops) * env
    return np.stack([x, x], 1)


# Footsteps on a wooden deck, a few metres off.
def footsteps(seconds=6.0):
    t = t_axis(seconds)
    x = np.zeros(len(t))
    for k in np.arange(0.2, seconds - 0.3, 0.55):
        i = int((k + rng.uniform(-0.03, 0.03)) * SR)
        n = int(0.12 * SR)
        thud = np.exp(-np.arange(n) / (0.02 * SR)) * (np.sin(2 * np.pi * 95 * np.arange(n) / SR) + 0.6 * rng.standard_normal(n) * np.exp(-np.arange(n) / (0.004 * SR)))
        x[i:i + n] += thud * rng.uniform(0.7, 1.0)
    x = lowpass(x, 1800, 2)
    return np.stack([x * 0.9, x], 1)


# Menu hover tick and confirm: soft brass taps.
def tick(seconds=0.12, f=1800):
    t = t_axis(seconds)
    x = np.sin(2 * np.pi * f * t) * np.exp(-t / 0.018) + 0.3 * np.sin(2 * np.pi * f * 2.7 * t) * np.exp(-t / 0.008)
    return np.stack([x, x], 1)


def confirm(seconds=1.4):
    t = t_axis(seconds)
    x = sum(np.sin(2 * np.pi * f * t) * np.exp(-t / d) for f, d in ((523.25, 0.5), (783.99, 0.45), (1046.5, 0.3)))
    x = lowpass(x, 3000, 2)
    return np.stack([x, x], 1)


if __name__ == "__main__":
    save("SFX/Intro_Ocean.wav", ocean(), 0.7)
    save("SFX/Intro_EngineHum.wav", engine_hum(), 0.7)
    save("SFX/Intro_Horn.wav", horn(), 0.8)
    save("SFX/Intro_SpeakerCrackle.wav", crackle(), 0.6)
    save("SFX/Intro_Footsteps.wav", footsteps(), 0.6)
    save("SFX/UI_Hover.wav", tick(), 0.4)
    save("SFX/UI_Confirm.wav", confirm(), 0.55)
    save("Music/Intro_Theme.wav", drone(), 0.6)
