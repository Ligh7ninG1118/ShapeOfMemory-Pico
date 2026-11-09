# Shape of Memory

The posable-pile shadow levels (Torch levels 2; the book's four chapters are listed in its [Chapters](#chapters)
asset) as a game rather than a debug level browser. Everything lives in `Assets/ShadowWorkshop/PicoFinal/`. The
deliverable scene is `Scenes/PicoFinal.unity`.

## Run it

- **MetaHackthone > Pico Final > Play** opens the scene and enters Play. It runs on desktop with the mouse.
- **MetaHackthone > Pico Final > Create or rebuild scene** regenerates the scene. The scene only holds the `MemoryGame`
  director and a light, and everything else is built at runtime.
- **MetaHackthone > Pico Final > Clear saved progress** wipes the editor's saved progress, and the game's app data on
  every connected headset (`adb shell pm clear com.metahackathon2026.shadow` through the editor's adb). The headset
  keeps its progress there, so the editor's own clear never reached it.
- The scene is first in the project's build list; the **Shape of Memory** build config builds it alone either way.
- **Start at a level** (launch argument `level`, from Build and Run's **Start at** or the gym's **Run on headset**):
  - One of the book's chapters opens straight into its intro, locked or not.
  - Any other `Torch2Level` in `Resources/ShadowArt/Torch2` plays as a book of one chapter, then its ending. Its
    progress keys are its own id, so the game's progress is untouched.
  - The ghost-pinch guide only runs on chapter I of the real book.
  - The log line `SHAPE_OF_MEMORY_LAUNCH` says what was opened.

## Chapters

The book's levels, in play order, are one asset: `Resources/ShapeOfMemory/Chapters.asset` (`MemoryChapters`). Open
it with **MetaHackthone > Pico Final > Chapters**. Nothing else in the game or its tests names a level.

- **To swap a level in:** make it in the Shadow Gym and **Export** it to `Resources/ShadowArt/Torch2/<id>.asset`. Fill
  in Button (the chapter's name), Riddle, Hint and Complete before you export. Then drag the asset into a slot.
- **To reorder:** drag the rows by their handles.
- **Each row has two buttons.** **Gym** opens that level in the Shadow Gym. **Headset** starts the game at that chapter
  on Build and Run's headset.
- **These go with the slot, not the level:**
  - the chapter number;
  - its colour (`MemoryPalette`, by order: Rose, Cornflower, Lilac, Peach);
  - the teaching, which happens on chapter I only (the ghost pinch and the goal line);
  - the ending's 2×2 plates. That layout is why the book has exactly four chapters.
- **Progress is saved per level id (the asset's name).** A level that is swapped in starts unfound, and the
  returning-player bookmark goes to it. Renaming a chapter's asset loses its saves. **Clear saved progress** clears the
  keys of every Torch2 level, including levels that are no longer in the book.
- **Check.** The inspector lists problems as you edit. **Pico Final > Check chapters** logs `SHAPE_OF_MEMORY_CHAPTERS`
  with either `ok` and the order, or each problem.
  - Errors: the book doesn't have exactly four chapters, a slot is empty, a level is in the book twice, a level has no
    pieces, or a level's lamps aren't 0, 1 or both. A build of the game stops on any error (`MemoryChaptersBuildCheck`).
  - Warnings: an empty or placeholder name or riddle (the gym's `NEW LEVEL` and its default riddle), an empty hint
    (HINT 1 shows nothing) or complete text (the FOUND page has no words), and a level outside
    `Resources/ShadowArt/Torch2` (Build and Run can't start at it). Chapter I lighting both walls is also a warning:
    the goal line is printed on the dark page, so it would not show. Warnings are logged and the build goes on.
  - The game logs the same problems at start.
- **Tests read the list.** EditMode `MemoryChaptersTests.TheShippingBookIsComplete` fails on any error or warning.
  The PlayMode tests refer to chapters by slot. After a swap, run both suites; a failure there means the new level
  breaks a rule the old one kept (for example, its words don't fit on the page).

## The book (`MemoryBook`)

The book has two layouts, picked by `MemoryGame.BookLayout` in the scene's inspector. The default is **Flat**, for
testing. The PlayMode tests run both.

- **Standing:** a giant hardback standing open at a right angle on the table. Its two pages sit exactly where the lamps'
  walls are (`Torch2Projection`: 1.13 × 1.06 m each, spine at the far corner), so the shadows fall on the pages and no
  level data changed.
- **Flat:** the same book at 31% scale (pages about 35 × 33 cm), lying open on the table in front of the orb. Its near
  edge is at the table's front, and the console moves to its right, just behind the right lamp. There it is turned
  30° toward the player and tilted up 20° on a pedestal, so a ray from a hand at the table meets the buttons face on
  instead of grazing a low, far slab (`MemoryGame.Console*`).
  - The shadows fall on the room's own walls, which keep their frames and colour.
  - The paper stays lit during a memory.
  - The stage spread is laid out for the book: the chapter, name and picture plate (? until found) on the left; the
    state, riddle (the found line once solved), hints and picture on the right.
  - Turning a page is by hand (below).
  - The table's scattered things can reach about 2 cm onto the book's far edge (`Torch2Transit.Area` starts at z −0.15).

Spreads:

| Spread | Left page | Right page |
| --- | --- | --- |
| 0, title | SHAPE *of* MEMORY, set like a title page, and how many memories are found | PLAY / CONTINUE / PLAY AGAIN and MEMORIES tiles; how to turn a page |
| 1–4, one chapter per memory | CHAPTER n, the name, its picture (found), ? (open) or a padlock (locked) | The riddle and a PLAY / PLAY AGAIN tile (nothing when locked) |
| 5, the end | THANK YOU (once Home is found) | The four memories' pictures |

- **Turning pages (standing):** ray-pinch a page's bottom outer corner (the folded corner) and draw your hand across.
  The corner follows the hand (30 cm of hand travel takes it all the way) and the paper bends after it. Let go and the
  page turns or falls back, depending on how far and how fast it was going: past about a third, or flicked. A quick
  pinch, a poke or a click turns it by itself. Corners show only where a turn is allowed: the ending's spread opens once
  Home is found, and no corner turns during a memory except the solved one.
- **Turning pages (flat):** pinch a page anywhere along its outer edge (a gilt line, lit when a hand is near) with the
  hand itself.
  - The page comes up by the point you hold: that row leads, so it folds toward it.
  - The held edge follows the hand round the spine. `PageBend.TipFor` solves for the tip that puts the held edge at the
    hand's angle.
  - Let go short of upright and it falls back; carried over, or flicked, it turns (`PageTurn.FlatFinish` 0.5).
  - A quick pinch that hardly moves turns it by itself, and so does a ray pinch or a click on the edge.
  - A hand at an edge hides its ray, and its poke or ray pinch there is ignored, so the grab isn't stolen. A hand whose
    ray is on a button off the book (the console) is not at the edge (`MemoryBook.HandBusy`): its ray stays drawn and
    its pinch is the button's. Before, a hand passing over the right page's edge on its way to the console lost its ray
    while it still had a button selected.
  - Hand path: `MemoryBook.Hands` reads both hands each frame. A pinch takes at 22 mm and lets go at 40 mm. The edge
    zone runs along the page's whole outer edge, from 10 cm in over the page to 7 cm past it and 14 cm up off it, at
    the book's scale.
  - `Grab`, `Drag` and `Drop` are public, so tests and the editor can drive it.
- **How a page turns:**
  - `PageTurn` moves two points in turn progress, the root at the spine and the tip at the free edge. Held, the tip
    follows the hand and drags the root once the page bends more than `MaxBend` (0.2 of the opening: 18° standing,
    36° flat). Past `BendLimit` (0.32) the root comes along at once, so a fast sweep swings the page instead of whipping
    it into a curl. Let go, both fall toward the side chosen. The tip has more air drag and a stiff bend spring, so on a
    tapped turn the corner leads, the edge trails a little after halfway, and it lands just after the spine side with a
    small flap. The whole turn takes about 0.8 s.
  - It is tuned as stiff paper. The first pass bent up to 0.42 at rest and about 130° on a fast flat sweep, twisted
    more across the page and wobbled as it fell, which read as cloth. `PageTurn`'s constants and `PageBend.Lead` are
    the knobs.
  - `PageBend` makes the shape from the book's opening, described by `BookShape`: 90° standing, 180° flat. Each row of
    the page is built from its direction, so it keeps the page's width. The angle stays within the opening, so the
    sheet can never pass through either page. The held row (standing: the bottom corner) bends a little more than the
    rest (`Lead`), which slants the fold slightly the way a page drawn by its corner folds. Standing, points rise up to
    7 cm as they swing, so the bottom edge clears the console.
  - `PageSheet` draws it: one two-sided mesh printed with snapshots of the pages it carries. Each page's content is
    built on a print root, rendered once by an orthographic camera on layer 31 into a 1024×960 texture, then hidden.
    Text, plates and tiles bend with the paper.
  - The shader (`Shaders/BookPage.shader`) composites the print over the paper. It shades each side by how it faces a
    light from the front and above; lying on a page, a side is exactly as bright as the unlit standing pages. The page
    darkens where it bends sharply.
  - A multiplied shade (`Shaders/BookPageShadow.shader`) darkens the standing pages where the sheet is close.
  - Both materials live in `PicoFinal/Resources/ShapeOfMemory/`, which keeps their shaders in builds.
  - Paper sounds are generated: a crinkle as a corner is picked up, a rustle as loud as the page is fast, and a flap
    when the edge lands.
- **Tiles** (`MemoryTile`): leather squares with a gilt icon, raised off the page. They rise on hover and sink when
  pressed, and an `AimZone` over the page keeps the ray drawn. On a page that is turning, about to be uncovered, or
  about to be landed on, tiles are built without buttons and pressed nearly flat, so the sheet passes over them. Live
  tiles pop up out of the page when it lands.
- **Layers on a page:** things stacked on a page sit `MemoryPages.Layer` (2 mm in page units) apart: a plate's frame
  behind it, its ? or padlock in front. At the flat book's 31% scale that is 0.6 mm, enough to keep them out of each
  other's depth. The ? used to sit at the plate's own depth and flickered through it.
- **Paper:** lit paper in the title and contents. While a memory plays, the lamps' walls cover the pages, and their
  unlit colour is the paper dimmed to 24% (`Torch2Room.Base`), so a page reads as paper in the dark rather than black.

## Flow

Title → **PLAY / CONTINUE** (turns to the first unsolved memory's chapter) or **MEMORIES** (the contents) → a chapter's
**PLAY** → intro → play → solved → **turn the page** (the page turns over by hand, then the outro, then the next
chapter's intro starts) … Home → turn the page → the end.

- **Memory found (`MemoryFound`):** played over the orb, where the player is looking, the moment a memory is solved.
  Bells ring up (`MemorySounds.Found`, over the level's own chime), gold glints burst out of the pile, and a leather
  plaque with a gilt edge, MEMORY FOUND over the memory's name, grows in above the rings, facing the player. It stays
  3.4 s, then shrinks away. Meanwhile the book's page prints the found picture, and 1.6 s in the console shows REPLAY ·
  CONTENTS and the page's edge starts to beckon (flat: the gilt line also widens).
- **Turning the solved page:** it turns like any other page, by hand or by itself, and the memory stays while it does.
  Only once the page lands on the next chapter (or the ending) and no hand holds the pile does the outro play
  (`SHAPE_OF_MEMORY_PAGE next`). Let go short and it falls back, and nothing else happens. The next chapter's PLAY
  tile shows but does nothing until the outro is done.

- **Outro (`MemoryOutro`):** this plays whenever a memory is left: the page turn after solving, REPLAY, CONTENTS, or
  pause RESTART / CONTENTS / TITLE.
  1. The lamps fade out with the cue.
  2. The orb opens: the rings and hub shrink and the wires draw in.
  3. The things go back to their places on the table one at a time, the last in first out (the intro's transit in
     reverse, 1.8× as fast), then shrink away.
  4. The next level's things grow in where they lie, which is the intro's first 0.35 s.
- **TITLE** riffles the pages back to the title (the pages in between stay blank).
- **Ending:** after Home, the book turns to its last spread with a rising arpeggio. The console offers TITLE.

### Level intro (`MemoryIntro`)

1. The level's things lie on the table (its table layout, or scattered) and the lamps are off. The level name floats
   above the table, the chapter is printed on the page, and the console is empty.
2. One at a time they lift into their places in the orb (`Torch2Transit`, below). Each lift has a whoosh and each
   arrival a soft knock.
3. The wires draw out to the hub and the rings close around it.
4. The lamps click on and flicker up. The pages light, the shadow appears and the closeness cue fades in. Input and
   the console come on.

- The pile always assembles at the level's **start** rotation, never the answer.
- The walls stay dark until the lamps are on, because the shadow is computed from the level data, not from the flying
  pieces.
- **Skipping:** a level's intro can be skipped once it has played through (`ShapeOfMemory.Intro.<level>`). A pinch, a
  click or Space skips it. After a pinch skip, the orb waits for the hand to open, so the same pinch doesn't grab it.
- **Table to orb (`Torch2Transit`, in the shared runtime):** one thing at a time, each flight 0.8 s, the next starting
  when 70% of one is done (the level's `transitFly` / `transitOverlap`). Each thing lifts straight up above everything
  on the table, swings round the part of the pile already built, and comes into its place along a clear line from
  outside. The order is planned backwards, by taking the finished pile apart (outermost first, each along a clear line
  and back down clear of what is already on the table), so a base goes in before what rests on it. Planning uses
  oriented boxes; an EditMode test checks the four levels with the real (convex) shapes, and the gym's Check level does
  the same for a designer's level. Intros take about 5–7 s.
- **Where things lie:** a level exported with a table layout (the gym's, below) uses it; the four shipped levels
  scatter: `Torch2Transit.Scatter` lays each thing on its thinnest side, turned close to a right angle, largest first,
  inside x ±0.58 m and z −0.15…0.48 m, seeded by the level's name.

### Onboarding (`MemoryGuide`)

- **First level:** a glowing ghost pinch (two fingers) reaches the orb's front right, closes, drags it across the front
  and lets go, with "PINCH THE ORB AND DRAG" above the orb. It stops for good once the player has rolled the orb 30°
  (`ShapeOfMemory.Taught`).
- **Any level:** if the shadow is off the wall for 1.5 s and the orb isn't held, a line above the orb says to pinch the
  ball in the middle and carry the orb back into the light.

### Sound (`MemorySounds`, generated placeholders)

A quiet room-tone loop, the lamp switch and hum, the lift whoosh, the settle knock, and the button click. The cue
layers (`Torch2Cue`) are silent while the lamps are off.

- Levels open in order. A solved level stays open, so after one full run every level is open.
- Progress (solved levels, the most hints used per level, table height) is saved on the device in PlayerPrefs
  (`ShapeOfMemory.*`).

## What the player sees

- **The room.** The table, the orb, the lamps and the lit pages come from `Torch2Workshop` in **bare** mode: no
  WARMTH %, no per-wall %, no debug buttons or prompts, no wall frames (the book is the frame) and no picture cards by
  the lamps. The table is deeper and wider so the book stands on it. Closeness shows only through the slide focus and
  the music layers (closeness cue). The orb's rings are 40% opaque (65% while rolled).
- **The pages while a memory plays:** the page its lamp lights carries the shadow. The page the lamps leave dark
  (Birthday and Good Morning light the left page, Bon Voyage the right) carries the chapter number and name, the
  riddle, the hint after the first HINT, the picture after the second, PAUSED while paused, and once solved the level's
  `complete` line, the found picture and "Turn the page". Home lights both pages, so its name and riddle go in the top
  margins, the hint in the bottom margin and each wall's picture in a top outer corner.
- **Console** (front right): physical push buttons. The cap sinks while pressed, the icon is on the cap, and the name
  is on the console in front. Hands can poke them or ray-pinch them (`MetaMenuButton`), and the mouse can click them.
  The buttons change with the phase:

| Phase | Buttons |
| --- | --- |
| Title | LOWER · RAISE (PLAY and MEMORIES are tiles on the page) |
| Contents | TITLE · LOWER · RAISE |
| Playing | HINT (two pips = hints left) · HOLD TO RESET (1 s hold, a ring of dots fills) · MENU |
| Paused | RESUME · RESTART · CONTENTS · TITLE · LOWER · RAISE |
| Solved | REPLAY · CONTENTS, 1.6 s after the reveal starts. The next memory is the page turn: the right page's corner (flat: edge) pulses gold. |
| Ending | TITLE |

During the intro, the outro, the reveal and page turns the game makes, the console and its slab are hidden.
- **Placement:** the table is placed 0.65 m in front of the head at floor + 0.72 m (+ the saved lift) when the game
  starts, and again after the headset's own recenter. That recenter is detected from the runtime's
  `trackingOriginUpdated` event, or from the head pose jumping more than 45° or 0.5 m in one frame. There is no in-game
  recenter button: when it was on the console, turning your head to press it moved the table sideways, and it stacked
  on top of the system recenter. Each LOWER / RAISE step is 2.5 cm and is saved.
- **Pico hands (`MemoryHands`):** on the Pico build's UnityXR rig, the Interaction SDK's hand mesh is hidden
  (`HandVisual.ForceOffVisibility`). It is fitted to Meta's skeleton and sat off the real hands there. Instead the
  tracked joints are drawn directly: slim bones between joints, small knuckle balls, and glowing index and thumb tips
  that brighten as a pinch closes (white at the 2.8 cm where a pinch registers). Nothing is retargeted, so it lines up
  wherever the tracking does, and passthrough shows the real hand around the bones.
- **Pico rays (`ShoulderHandAim`, in `Integration`):** on the Pico rig the hand ray always aims from an estimated
  shoulder through the point between the index and thumb knuckles, like Meta's own aim. Pico's own aim jitters and
  follows the wrist. A One Euro filter steadies the ray: heavy smoothing while the hand is nearly still, little while it
  sweeps. As the fingers close for a pinch (6.5 cm → 3 cm between the tips) the ray all but holds, so the pinch doesn't
  knock it off the button. Selection uses the distance-based `MetaPinchSelector`: down at 2.8 cm, up at 4.5 cm,
  normalized for hand size. On Quest the runtime's aim is untouched, unless it is a mocked pose.
  The shoulder is placed square to the body, not the head (`ShoulderHandAim.Body`). The body turns with the head only
  past 50°, and otherwise drifts after it over about 6 s. Placed from the head, a glance at the console off to the
  right swung the estimated shoulder about 11 cm forward. The ray then turned away from where the hand pointed, which
  felt like it came out of the arm.
- **Rays that stay (`AimZone` + `ZoneRays`, in `Integration`, reusable):** the SDK draws a hand ray only while it is on
  a button, so the ray flickered on and off between buttons. `ZoneRays` (one per scene) draws the hand rays and their
  cursor itself, and the SDK's ray and cursor visuals are switched off. A ray shows in two cases:
  - on a button: it goes to the SDK's hit, in hover colour, or white while selecting
  - inside any enabled `AimZone`: quieter, ending where it meets the zone's box

  It fades in and out over 0.12 s, and the cursor tightens as the pinch closes. Selection is still the SDK's. An
  `AimZone` is a box in its object's space (`Box`); a hand reaching inside the box counts as aiming at it. Shape of
  Memory puts zones on the console (sized to the current buttons, off while no buttons show) and on the level-card row,
  and hides a hand's ray while that hand is at the orb. To use them elsewhere, add `ZoneRays.Add(...)` once and an
  `AimZone` on each surface people aim at.
- Buttons are disabled while a hand holds the orb. The ray is ignored while a hand pinches inside the orb's grab
  range, because that pinch belongs to the pile.

Desktop: the orb controls are the same as in TorchLevels2 (left-drag rolls, right-drag moves, wheel, A/D W/S Z/C).
In editor and development builds only: **N** = next level (even if locked), **K** = finish the intro, or solve the
current level.

## Device logs

The Pico's logcat ring buffer is only 256 KB and rolls over within minutes. Build and Run's **Save headset log** streams
it to `Logs/Headsets/` from launch. By hand, use the editor's adb (the PATH adb kills the editor's adb server):

```bash
"D:/Unity/Hub/Editor/6000.3.21f1/Editor/Data/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb.exe" -s <serial> logcat -v time Unity:V AndroidRuntime:E *:S > Logs/ShapeOfMemory/pico-session.log
```

| Line | Meaning |
| --- | --- |
| `SHAPE_OF_MEMORY_READY` | Startup: the rig (`unityxr` on Pico, `ovr` on Quest) and the number of input subsystems |
| `SHAPE_OF_MEMORY_HEAD` | Whenever `HeadReady` changes: ready, focus (ours/Unity's), head device, `tracked`, `present` (`(unproven)` = never read true yet, so it doesn't gate; `none` = not offered) |
| `SHAPE_OF_MEMORY_HANDS` | Every 10 s: the head state as above, then per hand ray: tracked, ray state, aim (`shoulder` = fallback, `runtime`), selector (`distance` = MetaPinchSelector) |
| `SHAPE_OF_MEMORY_PRESS` | Every button or tile press (set/id) |
| `SHAPE_OF_MEMORY_PAGE` | A page the player turned landing (the spread), and `next` once a solved memory's page has turned over |
| `SHAPE_OF_MEMORY_PAGE` | A page the player turned landed on this spread |
| `SHAPE_OF_MEMORY_RECENTER` | `event` or `jump`: the system recenter that re-placed the table |
| `METAHACKTHONE_PINCH` | Every pinch down / up, with its distance |

## Build

**MetaHackthone > Build and Run...**, game **Shape of Memory**, for Quest or PICO (see [Build and Run](BuildAndRun.md)).
The build config `Editor/Shape of Memory.asset` builds this scene alone:
- under its own package `com.metahackathon2026.shadow` and the name "Shape of Memory", so it installs next to the main app;
- to `Builds/ShapeOfMemory.apk` for Quest and `Builds/ShapeOfMemory-Pico.apk` for PICO;
- with the app icon `Art/AppIcon/ShapeOfMemory-icon.png`: the Office level's bird shadow in a lamp's light on a dusk wall.
  The main app keeps the project's own icon.

Afterwards it restores the project's id and name, and for PICO the Quest configuration, even if the build fails.
`PicoBuild.OnProcessScene` swaps `MemoryGame.StandalonePlayer` for the UnityXR Pico player, as it does for the other
scenes. On Quest the scene keeps the shared Meta player.

**Pico Final > Build Pico APK** (or **(development)**) still builds the PICO APK through the same path and opens it in
Explorer. Builds can't start from inside the player loop, so scripted builds (MCP) must go through a menu item:
**MetaHackthone/Build and Run again** or this one.

## Code

| File | Role |
| --- | --- |
| `Runtime/MemoryGame.cs` | Director: phases, what each page shows, stage placement, console sets, the book's room, level loading |
| `Runtime/MemoryBook.cs` | The book: covers, page blocks and paper on the wall planes, spreads, corner drag, riffles and page sounds |
| `Runtime/MemoryPageTurn.cs` | `PageTurn` (how a turning page moves: held, let go, tapped) and `PageBend` (the shape it bends into) |
| `Runtime/MemoryPageSheet.cs` | `PageSheet`: the turning page's mesh, its two printed sides (snapshots of the page content) and its shade |
| `Shaders/BookPage.shader`, `Shaders/BookPageShadow.shader` | The turning page and the shade it casts; materials in `Resources/ShapeOfMemory/` |
| `Runtime/MemoryPages.cs` | Page printing (text, rules, picture plates, the title page) and `MemoryTile`, the button tiles |
| `Runtime/MemoryFound.cs` | The memory-found moment over the orb: plaque, glints and bells |
| `Runtime/MemoryButton.cs` | Console push button |
| `Runtime/Pressable.cs` | One press path for hands (MetaMenuButton) and mouse (ShadowControl); hold-to-fire. Actions run on the next Update |
| `Runtime/MemoryChapters.cs` | The chapter list asset (`Resources/ShapeOfMemory/Chapters.asset`) and its `Check()` |
| `Editor/MemoryChaptersTools.cs` | Chapters / Check chapters menu items, the list's inspector, the build check |
| `Runtime/MemoryProgress.cs` | Unlocks, hints, lift, per level id in the Chapters order; PlayerPrefs store (in-memory store for tests) |
| `Runtime/MemoryUi.cs` | Palette and builders; `MemoryKit` owns runtime materials and textures |
| `Editor/ShapeOfMemoryBuilder.cs` | Scene builder, icon import, icon material, Pico APK build |
| `Tools/ShadowArt/build_icons.py` | Placeholder icons (white on transparent). Replace any PNG with art of the same name and rebuild the scene |

Changes to shared shadow code:
- `Torch2Workshop.bare` (set before the first enable) skips the debug HUD in `Torch2World`.
- `Torch2Workshop.CardsShown` shows the target cards; `hostShowsPictures` builds none; `roomStyle` lets the host restyle
  the room before its walls are first lit. `Torch2Room` exposes `Table`, `wallQuads`, `shadowQuads` and `Base` (the
  unlit wall colour, default the old dark).
- `Torch2Transit` (table to orb, one at a time; planning and `Scatter`, which moved here from `MemoryIntro`), used by
  `MemoryIntro`, `MemoryOutro` and the gym's Play mode. `Torch2Piece` gained `onTable`, `tablePosition`, `tableEuler`
  and `tableGroup`; `Torch2Level` gained `transitFly` and `transitOverlap`.
- The orb's rings use `Resources/ShadowArt/Orb ring.mat` (transparent URP Lit, kept in Resources so builds keep its
  variant) at `Torch2Room.RingAlpha` 0.4, `RingLitAlpha` 0.65 while rolled. This shows in every Torch2 scene.
- `Torch2World.LampLevel` (0 = off: dark walls, beams off, lenses dim, silent cue through `Torch2Cue.Level`), and
  `Pieces` / `Wires` / `OrbParts` for the intro. `Torch2Room.Lamps(level)`.
- On the Pico rig, `MemoryGame` disables MRUK's hidden `MRUKGlobalContext` ticker. Otherwise it throws a
  `DllNotFoundException` for OVRPlugin every frame, because the Pico build strips OVRPlugin.
- `MetaShadowPlayer.Host` is now an `IShadowHost`, implemented by both `ShadowDemo` and `MemoryGame`.
- `MetaShadowPlayer.ReadHand` ignores a hand while the runtime reports the system gesture in its hand-aim flags
  (`UnityEngine.XR.Hands.MetaAimHand` `SystemGesture`; Pico's UnityXR rig has no `OVRHand`). This stops the
  system-menu pinch from grabbing the orb. It logs `SHAPE_OF_MEMORY_SYSTEM_GESTURE` on start and end. The PICO 4 Ultra
  sets the flag; a runtime without `XR_FB_hand_tracking_aim` can't, so the guard does nothing there.
- `MetaShadowPlayer` now ignores the UnityXR rig's static prefab hand (`HandSphereMap`, which always reads as tracked),
  attaches `ShoulderHandAim` to every hand ray (forced on the UnityXR rig), and adds `TrackedHand`, `UnityXRRig` and
  `Report`.
- `MetaShadowPlayer.HeadReady` (which gates hand drawing, button presses and orb input) lets a head signal (`isTracked`,
  `userPresence`) hold the hands back only after the runtime has reported it true once (`HeadSignal`). Some runtimes
  offer a signal and never set it, which used to hide the hands and drop every press for the whole session.
- TorchLevels2, the Gym and ShadowDemo behave as before.

Tests: EditMode `ShapeOfMemory.Tests` (progress rules, scatter layout, the aim filter, shoulder pointing, a glance aside
leaving the shoulders and the ray where they were, head signals, and the page turn: a tap's corner-first motion and
landing, letting go short, far or flicked, the held corner dragging the page, a fast sweep never bending it past the
limit, and the bend keeping each row's width and staying inside the book), `ShadowWorkshop.Tests` `Torch2TransitTests`
(table to place, one at a time, no clashes with the real shapes, groups move as one) and PlayMode
`ShapeOfMemory.PlayTests`. The PlayMode tests cover title → PLAY turns to the first chapter → hints printed on the page
→ solve → the found plaque → the solved page turns while the memory stays, and only once it lands the outro plays (the
next chapter's PLAY waiting) → the next chapter, a new session continuing
from the saved store, hold-to-reset needing the full hold, pause freezing the orb, lift, the contents with a locked
chapter and both corners, the last chapter's words (in the margins when it lights both walls) and the last spread. They drive buttons, tiles and corners through the
hand-ray path, and run once per book layout. On the flat book they also hold a page by its edge: lifted short of
upright it falls back, carried over it turns, and a quick pinch on the left edge turns it back. `MemoryBook.Frozen` / `Seek` hold a page turn still for captures.

## Not done yet

- The flat book needs a headset check:
  - Its size and where it lies (`MemoryBook.FlatScale`, `FlatPlace`).
  - The pinch thresholds and the edge zone (`MemoryBook.PinchOn` / `PinchOff` / `Reach*`).
  - Whether a pinch at the edge reads reliably while the hand is low over the table.
- The page drag needs a headset check:
  - `MemoryBook`: the 30 cm reach and the 7 cm lift.
  - `PageTurn`: the finish (0.35, corner-weighted) and its 0.25 s look-ahead, the springs and the fall.
  - `PageSheet`: the shade's strength and reach.
  - Check the print camera's one-off render cost (two orthographic renders when a turn starts) on the headset.
- Real art and audio: icons, the book's leather and paper, the title treatment and all sounds are generated
  placeholders. The title uses LiberationSans; a licensed display font can replace it as a TMP font asset.
