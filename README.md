# The Promised Port

A co-op ship game where 1–3 players follow a Captain's orders and slowly realize the calm voice they trust is steering everyone toward disaster.

GameJamPlus 2026/27 (11th edition, GJ+ Bangladesh) entry by Anadi, Rabib, Riham and Samprity. Built in Unity 6.6 as a 2.5D game for Windows. One run takes about 9 minutes.

**Themes:** *Something's Off* (the voice that knows too much) and *Better Together* (two-person chores and the hunt for the traitor).

| | |
|---|---|
| Build deadline | Wed 28 Oct, 11:59 PM Dhaka (our target: 11:00 PM) |
| Pitch deadline | Fri 30 Oct, 11:59 PM Dhaka |
| Submit | Windows build on itch.io + YouTube pitch, both on Hero Hub |

---

## The game

### The story in three parts

| Part | Time | Orders | What happens |
|---|---|---|---|
| **1 · Calm** | 0:00–2:30 | 1–5 | Easy orders: shovel coal, carry a message, close a door. Warm light, a friendly voice and tutorial prompts. Players learn the controls and learn to trust the Captain. The Stoker does nothing yet. |
| **2 · Off** | 2:30–6:30 | 6–11 | Orders start to feel wrong: "keep full speed," "file the ice warning unread," "lock the lifeboat store." The compass and clock disagree, and the Captain knows things he shouldn't. The Stoker is active and the colors cool down. |
| **3 · Truth** | 6:30–8:00 | 12 | The Captain admits he wanted the record for his own name. The lights flicker red and the ETA reads "—". |
| **Iceberg** | 8:00–8:30 | — | 30 seconds of steering with two crew on the wheel. The impact angle decides the damage. |
| **Results** | 8:30+ | — | The ending, the cost of each choice, the Stoker reveal, a short history note and Play again. |

The ship (the *Aurelia*) and the Captain are fictional. The game treats the real 1912 disaster respectfully: no victims are shown, and the game ends with a quiet factual note.

### Who is on board

There are always **4 crew seats**. 1–3 are human players and bots fill the rest. **One bot is secretly the Stoker**, picked at random every run. Judges will most likely play alone, so **1 human + 3 bots has to be fun on its own**. That is the main quality bar for the bots.

### Controls

| Action | Keyboard (P1) | Gamepad (P2, P3) | Notes |
|---|---|---|---|
| Move | W A S D | Left stick | Camera-relative on the deck; no jumping or running |
| Interact / work | E (hold) | A / ✕ (hold) | Shovel, steer, pull a lever, read a message, pick up or drop |
| Report "done" | R | Y / △ | Only for your own order. Honest if the station is green, a fake if it's red |
| Refuse | X | B / ○ | Said over the speaker, so everyone hears it |
| Call for help | Q | X / □ | The nearest free crew member comes to your station |
| Muster vote | F at the bell | RB at the bell | First thing to cut if we're behind |
| Steer (iceberg) | A / D | Left stick | Human inputs on the wheel are averaged; a bot adds none |

### The ship

One screen, seen from a fixed camera about 50–55° down. Walking from stern to bow takes about 6–7 seconds, and every room is always visible. Walls are low (or fade near the camera) so nobody gets hidden.

```
 stern                                                         bow →
┌──────────────┬───────────────────────┬────────────┬─────────────┐
│ BOILER ROOM  │       CORRIDOR        │  WIRELESS  │   BRIDGE    │
│ Boiler       │ Door levers (2p)      │  desk      │  Wheel (2p) │
│ coal chute   │ Lifeboat store        │            │             │
│ (2p)         │ Muster bell 🔔        │            │             │
└──────────────┴───────────────────────┴────────────┴─────────────┘
```

### Stations

| Station | What you do | Two people? |
|---|---|---|
| **Boiler** | Hold to shovel coal into the chute | Yes |
| **Doors** | Pull two levers together: open, closed or locked | Yes |
| **Wheel** | Hold the heading inside a band | Yes |
| **Wireless desk** | Read an incoming message, then file it or carry it on | No |

