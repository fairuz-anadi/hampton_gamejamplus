# The Promised Port

GameJamPlus 2026/27 entry. A 2.5D co-op ship game in Unity, made by Anadi, Rabib, Riham and Samprity.

## Getting started

1. Install **Unity 6.6, version `6000.6.7f1`**, through Unity Hub. It has to be this exact version on all four machines, or Unity will re-import everything and scenes will break.
2. Clone the repo, then in Unity Hub choose **Add → Add project from disk** and pick the repo folder.
3. Make your own branch before changing anything:

   | Who | Branch | Your sandbox scene |
   |---|---|---|
   | Anadi | `anadi-systems` | `Scenes/Sandbox/Anadi_Systems.unity` |
   | Rabib | `rabib-stations` | `Scenes/Sandbox/Rabib_Stations.unity` |
   | Riham | `riham-ai` | `Scenes/Sandbox/Riham_AI.unity` |
   | Samprity | `samprity-art` | `Scenes/Sandbox/Samprity_Art.unity` |

## Project setup (done)

The Unity project is set up: Universal 3D (URP) on `6000.6.7f1` with ProBuilder, AI Navigation, Cinemachine and Input System. Asset Serialization is Force Text, meta files are visible, the window is 1920×1080 Fullscreen Window, the backend is Mono and only the Input System is on. The 7 scenes are in `_Project/Scenes`, and MainMenu, Ship and Results are in Build Settings.

If something gets lost, run **Port → Run Project Setup** (`Scripts/Editor/ProjectSetup.cs`). It only recreates what's missing.

## Rules that stop merge conflicts

- Only Anadi edits `Ship.unity`, `MainMenu.unity` and `Results.unity`. Build your work as prefabs in your own sandbox scene, then ask Anadi to drop them in.
- Before you touch a shared prefab, say "taking X.prefab" in Discord, and "done" when you're finished.
- Merge into `main` at least every 3 hours, and 30 minutes before each milestone.
- Always commit the `.meta` file together with its asset. Never commit `Library/`, `Temp/`, `Logs/` or builds.
- The files in `Scripts/Core` are shared contracts. Tell the team before you change them.
- Write commit messages like you're telling a teammate what changed.

## Folder layout

```
Assets/_Project/
  Art/        Models, Materials, Textures, Animations, UI, VFX   (Samprity)
  Audio/      Music, SFX, Voice, Mixer                           (Samprity)
  Data/Resources/Orders/  the 12 Captain orders                  (Anadi; text by Riham)
  Prefabs/    Crew, Stations, UI
  Scenes/     MainMenu, Ship, Results, Sandbox/
  Scripts/    Core (Anadi), Debug (Anadi), Crew + Stations (Rabib), AI + Captain (Riham), UI + Audio (Samprity)
  Settings/
```

`Assets/InputSystem_Actions.inputactions` came with the Unity template. Rabib owns input, so he can rename or replace it.

## How the core systems work (Anadi)

Systems spawn on their own when `Ship.unity`, or any scene whose name ends in `_Systems`, loads. You never need to add them to a scene. In your own sandbox, call `GameSystems.Ensure()` from any script if you want orders and meters running.

| Need to… | Use |
|---|---|
| Make a crew member count as crew (players and bots) | Implement `ICrewActor`. Call `CrewRegistry.Register(this)` in `OnEnable` and `Unregister` in `OnDisable`. |
| Make a station work with orders (Rabib) | Implement `IStation`. Call `StationRegistry.Register(this)` in `OnEnable`. `IsDone` is the green light. `PrepareForOrder` turns it red. |
| Report "done" or refuse (player input, bots) | `OrderDirector.Instance.Report(actor)` or `.Refuse(actor)`. Report is honest when the station is green and a fake when it's red. |
| See an actor's current order | `OrderDirector.Instance.GetOrderFor(actor)` |
| React to orders (Captain, UI, audio) | `GameEvents.OrderIssued`, `OrderResolved`, `FakeNoticed` |
| React to the story parts | `GameEvents.StateChanged`, `PartChanged`, `TruthStarted` |
| Read the meters | `MeterSystem.Instance.CaptainTrust / ShipSafety / CrewTrust`, and `GameEvents.MetersChanged` |
| Change the meters (sabotage, votes, ice warnings) | `MeterSystem.Instance.Apply(new MeterDelta(captain, safety, crew))` |
| Run the iceberg scene (Rabib) | In `Awake`, set `GameManager.Instance.IcebergHandledExternally = true`. Start when the state becomes `Iceberg`, then call `GameManager.Instance.FinishIceberg(angle)` at impact. |
| Build the results screen (Samprity) | Read `RunRecord`: `Choices`, the final meters, `ImpactAngle`, `StokerSeat`. |

### Debug keys (Editor and Development builds only)

| Key | Action | Key | Action |
|---|---|---|---|
| F1 | skip to the next part | F5 | obey the oldest order |
| F2 | cycle meter presets | F6 | fake it |
| F3 | jump to the iceberg | F7 | refuse it |
| F4 | reveal the Stoker | F8 | restart the run |
| F9 | hide or show the debug panel | | |

The full team plan is `The_Promised_Port_Plan.html`, shared in the team chat.
