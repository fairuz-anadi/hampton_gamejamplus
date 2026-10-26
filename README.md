# The Promised Port

A short co-op ship game for 1–3 players. Follow the Captain's orders, keep the ship running, and decide how far you trust the calm voice on the speaker.

Made for GameJamPlus 2026/27 (GJ+ Bangladesh) by Anadi, Rabib, Riham and Samprity, in Unity 6.6. A run takes about 9 minutes, and bots fill any empty crew seats, so you can play solo.

**Themes:** Something's Off · Better Together

## How to play

- Run between the Boiler, the Wheel, the Door levers and the Wireless desk.
- The Captain gives orders on a timer. For each one you can **obey**, **fake it** or **refuse**.
- Some jobs need two people, so call a crewmate for help.
- Keep an eye on the station lights and the three meters.

| Action | Keyboard | Gamepad |
|---|---|---|
| Move | W A S D | Left stick |
| Interact (hold) | E | A / ✕ |
| Report done | R | Y / △ |
| Refuse | X | B / ○ |
| Call for help | Q | X / □ |
| Pause | Esc | Start |

On the main menu, press **Space** (keyboard) or **A** (gamepad) to take a seat, then **Enter** or **Start** to set sail. Up to 3 players can join, and bots fill the rest.

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

- Only Anadi edits `Ship.unity`, `MainMenu.unity` and `Results.unity`. Build your work as prefabs in your own sandbox scene, then ask Anadi to drop them in.
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
| Use what the meters do (Riham, Samprity) | `MeterRules`: `StokerCooldown`, `BotHelpDelay`, `BotIgnoresHelpChance`, `BotRefusesOffOrderChance`, `CaptainSharesPrivateLines`, `CaptainSuspicious`, `ShipLeaking` |
| Spawn the players who joined (Rabib) | `PlayerSlots.Devices[i]` is seat `i + 1`'s device: `PlayerInput.Instantiate(prefab, playerIndex: i, pairWithDevice: device)`. With nobody joined (pressing Play in a scene), use `PlayerSlots.FirstDevice`. |
| Fill the empty seats with bots (Riham) | `PlayerSlots.HumanCount` humans, `PlayerSlots.BotCount` bots. Pick the Stoker when the crew spawns: Restart and Play again reload the scene, so it re-rolls. |
| Pause, restart, change scene | `PauseMenu.Instance.Toggle()`, `GameManager.Restart()`, `GameManager.LoadScene(name)`. Don't bind Esc or Start yourself: the pause menu already handles them. |
| Reopen the lifeboat store (Rabib) | Set `RunRecord.LifeboatStoreLocked = false` when two crew pull the Door levers. Obeying order 9 locks it. |
| Run the iceberg scene (Rabib) | In `Awake`, set `GameManager.Instance.IcebergHandledExternally = true`. Start when the state becomes `Iceberg`, then call `GameManager.Instance.FinishIceberg(angle)` at impact. |
| Build the results screen (Samprity) | Read `RunRecord`: `Ending`, `Impact`, `Choices` (each has `Describe()`), the final meters, `StokerSeat`. Ending text is `EndingData.For(RunRecord.Ending)`, and `EndingEvaluator.FoundCount` gives "Endings found". `UI/ResultsScreen.cs` is a working placeholder to replace. |

The orders are `OrderData` assets in `Data/Resources/Orders`, issued in file-name order (`01_…` to `12_…`). Create new ones from **Create → Port → Order**.

## Debug keys (Editor and Development builds only)

| Key | Action | Key | Action |
|---|---|---|---|
| F1 | skip to the next part | F5 | obey the oldest order |
| F2 | cycle meter presets | F6 | fake it |
| F3 | jump to the iceberg | F7 | refuse it |
| F4 | reveal the Stoker | F8 | restart (reloads the scene) |
| F9 | hide or show the debug panel | 1 / 2 / 3 | at the iceberg: hit head-on, glancing or scrape |

## Builds (Anadi)

Use **Port → Build Windows (Development)** at each milestone. Development builds keep the debug keys and panel. Use **Port → Build Windows (Release)** for RC2 and the submission, which drops them. Builds go to `Builds/`, which git ignores. When zipping a build, leave out the `..._BackUpThisFolder_ButDontShipItWithYourGame` folder.

MainMenu, Ship and Results each have a working placeholder screen (`UI/MainMenuScreen`, `UI/PauseMenu`, `UI/ResultsScreen`) for Samprity to restyle.

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

Only CC0 or properly licensed third-party assets (for example Kenney packs, or CC0 sounds from Freesound or OpenGameArt). Add every one to `CREDITS.md` when you import it. All game code is written during the jam.

The full team plan is `The_Promised_Port_Plan.html`, shared in the team chat.