- Every station has a big **status light**: green means the work is done, red means it isn't (or it's broken). This is how players catch fake reports.
- **Two-person rule:** progress only moves while two crew hold Interact in the station's radius. Use **Call for help** to bring a bot or a friend.
- **Broken stations** (sabotaged by the Stoker) stay red with smoke until someone holds Repair for 3 seconds.
- **Carry orders:** pick an item up at one station and drop it at another (for example, the passenger list from the Wireless desk to the Bridge).

### Orders

The Captain gives a new order every **20–30 seconds**. Each order goes to one crew seat in rotation, humans and bots alike, with its own 20–30 s timer. At most **2 orders** are active at once, and never two for the same seat. Anyone can help with an order, but only the person it was given to chooses what to do with it.

### The three choices

| Choice | How | Normal order | "Off" order |
|---|---|---|---|
| **Obey** | Do the work, then Report done | Captain +5 · Safety +2 | Captain +6 · Safety −12 |
| **Fake it** | Report done while the station is still red | Captain +2 · Safety −4 | Captain +3 · Safety ±0 |
| **Refuse** | Press Refuse (public) | Captain −8 · Safety −3 | Captain −12 · Safety +4 · Crew +4 |
| **Timer runs out** | Do nothing (a quiet refusal) | Captain −5 · Safety −2 | Captain −5 |

Every fake has a **50% chance** that the Captain "notices" it 15–40 seconds later: **Captain −10**, and he names the faker over the speaker. A crewmate standing in the room who sees the red light costs **Crew −5**.

Obeying feels good, but on an "off" order it costs the ship.

### The 12 orders

Red rows in the plan (marked **OFF** here) are the orders that are secretly wrong.

| # | Part | The Captain says | Station | Kind | Timer | What obeying really does |
|---|---|---|---|---|---|---|
| 1 | Calm | "Morning, crew. Boiler two is hungry. Two shovels, quick now." | Boiler (2p) | normal | 30 s | Teaches two-person chores |
| 2 | Calm | "Run the passenger list up to the bridge." | Wireless → Bridge | normal | 30 s | Teaches carrying |
| 3 | Calm | "Close door four. Routine inspection." | Doors (2p) | normal | 25 s | Teaches Report done, and that levers need two |
| 4 | Calm | "Log the weather report at the wireless desk." | Wireless | normal | 25 s | Shows that ice warnings arrive here |
| 5 | Calm | "Two hands on the wheel. Hold her steady while I check the charts." | Wheel (2p) | normal | 30 s | Teaches steering for the finale |
| 6 | Off | "Keep her at full speed. We've a schedule to beat." | Boiler (2p) | **OFF** | 30 s | Speeds up in an ice field |
| 7 | Off | "An ice message? Old news. File it away. Unread." | Wireless | **OFF** | 20 s | The warning never reaches the bridge |
| 8 | Off | "Open door four again. Let the air through." | Doors (2p) | normal | 25 s | A breather, and "how did he know it was closed?" |
| 9 | Off | "Lock the lifeboat store. Can't have people panicking over nothing." | Doors (2p) | **OFF** | 25 s | Fewer lifeboats ready later |
| 10 | Off | "Take my log to the wireless desk. Don't read it." | Bridge → Wireless | normal | 30 s | Optional clue: hold E to read "Record crossing — my name in every paper" |
| 11 | Off | "Two points north. The compass is wrong, not me. Trust me." | Wheel (2p) | **OFF** | 30 s | Steers into the ice field |
| 12 | Truth | "Full ahead. History remembers the fast, not the careful." | Boiler (2p) | **FINAL** | 25 s | Leads into the monologue, then the iceberg |

The orders always come in this order during the jam. Shuffling orders 6–11 is a post-jam replay feature. The first order waits until the tutorial prompt is dismissed.

