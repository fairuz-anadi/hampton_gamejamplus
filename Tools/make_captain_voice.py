"""Records the Captain's lines with ElevenLabs. Owner: Samprity.

The API key is read from the ELEVENLABS_API_KEY environment variable (set it once with
`setx ELEVENLABS_API_KEY "..."`; on Windows it is also read straight from the registry, so no restart is needed).
It is never printed, written to a file, or put in the game: the game only ships the recordings.

    python -I Tools/make_captain_voice.py --credits
        what your plan has left this month

    python -I Tools/make_captain_voice.py --audition
        records three short lines (warm, drunk, cold) in each shortlisted built-in voice, and downloads the preview of
        the best "sea captain" voices in the ElevenLabs voice library (previews cost nothing). Listen in
        Tools/captain_voice/auditions/ and pick one.

    python -I Tools/make_captain_voice.py --generate --voice George [--names] [--model eleven_multilingual_v2]
        records every line from Tools/captain_voice/lines.json (made by Port > Samprity > Export Captain Lines) into
        Assets/_Project/Audio/Voice/Captain. Lines already recorded are skipped, so it can be stopped and run again.
        --names also records the lines that name a crew member once per crew name (Arthur, Ezra, Mabel, Jonah);
        without it those lines are recorded once, saying "sailor". A library voice is given by its voice id and
        is added to your ElevenLabs account first. Then run Port > Samprity > Build Captain Voice Bank.

How he sounds follows the line's mood: warm in Part 1, looser and slurring as he drinks in Part 2, flat and cold
in Part 3.
"""
import argparse
import json
import os
import sys
import time
import urllib.error
import urllib.parse
import urllib.request

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..")
DIR = os.path.join(ROOT, "Tools", "captain_voice")
LINES = os.path.join(DIR, "lines.json")
AUDITIONS = os.path.join(DIR, "auditions")
CLIPS = os.path.join(ROOT, "Assets", "_Project", "Audio", "Voice", "Captain")
API = "https://api.elevenlabs.io"

# Built-in ElevenLabs voices worth hearing as the Captain (ids from ElevenLabs' default voice list).
SHORTLIST = {
    "George": "JBFqnCBsd6RMkjVDRZzb",   # warm British, a little raspy
    "Daniel": "onwK4e9ZLuTAKqWW03F9",   # British, authoritative
    "Callum": "N2lVS1w4EtoT3dr4eOWO",   # husky, intense
}
# Also usable with --voice, not auditioned by default (saves credits): Bill pqHfZKP75CvOlQylNhV4 (older, American),
# Clyde 2EiwWnXFnvU5JabPnv8n (gravelly veteran, American).
EXTRA = {"Bill": "pqHfZKP75CvOlQylNhV4", "Clyde": "2EiwWnXFnvU5JabPnv8n"}
LIBRARY_SEARCHES = ["sea captain", "ship captain", "old british gravelly", "old sailor"]

# Voice settings by mood, the "gruff" take she chose (28 Oct): 0 warm and in charge .. 3 drunk, 4 the act dropped.
# Lower stability and more style make him rougher; a touch slower throughout.
MOODS = {
    0: dict(stability=0.3, similarity_boost=0.75, style=0.75, use_speaker_boost=True, speed=0.92),
    1: dict(stability=0.26, similarity_boost=0.74, style=0.8, use_speaker_boost=True, speed=0.91),
    2: dict(stability=0.22, similarity_boost=0.72, style=0.88, use_speaker_boost=True, speed=0.89),
    3: dict(stability=0.18, similarity_boost=0.7, style=0.95, use_speaker_boost=True, speed=0.86),
    4: dict(stability=0.7, similarity_boost=0.85, style=0.3, use_speaker_boost=True, speed=0.88),
}
# Recorded in this order, so if the month's credits run short it's the chatter that waits, not the story.
PRIORITY = ["Order", "Part1Start", "Part2Start", "RevealLine", "Monologue", "MonologueLost", "IcebergCall", "MoodStart",
            "FakeNoticed", "ReportDone", "Refuse", "Expired", "Slip", "Praise", "IceWarningDelivered", "DoorFour",
            "PrivateLines", "SuspiciousLines", "Rambles", "Tutorial", "Talk"]


