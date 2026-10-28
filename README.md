# The Promised Port

A short co-op ship game for 1–3 players. You're the newest crew member on a four-deck passenger liner, two days out to the Promised Port. Follow the Captain's orders, keep the ship running, and decide how far you trust the warm voice on the speaker, because something about this voyage is off.

Made for GameJamPlus 2026/27 (GJ+ Bangladesh) by Anadi, Rabib, Riham and Samprity, in Unity 6.6. A run takes about 9 minutes, and bots fill any empty crew seats, so you can play solo.

**Themes:** Something's Off · Better Together

## How to play

- The Captain gives **you** the orders, on a timer: shovel coal at the Boiler, hold the Wheel, pull the Door levers, work the Wireless desk. For each one you can **obey**, **fake it** or **refuse**.
- The bots are the rest of the crew. They repair things, carry ice warnings, help when you call, and do chores around the ship (galley, saloon, bunks, coal) in between. One of them is secretly the Captain's man.
- Some jobs need two people, so call a crewmate for help.
- Watch the station lights, the three meters (Captain's Trust, Ship Safety, Crew Trust) and the readout under the clock: compass, wheel, clock and how far it is to port.
- The minimap in the bottom-right corner shows the deck, the stations, your crew and any ice warning waiting to be carried.
- Listen to your crewmates. When an order sounds wrong, someone near you may whisper a doubt, or urge you to obey.
- Study the chart on the bridge table (**E**). It has the course everyone was told, and another one in pencil.
- The voyage has three parts, each opened by its own title card: *Just doing my job*, *Why is he asking me this?* and *What are we sailing toward?*. It ends at the iceberg, with the wheel in your hands, and one of three endings.

| Action | Keyboard and mouse | Gamepad |
|---|---|---|
| Move | W A S D | Left stick |
| Look around | Mouse | Right stick |
| Interact (hold) | E | A / ✕ |
| Report done | R | Y / △ |
| Refuse | X | B / ○ |
| Call for help | Q | X / □ |
| Study the chart (at the bridge table) | E | A / ✕ |
| Skip a part's title card | Enter or Space | A / ✕ |
| Pause | Esc | Start |

The main menu has **Begin Voyage**, **Settings**, **Credits** and **Quit**. The how-to-play card is under **Settings → Controls**. It no longer pops up when the voyage starts. Friends join by pressing **Start** on a controller, up to 3 players, and bots fill the other seats. Every menu works with the mouse, the keyboard (arrows, Enter, Esc) or a gamepad (d-pad, A, B).

## Getting started

1. Install **Unity 6.6, version `6000.6.7f1`**, through Unity Hub. It has to be this exact version on all four machines, or Unity will re-import everything and scenes will break.
2. Clone the repo, then in Unity Hub choose **Add → Add project from disk** and pick the repo folder. The first open takes a while while Unity builds `Library/`.
3. Make your own branch before changing anything:

   | Who | Role | Branch | Your sandbox scene |
   |---|---|---|---|
   | Anadi | Systems lead, builds and submission | `anadi-systems` | `Scenes/Sandbox/Anadi_Systems.unity` |
   | Rabib | Players, stations, iceberg scene | `rabib-stations` | `Scenes/Sandbox/Rabib_Stations.unity` |
   | Riham | Bots, the Captain, all writing | `riham-ai` | `Scenes/Sandbox/Riham_AI.unity` |
   | Samprity | Art, audio, UI and feel | `samprity-art` | `Scenes/Sandbox/Samprity_Art.unity` |

## Project setup

Universal 3D (URP) on `6000.6.7f1`, with ProBuilder, AI Navigation, Cinemachine and Input System. Asset Serialization is Force Text, meta files are visible, the window is 1920×1080 Fullscreen Window, the backend is Mono and only the new Input System is on. MainMenu, Ship and Results are in Build Settings.

If something gets lost, run **Port → Run Project Setup** (`Scripts/Editor/ProjectSetup.cs`). It only recreates what's missing.

## Rules that stop merge conflicts

- Only Anadi edits `Ship.unity`, `MainMenu.unity` and `Results.unity`. Build your work as prefabs in your own sandbox scene, then ask Anadi to drop them in. **Port → Assemble Ship Scene** does that: it puts Samprity's `ShipDeck`, a whole-deck camera, Rabib's player join, his station scripts on the deck's `Station_*` objects and the carry items for orders 2 and 10 into `Ship.unity`. Run it again whenever the deck is rebuilt.
- Before you touch a shared prefab, say "taking X.prefab" in Discord, and "done" when you're finished.
- Pull `main` before starting anything new. Never force-push `main`.
- Merge into `main` at least every 3 hours, and 30 minutes before each milestone.
- Always commit the `.meta` file together with its asset. Never commit `Library/`, `Temp/`, `Logs/`, `UserSettings/` or builds.
- The files in `Scripts/Core` are shared contracts. Tell the team before you change them.
- If a scene conflicts, keep Anadi's version and redo your small change. Don't hand-merge scene YAML at 2 AM.
- Write commit messages like you're telling a teammate what changed.

## Folder layout

```
Assets/_Project/
  Art/        Models, Materials, Textures, Animations, UI, VFX   (Samprity)
  Audio/      Music, SFX, Voice, Mixer                           (Samprity)
  Data/       Resources/Orders/ (the 12 orders), Resources/Endings/ (the 3 endings)   (Anadi; text by Riham)
  Prefabs/    Crew, Stations, UI
  Scenes/     MainMenu, Ship, Results, Sandbox/
  Scripts/    Core + Debug + Editor (Anadi), Crew + Stations (Rabib), AI + Captain (Riham), UI + Audio (Samprity)
  Settings/   URP, Input
```

`Assets/InputSystem_Actions.inputactions` came with the Unity template. Rabib owns input and can rename or replace it.

## How the core systems work (Anadi)

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
| Run the ice warnings (Rabib's Wireless desk, Riham's Stoker) | `IceWarnings` schedules MSG 1–3 at 3:20, 4:30 and 5:40 and applies the meters. Show one on `GameEvents.IceWarningArrived(n)`, call `IceWarnings.Instance.Deliver(n)` at the Bridge or `FileUnread(n)` at the desk, and use `TimeLeft(n)` for a countdown. The Stoker calls `HideNext()`. |
| Use the plan's numbers for sabotage (Riham) | `MeterRules.SabotageJamStation`, `SabotageFalseReport`, `StokerActive`, `RepairSeconds` and the rest. Apply them with `MeterSystem.Instance.Apply(...)`. The muster vote is switched off in the final plan (`CrewAISystems` no longer adds `MusterVote`; the code is still there). |
| Use what the meters do (Riham, Samprity) | `MeterRules`: `StokerCooldown`, `BotHelpDelay`, `BotIgnoresHelpChance`, `BotRefusesOffOrderChance`, `CaptainSharesPrivateLines`, `CaptainSuspicious`, `ShipLeaking` |
| Spawn the players who joined (Rabib) | `PlayerSlots.Devices[i]` is seat `i + 1`'s device: `PlayerInput.Instantiate(prefab, playerIndex: i, pairWithDevice: device)`. With nobody joined (pressing Play in a scene), use `PlayerSlots.FirstDevice`. |
| Fill the empty seats with bots (Riham) | `PlayerSlots.HumanCount` humans, `PlayerSlots.BotCount` bots. Pick the Stoker when the crew spawns: Restart and Play again reload the scene, so it re-rolls. |
| Pause, restart, change scene | `PauseMenu.Instance.Toggle()`, `GameManager.Restart()`, `GameManager.LoadScene(name)`. Don't bind Esc or Start yourself: the pause menu already handles them. |
| Reopen the lifeboat store (Rabib) | Set `RunRecord.LifeboatStoreLocked = false` when two crew pull the Door levers. Obeying order 9 locks it. |
| Run the iceberg scene (Rabib) | In `Awake`, set `GameManager.Instance.IcebergHandledExternally = true`. Start when the state becomes `Iceberg`, then call `GameManager.Instance.FinishIceberg(angle)` at impact. |
| The ending screen | `UI/ResultsScreen.cs` draws it from `RunRecord`: the ending's ribbon, the Voyage Chart (every order as a badge along the route that bent north to Grey Cove), the three meters as dials, a WANTED poster for the Stoker, the endings found as ships in bottles, and wooden-sign buttons. The art is painted at runtime by `UI/EndingPainter.cs`; the text is Pirata One and Patrick Hand (`Art/UI/Resources/Fonts`). Ending text is `EndingData.For(RunRecord.Ending)`. |
| Who gets the orders | `OrderDirector` gives every Captain's order to a human whenever one is aboard (bots only get orders in a bots-only run). Quick obeys earn a speed reward. |
| Bots with nothing to do | `AI/BotChores.cs`: idle bots walk to the deck's `Chore_*` markers and work them. `BotBrain` calls it from its idle branch. |
| Crew voices and names | `UI/CrewWhispers.cs` (a crewmate's doubt, or the Stoker's push, on suspicious orders), `UI/CrewNameTags.cs` (names over heads). Names come from `CrewRegistry.CrewName(actor)`. |
| The Grey Cove reveal | `UI/ShipReadout.cs` turns the "Port" row into a countdown to GREY COVE during the truth speech. `ShipReadout.RevealGreyCove()` forces it. |
| The bridge chart | `UI/ChartView.cs`: "E Study the chart" at the chart table opens `Art/Textures/BridgeChart.png` full screen. |
| A part's title card | `UI/PartCard.cs` listens to `GameEvents.StateChanged`, closes the screen in a circle, shows the part's card (`Art/UI/Resources/PartCards`) and freezes time. Check `PartCard.IsShowing` before starting anything a player should see (the Captain's director already waits for it). |
| The upper decks | `ShipDeckBuilder` builds two cabin floors and an open top deck (lifeboats, second funnel, vents, deckhouse) over the deck you play on. They have no colliders. Each 8 m bay has a `RoofFade`, so the floors over the player and the camera cut away. |

The orders are `OrderData` assets in `Data/Resources/Orders`, issued in file-name order (`01_…` to `12_…`). Create new ones from **Create → Port → Order**.

## Debug keys (Editor and Development builds only)

| Key | Action | Key | Action |
|---|---|---|---|
| F1 | skip to the next part | F5 | obey the oldest order |
| F2 | cycle meter presets | F6 | fake it |
| F3 | jump to the iceberg | F7 | refuse it |
| F4 | reveal the Stoker | F8 | restart (reloads the scene) |
| F9 | hide or show the debug panel | 1 / 2 / 3 | at the iceberg: hit head-on, glancing or scrape |
| F10 | deliver the waiting ice warning | F11 | the Stoker hides the next ice warning |

## Builds (Anadi)

Use **Port → Build Windows (Development)** at each milestone. Development builds keep the debug keys and panel. Use **Port → Build Windows (Release)** for RC1, RC2 and the submission. It drops the debug keys, sets the version to 1.0, adds a `README.txt` (controls and player count), and packs `Builds/ThePromisedPort_Windows_v1.0.zip` without the Burst `..._ButDontShipItWithYourGame` folder. Builds go to `Builds/`, which git ignores.

The screens share keyboard and gamepad navigation through `UI/MenuControls`. The main menu is Samprity's `Intro/OpeningMenu`; the how-to-play text (`IntroCard.BuildManual`) lives under its **Settings → Controls**. Ship has `UI/PauseMenu`, the part cards (`UI/PartCard`) and the chart view; Results has the ending screen (`UI/ResultsScreen`).

**Balance:** run **Port → Balance Report** after changing any order's numbers. It plays the 12 orders through common strategies and prints the ending each impact gives, so you can check all 3 endings stay reachable.

## Milestones (Dhaka time)

| When | Milestone |
|---|---|
| Tue 27 · 18:00 | **M1:** one order loop playable with 2 humans + 2 bots, and a first Windows build |
| Wed 28 · 02:00 | **M2:** a full run from menu to results, solo with 3 bots, no softlocks |
| Wed 28 · 17:00 | **M3:** feature freeze. Only bug fixes, text, balance and audio levels after this |
| Wed 28 · 23:00 | **M4:** submitted on Hero Hub, one hour early |
| Fri 30 · 21:00 | **M5:** pitch uploaded to YouTube and submitted |

If a milestone slips by more than an hour, go straight to the cut list. Don't push everything later.

## Assets and credits

Only CC0 or properly licensed third-party assets (for example Kenney packs, or CC0 sounds from Freesound or OpenGameArt). Add every one to `CREDITS.md` and to `Data/Resources/Credits.txt` when you import it, since that file is the in-game credits screen. Fonts (Crimson Text, Cinzel, Special Elite, Pirata One, Patrick Hand, Bungee) are all SIL OFL or Apache, with their licences beside them. All game code is written during the jam.

The full team plan is `The_Promised_Port_Plan.html`, shared in the team chat.
