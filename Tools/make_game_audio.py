"""Turns the downloaded sounds into game-ready WAVs in Assets/_Project/Audio. Owner: Samprity.

    python Tools/make_game_audio.py <folder with the downloaded mp3s> <ffmpeg.exe>

Loops get an equal-power crossfade so they repeat without a click, one-shots are trimmed and faded,
footsteps and gull calls are cut into single hits. Loops are levelled by loudness (RMS), one-shots by peak.
Sources and licences are listed in CREDITS.md.
"""
import os
import subprocess
import sys
import tempfile

import numpy as np
from scipy.io import wavfile
from scipy.signal import resample

SRC, FFMPEG = sys.argv[1], sys.argv[2]
ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "Assets", "_Project", "Audio")


def load(name, rate, channels):
    # Some ffmpeg builds (Remotion's) can't write raw floats, but all of them write 16-bit WAV.
    tmp = os.path.join(tempfile.gettempdir(), "port_audio_decode.wav")
    subprocess.run([FFMPEG, "-v", "error", "-y", "-i", os.path.join(SRC, name + ".mp3"), "-c:a", "pcm_s16le",
                    "-ac", str(channels), "-ar", str(rate), tmp], check=True)
    _, data = wavfile.read(tmp)
    return (data.astype(np.float64) / 32768.0).reshape(-1, channels)


def save(rel, x, rate):
    path = os.path.join(ROOT, rel + ".wav")
    os.makedirs(os.path.dirname(path), exist_ok=True)
    x = np.clip(x, -1.0, 1.0)
    wavfile.write(path, rate, (x * 32767).astype(np.int16) if x.shape[1] > 1 else (x[:, 0] * 32767).astype(np.int16))
    print(f"{rel:32s} {len(x) / rate:6.2f}s {x.shape[1]}ch {rate}Hz {os.path.getsize(path) / 1e6:5.2f} MB")


def rms_db(x):
    return 20 * np.log10(np.sqrt(np.mean(x ** 2)) + 1e-9)


def level_rms(x, target_db):
    x = x * 10 ** ((target_db - rms_db(x)) / 20)
    peak = np.max(np.abs(x))
    return x * (0.97 / peak) if peak > 0.97 else x


def level_peak(x, target_db=-1.0):
    return x * (10 ** (target_db / 20) / (np.max(np.abs(x)) + 1e-9))


def steadiest(x, rate, seconds, skip=1.0):
    """Start (in samples) of the window whose loudness changes least, so the loop has no obvious event in it."""
    hop = rate // 2
    frames = np.array([rms_db(x[i:i + hop]) for i in range(0, len(x) - hop, hop)])
    need = int(seconds * 2)
    first = int(skip * 2)
    best, best_score = first, 1e9
    median = np.median(frames)
    for s in range(first, max(first + 1, len(frames) - need)):
        w = frames[s:s + need]
        score = np.std(w) + max(0.0, median - np.mean(w)) * 0.5
        if score < best_score:
            best, best_score = s, score
    return best * hop


def loop(name, rel, seconds, rate=32000, channels=1, fade=2.0, start=None, target_db=-20.0):
    x = load(name, rate, channels)
    n, f = int(seconds * rate), int(fade * rate)
    if n + f > len(x):
        n = len(x) - f
    s = steadiest(x, rate, (n + f) / rate) if start is None else int(start * rate)
    s = min(s, len(x) - n - f)
    seg = x[s:s + n + f]
    out = seg[:n].copy()
    t = np.linspace(0, np.pi / 2, f)[:, None]
    out[:f] = seg[:f] * np.sin(t) + seg[n:n + f] * np.cos(t)
    save(rel, level_rms(out, target_db), rate)


def trim(x, rate, floor_db=-40.0):
    env = np.max(np.abs(x), axis=1)
    above = np.nonzero(env > np.max(env) * 10 ** (floor_db / 20))[0]
    if len(above) == 0:
        return x
    a = max(0, above[0] - int(0.005 * rate))
    return x[a:above[-1] + int(0.02 * rate)]


def shape(x, rate, fade_in=0.004, fade_out=0.15):
    x = x.copy()
    fi = max(1, int(fade_in * rate))
    fo = max(1, min(len(x) - fi, int(max(0.03, fade_out * len(x) / rate) * rate)))
    x[:fi] *= np.linspace(0, 1, fi)[:, None]
    x[-fo:] *= np.linspace(1, 0, fo)[:, None] ** 2
    return x


def oneshot(name, rel, max_seconds=None, start=0.0, rate=44100, channels=1, peak_db=-1.0, stretch=1.0):
    x = load(name, rate, channels)[int(start * rate):]
    x = trim(x, rate)
    if max_seconds:
        x = x[:int(max_seconds * rate)]
    if stretch != 1.0:
        x = resample(x, int(len(x) * stretch), axis=0)
    save(rel, level_peak(shape(x, rate), peak_db), rate)