def priority(line):
    src = line.get("source", "")
    for i, p in enumerate(PRIORITY):
        if src.startswith(p):
            return i
    return len(PRIORITY)
AUDITION_LINES = [
    (0, "warm", "Ahem. Is this on? It's on. Good morning, crew! This is your Captain speaking. Two days to the Promised Port."),
    (3, "drunk", "Who keeps bringing me these? Is it you, Arthur? Stop it. Stop being helpful. It's very annoying. Hic!"),
    (4, "cold", "Crew. All of you. Listen. This is it. We were never going to the Promised Port."),
]


def api_key():
    key = os.environ.get("ELEVENLABS_API_KEY", "").strip()
    if not key and sys.platform == "win32":
        try:
            import winreg
            with winreg.OpenKey(winreg.HKEY_CURRENT_USER, "Environment") as k:
                key = str(winreg.QueryValueEx(k, "ELEVENLABS_API_KEY")[0]).strip()
        except OSError:
            key = ""
    if not key:
        sys.exit("No ElevenLabs key: run  setx ELEVENLABS_API_KEY \"your-key\"  in a terminal first.")
    return key


def call(method, path, key, body=None, query=None, raw=False, retries=4):
    url = API + path + ("?" + urllib.parse.urlencode(query) if query else "")
    data = json.dumps(body).encode() if body is not None else None
    for attempt in range(retries):
        req = urllib.request.Request(url, data=data, method=method)
        req.add_header("xi-api-key", key)
        if data is not None:
            req.add_header("Content-Type", "application/json")
        req.add_header("Accept", "audio/mpeg" if raw else "application/json")
        try:
            with urllib.request.urlopen(req, timeout=120) as r:
                out = r.read()
                return out if raw else json.loads(out.decode() or "{}")
        except urllib.error.HTTPError as e:
            detail = e.read().decode(errors="replace")[:300]
            if e.code == 429 and attempt < retries - 1:
                time.sleep(2 ** attempt * 2)
                continue
            sys.exit(f"ElevenLabs said {e.code} for {path}: {detail}")
    sys.exit(f"ElevenLabs kept refusing {path}")


def credits(key):
    s = call("GET", "/v1/user/subscription", key)
    used, limit = s.get("character_count", 0), s.get("character_limit", 0)
    return used, limit, s.get("tier", "?")


def tts(key, voice_id, text, mood, model):
    body = {"text": text, "model_id": model, "voice_settings": MOODS.get(mood, MOODS[1])}
    return call("POST", f"/v1/text-to-speech/{voice_id}", key, body, {"output_format": "mp3_44100_128"}, raw=True)


def resolve_voice(key, voice):
    """A shortlist name, a voice in your account (by name or id), or a library voice id (added to your account)."""
    if voice in SHORTLIST or voice in EXTRA:
        return {**SHORTLIST, **EXTRA}[voice], voice
    mine = call("GET", "/v1/voices", key).get("voices", [])
    for v in mine:
        if voice in (v.get("voice_id"), v.get("name")):
            return v["voice_id"], v.get("name", voice)
    for q in LIBRARY_SEARCHES:
        for v in call("GET", "/v1/shared-voices", key, query={"page_size": 30, "search": q}).get("voices", []):
            if v.get("voice_id") == voice:
                call("POST", f"/v1/voices/add/{v['public_owner_id']}/{voice}", key, {"new_name": f"Captain - {v.get('name', voice)}"})
                return voice, v.get("name", voice)
    sys.exit(f"Couldn't find the voice '{voice}'. Use a name from the shortlist, your account, or a library voice id.")


def audition_one(key, model, voice):
    """Our three lines (warm, drunk, cold) in one voice, a library one included (added to your account first)."""
    os.makedirs(AUDITIONS, exist_ok=True)
    vid, name = resolve_voice(key, voice)
    safe = "".join(c if c.isalnum() else "_" for c in name)[:40]
    for mood, tag, text in AUDITION_LINES:
        out = os.path.join(AUDITIONS, f"{safe}_{mood}_{tag}.mp3")
        if not os.path.exists(out):
            with open(out, "wb") as f:
                f.write(tts(key, vid, text, mood, model))
        print(f"  {os.path.normpath(out)}")