### The three meters

All meters run from 0 to 100 and start at **Captain's Trust 50 · Ship Safety 80 · Crew Trust 60**.

| Meter | What it does |
|---|---|
| **Captain's Trust** | The temptation, not the ending. At **75+** the Captain shares "private" lines (things he shouldn't know). At **25 or below** he gets suspicious ("I'm watching you, Number Two") and the Stoker's cooldown drops from 40 s to 30 s. |
| **Ship Safety** | Decides the ending, together with the impact angle. Below **50** the HUD shows small leaks and the pumps sound strained. Every sabotage costs Safety. |
| **Crew Trust** | How good the bots are. At **70+** bots answer help calls at once and refuse "off" orders more often. At **30 or below** they take 6 s to come, and sometimes don't. Wrong votes, caught fakes and ignored help calls lower it. |

### Ice warnings

Three numbered ice warnings ("MSG 1, 2, 3") arrive at the Wireless desk during Part 2, at about **3:20, 4:30 and 5:40**, with Morse beeps and a blinking light.

- Anyone can carry one to the Bridge within **40 s**: **Safety +5**.
- If it's late or filed unread: **Safety −5**.
- Honest bots only deliver warnings when they have no active order.
- The numbering is how players spot a warning the Stoker hid: the log jumps from MSG 3 to MSG 5.

### The Stoker

The Stoker is always a bot and secretly follows the Captain's private orders. It does nothing in Part 1. After that it has a **40 s cooldown**, and it only acts when no human is in the same room. It fakes about half of its normal orders and always obeys "off" orders.

| Sabotage | Effect | Clue it leaves |
|---|---|---|
| **Jam a station** | The station breaks (red, smoke) until someone holds Repair for 3 s. **Safety −6** | The Stoker was the last one near that station |
| **Hide an ice warning** | The next warning never shows up. **Safety −8** when it would have arrived | A gap in the wireless log numbering |
| **False report and blame** | Reports another crew member's task as done while it's still red. **Crew −6** | "Number One reports done", but Number One was across the ship |

Players hear a short burst of **speaker static 2–3 s before every sabotage**. It's a learnable clue, and it shows the Captain and the Stoker are on the same side. For the minimum game, 3 sabotages per run is enough.

### Muster vote (first thing to cut)

Any human rings the bell. Everyone has **15 s** to pick a crew portrait. Bots vote for whoever looks most suspicious (who was near a station when it broke, who reported done on a red light), with a little randomness. The majority pick, or the caller's pick on a tie, is **locked out of every station for 60 s** but can still walk around. Humans can be voted out too.

- **Right (the Stoker):** sabotage stops for 60 s, **Crew +10**.
- **Wrong:** **Crew −15**, and the team loses a worker for 60 s.
- 90 s cooldown between votes.

### The iceberg

The iceberg appears dead ahead and two crew hold the wheel (a bot fills in when you play solo, but adds no steering). The instinct is to turn away, which is exactly what makes it worse. At contact, the game measures the angle between the ship's heading and the iceberg:

| Impact | Angle | Extra cost |
|---|---|---|
| Head-on | within 10° | Safety ±0 |
| Glancing | 10–45° | Safety −5 |
| Scrape | above 45° | Safety −10 (the long gash down the side) |

### Endings

| Ending | When |
|---|---|
| **Rare "what if"** | Head-on **and** Safety ≥ 60. The ship stays afloat, then a quiet line: "In April 1912, the real ship was lost." |
| **Many saved** | Safety ≥ 35, **or** head-on. The ship sinks, but the lifeboats got away in time. If the lifeboat store is still locked at impact, this drops to "Captain wins". Any two crew can reopen it at the Door levers at any time. |
| **Captain wins** | Everything else. The lies and the Stoker won. |

The results screen always lists every choice and what it cost, and reveals who the Stoker was.

### "Something's off" moments

These are what make players say "wait, what?". We build at least 4:

1. **The narrator knows:** "You said boiler two was fed, Number Three. It wasn't." There's no camera in that room.
2. **Compass vs clock:** in Part 2 the HUD compass drifts north while the wheel shows straight, and the clock skips back a minute once.
3. **The ETA rises:** "Promised Port: ETA 2 days" goes up after each off order and reads "—" in Part 3.
4. **Subtitles correct themselves:** "We arrive ~~safe~~ fast."
5. **Scrambled warnings:** ice warnings arrive with more words blacked out each time.
6. **The voice changes:** more reverb, a little slower and closer, part by part.

### Replay hooks

- The Stoker is a different random bot every run.
- The results screen shows "Endings found: 1/3" (saved between sessions).
- Sabotage timing changes every run.

### Never cut

Solo play with bots, the three choices, the three meters, the Stoker's sabotage, the iceberg scene, and the results screen that shows what each choice cost. If we fall behind, cut in this order: muster vote → recorded Captain voice → rare ending → order 10 and 1–2 "off" moments → 3 humans → carry orders.

---

## For the team

### Getting started

1. Install **Unity 6.6, version `6000.6.7f1`**, through Unity Hub. It has to be this exact version on all four machines, or Unity will re-import everything and scenes will break.
2. Clone the repo, then in Unity Hub choose **Add → Add project from disk** and pick the repo folder. The first open takes a while while Unity builds `Library/`.
3. Make your own branch before changing anything:

   | Who | Role | Branch | Your sandbox scene |
   |---|---|---|---|
   | Anadi | Systems lead, builds and submission | `anadi-systems` | `Scenes/Sandbox/Anadi_Systems.unity` |
   | Rabib | Players, stations, iceberg scene | `rabib-stations` | `Scenes/Sandbox/Rabib_Stations.unity` |
   | Riham | Bots, the Stoker, the Captain, all writing | `riham-ai` | `Scenes/Sandbox/Riham_AI.unity` |
   | Samprity | Art, audio, UI and feel | `samprity-art` | `Scenes/Sandbox/Samprity_Art.unity` |

### Project setup

Universal 3D (URP) on `6000.6.7f1`, with ProBuilder, AI Navigation, Cinemachine and Input System. Asset Serialization is Force Text, meta files are visible, the window is 1920×1080 Fullscreen Window, the backend is Mono and only the new Input System is on. MainMenu, Ship and Results are in Build Settings.

If something gets lost, run **Port → Run Project Setup** (`Scripts/Editor/ProjectSetup.cs`). It only recreates what's missing.

### Rules that stop merge conflicts

- Only Anadi edits `Ship.unity`, `MainMenu.unity` and `Results.unity`. Build your work as prefabs in your own sandbox scene, then ask Anadi to drop them in.
- Before you touch a shared prefab, say "taking X.prefab" in Discord, and "done" when you're finished.
- Pull `main` before starting anything new. Never force-push `main`.
- Merge into `main` at least every 3 hours, and 30 minutes before each milestone.
- Always commit the `.meta` file together with its asset. Never commit `Library/`, `Temp/`, `Logs/`, `UserSettings/` or builds.
- The files in `Scripts/Core` are shared contracts. Tell the team before you change them.
- If a scene conflicts, keep Anadi's version and redo your small change. Don't hand-merge scene YAML at 2 AM.
- Write commit messages like you're telling a teammate what changed.

### Folder layout

```
Assets/_Project/
  Art/        Models, Materials, Textures, Animations, UI, VFX   (Samprity)
  Audio/      Music, SFX, Voice, Mixer                           (Samprity)
  Data/       Resources/Orders/ (the 12 orders), Endings/        (Anadi; text by Riham)
  Prefabs/    Crew, Stations, UI
  Scenes/     MainMenu, Ship, Results, Sandbox/
  Scripts/    Core + Debug + Editor (Anadi), Crew + Stations (Rabib), AI + Captain (Riham), UI + Audio (Samprity)
  Settings/   URP, Input
```