def slices(name, rel, count, length, gap, rate=44100, threshold=0.3, peak_db=-3.0, skip=0.0):
    """Cuts single hits (footsteps, calls) out of a recording of many, the loudest `count` of them."""
    x = load(name, rate, 1)
    env = np.abs(x[:, 0])
    win = int(0.01 * rate)
    env = np.convolve(env, np.ones(win) / win, mode="same")
    level = np.max(env) * threshold
    onsets, last = [], -10 ** 9
    for i in np.nonzero((env[1:] > level) & (env[:-1] <= level))[0]:
        if i - last > gap * rate and i > skip * rate:
            onsets.append(i)
            last = i
    hits = []
    for k, on in enumerate(onsets):
        a = max(0, on - int(0.015 * rate))
        b = min(len(x), on + int(length * rate))
        if k + 1 < len(onsets):
            b = min(b, onsets[k + 1] - int(0.02 * rate))
        if b - a > 0.08 * rate:
            hits.append((np.max(np.abs(x[a:b])), a, b))
    if len(hits) < count:
        # No clean gaps between the hits (a flock, not single calls): take the loudest separate windows instead.
        n, hop = int(length * rate), int(0.05 * rate)
        energy = [(rms_db(x[i:i + n]), i) for i in range(0, len(x) - n, hop)]
        hits = []
        for _, a in sorted(energy, reverse=True):
            if len(hits) == count:
                break
            if all(abs(a - h[1]) >= n for h in hits):
                hits.append((0.0, a, a + n))
    hits = sorted(sorted(hits, reverse=True)[:count], key=lambda h: h[1])
    for k, (_, a, b) in enumerate(hits, 1):
        save(f"{rel}_{k:02d}", level_peak(shape(x[a:b], rate, fade_out=0.3), peak_db), rate)
    if not hits:
        print("!! no hits found in", name)


# Ambience beds (loops)
loop("sea_hull", "Ambience/Amb_SeaHull", 20, rate=44100, channels=2, fade=1.5, start=0)
loop("px_ambience", "Ambience/Amb_Deck", 40, rate=24000, channels=2)
loop("sea_storm", "Ambience/Amb_SeaStorm", 30)
loop("wind", "Ambience/Amb_Wind", 30)
loop("rigging", "Ambience/Amb_Rigging", 30)
loop("creak", "Ambience/Amb_HullCreak", 40, rate=22050)
loop("px_engine", "Ambience/Amb_Engine", 24, rate=24000, fade=1.5)
loop("boiler", "Ambience/Amb_BoilerRoar", 24, rate=22050, fade=1.5)
loop("passengers", "Ambience/Amb_Passengers", 30, rate=22050)
loop("ballroom", "Ambience/Amb_Ballroom", 60, rate=22050, fade=3.0, start=20)
loop("pump", "Ambience/Amb_Pumps", 20, rate=22050)
slices("gulls", "Ambience/Amb_Gull", 4, 2.2, 0.9, threshold=0.12, peak_db=-6.0)
oneshot("px_horn", "Ambience/Amb_Horn", rate=24000, channels=2, peak_db=-2.0)

# Footsteps
slices("steps_wood", "SFX/Steps/Step_Wood", 8, 0.38, 0.25)
slices("steps_metal", "SFX/Steps/Step_Metal", 8, 0.38, 0.25)

# Stations
loop("shovel", "SFX/Stations/Sta_ShovelLoop", 18, rate=32000, fade=1.0, target_db=-18.0)
loop("wheel", "SFX/Stations/Sta_WheelCreak", 20, rate=32000, fade=1.0, target_db=-18.0)
loop("morse", "SFX/Stations/Sta_MorseLoop", 16, rate=32000, fade=0.5, start=8, target_db=-20.0)
oneshot("morse", "SFX/Stations/Sta_MorseBurst", max_seconds=3.0, start=2.0)
oneshot("lever", "SFX/Stations/Sta_Lever")
oneshot("door", "SFX/Stations/Sta_DoorClose", max_seconds=4.0)
oneshot("paper", "SFX/Stations/Sta_Paper", max_seconds=2.5, peak_db=-3.0)
oneshot("clank", "SFX/Stations/Sta_Clank")

# Signals
oneshot("order_bell", "SFX/Signals/Sig_OrderBell", peak_db=-2.0)
oneshot("tick", "SFX/Signals/Sig_Tick")
oneshot("chime", "SFX/Signals/Sig_Success", peak_db=-2.0)
oneshot("hm", "SFX/Signals/Sig_Hm")
oneshot("hiss", "SFX/Signals/Sig_Hiss")
oneshot("klaxon", "SFX/Signals/Sig_Alarm", max_seconds=2.8, peak_db=-2.0)
oneshot("whistle", "SFX/Signals/Sig_Whistle", peak_db=-2.0)
oneshot("muster", "SFX/Signals/Sig_Muster", max_seconds=8.0)

# Captain's speaker
oneshot("click", "SFX/Captain/Cap_SpeakerOn")
oneshot("click", "SFX/Captain/Cap_SpeakerOff", stretch=1.35, peak_db=-4.0)
loop("crackle", "SFX/Captain/Cap_Crackle", 15, rate=22050, fade=1.0)
oneshot("static", "SFX/Captain/Cap_Static", max_seconds=2.5, peak_db=-3.0)

# Finale
oneshot("lookout", "SFX/Finale/Fin_LookoutBell", rate=48000)
loop("drone", "SFX/Finale/Fin_Drone", 40, rate=32000, fade=3.0, start=0)
oneshot("boom", "SFX/Finale/Fin_ImpactBoom", rate=44100, channels=2)
oneshot("crash_wood", "SFX/Finale/Fin_ImpactWood", rate=44100, channels=2)
oneshot("ice_crack", "SFX/Finale/Fin_ImpactIce", max_seconds=5.0, rate=48000)
loop("flood", "SFX/Finale/Fin_Flood", 20, rate=32000)

# Music
loop("waltz", "Music/Mus_Waltz", 56, rate=32000, fade=2.0, start=0, target_db=-18.0)
loop("cello", "Music/Mus_Sustained", 40, rate=32000, fade=4.0, target_db=-20.0)
