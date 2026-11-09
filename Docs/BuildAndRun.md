# Build and Run

Put a game on a Quest or PICO headset straight from the Unity editor: pick the game, pick the headset, press one button.

## Once per computer

1. In Unity Hub, under **Installs > 6000.3.21f1 > Add modules**, add **Android Build Support** with **OpenJDK** and
   **Android SDK & NDK Tools**.
2. The project must be on the Android build target. If it isn't, the window offers to switch. The first switch
   reimports assets, which takes a while.

## Once per headset

- **Quest:** turn on developer mode for the headset in the Meta Horizon phone app.
- **PICO:** turn on USB debugging in the headset's developer settings.
- Plug the headset into the computer with a USB-C **data** cable. Put it on and accept **Allow USB debugging**, with
  **Always allow** ticked.

## Every time

1. Open **MetaHackthone > Build and Run...**. There is also a toolbar version next to Play, with a dropdown for the
   game and headset and a **Build and Run** button. Unity hides new toolbar items, so turn it on once: right-click the
   main toolbar, or open its **⋮** menu at the right end, and tick **MetaHackthone/Build and Run**.
2. **Game:** for example **Shape of Memory**. **Headset:** **Any headset** picks the first plugged-in headset the game
   can run on.
3. Press **Build and Run on ...**. Unity asks to save open scenes, builds, installs and starts the app.
   - The first build takes several minutes. Later builds are quicker.
   - If the headset is asleep, the app starts when you put it on.

**Start at** (Shape of Memory) picks a level to open straight into, instead of the title.
- One of the four opens its chapter.
- Any other level in `Resources/ShadowArt/Torch2`, such as yours from the gym, plays as a book of one chapter. It keeps
  its own progress, so the game's isn't touched.

Under **Last build**, the window says whether the APK is out of date:
- **Out of date** means the game's code or the picked level changed after the build. Build and Run to include it.
- **Up to date** means **Restart** starts the level without building.

| Button | What it does |
| --- | --- |
| **Build and Run** | Build, install, start. Starting always restarts the app from the beginning. |
| **Build only** | Build the APK without a headset. Use **Headset: No headset (build only)** to pick Quest or Pico. |
| **Install and Run** | Install the last build again, without building. |
| **Restart** / **Stop** | Start the installed app again, or close it. |
| **Clear data** | Delete the app's saved progress on the headset. For example, Shape of Memory's unlocked levels. |
| **Uninstall** | Remove the app and its saved progress from the headset. |

Options:
- **Development build** (on by default) keeps debug keys, such as Shape of Memory's **N** and **K**, and full logs.
  Turn it off for the build you hand in.
- **Save headset log** streams the app's log to `Logs/Headsets/` while it runs. PICO keeps only a few minutes of log, so
  this is the only way to read a session afterwards.
- **Clean build** rebuilds everything from scratch, which is slow. Use it when a build behaves oddly.

## From the Shadow Gym

The gym's inspector has **Run on headset** under **Level**. It exports the level, builds, installs, then starts Shape of
Memory at the level on the headset picked in Build and Run.

It rebuilds every time. Export rewrites the level file even when nothing changed, and the out-of-date check goes by file
times. The first build takes several minutes; later ones take a minute or two. To switch between levels already in the
last build without building, use the window's **Start at** and **Restart**.

## Where things go

- APKs go in `Builds/`: `<name>.apk` for Quest and `<name>-Pico.apk` for PICO. Each has a `<apk>.json` next to it
  recording what was built and when.
- Logs go in `Logs/Headsets/<apk>-<headset>-<time>.log`.

## When it goes wrong

| Message | What to do |
| --- | --- |
| No headset found | Use a data cable, not a charging-only one, and check developer mode. Try another USB port. |
| Unauthorized headset | Put the headset on and accept **Allow USB debugging**. |
| The copy on the headset was built on another computer | Every computer signs its builds with its own key, so Android won't update one computer's build with another's. The window offers to uninstall the old copy first. That also deletes its saved progress on the headset. |
| Install fails with `INSTALL_FAILED_MISSING_SHARED_LIBRARY` | The APK asks for a Horizon OS library the headset doesn't have. `StripPlatformSupplement` already removes the Meta Platform SDK's `hzplatformclientcore` request (Quest 2 on Horizon OS v74 lacks it), so a new package must have added another. Ask an engineer. |
| Black screen on a Quest 2 | Horizon OS older than v65. Update the headset. |
| adb isn't working | Android Build Support is missing from this Unity install. See "Once per computer". |
| Unity crashed during a build | Nothing to do. When the editor next loads, it restores the project's package id, name, default icon and Quest configuration, and logs `BUILD_AND_RUN_RECOVERED`. Don't check in `ProjectSettings` or XR settings that a crashed build left changed before then. |

