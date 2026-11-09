# Shape of Memory

A hands-only mixed-reality shadow puzzle for Meta Quest and PICO. A pile of everyday objects floats in an orb between
two lamps. Turn it until its shadows on the walls become a remembered picture. The chapters live in a book that opens
onto the walls.

This repository holds the game and the shadow levels and tools it grew out of. It was split out of the team's
MetaHackathon2026 project, so some folder, menu and namespace names still say `MetaHackthone` or `CloudGarden`.

## Requirements

- Unity **6000.3.21f1** with Android Build Support (SDK, NDK, OpenJDK).
- Git on the PATH. Unity fetches two packages from GitHub on first open:
  - the PICO OpenXR SDK 1.4.1 (`Pico-Developer/PICO-Unity-OpenXR-SDK`, pinned to commit `3aa3e62`);
  - MCP for Unity 10.3.0 (`CoplayDev/unity-mcp`), an editor-only tool for AI assistants.
- Meta XR All-in-One SDK **207.0.0**, OpenXR **1.17.0** and URP **17.3.0**, all from the Unity registry.

The first open imports everything and takes a while.

## Play

- **Shape of Memory:** **MetaHackthone > Pico Final > Play**. On desktop it runs with the mouse. The scene is
  `Assets/ShadowWorkshop/PicoFinal/Scenes/PicoFinal.unity`.
- **Shadow level browser:** open any scene in `Assets/ShadowWorkshop/Scenes/` and press Play. PREV / NEXT cycle
  through every shadow level. `TorchLevels2.unity` holds the four orb levels the game uses.
- **Shadow Gym** (level design): **MetaHackthone > Shadow Workshop > Gym > Open gym**.

## Put it on a headset

**MetaHackthone > Build and Run...** Pick **Shape of Memory**, then a Quest or a PICO plugged in over USB, then press
Build and Run. It builds `Builds/ShapeOfMemory[-Pico].apk` as `com.metahackathon2026.shadow`, installs it, starts it
and saves the device log. See [Docs/BuildAndRun.md](Docs/BuildAndRun.md).

## Layout

| Path | What it is |
| --- | --- |
| `Assets/ShadowWorkshop/PicoFinal/` | The game: scene, `Memory*` runtime, book and page shaders, sky, icons, sounds and tests |
| `Assets/ShadowWorkshop/Runtime/` | Shadow projection, scoring and levels (`Torch2*` is the orb, also used by the game) |
| `Assets/ShadowWorkshop/Integration/` | Hands, rays and the level browser (`MetaShadowPlayer`, `ShadowDemo`) |
| `Assets/ShadowWorkshop/Editor/` | The Shadow Gym and the level importers |
| `Assets/ShadowWorkshop/Resources/ShadowArt/` | Level assets (`Torch2/` holds the game's chapters) |
| `Assets/MetaHackthone/` | The shared player rig, pinch handling, PICO passthrough, and Build and Run |
| `Assets/Scenes/` | Prop sets used by the levels, with their import tool (`Tools > Scene Props`) |
| `Tools/ShadowArt/` | Python generators for the level geometry and icons |
| `Docs/` | [Shape of Memory](Docs/ShapeOfMemory.md), [shadow levels and gym](Docs/ShadowWorkshop.md), [Build and Run](Docs/BuildAndRun.md) |

## Tests

Use **Window > General > Test Runner**. EditMode covers projection, scoring, the gym, the book and the build configs.
PlayMode covers the game flow and the level browser.

## Third-party content

- `Assets/Scenes/Store/Sari3D/CozyShops_Lite` (CozyShops Lite by Sari3D) and
  `Assets/ShadowWorkshop/Art/Torch2/Free Low Poly Game Assets` are free Unity Asset Store packs, under the Asset Store
  EULA.
- Meta XR SDK, PICO OpenXR SDK and the other packages come in through `Packages/manifest.json` and keep their own
  licences.
