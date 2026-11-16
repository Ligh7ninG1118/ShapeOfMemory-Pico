using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShapeOfMemory.ArtDressing
{
    // Puts the environment artist's models on the table-top things Shape of Memory builds from primitives at runtime,
    // without changing the game: no edit to MemoryGame, MemoryBook or PicoFinal.unity, and nothing of the game's is
    // destroyed or moved. The art is parented to the game's own objects, so it follows them (the stand sinking, the
    // book flying, the table re-placed on recenter), and the game's boxes it covers stop drawing (Renderer
    // .forceRenderingOff) but stay enabled, so everything that reads, tints or fades them works as before.
    //
    //   Sculptor table (every room's)  ->  SM_Table_01, fitted to the table's top and footprint
    //   Reading stand                  ->  BookStand_01, its board under the book, sinking with the stand
    //   Lamp 0 / Lamp 1                ->  SM_Spotlight_01, lens (model +z) along the lamp's beam (the flashlight models hidden)
    //   Book, cover shut               ->  SM_Book_01, its cover turning with MemoryBook.Opening; the game's book
    //                                      comes back as soon as the cover lifts past OpenSwap degrees (its pages are
    //                                      the game's interface: the art book has none of that)
    //
    // The art fades from black with the game's own fade (MemoryShade tints the boxes it covers; the same level is read
    // back off them). Starts by itself whenever a MemoryGame is in the loaded scene; the prefabs come from
    // Resources/ShapeOfMemoryArtDressing (PicoFinalArtDressingSet). Enabled = false (at runtime) puts the game's boxes
    // back. No colliders: the art never blocks look-and-pinch.
    public sealed class PicoFinalArtDressing : MonoBehaviour
    {
        public const string SetName = "ShapeOfMemoryArtDressing";
        public static bool Enabled = true;

        // The table: the art slab's top is at its local y = 0 and its footprint (without the floating chips) is this.
        static readonly Vector2 SlabSize = new Vector2(2.442f, 1.878f);
        // The stand: the art lectern at its own proportions, 0.45 of its modelled size (0.40 x 0.44 x 0.41 m): at that
        // size its board, from the lip to where the crest starts, is as long as the book on it (0.34 m), so the book's
        // top edge meets the board's. Turned so its lip faces the reader, its base on the table, its board tilted
        // 31.9 degrees with a 4.3 cm lip.
        const float StandScale = 0.45f;
        // ...and widened (x only) so its board is as wide as the book lying open on it (0.71 m: the board is 0.744 of
        // the model across). Height and depth stay at StandScale, so the board's slope and the book's pose don't change.
        // The drawer's knob is given the inverse across its width, so it keeps its own round shape.
        const float StandWidth = 0.957f;
        const string KnobName = "SM_BookStandDrawerKnob_01";
        static readonly Vector3 StandOffset = new Vector3(0f, 0.00225f, -0.088f);
        // So the book rests on that board: MemoryBook.StandPose, the book's root (the spine's near end) in stage space,
        // on the art board against its lip, at the board's tilt. That is within 1.5 cm of the game's own
        // (0, 0.228, -0.242) at 30 degrees. The prompt over the book ("Label" under the stage) moves by as much.
        static readonly Pose BookOnStand = new Pose(new Vector3(0f, 0.2421f, -0.2413f), Quaternion.Euler(-31.9f, 0f, 0f));
        // The title's descent (presented, upright a metre ahead -> the stand) arcs up by TitleArc instead of the game's
        // 0.05 m, so the book clears the art stand's crest (simulated along the flight: at 0.05 it cuts through the crest
        // halfway down; from 0.15 nothing of the stand enters it). Same target, same MemoryGame.DescendSeconds.
        const float TitleArc = 0.2f;
        // The lamps: the art sphere, centred on the lamp, its lens (the glowing disc in the gold ring, +z in the model) along
        // the beam (+z of the lamp, toward the pile), so no turn. (It was turned 180 degrees, which put the plain back
        // disc toward the pile and the lens toward the player: checked by rendering the prefab, 2026-11-14.)
        const float LampScale = 0.4f;
        static readonly Vector3 LampCentre = new Vector3(-0.008f, -0.002f, -0.112f);
        // The book, in the book's own space (x across from the spine, y up through the closed book, z along the
        // spine, 0 at its near end; scaled by MemoryBook.FlatScale on the stand): the closed game book spans
        // x -0.09..1.15, y -0.042..0.042, z -0.02..1.08. The art book is modelled with its thickness along x, away from
        // the hinge along y and the spine along z, the cover half at +x: turned so that maps onto the game's axes, at its
        // own proportions (0.211 thick against the game's 0.084), its underside on the game book's underside. So its
        // cover is CoverLift above the game's: the game's cover face (title, edge band, the click's collider) is
        // lifted by that much while the art book shows, and put back when the game's book takes over.
        static readonly Vector3 BookAt = new Vector3(0f, -0.042f + 0.1055f, 0.528f);
        static readonly Vector3 BookFit = Vector3.one;
        const float CoverLift = 0.169f - 0.042f;
        static readonly Quaternion BookTurn = Quaternion.LookRotation(Vector3.back, Vector3.right);
        // The cover lifted past this (degrees), the game's own book takes over.
        public const float OpenSwap = 12f;
        static readonly Color StandWood = new Color(.24f, .16f, .11f);   // MemoryGame's Wood (private there)

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor"), OpeningId = Shader.PropertyToID("_Opening");
        static PicoFinalArtDressing instance;

        MemoryGame game;
        PicoFinalArtDressingSet set;
        float nextScan;
        bool shown = true;
        MaterialPropertyBlock block;   // made in Awake: Unity refuses one in a field initializer

        // A piece of art and what it covers.
        sealed class Mount
        {
            public Transform host, art;
            public Renderer[] artRenderers;
            public readonly List<Renderer> hidden = new List<Renderer>();
            public Renderer levelFrom;      // a covered box whose fade we copy
            public Color levelRef;           // its colour at full brightness
            public float level = -1;
            public bool isBook;
        }
        readonly List<Mount> mounts = new List<Mount>();
        readonly HashSet<Transform> mounted = new HashSet<Transform>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            TryStart();
        }
        static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => TryStart();
        static void TryStart()
        {
            if (instance) return;
            var game = FindFirstObjectByType<MemoryGame>();
            if (!game) return;
            var set = Resources.Load<PicoFinalArtDressingSet>(SetName);
            if (!set) { Debug.LogWarning("SHAPE_OF_MEMORY_ART no Resources/" + SetName + ": the game's own boxes stay"); return; }
            var host = new GameObject("Art dressing");
            host.transform.SetParent(game.transform, false);
            instance = host.AddComponent<PicoFinalArtDressing>();
            instance.game = game;
            instance.set = set;
            Debug.Log("SHAPE_OF_MEMORY_ART dressing on");
        }

        void Awake() => block = new MaterialPropertyBlock();
        void OnDestroy() { if (instance == this) instance = null; }

        void LateUpdate()
        {
            if (!game) { Destroy(gameObject); return; }
            if (Enabled && Time.unscaledTime >= nextScan) { nextScan = Time.unscaledTime + 0.25f; Scan(); }
            if (Enabled && set.Stand) ArcTitleDescent();
            if (Enabled != shown) { shown = Enabled; foreach (var m in mounts) if (m.art) m.art.gameObject.SetActive(shown); }
            for (int i = mounts.Count - 1; i >= 0; i--)
            {
                var m = mounts[i];
                if (!m.host || !m.art) { if (m.host) mounted.Remove(m.host); Restore(m); mounts.RemoveAt(i); continue; }
                bool show = shown;
                if (m.isBook && show) show = BookShut();
                if (m.isBook) { foreach (var r in m.artRenderers) if (r) r.enabled = show; LiftCover(show); }
                foreach (var r in m.hidden) if (r) r.forceRenderingOff = show;
                if (show) Shade(m);
            }
        }

        // ---- Finding the game's things (they come and go with the rooms)
        void Scan()
        {
            var stage = game.Stage;
            if (!stage) return;
            foreach (var t in stage.GetComponentsInChildren<Transform>(true))
            {
                switch (t.name)
                {
                    case "Sculptor table": if (set.Table && !mounted.Contains(t)) MountTable(t); break;
                    case "Reading stand": if (set.Stand && !mounted.Contains(t)) MountStand(t); break;
                    case "Lamp 0":
                    case "Lamp 1": if (set.Lamp && !mounted.Contains(t)) MountLamp(t); break;
                }
            }
            var book = game.Book;
            if (set.Book && book && !mounted.Contains(book.transform)) MountBook(book);
            if (set.Stand && book && !bookMoved) MoveBookOntoStand(book, stage);
            // Flashlights arrive with a memory's room, after the lamp is dressed.
            foreach (var m in mounts)
            {
                if (m.isBook || !m.host || !m.host.name.StartsWith("Lamp ")) continue;
                foreach (var r in m.host.GetComponentsInChildren<Renderer>(true))
                    if (!r.transform.IsChildOf(m.art) && !m.hidden.Contains(r) && !(r is ParticleSystemRenderer)) m.hidden.Add(r);
            }
        }

        Mount Add(Transform host, GameObject prefab, Transform parent, bool isBook = false)
        {
            var art = Instantiate(prefab, parent, false).transform;
            art.name = "Art: " + prefab.name;
            // No colliders that could block the game's rays; ones on Ignore Raycast (the drawer knob's) are handles.
            foreach (var c in art.GetComponentsInChildren<Collider>(true)) if (c.gameObject.layer != IgnoreRaycast) Destroy(c);
            foreach (var r in art.GetComponentsInChildren<Renderer>(true))
            {
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            }
            SetLayer(art, host.gameObject.layer);
            var m = new Mount { host = host, art = art, artRenderers = art.GetComponentsInChildren<Renderer>(true), isBook = isBook };
            mounts.Add(m);
            mounted.Add(host);
            return m;
        }

        void MountTable(Transform table)
        {
            var renderer = table.GetComponent<Renderer>();
            var s = table.localScale;
            var m = Add(table, set.Table, table.parent);
            float sx = s.x / SlabSize.x, sz = s.z / SlabSize.y;
            m.art.localPosition = table.localPosition + new Vector3(0, s.y * 0.5f + 0.001f, 0);
            // Turned round: the ivy side away from the player (the slab is centred, so it stays on the footprint).
            m.art.localRotation = table.localRotation * Quaternion.Euler(0, 180, 0);
            m.art.localScale = new Vector3(sx, Mathf.Min(sx, sz), sz);
            if (renderer) { m.hidden.Add(renderer); m.levelFrom = renderer; m.levelRef = MemoryPalette.Table; }
        }

        void MountStand(Transform stand)
        {
            var m = Add(stand, set.Stand, stand);
            m.art.localPosition = StandOffset;
            m.art.localRotation = Quaternion.Euler(0, 180, 0);
            m.art.localScale = new Vector3(StandWidth, StandScale, StandScale);
            var knob = m.art.Find(KnobName);
            if (knob) { var k = knob.localScale; knob.localScale = new Vector3(k.x * StandScale / StandWidth, k.y, k.z); }
            foreach (Transform part in stand)
            {
                if (part == m.art || !part.name.StartsWith("Stand ")) continue;
                var r = part.GetComponent<Renderer>();
                if (r) { m.hidden.Add(r); if (part.name == "Stand board") { m.levelFrom = r; m.levelRef = StandWood; } }
            }
        }

        void MountLamp(Transform lamp)
        {
            var m = Add(lamp, set.Lamp, lamp);
            var turn = Quaternion.identity;
            m.art.localRotation = turn;
            m.art.localScale = Vector3.one * LampScale;
            m.art.localPosition = -(turn * LampCentre) * LampScale;
            // The lamps have no box of their own to fade with: they follow the table's.
        }

        void MountBook(MemoryBook book)
        {
            var fit = new GameObject("Art: book fit").transform;
            fit.SetParent(book.transform, false);
            fit.localPosition = BookAt;
            fit.localScale = BookFit;
            var m = Add(book.transform, set.Book, fit, true);
            m.art.localRotation = BookTurn;
            m.art.localPosition = Vector3.zero;
            // Opened, the turning half sweeps over the other side of the spine: keep it from being culled.
            foreach (var r in m.artRenderers) r.localBounds = new Bounds(new Vector3(0, 0, 0), new Vector3(2.4f, 2.4f, 1.2f));
            foreach (var r in book.GetComponentsInChildren<Renderer>(true))
            {
                if (r.transform.IsChildOf(fit) || !r.sharedMaterial) continue;
                var name = r.sharedMaterial.name;
                if (!(name.StartsWith("Graybox") || name.StartsWith("Paper"))) continue;   // keep the band, icons, prints
                m.hidden.Add(r);
                if (r.name == "Cover" && !m.levelFrom) { m.levelFrom = r; m.levelRef = MemoryBook.Leather; }
            }
        }

        // ---- The book on the art stand, and the game's cover face on the art cover
        bool bookMoved;
        Transform prompt;
        Vector3 promptRest;
        Transform coverFace;
        Vector3 coverRest, coverUp;
        bool coverLifted;

        // The art board is higher than the game's: the book's rest pose (MemoryBook.StandPose, a public field the game
        // sets once) goes onto it, the book is put there if it already lies on the stand, and the prompt over it (the
        // stage's "Label", placed once from the same pose) moves by as much. Everything that places the book on the
        // stand (landing, MENU, solved) goes through StandPose, so it lands on the art board from then on.
        void MoveBookOntoStand(MemoryBook book, Transform stage)
        {
            bookMoved = true;
            var old = book.StandPose;
            book.StandPose = BookOnStand;
            if (book.OnStand) book.Place(book.Stand(book.Opening), MemoryBook.Where.Stand);
            foreach (Transform t in stage)
                if (t.name == "Label" && t.Find("Prompt card")) { prompt = t; promptRest = t.localPosition; t.localPosition = promptRest + (BookOnStand.position - old.position); }
            Debug.Log("SHAPE_OF_MEMORY_ART book onto the art stand: " + old.position.ToString("F3") + " -> " + BookOnStand.position.ToString("F3") + (prompt ? ", prompt moved" : ", no prompt found"));
        }

        // The title's descent is the only flight that starts from Presented (MemoryGame issues it once, with no callback,
        // and the cover can't be pressed while the title plays). On the frame it starts, it is flown again to the same
        // stand pose over the same time with a higher arc. Runs every frame, after the game's Update.
        bool titleArced;
        void ArcTitleDescent()
        {
            var book = game.Book;
            if (titleArced || !book || !book.Flying || book.At != MemoryBook.Where.Presented) return;
            titleArced = true;
            book.Fly(book.Stand(0), MemoryBook.Where.Stand, MemoryGame.DescendSeconds, null, TitleArc);
        }

        // The art cover is CoverLift (book space) above the game's: while the art book shows, the game's cover face
        // (title, band, the click's collider) sits on it; when the game's book takes over it goes back. The lift is
        // along the book's "up" through the closed book, held in the left board's own space so it turns with the cover.
        void LiftCover(bool lift)
        {
            var book = game.Book;
            if (!coverFace && book && book.CoverFace)
            {
                coverFace = book.CoverFace; coverRest = coverFace.localPosition;
                var board = coverFace.parent;
                coverUp = board.InverseTransformDirection(book.transform.up).normalized;   // the book is shut when this first runs
            }
            if (!coverFace || lift == coverLifted) return;
            coverLifted = lift;
            coverFace.localPosition = lift ? coverRest + coverUp * CoverLift : coverRest;
        }

        bool BookShut()
        {
            var book = game.Book;
            return book && book.At != MemoryBook.Where.Walls && book.Opening < OpenSwap;
        }

        // ---- Fading with the game, and the book's cover
        void Shade(Mount m)
        {
            float level = 1;
            var from = m.levelFrom;
            if (!from && !m.isBook) foreach (var other in mounts) if (other.levelFrom && other.host && other.host.name == "Sculptor table") { from = other.levelFrom; m.levelRef = other.levelRef; break; }
            if (from)
            {
                from.GetPropertyBlock(block);
                if (block.HasColor(BaseColorId))
                {
                    var c = block.GetColor(BaseColorId);
                    float reference = Mathf.Max(m.levelRef.r, Mathf.Max(m.levelRef.g, m.levelRef.b));
                    if (reference > 0.001f) level = Mathf.Clamp01(Mathf.Max(c.r, Mathf.Max(c.g, c.b)) / reference);
                }
            }
            float opening = m.isBook && game.Book ? game.Book.Opening : 0;
            if (!m.isBook && Mathf.Abs(level - m.level) < 0.002f) return;
            m.level = level;
            foreach (var r in m.artRenderers)
            {
                if (!r) continue;
                if (level >= 0.999f && !m.isBook) { r.SetPropertyBlock(null); continue; }
                r.GetPropertyBlock(block);
                block.SetColor(BaseColorId, new Color(level, level, level, 1));
                if (m.isBook) block.SetFloat(OpeningId, opening);
                r.SetPropertyBlock(block);
            }
        }

        void Restore(Mount m) { foreach (var r in m.hidden) if (r) r.forceRenderingOff = false; if (m.isBook) LiftCover(false); }

        const int IgnoreRaycast = 2;
        static void SetLayer(Transform t, int layer) { if (t.gameObject.layer != IgnoreRaycast) t.gameObject.layer = layer; foreach (Transform c in t) SetLayer(c, layer); }
    }
}