`Assets/InputSystem_Actions.inputactions` came with the Unity template. Rabib owns input and can rename or replace it.

### How the core systems work (Anadi)

The systems spawn on their own when `Ship.unity`, or any scene whose name ends in `_Systems`, loads. You never need to add them to a scene. In your own sandbox, call `GameSystems.Ensure()` from any script if you want orders and meters running.

**The golden rule:** players and bots use exactly the same station methods, so Rabib and Riham never block each other.

| Need to… | Use |
|---|---|
| Make a crew member count as crew (players and bots) | Implement `ICrewActor`. Call `CrewRegistry.Register(this)` in `OnEnable` and `Unregister` in `OnDisable`. |
| Make a station work with orders (Rabib) | Implement `IStation`. Call `StationRegistry.Register(this)` in `OnEnable`. `IsDone` is the green light. `PrepareForOrder` turns it red. |
| Report "done" or refuse (player input, bots) | `OrderDirector.Instance.Report(actor)` or `.Refuse(actor)`. Report is honest when the station is green and a fake when it's red. |
| See an actor's current order | `OrderDirector.Instance.GetOrderFor(actor)` |
| React to orders (Captain, UI, audio) | `GameEvents.OrderIssued`, `OrderResolved`, `FakeNoticed` |
| React to the story parts | `GameEvents.StateChanged`, `PartChanged`, `TruthStarted` |
| React to stations and ice warnings | `GameEvents.StationBroken`, `StationRepaired`, `HelpCalled`, `IceWarningArrived`, `IceWarningDelivered`, `IceWarningLost` |
| Read the meters | `MeterSystem.Instance.CaptainTrust / ShipSafety / CrewTrust`, and `GameEvents.MetersChanged` |
| Change the meters (sabotage, votes, ice warnings) | `MeterSystem.Instance.Apply(new MeterDelta(captain, safety, crew))` |
| Run the iceberg scene (Rabib) | In `Awake`, set `GameManager.Instance.IcebergHandledExternally = true`. Start when the state becomes `Iceberg`, then call `GameManager.Instance.FinishIceberg(angle)` at impact. |
| Build the results screen (Samprity) | Read `RunRecord`: `Choices`, the final meters, `ImpactAngle`, `StokerSeat`, `LifeboatStoreLocked`. |

The orders are `OrderData` assets in `Data/Resources/Orders`, issued in file-name order (`01_…` to `12_…`). Create new ones from **Create → Port → Order**.

### Debug keys (Editor and Development builds only)

| Key | Action | Key | Action |
|---|---|---|---|
| F1 | skip to the next part | F5 | obey the oldest order |
| F2 | cycle meter presets | F6 | fake it |
| F3 | jump to the iceberg | F7 | refuse it |
| F4 | reveal the Stoker | F8 | restart the run |
| F9 | hide or show the debug panel | | |

### Milestones (Dhaka time)

| When | Milestone |
|---|---|
| Tue 27 · 18:00 | **M1:** one order loop playable with 2 humans + 2 bots, and a first Windows build |
| Wed 28 · 02:00 | **M2:** a full run from menu to results, solo with 3 bots, no softlocks |
| Wed 28 · 17:00 | **M3:** feature freeze. Only bug fixes, text, balance and audio levels after this |
| Wed 28 · 23:00 | **M4:** submitted on Hero Hub, one hour early |
| Fri 30 · 21:00 | **M5:** pitch uploaded to YouTube and submitted |

If a milestone slips by more than an hour, go straight to the cut list. Don't push everything later.

### Assets and credits

Only CC0 or properly licensed third-party assets (for example Kenney packs, or CC0 sounds from Freesound or OpenGameArt). Add every one to `CREDITS.md` when you import it. All game code is written during the jam.

The full team plan is `The_Promised_Port_Plan.html`, shared in the team chat.
