# Shape of Memory

The posable-pile shadow levels (Torch levels 2; the book's four chapters are listed in its [Chapters](#chapters)
asset) as a game rather than a debug level browser. Everything lives in `Assets/ShadowWorkshop/PicoFinal/`. The
deliverable scene is `Scenes/PicoFinal.unity`.

## Run it

- **MetaHackthone > Pico Final > Play** opens the scene and enters Play. It runs on desktop with the mouse.
- **MetaHackthone > Pico Final > Create scene or add what it is missing** makes the scene if there is none, and
  otherwise only adds missing pieces to it: edits made in the scene stay (see [The scene](#the-scene)).
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

## The scene

`Scenes/PicoFinal.unity` holds the play area as real objects, so it can be looked at and edited in the editor
(2026-11-13; before, it held only the director and a light, and everything was built at runtime):

| Object | What it is |
| --- | --- |
| `Shape of Memory` | `MemoryGame`: the director, its materials and its look (`MemoryLook`, under `Looks/`). |
| `Lighting/Key light` | The table-top's key light; the look sets its colour and direction. |
| `Play Area` | `MemoryGame.PlayArea`. Its origin is the middle of the table top, +z away from the player. In a headset the game places it from the eyes (`StageAhead` behind, `StageBelow` over its origin); on the desktop it stays where the scene has it. `MemoryPlayArea` draws the eyes, the orb, the book standing on the walls and their labels in the Scene view. |
| `Play Area/Table` | `MemoryGame.Table`: the environment artist's `SM_Table_01`. The memories' rooms keep their own table box, hidden. |
| `Play Area/Reading stand` | `MemoryGame.ReadingStand`: `BookStand_01`. Its pivot sits on the table top: it sinks there (height scaled) as the book rises. |
| `.../Reading stand/Book rest` | `MemoryGame.BookRest`: the board's surface under the book's spine, at the board's tilt. Move it and the book moves. |
| `.../Book rest/SM_Book_01` | `MemoryGame.BookModel`: the environment artist's book, **not used** since 2026-11-15 evening (`MemoryGame.ArtBook` off: the game hides it and builds our own book, see [The book](#the-book-memorybook)). With `ArtBook` on, `MemoryBook.Dress` opens it with the game (shut, the cover lifting, open, flying, on the walls) and prints the pages on it. |
| `Play Area/Play strip` | `MemoryGame.ButtonPanel`: the play strip under the orb, HINT / HOLD TO RESET / PAUSE while a memory plays; it rises out of the table ([The toolbar](#the-toolbar)). |
| `Editor only/Seated player` | Tagged EditorOnly (not in builds), hidden in Play: a 1.70 m adult on a 45 cm chair (`Art/Dev/SeatedPlayer.fbx`, `Tools/ShadowArt/build_seated_player.py`), its eyes where the game puts a player's eyes. `MemorySeatedPlayer.Stature` changes the player; the label gives the eyes' and the table top's height over that player's floor. |

Each `MemoryGame` field left empty is built from primitives at runtime, as before. The environment artist's art add-on
(`PicoFinalArtDressing`) now dresses only the lamps: its Table, Stand and Book slots are empty, since the scene holds
those.

The scene uses the **Env art** look: the environment artist's scene as she sets it up (cs:189: a lighter sea that fades
into a mist below, more of it the further down you look). **Env art, our fog** is the same with our shorter fade under
the sea (4.5 m instead of 7) and no cloud floor under the gaps, kept for comparison; **Look > Rebuild presets** copies her
current sea colours and mist into it again.

Fixes on the environment artist's models (2026-11-14), all redone by menu when the source changes:

- **Cloud sea shape.** Her sea pieces (`Art/Art Assets/Meshes/CloudSea/Sea_*`) are our kit's `Sea_A`–`D` processed. The
  kit now builds them as a few big soft mounds lumped like the puffs (`soft_sea` in `build_cloud_kit.py`; the puffs and
  banks come out byte for byte as before, and each sea piece keeps its old footprint so the layout still closes up).
  **Pico Final > Art > Rebuild the environment's sea meshes from the cloud kit** writes them into her assets in place
  with her own recipe, read back from her files: folds filled (smoothing that only moves outward), area-weighted
  normals, and UV2 = the normals diffused over the mesh (`ShapeOfMemoryMeshes.cs`, `CloudSeaMeshes`).
- **Haze, clouds and ruins alike.** The cloud sea (`EnvCloudSea.shader`) and the ruins (`FloatHaze.hlsl`) share one
  distance curve: smoothstep from 0 to 350 m, up to 0.85 (the giants' amounts stay 0.566). So a cloud and a ruin at the
  same distance fade the same, the middle distance stays solid (about 14% at 90 m, 53% at 200 m) and the far ring melts
  into the sky. (The ruins used to reach 0.8 by 107 m and the sea a third of that; with the sea on the ruins' curve the
  mid-distance clouds looked see-through.) The sea's haze and soft edges read the sky no lower than just under the
  horizon (`_HazeSkyFloor`): Flow goes near black below it, which drew a dark outline round every lump seen from above.
  In game the look's fog (next) takes over; this is what her scene shows on its own in the editor.
- **One fog for the scene, set on the look** (`MemoryLook.Fog`, `Runtime/MemoryFog.cs`, `Shaders/FloatingChips/PicoFinalFog.hlsl`).
  The user asked for the fog of our 2026-11-12 build back ("much more noticeable") and for softer edges between clouds and
  between clouds and buildings. Her sea, ruins and giants all take one fog in place of each material's own haze
  (`_PicoFinalFog`; MemoryEnvironment publishes it every frame, so the look asset can be tuned in Play mode and keeps its
  values). Heights are metres from the floor under the stage; her cloud tops lie about 8 m under it.
  - *Colour: the sky right above* (the user: "blend with the skybox's color directly above it"). Everything below the
    horizon takes the colour of the sky in the same direction just over the cloud horizon (`SkyAbove` +3 degrees):
    Flow's bright band there goes orange toward the glow and pink, lavender and violet round the rest. (The first
    version read the sky just *below* the horizon, a flat dark purple everywhere, then tinted it 55% toward a fixed
    rose, so every colour variant looked alike.) The band is drawn once into a strip, one texel per direction round
    the horizon and averaged sideways (`SkyWidth` ±12 degrees), by `MemoryFogRing` (`FogRing.shader`,
    `Resources/ShapeOfMemory/Fog ring.mat`), and redrawn only when the sky changes. Reading the sky straight above each
    point printed every wisp painted in the band down the screen as a vertical streak. Higher up, over the next ~9
    degrees, the fog takes the sky right behind, for the ruins floating in the sky. `Colour`/`ColourShare` push it toward
    a colour of its own; the user compared one colour for all the fog (the sky's colour under the arch ahead right,
    #BC87BB, and 5 to 30% darker; `Colour` #A97AA8 is kept) and chose the sky's own colours (`ColourShare` 0). With a
    share, far out (0.6 to 1.4 of the full distance, `ColourToSkyFar`) the colour still gives way to the sky right
    above, so the far ring melts into the sky (orange toward the glow); off, the far edge is one flat band. Captures in
    `.agents/local/captures/fixes-2026-11-14/` (`fog_sky_above_variants.png`, `fog_one_colour_steps.png`).
    Note: `Shader.SetGlobalColor` hands the shader the colour as it
    is, with no sRGB-to-linear step (unlike a material's colour), so `MemoryFog.Publish` sends `.linear`; without it a
    picked colour rendered far lighter (#BC87BB came out about #DEBDDA).
  - *Distance:* the 11/12 curve, 25 to 400 m up to 0.9, ease-out (about a third at 100 m, half at 150 m). A material's own
    Haze Amount / Haze at full caps it, so the giants (0.566) stay on the horizon.
  - *Mist between the clouds* (`MistTop` -9.5, `MistDepth` 4.5, 11/12's: from 1.5 m under the tops, full 4.5 m lower),
    in the sky's colour above (`MistSky` 1; `MistColour` mixes in a colour of its own), so the gaps read as fog and
    each cloud rises out of it. The sea's outlines fade toward what lies behind them (looking down: the mist; looking
    up: the sky), so an outline melts into the cloud behind it instead of showing the sky through it.
  - *Buildings into the cloud tops* (`WrapTop` -6 to `WrapBottom` -8.5, 0.8): ruins fade just above the cloud tops into
    the same fog (`WrapSky` 1; 0 = the cloud tops' own colour, worked out from the sea's material, `MemoryFog.CloudTop`),
    then into the mist below, so a building standing in a cloud has no hard line where it meets it, and its foot takes
    the colour of the sky behind it. The clouds themselves don't take this.
  - The Env art and Env art, our fog looks have it on; Ours, Blend and Mixed keep their own haze (our cloud kit has
    its own). No depth texture is needed (it is off on the headsets for frame rate), so these are colour fades by height
    and distance, not true soft intersections: a building's base fades at the cloud-top height wherever it stands.
- **Solid outlines.** The sea's and the giant clouds' silhouette dissolve (alpha to coverage, `_EdgeCoverage`) is off and
  their edge fade lighter, so the clouds read as puffy but not see-through.
- **Far ring** (`Environment/Art/Cloud_Sea/Far ring` in her scene). Rings of her sea's far pieces from the sea's edge
  (190 m) out to 630 m, bigger the further out, tops at the sea's level, with 16 banks and puffs rising into the sky's
  cloud band away from the glow, so the sea runs on into the painted clouds. 12 combined meshes (12 draws, about 50k
  triangles) in `Art/Art Assets/Meshes/CloudSeaFar`. Inside the sea's group, so a look that hides or recolours the sea
  does the same to it. **Pico Final > Art > Rebuild the far cloud ring** lays it out again (seeded) and saves her scene.
- **Book** (`Art/Art Assets/Meshes/SM_Book_01_SpineFixed.asset`, on the scene's `SM_Book_01`; unused while
  `MemoryGame.ArtBook` is off, from 2026-11-15 evening). The model's hinge is on
  the spine's outer apex, so opened flat the spine folded into a ridge above the page faces (a seam through the paper
  with the bands' tips in it), the band ends tore into flaps at the head and tail, and the gutter between the page
  blocks was open. The fixed copy gives the spine one smooth opening weight, lowers its middle 8 mm (model units) so it
  stays under the pages, and carries each page face down to the hinge. **Pico Final > Art > Rebuild the fixed book
  mesh** (`BookMeshFix`). Left: a small hollow at the head and tail where the cover's turn-ins meet.
- **Lamps.** The lamp sphere's lens is at its +z, so `PicoFinalArtDressing` no longer turns it round (its back faced the pile).

Round the play area, the look's environment scene (`PicoFinal_Environment`, the cloud sea and the ruins) opens beside
`PicoFinal` in the editor, and the look's sky is drawn round the Scene and Game views (an object that is never saved).
**Pico Final > Scene** turns either off. The environment scene is the environment artist's: edit its cloud sea and
ruins there. Play takes the open scene over as it is (`MemoryEnvironment`).

## Chapters

The book's levels, in play order, are one asset: `Resources/ShapeOfMemory/Chapters.asset` (`MemoryChapters`). Open
it with **MetaHackthone > Pico Final > Chapters**. Nothing else in the game or its tests names a level.

- **To swap a level in:** make it in the Shadow Gym and **Export** it to `Resources/ShadowArt/Torch2/<id>.asset`. Fill
  in Button (the chapter's name), Riddle, Hint and Complete before you export. Then drag the asset into a slot, and
  run **Text > Copy asset text into tables** and translate its new lines (see [Text and languages](#text-and-languages)).
- **To reorder:** drag the rows by their handles.
- **Each row has two buttons.** **Gym** opens that level in the Shadow Gym. **Headset** starts the game at that chapter
  on Build and Run's headset.
- **These go with the slot, not the level:**
  - the chapter number;
  - its colour (`MemoryPalette`, by order: Rose, Cornflower, Lilac, Peach);
  - the teaching, which happens on chapter I only (the ghost pinch and the goal line);
  - the ending's 2×2 plates. That layout is why the book has exactly four chapters.
- **Progress is saved per level id (the asset's name).** A level that is swapped in starts unfound. Renaming a
  chapter's asset loses its saves. **Clear saved progress** clears the
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

## Credits

The book's last spread, after THANK YOU, is printed from one asset: `Resources/ShapeOfMemory/Credits.asset`
(`MemoryCredits`). Open it with **MetaHackthone > Pico Final > Credits**, which makes it if it's missing.

- **Left page:** `madeBy` ("Made with love by") over a gilt rule, then `names` in two columns, read down the left
  one first in list order (an odd one out goes on the left). The team is listed alphabetically by family name. There is
  no studio logo (one was tried and taken out on 2026-11-11).
- **Right page:** `thanksTo` ("With thanks to") over a gilt rule, then each of `sections`: its heading in gilt capitals
  and its lines under it. The block shrinks to fit when the list grows long.
- **Missing pieces keep their space.** A section with no lines prints "to come". The inspector lists it, and an empty
  team, as warnings, and a build of the game logs them (`MemoryCreditsBuildCheck`) and goes on.
- The pages are printed from the asset whenever the spread is shown, so an edit shows the next time it is turned to.

## Text and languages

All of the game's text is in Unity Localization string tables (package `com.unity.localization`), in English and
Simplified Chinese. No player-facing text is written in the code. The game uses the headset's system language. Any other
language gets English, and so does any line that has no translation yet.

- **To edit text:** open **MetaHackthone > Pico Final > Text > Open tables** (or **Window > Asset Management >
  Localization Tables**). Each language is a column, so you can edit English and Chinese side by side. Each key has a
  comment that says where its line is printed. The tables are in `PicoFinal/Localization/Tables`:
  - `ShapeOfMemory UI` holds the words the code prints. Keys look like `how/turn/hands` or `strip/hint`.
  - `ShapeOfMemory Book` holds the chapters' words, keyed `<level asset>/button|riddle|hint|complete`, and the
    credits headings.
  - The fonts are not in a table: see **Fonts** below.
  - The cover: English stacks `cover/title/top`, `of` and `bottom` (SHAPE / of / MEMORY). Chinese prints `title`
    (浮影集) on one line instead, so its `cover/title/*` entries are never shown.
- **Markup in a line:**
  - `<em>…</em>` prints in the highlight colour (the aqua of the edge it names).
  - `\n` is a line break.
  - A *smart* line takes numbers: `chapter/number` gets `{0}` Roman (IV), `{1}` Chinese (四) and `{2}` a digit;
    `progress/some` gets `{0}` found and `{1}` total.
- **Designers still write a level's words in the gym.** The level asset's text is the fallback. After editing a level
  (or the credits headings), run **Text > Copy asset text into tables**. It copies the new English into the Book table
  and logs which lines changed, so their Chinese can be updated.
- **Check:** run **Text > Check text**. It warns about any line missing in either language, and any Book line whose
  English no longer matches its asset.
  - EditMode `MemoryTextTests` fails on the same problems.
  - It also fails on a key the code uses that the table lacks.
  - It also fails on a character (in the tables or the credits) that a baked font atlas doesn't hold. Run **Pico
    Final > Fonts > Bake atlases** after adding text with new characters.
- **How the language is chosen:** Localization Settings (`Assets/Localization`) try the command line first, then the
  system language (Android's locale). If neither matches, the game uses English. `zh-CN`, `zh-SG` and `zh-Hans-*` all
  reach `zh-Hans`. Traditional Chinese (`zh-TW`, `zh-HK`) gets English. No choice is saved between runs, so changing the
  headset's language changes the game's.
- **Switching languages in the editor:** in Play mode, press **L** (development builds too), or use **Window > Asset
  Management > Localization Scene Controls**. The book, the cover, the buttons and the guide print again in the new
  language.
- **Chinese typography:**
  - Chinese prints upright, because CJK has no italics and TMP's slant would fake one (`MemoryText.Upright`).
  - The log line is `SHAPE_OF_MEMORY_TEXT`.
- **Builds:** Addressables builds the tables with every player build ("Build Addressables on Player Build" is set in
  the project, not left to each person's preferences).

### Fonts

Theme: dreamy, warm, classic (chosen 2026-11-14). `MemoryText.Role` picks a face for each label; `MemoryFonts`
(`Resources/ShapeOfMemory/Fonts.asset`) holds the faces as static TMP atlases, baked in the editor. Nothing is made at
run time, and the TTFs never ship.

| Role | English | Chinese | Used for |
|---|---|---|---|
| Text | Merriweather Regular, with its Italic and Bold in the weight table | Noto Serif SC Regular (Bold in its weight table) | Sentences, riddles, hints, and anything you press (tiles, the button post) in Bold |
| Caps | Cinzel SemiBold | Noto Serif SC Bold | Kickers, chapter numbers, banners, small headings (a Bold label in `MemoryPages.Text` by default) |
| Title | Cinzel Decorative Bold | Noto Serif SC Bold | The cover's title, chapter names, THANK YOU. Large sizes only |

- The TTFs are in `PicoFinal/Fonts`, with each family's OFL licence; the credits name them under Art & Assets.
  `Tools/ShadowArt/build_fonts.py` makes them from Google Fonts' originals: fixed-weight instances of the variable
  fonts, renamed "Memory Text" / "Memory Caps" because Cinzel and Merriweather reserve their names under the OFL, and
  Noto Serif SC cut down to GB2312 plus Latin.
- **Pico Final > Fonts > Bake atlases** rebakes every atlas. Latin atlases hold ASCII, Latin-1 and typographic
  punctuation; Chinese ones hold what the Chinese tables and the credits use, plus ASCII and Latin-1. Kerning is kept
  between ASCII characters only, which keeps the assets near 41 MB of YAML in total.

## The book (`MemoryBook`)

**Our own book** (since 2026-11-15 evening, when the user went back to it from the environment artist's `SM_Book_01`;
`MemoryGame.ArtBook` off): the game's boards, page blocks, paper and spine, the boards and spine in a finely grained
leather in the artist's book's burgundy (`MemoryBook.Burgundy`, generated grain). The front board's outside carries
**Cover art**, a generated 512 px texture after the artist's book: burgundy leather darker toward the edges, mottled and
grained; a wide gilt Greek-key border (about 5% of the cover) between a gilt rule outside and a double rule inside, a
square with a dot at each of its corners, and a gilt corner piece (concave edge, tooled line) in each inner corner. The
title prints over it. The texture and its material are made at run time (`MemoryBook.CoverArt`, `Decorate`), not
assets: change the colours or tooling there. Shut, the spine wraps the hinge (centred on it, as thick as the book) and
moves back behind it over the cover's first 40°: set beside the hinge, it left a wedge each side where the page blocks'
cream ends showed as a seam. It is not under
the cover face, so it comes up with the book after the title rather than with it.

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
| 0, Settings (`SettingsSpread`) | SETTINGS: table height (LOWER / RAISE), language (each by its own name, in its own face) | MUSIC ON / OFF, HOLD TO START OVER (2 s) |
| 1, Begin (`BeginSpread`; the cover opens here) | TO THE READER (读者须知): turn a page, press, pinch a ribbon, turn the objects | How many memories are found, "Turn the page to begin", table height, "Settings: turn back a page" |
| 2–5, one chapter per memory (`SpreadOf`) | CHAPTER n, the name, its picture (found), ? (open) or a padlock (locked) | The riddle and PLAY under it (found: PLAY AGAIN) |
| 6, the end | THANK YOU (once Home is found) | The four memories' pictures |
| 7, credits | "Made with love by", then the team in two columns | "With thanks to", then music, art and tools by section ([Credits](#credits)) |
| 8, the back endpaper (`QuitSpread`, the last; `Phase.Quit`) | CLOSE THE BOOK? (合上这本书？), "The book keeps every memory you find.", how many are found (once any are) | KEEP READING (back to where QUIT's ribbon was touched, else the credits) · QUIT (a press: the page is the question) |

The option in use on Settings has a gilt frame round its tile. Begin turns both ways (back is Settings); Settings only
forward. The credits turn on to the back endpaper, which only turns back. A backward turn from the end only lands on a
chapter that is open (the credits' ribbon reaches them early).

- **Turning pages (standing):** ray-pinch a page's bottom outer corner (the folded corner) and draw your hand across.
  The corner follows the hand (30 cm of hand travel takes it all the way) and the paper bends after it. Let go and the
  page turns or falls back, depending on how far and how fast it was going: past about a third, or flicked. A quick
  pinch, a poke or a click turns it by itself. Corners show only where a turn is allowed: the ending's spread opens once
  Home is found, the ending turns on to the credits, and no corner turns during a memory except the solved one.
- **Turning pages (flat):** pinch a page anywhere along its outer edge (a gilt line, lit when a hand is near) with the
  hand itself.
  - The page comes up by the point you hold: that row leads, so it folds toward it.
  - The held edge follows the hand round the spine. `PageBend.TipFor` solves for the tip that puts the held edge at the
    hand's angle. Or, whichever turns it further, it follows the hand across the book as if the hand kept to the circle
    the edge sweeps (upright with the hand over the spine): sitting back from the stand, the arm can't lift over the
    book, and the page lay flat until the hand crossed the spine, then jumped (2026-11-15, "sticky").
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

The UI's rule (2026-11-15): **the book chooses and starts; QUIT is a ribbon off the walls and a hold on the strip.** Aqua means pinch it and
move it (a page's edge, the orb's rings, the border of a ribbon a pinch would lift); a leather tile with a gilt icon means touch it (page tiles, the play strip). Anything that throws something away is a hold with a filling ring (RESET 1 s, QUIT 1.5 s,
START OVER 2 s).

Title in the dark → the cover → Begin → turn to a chapter → **PLAY** (the stand's drawer slides open) → the book rises,
intro → play → solved → the book comes down at the found chapter → turn the page → the next chapter → PLAY …
Home → turn the page → the end → the credits. Plan and its reasoning: the UI plan artifact (2026-11-15).

- **Memory found (`MemoryFound`):** played over the orb, where the player is looking, the moment a memory is solved.
  Bells ring up (`MemorySounds.Found`, over the level's own chime), gold glints burst out of the pile, and a leather
  plaque with a gilt edge, MEMORY FOUND over the memory's name, grows in above the rings, facing the player. It stays
  3.4 s, then shrinks away. Meanwhile the book's page prints the found picture.
- **Turning the solved page:** it turns like any other page, by hand or by itself, and the memory stays while it does.
  Only once the page lands on the next chapter (or the ending) and no hand holds the pile does the outro play
  (`SHAPE_OF_MEMORY_PAGE next`). Let go short and it falls back, and nothing else happens. The next chapter's PLAY
  waits for the outro.

- **Outro (`MemoryOutro`):** this plays once a found memory has held its moment (then the book comes down), and whenever
  a memory is left from a pause. Since 2026-11-16 a found memory's things go back and fade as well (they used to stay
  on the table until its page was turned), so the table is clear when the book comes down.
  1. The lamps fade out with the cue.
  2. The orb opens: the rings and hub shrink and the wires draw in.
  3. The things go back to their places on the table one at a time, the last in first out (the intro's transit in
     reverse, 1.8× as fast), then shrink away.
  4. The next level's things grow in where they lie, which is the intro's first 0.35 s.
- **Ribbons**, pinched, turn one page straight to their spread (`MemoryBook.TurnStraight`: its front the open page, its
  back the arriving one; no riffle through the spreads between).
- **Ending:** after Home, the book turns to the ending's spread with a rising arpeggio. Its page beckons on to the
  credits, the last spread; turning back from them to the ending doesn't play the arpeggio again.

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
  inside x ±0.58 m and z −0.15…0.48 m, seeded by the level's name, 2.5 cm in front of both walls (`WallClear`: the
  area's back corners reach past them, and things came up through the book's pages) and out of the host's
  `Torch2Transit.KeepOut` (Shape of Memory: the stand's footprint plus 2 cm, `MemoryGame.KeepTableClear`; the stand
  comes up out of the table under the book after a find while the things stay, and came up through them). With the
  front middle kept clear, Shape of Memory scatters over x ±0.72 m, z −0.15…0.74 m (`Torch2Transit.HostArea`), and a
  layout that still overlaps is laid again from another seed (up to 8 times). Checked against 24 stand sizes × 4
  levels: nothing on the stand, everything at least 2.7 cm in front of the pages.

### The found moment (`MemorySolveMoment`, `MemorySolveCue`)

- **The end snap:** turned within `heldSnapAngle` of the answer while held (or let go within `snapAngle`), the orb leaves
  the hand and glides into the exact answer over `Torch2Workshop.SnapSeconds` (0.7 s when already there, up to 1.3 s
  from the snap's widest angle). It sets off at its average speed and eases to rest (`Torch2Workshop.Glide`); the old
  0.25–0.5 s ease-out set off at three times that and read as a jerk. A generated harp run swells under the glide so its
  top lands with the orb (`MemorySounds.Swell`), then the shared bells and MEMORY FOUND plaque play (`MemoryFound`).
- **The lock** (`Torch2Workshop.HoldToSolve`, the snap-nerf session's): left alone within reach of an answer for a
  second, the light comes up and the orb glides into the answer. A hand still on the orb counts as leaving it alone
  while it moves it less than `StillAngle` (2.5°) and `StillDrift` (1 cm) from where the wait started (2026-11-16, the
  user: a still hand at the answer should lock too); the lock then takes the orb out of the hand. Within reach means passing, or warm within `snapAngle`;
  a level's `lockAngle` (above 0) caps it, however the shadow scores. Birthday (10°) and GoodMorning (6°) have one
  (2026-11-15: the user found their snap too wide; measured, Birthday locked up to about 25° off, GoodMorning up to 16°).
  Home locks up to about 20° off and BonVoyage about 8° (no cap).
- **Each chapter's own moment:** `Resources/ShapeOfMemory/Cues/<chapter>.asset` (**MetaHackthone > Pico Final > Solve
  cues > Select cues**). Its **Sounds** play from the moment the memory is found (each with a delay and volume). Its
  **Layers** are paper cut-outs that come alive on the lit page round the shadow: clouds streaming past Bon Voyage's
  plane, the birthday candles lighting one by one, the sun rising behind the chef, hearts over Home. They fade off the
  lamp's spot and go out with the lamps.
- Layer places and sizes are in **spot units**: the lamp's spot on its page, (0,0) its middle, 1 its radius, x across the
  page as the player sees it. A flipped find is turned upright first; the cut-outs start after that.
- Cut-out art is one atlas, `Art/Cues/CueAtlas.png`, drawn by `Tools/ShadowArt/build_cue_atlas.py` (white paper with an
  ink outline, so a layer's tint colours it). **Make default cues** rewrites the four chapters' layers as designed and
  keeps their sounds.
- New sounds go in `Audio/Cues/`. The shipped ones (Soundsnap, plus one Pixabay chime) are cut from the downloads by
  `Tools/ShadowArt/trim_cue_audio.py`: a window of each, short fades, peak at -1 dBFS, and the jet's channels swapped so its
  pass follows the plane to the right. **Make default cues** fills a chapter's Sounds only while the list is empty, so
  tuned delays and volumes stay. Birthday's cut-outs are timed to its sounds: the candles light, the breath at 2.0 s puts
  them out (layer `until`), and the horn at 2.4 s sets off the confetti.
- **Each chapter's lamp slide** (`Torch2Level.leftSlide` / `rightSlide`, the picture the lamp projects, blurred and grey
  while cold, sharp and in colour once found): `Art/Slides/Slide_<chapter>.png`, drawn by `Tools/ShadowArt/build_slides.py`
  round each chapter's shadow and cut-outs (Birthday: party wallpaper and bunting; Bon Voyage: sky and an airfield;
  Good Morning: a kitchen whose window is where the cut-out sun rises; Home: a living room, one slide per wall). Re-running
  the script rewrites the PNGs in place. A gym **Export** writes its own slides back to the level, so open the level in
  the gym first (it copies them in).

### Onboarding (`MemoryGuide`)

- **First level:** a glowing ghost pinch (two fingers) reaches the orb's front right, closes, drags it across the front
  and lets go, with "PINCH THE ORB AND DRAG" above the orb. It stops for good once the player has rolled the orb 30°
  (`ShapeOfMemory.Taught`).
- **No "stuck" nudge:** the "STUCK? Press HINT" line that came up after a minute without a hint was removed
  (2026-11-15); HINT on the toolbar is the way in.
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
- **Dream architecture (`MemoryDream`, `MemoryLook.DreamPieces` + `DreamMaterial`). Not in the shipped scene:** env art
  is redoing the background, so PicoFinal leaves both fields empty and the scene build doesn't set them. To preview it,
  use **MetaHackthone > Pico Final > Dream background > Add to open scene** (and **Remove from open scene**), then save
  the scene if you want to keep it. The kit, its script and the code all stay. When it's on: once the sky is up, 20 pieces of
  abstract Mediterranean architecture surface one by one beyond the book, then bob and sway slowly: plaster walls with
  arched openings, a portal, an arcade, stairs, a bridge, a column, and a domed chapel with blue doors. They never show
  over passthrough, have no colliders and cast no shadows.
  - **The kit.** `Tools/ShadowArt/build_dream_kit.py` (Blender 4.2) models 14 pieces with soft bevels and lights them in
    Cycles: a dusk sky matched to WindwardEveningSky, a warm key from the player's front left, the glow behind as a rim,
    and warm light inside every opening. It bakes that light into one 2048 atlas (`Art/Dream/DreamKit.png`) and writes
    the meshes (`Art/Dream/DreamKit.fbx`). In the game they are unlit, on one material (`Materials/Dream kit.mat`, URP
    Unlit). It also renders a preview and a contact sheet to check before importing. To change a piece, the palette or
    the light, edit the script, run it, then rebuild the scene or set the two fields.
  - **Size and placement.** Each copy is 0.6–1.7 times its piece and sits as far away as makes it 6–16° wide (6–40 m),
    so the nearest is four times the distance of the walls' corner. The book grows into the walls well in front of
    them, and while it is up they are behind it (a few show above it). Each stands upright, turned to the eyes and then
    15–35° away so its arches' lit sides show. The layout is seeded, so it is the same every launch: change `Seed` or the
    band constants to re-roll it.
- **Clouds (`MemoryClouds`, `MemoryLook.CloudPieces` + `CloudMaterial` + `CloudsUnderFeet` + `CloudShape`). In the shipped scene.**
  Stylised dusk cumulus round the player, so the table floats over a sea of clouds with big banks either side and the
  glow open straight ahead. They are hidden over passthrough and through the title. They fade in together with the sky
  (one alpha), then stay fully shown. They have no colliders and cast no shadows. To take them out or put them back, use
  **MetaHackthone > Pico Final > Clouds > Remove from open scene** (or **Add to open scene**) and save the scene.
  - **The kit.** `Tools/ShadowArt/build_cloud_kit.py` (Blender 4.2, about a minute) builds 10 clouds: 4 flat sea
    banks (since 2026-11-14 a few big soft mounds, lumped like the puffs), 4 puffs and 2 long banks. Towers were dropped because they read as stone pillars at mid distance. Each cloud
    is a few domes covered with metaball lobes, spread evenly, and is squashed flat at the base. Its normals come from
    the dense surface, then lean toward an inflated, smoothed copy, so light reads as one soft mass. The vertex colour
    holds fold occlusion (R) and height in the cloud (G). Each cloud also has a `_LOD1` (about a third of the
    triangles) and a `_LOD2` (about a tenth), whose normals lean further toward the smooth copy, so their big triangles
    don't show as facets in the shading. The script writes `Art/Clouds/CloudKit.fbx` and, with `--preview`, a
    contact sheet.
  - **The shader.** `Shaders/Cloud.shader` (`Materials/Memory cloud.mat`) draws with the opaque geometry, front to
    back, writing depth, and reads no texture per pixel.
    - Light is a four-colour ramp (shadow, body, lit, hot) toward the sky's glow, mixed with height and darkened in the
      folds, plus a silver lining on edges seen against the glow.
    - Haze is computed per vertex. It pulls toward the sky's own colour along the view, using a blurred mip of the
      cubemap that `MemorySky` publishes. It grows with distance and below the sea's tops, so far clouds and the sea's
      underside melt into the sky.
    - Soft edges: where the surface turns away from the eye, the colour goes to that same sky colour (`_EdgeSoft`,
      `_EdgeStart`). Outlines against the sky stay soft, and facets of a coarse mesh don't show along them.
    - Vertices billow slowly along their normals. The output is dithered like the sky against dusk banding.
    - The fade: the output is premultiplied with alpha `1 - _CloudHide`, which `MemoryClouds` sets from the sky's fade.
      At alpha 1 that is exactly the opaque colour. Blending costs next to nothing on a tiled GPU and keeps early depth
      rejection.
    - Tune it on the material.
  - **Layout** (seeded, the same every launch). Cloud space is the stage's, moved down onto the floor every frame.
    - The sea's tops sit `Drop` (8 m) under the floor, or 0.3 m under it when `CloudsUnderFeet` is on (the player
      stands on the sea). 80 sea banks reach 170 m out, bigger with distance.
    - 12 puffs rise out of the sea 10–70 m away. Nothing comes above the floor within 8 m of the eyes.
    - 14 big clouds (puffs scaled up 2.6–4.4 times, and long banks) sit 45–175 m out. They stay clear of the glow
      (±16° ahead).
    - Sea pieces take their `_LOD1` past 30 m and their `_LOD2` past 150 m: seen flat from above against more sea,
      a coarse outline doesn't show. Puffs and big clouds go by how wide they look (`_LOD1` under 8°, `_LOD2` under
      3°). They stand against the sky, often the glow, and the big ones are wide even far away, so nearly all use their
      near mesh. About 148k triangles in all. The whole scene is 106 renderers on one material.
  - **Motion.** Sea and puffs sway in place, each on its own 40–80 s period, so nothing under the feet moves as a
    whole: that would read as the player moving. The big clouds drift with the wind at 0.7 m/s, under a degree a
    second at 50 m. One that drifts past 175 m sinks into the sea over 25 s, moves to its mirror image upwind and rises
    there.
  - **Ruins (`MemoryLook.RuinPieces` + `RuinMaterial`, or the environment artist's `RuinModels`, laid out by `MemoryLook.Ruins`).** Weathered classical stone stands
    among the clouds, 45–280 m out and all the way round, as in the reference: square pillars, Doric columns, arches,
    an arcade, a wall with a doorway and a temple fragment. A cluster of loose blocks was dropped (it read as a
    placeholder).
    - **The kit.** `Tools/ShadowArt/build_ruin_kit.py` (Blender 4.2, seconds) builds 9 pieces from boxes, tapered
      cylinders and extruded arch outlines, with small bevels. Broken ones are cut by tilted planes and capped. Fold
      occlusion and height go in the vertex colour, as for the clouds. The script writes `Art/Ruins/RuinKit.fbx` and,
      with `--preview`, a contact sheet.
    - **The look.** They use the cloud shader on `Materials/Memory ruin.mat`, with a stone palette (violet-grey in
      shadow, warm where the glow reaches), no billow and firmer edges. So they share the clouds' light, the haze into
      the sky and the fade-in.
    - **Layout** (`MemoryClouds`, seeded). 24 ruins, spread evenly all round (a golden-angle spiral) and in distance
      (a Halton sequence, so near, mid and far always fill).
      Every piece appears at least once, then pillars, columns and arches come up twice as often as the arcade, wall
      and temple.
      - They stand with their bases sunk 20–40% into the sea, where the haze swallows them. Each is turned up to 70°
        off facing the player.
      - Up to 5 broken pieces float 4–18 m over the sea, tilted, bobbing slowly.
      - None rises more than 14° over the horizon. A standing piece that would sinks deeper (up to 60% of its height),
        so they frame the horizon rather than tower over the book.
      - They keep clear of only ±6° of the glow, so some stand by it.
      - They are static; the clouds drift past them. The ruins add about 12k triangles and 24 draws.
  - **Draw order.** The sky dome and the dark dome draw after all opaque geometry (`MemorySky.SkyQueue` and
    `DarkQueue`), so the depth test skips every pixel the clouds, the book or the room already cover.
- **The pages while a memory plays:** the page its lamp lights carries the shadow. The page the lamps leave dark
  (Birthday and Good Morning light the left page, Bon Voyage the right) carries the chapter number and name, the
  riddle, the hint after the first HINT, the picture after the second, PAUSED while paused, and once solved the level's
  `complete` line, the found picture and "Turn the page". Home lights both pages, so its name and riddle go in the top
  margins, the hint in the bottom margin and each wall's picture in a top outer corner. The riddle's margin box
  (`MemoryGame.MarginRiddle*`) is tall enough for four lines of verse and narrow enough to clear that corner picture.
- **Small tracked headings** (CHAPTER n, TO THE READER, SETTINGS) go through `MemoryGame.Kicker`: Chinese sets them
  `KickerCjk` (1.6×) larger with less tracking, since four upright characters at the English size read as a footnote.
### The toolbar

The play strip's buttons (`MemoryButton`) are in the page tiles' look: a square leather cap with a gold icon that sinks
while pressed, an aqua collar on hover, hint pips, a hold's square ring of dots just outside the collar.

| Where | When | Buttons |
| --- | --- | --- |
| The play strip, lying across the table's front under the orb (`MemoryGame.StripAt`: 30 cm ahead, 52 cm under the eyes, tilted to them; 53 cm wide) | While playing: it rises out of the table (0.35 s) and sinks again | HINT (two pips) · HOLD TO RESET (1 s) · PAUSE · HOLD TO QUIT (1.5 s), in a row, names under the caps |
| QUIT's ribbon (ember, the right-hand one; [Ribbons](#ribbons-memorybookmarks)) | Whenever the book lies open on its stand: browsing, paused or found | Turns to the back endpaper, which asks: KEEP READING or QUIT |

**Mistouch guard** (2026-11-16, the user: hands rolling the orb pressed it, unseen): on a headset the strip takes a
fingertip only while the player looks down at it, the head at least 36° under the horizon (it sleeps again above 33°)
and within 45° of straight ahead (`MemoryGame.StripWake/StripSleep/StripYaw`, `LooksAtStrip`). The strip is about 60°
under the eyes, the orb about 27° (its lowest ring 33°), so looking at the orb never wakes it. Asleep, its buttons dim
and a finger doesn't light them (`MemoryButton.Awake`); the device log says `refused=not looking at the strip`. A hand
that held the orb in the last 0.6 s presses nothing on it either (`OrbCooldown`: letting go, the hand drops onto it).
The desktop's strip never sleeps.

Why there (2026-11-15 device round): the upright post by the right hand stood in front of the right lamp, and a post on
either side would, since the lamps sit about 57 cm out, level with the head. Under the orb in view, the strip hides no
lamp, ring or shadow, is about 28 cm from the lamps' beams and outside the orb's reach, and a seated player reaches it
with the forearm. Losing focus to the headset's menu while playing pauses, as PAUSE does (`MemoryGame.PauseForSystem`;
VRC.Quest.Functional.2).

QUIT (2026-11-15, the user's call): it had its own plaque on a post at the table's near right corner, there from the end
of the title on; it stood against the stand's corner in view and sat awkwardly. Now, off the walls it is a ribbon to a
page that asks, and in a memory a hold on the strip with the others. In flight (rising, the intro, coming down) there is
none: the headset's own menu is always there. Settings' LEFT / RIGHT HAND went with the plaque (it only moved the plaque;
`MemoryProgress.LeftHanded` is still stored).

### The drawer (`MemoryDrawer`)

The reading stand's drawer (`BookStandDrawer`, the environment artist's mechanism) is part of a memory's start, and
nothing more: PLAY (or PLAY AGAIN) on the page runs it out (0.22 s, a wooden slide), the memory starts (the book rises,
gold motes fly from the drawer's mouth to where the things come up on the table), and it slides shut with a knock as the
stand sinks. While it runs, the pages and the ribbons hold still and PLAY waits. No hand, look or press moves it.

History (2026-11-15): for one evening the drawer was the only way to start a memory (a pinch and a tug or a push on its
front, with a glow, a light, motes off the page, a hum, an aqua halo and band, and a ghost hint). On device players
didn't notice it or found it hard to work, so PLAY went back onto the page and all of those cues went.

- A stand without one (an empty `ReadingStand`): PLAY just starts the memory.
- `Art/Runtime/ShapeOfMemory.ArtDressing.asmref` puts `Art/Runtime` in the `ShapeOfMemory` assembly so the game can drive
  the mechanism.

### Ribbons (`MemoryBookmarks`)

Silk ribbons come out from between the pages at the book's near edge, up and over the stand's front rail, and hang in
front of it: Settings (gear) and Begin (book) left of the drawer's knob, the credits (heart) and QUIT (power, ember) right
of it; no ribbon per chapter (2026-11-15). The rail runs 1.5–4.5 cm in front of the book's near edge and stands
3.6–4.1 cm off the board (raycast on the scene's stand); each ribbon rises to just above its back edge, crosses its top
and hangs just past its front. Those points are measured from the Book Rest (`MemoryBookmarks.BookHeight` gives the book's
height on it), so a book of another thickness doesn't push them into the rail (our own book lies 2 cm lower than the
artist's, and the ribbons went through the rail when it came back). The silk is a generated woven texture (fine ribs, a sheen, stitched hems, a crease over the rail, a
swallowtail end), tinted per ribbon, one strip of silk from the pages to its foot (a mesh rebuilt as it moves,
2026-11-16: it was a hanging quad hinged to the span over the rail, and stirred, its twist opened a gap either side of
its middle). It rolls over the rail's front edge (`Roll`, 3 mm of silk), and a lift, the sway and the stir bend it along
its length and twist it more toward the foot, the mark embroidered in a deeper shade of its own colour; the ribbons sway slowly,
never while a hand or a look is on one; the open spread's ribbon hangs longer. They show whenever the book lies open on
the stand: browsing, and with a memory paused or found (a ribbon then leaves the memory, as turning the page away does).

**Picking** (2026-11-16, the user: lifting was hard to work; `MemoryBookmarks.PinchPicks`, on). A pinch on a ribbon is
the choice: within 4.5 cm of its hanging silk, a pinch takes it, it lifts itself over the rail in about 0.25 s (as the
desktop's click lifts it) and one page turns to its spread, whether the hand lets go or not; a hand rising with it only
lifts it sooner. The Begin page says how: PINCH A RIBBON (捏住书签). Turn `PinchPicks` off for the lift below.

**Lifting** (2026-11-15: the user found the poke unintuitive and asked to lift them; off since 2026-11-16). A pinch within 3.5 cm of a ribbon's
hanging silk takes it (a pinch that closed up to 0.4 s before reaching it counts, as at the page edges); its border
lights aqua while a pinch would take it and while it is held. Held, the silk points at the fingers, swinging about the
rail; lifted 60° from hanging (toward the reader and up: raising its foot to the rail, or drawing it out about 10 cm)
the book riffles to its spread and the ribbon falls back with a little swing. Let go short of that and it falls back and
nothing happens. A look and a pinch takes it too: the hand's rise lifts it (12 cm for all the way). The desktop's click
lifts it by itself (0.3 s). Fingertip and ray presses do nothing (a hand passing by turned the book). The Begin page
said how: LIFT A RIBBON. Device log: `SHAPE_OF_MEMORY_RIBBON` (taken, let go at, lifted -> spread).

They grow out of the book when they appear, one after another (0.5 s each, 0.08 s apart): the silk slides out from
between the pages, over the rail and down; they draw back in (0.25 s) when hidden. A hand within 9 cm of one stirs it as
a breeze does a beckoning page (it lifts about 12°, more toward its foot, and flutters and twists along its length); the
aqua border comes at 4.5 cm, where a pinch would take it. Picked, one page turns straight to its spread.

### The wall tone (`MemoryGame.WallTone`)

The pages' colour on the walls where no lamp reaches, and so the shadows' (`MemoryBook.Dark` → `Torch2Room.Base`): dusk
plum `#5E4E70` since 2026-11-15 (Ink700 `#2A2436` read too dark). Picked from a sheet of seven
(`.agents/local/captures/ui-pass-2026-11-15/walls/`): the two pale "paper in shadow" tones washed the shadow out. On a
pale tone (`PaleWalls`) the dark page's words switch to ink, gilt and deep aqua. `MemoryGame.UseWallTone(color)` changes
it live.

- **Placement:** the table is placed 0.65 m in front of the head at floor + 0.72 m (+ the saved lift) when the game
  starts, and again after the headset's own recenter. That recenter is detected from the runtime's
  `trackingOriginUpdated` event, or from the head pose jumping more than 45° or 0.5 m in one frame. There is no in-game
  recenter button: when it was on the console, turning your head to press it moved the table sideways, and it stacked
  on top of the system recenter. Each LOWER / RAISE step is 2.5 cm and is saved.
- **Pico hands (`MemoryHandSkin`, `MemoryHands`):** on the Pico build's UnityXR rig, the Interaction SDK's hand mesh is
  hidden (`HandVisual.ForceOffVisibility`). It is fitted to Meta's skeleton and sat off the real hands there.
  - With a hand look on the game (`MemoryGame.HandLook`, assets in `Looks/Hands`), our own skinned hand is drawn
    (`MemoryHandSkin`). The mesh is `Art/Hands/MemoryHand_R/_L`, built in Blender by `Tools/ShadowArt/build_hand.py`.
    Its bones are placed from the tracked joint positions alone: each joint's bone sits on its joint, points at the
    next joint and stretches to reach it. So the skin takes the tracked hand's proportions, and the fingertips are
    where presses land.
  - The looks are **Ink** (a pen-drawn outline that is redrawn 8 times a second, over a see-through paper wash with
    hatching on the shaded side) and **Lantern** (warm light, brightest facing the eye, with motes drifting off the
    fingers). In both, the index and thumb tips are aqua and brighten as a pinch closes.
  - Without a look, the tracked joints are drawn as bones (`MemoryHands`): slim bones between joints, small knuckle
    balls, and glowing tips.
  - Switch with **Pico Final > Hands > Use Ink / Use Lantern / Use the joint bones**, then save the scene.
    **Hands > Show posed hands in the Scene view** poses both hands by the book (the editor has no tracking).
  - To change the mesh, edit `build_hand.py`, run it, then **Hands > Import hand mesh**. **Hands > Make looks**
    rebuilds the looks and their materials (`Materials/Hands`).
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
| `Runtime/MemoryBook.cs` | The book: covers (grained leather; the front's tooled cover art, generated), spine, page blocks and paper on the wall planes, spreads, corner drag, riffles, one-page jumps and page sounds |
| `Runtime/MemoryPageTurn.cs` | `PageTurn` (how a turning page moves: held, let go, tapped) and `PageBend` (the shape it bends into) |
| `Runtime/MemoryPageSheet.cs` | `PageSheet`: the turning page's mesh, its two printed sides (snapshots of the page content) and its shade |
| `Shaders/BookPage.shader`, `Shaders/BookPageShadow.shader` | The turning page and the shade it casts; materials in `Resources/ShapeOfMemory/` |
| `Runtime/MemoryPages.cs` | Page printing (text, rules, picture plates, the title page) and `MemoryTile`, the button tiles (a hold with a ring of dots; a chosen one's gilt frame) |
| `Runtime/MemoryButton.cs` | The toolbar's buttons: square leather cap, gold icon, name beside it, hint pips, hold ring |
| `Runtime/MemoryDrawer.cs` | The stand's drawer in a memory's start: runs out, starts it, shuts; gold motes to the table; slide and knock sounds |
| `Runtime/MemoryBookmarks.cs` | The ribbons: generated silk, routed over the stand's rail (points from the Book Rest) either side of the knob, sway, pinch-and-lift to riffle |
| `Runtime/MemoryFound.cs` | The memory-found moment over the orb: plaque, glints and bells |
| `Runtime/MemoryDream.cs` | Architecture drifting beyond the book once the sky is up: seeded layout, surfacing, bob and sway |
| `Tools/ShadowArt/build_dream_kit.py` | Models the dream kit in Blender and bakes its light into `Art/Dream/DreamKit.png` |
| `Runtime/MemoryClouds.cs` | Clouds round the player: seeded sea, puffs and big clouds, sway, wind drift, reveal with the sky |
| `Shaders/Cloud.shader` | The clouds' (and ruins') ramp-lit shader with sky-coloured haze, soft edges, billow and the fade with the sky |
| `Tools/ShadowArt/build_cloud_kit.py` | Builds the cloud kit in Blender (`Art/Clouds/CloudKit.fbx`: near, `_LOD1` and `_LOD2` meshes) |
| `Tools/ShadowArt/build_ruin_kit.py` | Builds the ruin kit in Blender (`Art/Ruins/RuinKit.fbx`: pillars, columns, arches, temple, blocks) |
| `Runtime/MemoryHandSkin.cs` | The skinned hands: bones placed from the tracked joints (`Rig`), one hand's renderer and motes (`SkinnedHand`) |
| `Runtime/MemoryHandLook.cs` | A hand look: the hand meshes, the materials in draw order, motes (`Looks/Hands/Ink`, `Lantern`) |
| `Runtime/MemoryHandPose.cs` | Made-up poses (open, pinch, point) round the mesh's rest pose, for the editor preview and tests |
| `Editor/MemoryHandTools.cs` | Hands menu: import the mesh from `build_hand.py`'s JSON, make the looks, pick one, posed preview |
| `Shaders/Hands/*.shader` | `HandDepth` (drawn first), `HandInkLine`, `HandInkWash`, `HandLantern`, `HandMote` |
| `Tools/ShadowArt/build_hand.py` | Builds our hand in Blender, rigged to the 26 OpenXR joints (`Art/Hands/Source/MemoryHand.json`) |
| `Runtime/MemoryButton.cs` | Console push button |
| `Runtime/Pressable.cs` | One press path for hands (MetaMenuButton) and mouse (ShadowControl); hold-to-fire. Actions run on the next Update |
| `Runtime/MemoryChapters.cs` | The chapter list asset (`Resources/ShapeOfMemory/Chapters.asset`) and its `Check()` |
| `Editor/MemoryChaptersTools.cs` | Chapters / Check chapters menu items, the list's inspector, the build check |
| `Runtime/MemoryCredits.cs` | The credits asset (`Resources/ShapeOfMemory/Credits.asset`) and its `Check()` |
| `Editor/MemoryCreditsTools.cs` | The Credits menu item, the asset's inspector, the build's warnings |
| `Runtime/MemoryProgress.cs` | Unlocks, hints, lift, per level id in the Chapters order; PlayerPrefs store (in-memory store for tests) |
| `Runtime/MemoryUi.cs` | Palette and builders; `MemoryKit` owns runtime materials and textures |
| `Runtime/MemoryText.cs` | Every printed word from the Localization tables (`Get`, `Name`/`Riddle`/`Hint`/`Complete`, `Chapter`), a label's face by `Role`, upright CJK, `Check()` |
| `Runtime/MemoryFonts.cs` | The faces by role and script (`Resources/ShapeOfMemory/Fonts.asset`) |
| `Editor/MemoryFontsTools.cs` | Fonts menu: Bake atlases, Select fonts asset |
| `Tools/ShadowArt/build_fonts.py` | Makes the TTFs in `PicoFinal/Fonts` from Google Fonts' originals |
| `Editor/MemoryTextTools.cs` | Text menu: Open tables, Copy asset text into tables, Check text |
| `Editor/ShapeOfMemoryBuilder.cs` | Scene builder, icon import, icon material, Pico APK build |
| `Editor/ShapeOfMemoryMeshes.cs` | Rebuilds the environment's cloud sea meshes from the cloud kit, and the book with its spine fixed for opening |
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
chapter and both corners, the last chapter's words (in the margins when it lights both walls), the ending and the credits after it. They drive buttons, tiles and corners through the
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
  placeholders.