## Adding a game

**Assets > Create > MetaHackthone > Build and Run config.** The window lists every config in the project, wherever it
lives.

| Field | Meaning |
| --- | --- |
| Scenes | The scenes to build, first one first. Empty means the project's build list (File > Build Profiles). |
| Package Id / Product Name | Empty means the project's own. A separate id installs next to the other games instead of replacing them. |
| Apk Name | File name in `Builds/`, without `.apk`. |
| Icon | Optional. The app icon for this game's builds only: square, 1024 px, opaque. The build sets it as the project's default icon and puts the old one back afterwards. Empty means the project's own (none at the moment). |
| Quest / Pico | The platforms it builds for. |
| Level Folder | Optional. A folder of level assets. The window offers them under **Start at**, and the launch passes the picked one's file name as the launch argument `level`. |

Current configs:
- **Shape of Memory** (`Assets/ShadowWorkshop/PicoFinal/Editor/`): `PicoFinal.unity` alone, as
  `com.metahackathon2026.shadow`, built to `ShapeOfMemory.apk`. Its levels come from `Resources/ShadowArt/Torch2`.
  Its icon is `PicoFinal/Art/AppIcon/ShapeOfMemory-icon.png`.

## For engineers

- `BuildRunner.Build(config, platform, development, clean)` is the only build path. The old menu items
  (**Build Quest APK**, **Pico Final > Build Pico APK**) call it.
  - Development builds use `Development | CompressWithLz4`. Release builds use `CompressWithLz4HC`.
  - The old APK is deleted first. An up-to-date incremental build would otherwise leave an identical file with its old
    timestamp.
- A build temporarily changes tracked settings: the package id and name, and for PICO the OpenXR features, the manifest
  and the PICO config (`PicoBuild.Prepare`).
  - The originals go to `Library/MetaHackthoneBuild/session.json` first.
  - The build's `finally` block puts them back. After a crash, the next editor load does.
  - `BuildRunner.IsPico` tells `PicoBuild`'s hooks which kind of build is running. Builds started outside BuildRunner
    still go by the `-Pico.apk` file name.
- Launch arguments: `Headsets.Launch` force-stops the app, then runs `am start -n <activity> --es level '<id>'`.
  - `PhysicalAssembly.LaunchArguments.Get("level")` reads the start intent's extras. In the editor and in tests it reads
    `LaunchArguments.Overrides`.
  - `MemoryGame.Start` uses it: one of the four goes through `Load(index, force)`, and any other `Torch2Level` gets a
    `MemoryProgress` with that one id.
- `BuildRunner.Stale` decides whether an APK is out of date. It compares the APK's time with:
  - the editor's copy of every player assembly in `Library/ScriptAssemblies`. Unity rewrites one only when its code
    recompiles.
  - the level's `AssetDatabase.GetDependencies`.

  Scene or other asset changes outside the level aren't noticed.
- Build and Run records each install in EditorPrefs (APK time per headset and package). `Headsets.HasInstalled` checks
  that record and `pm path`, so Run on headset can skip the install.
- The gym's button is a hook: `Torch2GymTools.RunOnHeadset`, set by `ShapeOfMemoryBuilder`. The gym itself doesn't
  depend on Build and Run.
- `Headsets` only uses the editor's own adb. The PATH adb is another version and kills the editor's adb server.
  - It tells Quest from PICO by `ro.product.manufacturer`.
  - Logs stream through `cmd.exe`, so capture survives script reloads.
- Batch builds (close the editor first):
  `Unity.exe -batchmode -quit -projectPath . -buildTarget Android -executeMethod PhysicalAssembly.Editor.BuildRunner.BuildFromCommandLine -deployConfig "Shape of Memory" -devicePlatform Quest`.
  Add `-release` for a release build and `-clean` for a clean one.
- MCP: `execute_menu_item` **MetaHackthone/Build and Run again** builds and runs whatever the window has picked. Builds
  can't start from `execute_code`, which runs inside the player loop.