def audition(key, model):
    os.makedirs(AUDITIONS, exist_ok=True)
    print("Recording the shortlist (three short lines each)...")
    for name, vid in SHORTLIST.items():
        for mood, tag, text in AUDITION_LINES:
            out = os.path.join(AUDITIONS, f"{name}_{mood}_{tag}.mp3")
            if not os.path.exists(out):
                with open(out, "wb") as f:
                    f.write(tts(key, vid, text, mood, model))
        print(f"  {name:8} {vid}")
    print("Voice library: previews of the best matches (free)...")
    seen = set()
    for q in LIBRARY_SEARCHES:
        found = call("GET", "/v1/shared-voices", key, query={"page_size": 12, "search": q, "gender": "male"}).get("voices", [])
        for v in found:
            vid, url = v.get("voice_id"), v.get("preview_url")
            if not vid or not url or vid in seen or len(seen) >= 10:
                continue
            seen.add(vid)
            safe = "".join(c if c.isalnum() else "_" for c in v.get("name", vid))[:40]
            out = os.path.join(AUDITIONS, f"library_{safe}.mp3")
            try:
                with urllib.request.urlopen(url, timeout=60) as r, open(out, "wb") as f:
                    f.write(r.read())
            except OSError:
                continue
            print(f"  {v.get('name', '?')[:34]:34} id {vid}  {v.get('accent', '')}, {v.get('age', '')}: {(v.get('description') or '')[:70]}")
    print(f"\nListen in {os.path.normpath(AUDITIONS)} and pick one.")


def generate(key, voice, names, model, force):
    if not os.path.exists(LINES):
        sys.exit("No lines yet: run Port > Samprity > Export Captain Lines in Unity first.")
    lines = json.load(open(LINES, encoding="utf-8"))["lines"]
    todo = [l for l in lines if names or l["variant"] in ("", "*")]
    todo = [l for l in todo if not os.path.exists(os.path.join(CLIPS, l["id"] + ".mp3"))]
    todo.sort(key=priority)
    need = sum(len(l["speak"]) for l in todo)
    used, limit, tier = credits(key)
    print(f"{len(todo)} lines to record, about {need} characters. Your {tier} plan has {limit - used} of {limit} left this month.")
    if need > limit - used and not force:
        sys.exit("Not enough credits for all of them. Leave out --names, use --model eleven_flash_v2_5 (half price), or add --force to record what fits.")
    vid, name = resolve_voice(key, voice)
    os.makedirs(CLIPS, exist_ok=True)
    with open(os.path.join(DIR, "voice.txt"), "w", encoding="utf-8") as f:
        f.write(f"{name} ({vid}), {model}")
    for i, l in enumerate(todo, 1):
        out = os.path.join(CLIPS, l["id"] + ".mp3")
        audio = tts(key, vid, l["speak"], l["mood"], model)
        with open(out, "wb") as f:
            f.write(audio)
        print(f"  {i}/{len(todo)}  mood {l['mood']}  {l['speak'][:70]}")
    print("Done. In Unity: Port > Samprity > Build Captain Voice Bank.")


def main():
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    p.add_argument("--credits", action="store_true")
    p.add_argument("--audition", action="store_true")
    p.add_argument("--audition-voice", help="record our three audition lines in this one voice (name or id)")
    p.add_argument("--generate", action="store_true")
    p.add_argument("--voice", default="George")
    p.add_argument("--names", action="store_true", help="record named lines once per crew name")
    p.add_argument("--model", default="eleven_multilingual_v2")
    p.add_argument("--force", action="store_true")
    a = p.parse_args()
    key = api_key()
    if a.credits:
        used, limit, tier = credits(key)
        print(f"{tier} plan: {used} of {limit} characters used this month, {limit - used} left.")
    if a.audition:
        audition(key, a.model)
    if a.audition_voice:
        audition_one(key, a.model, a.audition_voice)
    if a.generate:
        generate(key, a.voice, a.names, a.model, a.force)
    if not (a.credits or a.audition or a.audition_voice or a.generate):
        p.print_help()


if __name__ == "__main__":
    main()
