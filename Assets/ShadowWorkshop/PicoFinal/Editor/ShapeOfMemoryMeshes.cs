using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ShapeOfMemory.Editor
{
    // Mesh processing for the environment artist's models (2026-11-14). Both write .asset meshes next to theirs in
    // Art/Art Assets/Meshes, as they did for SM_BookStand_01_FacesFixed, and leave their FBX files alone: run them again
    // after the source changes.
    static class MeshTopology
    {
        // Vertices welded by position (the importer splits them at UV and normal seams), and each welded vertex's
        // neighbours along the triangles' edges.
        public static int Weld(Vector3[] positions, out int[] weld)
        {
            var key = new Dictionary<Vector3, int>();
            weld = new int[positions.Length];
            int count = 0;
            for (int i = 0; i < positions.Length; i++)
            {
                if (!key.TryGetValue(positions[i], out int k)) { k = count++; key[positions[i]] = k; }
                weld[i] = k;
            }
            return count;
        }

        public static List<int>[] Neighbours(int[] triangles, int[] weld, int count)
        {
            var sets = new HashSet<int>[count];
            for (int i = 0; i < count; i++) sets[i] = new HashSet<int>();
            for (int t = 0; t < triangles.Length; t += 3)
                for (int e = 0; e < 3; e++)
                {
                    int a = weld[triangles[t + e]], b = weld[triangles[t + (e + 1) % 3]];
                    if (a == b) continue;
                    sets[a].Add(b); sets[b].Add(a);
                }
            var lists = new List<int>[count];
            for (int i = 0; i < count; i++) lists[i] = new List<int>(sets[i]);
            return lists;
        }

        // Area-weighted normals over the welded mesh (Unity's RecalculateNormals keeps seams split).
        public static Vector3[] Normals(Vector3[] positions, int[] triangles, int[] weld, int count)
        {
            var sum = new Vector3[count];
            for (int t = 0; t < triangles.Length; t += 3)
            {
                var a = positions[triangles[t]]; var b = positions[triangles[t + 1]]; var c = positions[triangles[t + 2]];
                var n = Vector3.Cross(b - a, c - a);
                for (int e = 0; e < 3; e++) sum[weld[triangles[t + e]]] += n;
            }
            var normals = new Vector3[positions.Length];
            for (int i = 0; i < positions.Length; i++) normals[i] = sum[weld[i]].normalized;
            return normals;
        }

        public static float MeanEdge(Vector3[] positions, int[] triangles)
        {
            double sum = 0;
            for (int t = 0; t < triangles.Length; t += 3)
                for (int e = 0; e < 3; e++) sum += (positions[triangles[t + e]] - positions[triangles[t + (e + 1) % 3]]).magnitude;
            return (float)(sum / triangles.Length);
        }
    }

    // ---- The environment scene's cloud sea
    //
    // Art/Art Assets/Meshes/CloudSea/<cloud>_Full, _LOD1_Smooth and _LOD2_Smooth are the cloud kit's <cloud>, <cloud>_LOD1
    // and <cloud>_LOD2 (Art/Clouds/CloudKit.fbx, Tools/ShadowArt/build_cloud_kit.py), processed by the environment artist.
    // Their recipe, read back from their assets on 2026-11-14:
    //   positions  the folds between lumps filled: Laplacian smoothing that may only move a vertex outward along its
    //              kit normal (0.5 a pass, FoldPasses at the near mesh's edge length; it reproduces their _Full meshes to
    //              4 cm on average; their LODs were smoothed some other way, these get the same fill at their own scale)
    //   normals    recalculated from those positions, area-weighted, welded (exact match)
    //   UV2        the "big shape" normal EnvCloudSea.shader reads: the normals diffused over the mesh, each pass
    //              normalize(itself + its neighbours), (BulkReach / mean edge)^2 passes, at least 4 (exact match)
    //   colour     the kit's (R fold occlusion, G height)
    // Rebuild writes into the existing assets (same objects), so the environment scene's renderers keep pointing at them.
    public static class CloudSeaMeshes
    {
        public const string Folder = ShapeOfMemoryBuilder.Root + "/Art/Art Assets/Meshes/CloudSea";
        public const string KitPath = ShapeOfMemoryBuilder.Root + "/Art/Clouds/CloudKit.fbx";
        const float FoldStep = 0.5f, FoldPassesAtEdge = 49, FoldEdge = 0.656f, BulkReach = 3.85f;
        static readonly string[,] Tiers = { { "", "_Full" }, { "_LOD1", "_LOD1_Smooth" }, { "_LOD2", "_LOD2_Smooth" } };

        [MenuItem("MetaHackthone/Pico Final/Art/Rebuild the environment's sea meshes from the cloud kit")]
        static void RebuildSea() => Rebuild("Sea_A", "Sea_B", "Sea_C", "Sea_D");

        public static void Rebuild(params string[] clouds)
        {
            var kit = new Dictionary<string, Mesh>();
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(KitPath)) if (o is Mesh m) kit[m.name] = m;
            int done = 0;
            foreach (var cloud in clouds)
                for (int tier = 0; tier < Tiers.GetLength(0); tier++)
                {
                    string from = cloud + Tiers[tier, 0], to = Folder + "/" + cloud + Tiers[tier, 1] + ".asset";
                    var target = AssetDatabase.LoadAssetAtPath<Mesh>(to);
                    if (!kit.TryGetValue(from, out var source) || !target) { Debug.LogError($"SHAPE_OF_MEMORY_MESH missing {from} in the kit or {to}"); continue; }
                    Process(source, target);
                    done++;
                }
            AssetDatabase.SaveAssets();
            Debug.Log($"SHAPE_OF_MEMORY_MESH rebuilt {done} sea meshes from {KitPath}");
        }

        static void Process(Mesh source, Mesh target)
        {
            var positions = source.vertices; var kitNormals = source.normals; var colours = source.colors; var triangles = source.triangles;
            int count = MeshTopology.Weld(positions, out var weld);
            var neighbours = MeshTopology.Neighbours(triangles, weld, count);
            float edge = MeshTopology.MeanEdge(positions, triangles);

            // Fill the folds: smooth, but never inward of where the kit put the vertex.
            var start = new Vector3[count]; var outward = new Vector3[count];
            for (int i = 0; i < positions.Length; i++) { start[weld[i]] = positions[i]; outward[weld[i]] += kitNormals[i]; }
            for (int i = 0; i < count; i++) outward[i] = outward[i].normalized;
            var current = (Vector3[])start.Clone();
            int passes = Mathf.Max(1, Mathf.RoundToInt(FoldPassesAtEdge * (FoldEdge / edge) * (FoldEdge / edge)));
            for (int pass = 0; pass < passes; pass++)
            {
                var next = new Vector3[count];
                for (int i = 0; i < count; i++)
                {
                    var mean = Vector3.zero;
                    foreach (int j in neighbours[i]) mean += current[j];
                    mean /= Mathf.Max(1, neighbours[i].Count);
                    var p = current[i] + FoldStep * (mean - current[i]);
                    next[i] = Vector3.Dot(p - start[i], outward[i]) < 0 ? start[i] : p;
                }
                current = next;
            }
            var filled = new Vector3[positions.Length];
            for (int i = 0; i < positions.Length; i++) filled[i] = current[weld[i]];

            var normals = MeshTopology.Normals(filled, triangles, weld, count);
            // The big shape: the normals diffused over the mesh.
            var bulk = new Vector3[count];
            for (int i = 0; i < positions.Length; i++) bulk[weld[i]] = normals[i];
            int diffuse = Mathf.Max(4, Mathf.RoundToInt((BulkReach / edge) * (BulkReach / edge)));
            for (int pass = 0; pass < diffuse; pass++)
            {
                var next = new Vector3[count];
                for (int i = 0; i < count; i++)
                {
                    var s = bulk[i];
                    foreach (int j in neighbours[i]) s += bulk[j];
                    next[i] = s.normalized;
                }
                bulk = next;
            }
            var uv2 = new List<Vector3>(positions.Length);
            for (int i = 0; i < positions.Length; i++) uv2.Add(bulk[weld[i]]);

            var serialized = new SerializedObject(target);
            bool readable = serialized.FindProperty("m_IsReadable").boolValue;
            target.Clear();
            target.indexFormat = source.indexFormat;
            target.vertices = filled;
            target.normals = normals;
            target.colors = colours;
            target.SetUVs(2, uv2);
            target.triangles = triangles;
            target.RecalculateBounds();
            EditorUtility.SetDirty(target);
            serialized.Update();
            serialized.FindProperty("m_IsReadable").boolValue = readable;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Debug.Log($"SHAPE_OF_MEMORY_MESH {target.name} <- {source.name}: {positions.Length} vertices, edge {edge:0.000}, {passes} fill passes, {diffuse} bulk passes, {target.bounds.size:F1}");
        }
    }

    // ---- The far ring: the cloud sea carried on from its edge (about 170 m) toward the horizon (2026-11-14, the user: "lay
    // more cloud in the distance, so it transits more seamlessly from the cloud sea to the skybox cloud"). Rings of the sea's
    // own far pieces (the environment artist's _LOD2_Smooth meshes on her MT_CloudSea), larger the further out, their tops
    // at the sea's level, and a few banks and puffs rising into the sky's cloud band away from the glow. The haze takes the
    // outer rings most of the way to the sky's colour, so the sea runs into the painted clouds. Combined into Sectors meshes
    // (one draw each) in Art/Art Assets/Meshes/CloudSeaFar, placed in the environment scene at Environment/Art/Cloud_Sea/
    // Far ring: inside the sea's group, so every look that hides the sea or recolours it does the same to the ring.
    // Seeded: the same every run. About 140 pieces, 40k triangles, 12 draws.
    public static class CloudSeaFarRing
    {
        public const string Folder = ShapeOfMemoryBuilder.Root + "/Art/Art Assets/Meshes/CloudSeaFar";
        public const string ScenePath = ShapeOfMemoryBuilder.Root + "/Scenes/PicoFinal_Environment.unity";
        public const string GroupName = "Far ring";
        const int Sectors = 12, Seed = 1114;
        const float SeaTop = -8.2f;                  // the sea's tops (its edge pieces: -7.3 .. -9.1 m from the floor)
        const float Eyes = 1.215f;                   // the eyes over the floor (MemoryGame: StageBelow over the table top)
        const float GlowYaw = -15, GlowClear = 40;   // no risers this near the glow (the look's glow faces -20)
        // Rings of sea pieces: radius (m), piece scale, count.
        static readonly (float radius, float scale, int count)[] Rings =
            { (192, 3.8f, 26), (245, 4.6f, 26), (310, 5.8f, 26), (395, 7.4f, 25), (500, 11f, 20), (630, 14f, 18) };
        // Risers: banks and puffs between these radii whose tops stand this many degrees over the horizon.
        const int Risers = 16;
        const float RiserNear = 380, RiserFar = 640, RiserLow = 0.6f, RiserHigh = 3.2f;
        static readonly string[] SeaPieces = { "Sea_A", "Sea_B", "Sea_C", "Sea_D" };
        static readonly string[] RiserPieces = { "Bank_A", "Bank_B", "Puff_A", "Puff_B", "Puff_D" };

        [MenuItem("MetaHackthone/Pico Final/Art/Rebuild the far cloud ring")]
        static void RebuildMenu() => Rebuild();

        struct Piece { public Mesh mesh; public Vector3 at; public float yaw, scale; }

        public static void Rebuild()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(ScenePath);
            if (!scene.IsValid() || !scene.isLoaded) { Debug.LogError("SHAPE_OF_MEMORY_MESH open " + ScenePath + " first (Pico Final > Scene shows it beside PicoFinal)"); return; }
            Transform root = null;
            foreach (var r in scene.GetRootGameObjects()) if (r.name == MemoryEnvironment.RootName) root = r.transform;
            var sea = root ? root.Find(MemoryEnvironment.ArtName + "/" + MemoryEnvironment.SeaName) : null;
            var material = AssetDatabase.LoadAssetAtPath<Material>(ShapeOfMemoryBuilder.EnvSeaMaterialPath);
            if (!sea || !material) { Debug.LogError("SHAPE_OF_MEMORY_MESH no Environment/Art/Cloud_Sea or MT_CloudSea"); return; }
            System.Func<string, Mesh> far = name => AssetDatabase.LoadAssetAtPath<Mesh>(CloudSeaMeshes.Folder + "/" + name + "_LOD2_Smooth.asset");

            var pieces = new List<Piece>();
            var random = new System.Random(Seed);
            float Range(float a, float b) => a + (float)random.NextDouble() * (b - a);
            for (int ring = 0; ring < Rings.Length; ring++)
            {
                var (radius, scale, count) = Rings[ring];
                float offset = Range(0, 360f / count);
                for (int k = 0; k < count; k++)
                {
                    var mesh = far(SeaPieces[random.Next(SeaPieces.Length)]);
                    float s = scale * Range(0.85f, 1.15f), az = offset + 360f * k / count + Range(-0.3f, 0.3f) * 360f / count, r = radius + Range(-0.18f, 0.18f) * radius * 0.25f;
                    float top = SeaTop + Range(-1f, 1f);
                    pieces.Add(new Piece { mesh = mesh, scale = s, yaw = Range(0, 360), at = Polar(az, r, top - mesh.bounds.max.y * s) });
                }
            }
            // risers on a golden-angle spiral, out of the glow
            for (int k = 0, made = 0; made < Risers && k < Risers * 4; k++)
            {
                float az = Mathf.Repeat(37f + k * 137.508f, 360f);
                if (Mathf.Abs(Mathf.DeltaAngle(az, GlowYaw)) < GlowClear) continue;
                var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(CloudSeaMeshes.Folder + "/" + RiserPieces[random.Next(RiserPieces.Length)] + "_LOD2_Smooth.asset");
                float r = Mathf.Lerp(RiserNear, RiserFar, Halton(made + 1, 2));
                float topY = Eyes + r * Mathf.Tan(Mathf.Deg2Rad * Mathf.Lerp(RiserLow, RiserHigh, Halton(made + 1, 3)));
                float height = mesh.bounds.size.y, s = Mathf.Max(5f, (topY - SeaTop + 12f) / height);   // its base well under the sea
                pieces.Add(new Piece { mesh = mesh, scale = s, yaw = Range(0, 360), at = Polar(az, r, topY - mesh.bounds.max.y * s) });
                made++;
            }

            // one mesh per sector of the ring, in the environment root's space
            System.IO.Directory.CreateDirectory(Folder);
            var group = sea.Find(GroupName);
            if (!group) { group = new GameObject(GroupName).transform; Undo.RegisterCreatedObjectUndo(group.gameObject, "Far cloud ring"); group.SetParent(sea, false); }
            group.SetPositionAndRotation(root.position, root.rotation); group.localScale = Vector3.one;
            for (int i = group.childCount - 1; i >= 0; i--) Undo.DestroyObjectImmediate(group.GetChild(i).gameObject);
            var like = sea.GetComponentInChildren<MeshRenderer>(true);
            int triangles = 0;
            for (int sector = 0; sector < Sectors; sector++)
            {
                var positions = new List<Vector3>(); var normals = new List<Vector3>(); var colours = new List<Color>(); var bulk = new List<Vector3>(); var indices = new List<int>();
                foreach (var p in pieces)
                {
                    float az = Mathf.Repeat(Mathf.Atan2(p.at.x, p.at.z) * Mathf.Rad2Deg, 360f);
                    if ((int)(az / (360f / Sectors)) % Sectors != sector) continue;
                    var turn = Quaternion.Euler(0, p.yaw, 0);
                    int start = positions.Count;
                    var v = p.mesh.vertices; var n = p.mesh.normals; var c = p.mesh.colors; var b = new List<Vector3>(); p.mesh.GetUVs(2, b);
                    for (int i = 0; i < v.Length; i++)
                    {
                        positions.Add(p.at + turn * (v[i] * p.scale));
                        normals.Add(turn * n[i]);
                        colours.Add(c.Length > i ? c[i] : Color.white);
                        bulk.Add(b.Count > i ? turn * b[i] : turn * n[i]);
                    }
                    foreach (int t in p.mesh.triangles) indices.Add(start + t);
                }
                string path = $"{Folder}/Far ring {sector:00}.asset";
                var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                bool made = !mesh;
                if (made) mesh = new Mesh();
                mesh.Clear();
                mesh.name = $"Far ring {sector:00}";
                mesh.indexFormat = positions.Count > 65535 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
                mesh.SetVertices(positions); mesh.SetNormals(normals); mesh.SetColors(colours); mesh.SetUVs(2, bulk); mesh.SetTriangles(indices, 0);
                mesh.RecalculateBounds();
                if (made) AssetDatabase.CreateAsset(mesh, path); else EditorUtility.SetDirty(mesh);
                triangles += indices.Count / 3;

                var go = new GameObject(mesh.name);
                Undo.RegisterCreatedObjectUndo(go, "Far cloud ring");
                go.transform.SetParent(group, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.lightProbeUsage = like ? like.lightProbeUsage : UnityEngine.Rendering.LightProbeUsage.Off;
                renderer.reflectionProbeUsage = like ? like.reflectionProbeUsage : UnityEngine.Rendering.ReflectionProbeUsage.Off;
            }
            AssetDatabase.SaveAssets();
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
            Debug.Log($"SHAPE_OF_MEMORY_MESH far cloud ring: {pieces.Count} pieces ({pieces.Count - Rings.Sum(r => r.count)} risers), {triangles} triangles in {Sectors} meshes, {Rings[0].radius}-{Rings[Rings.Length - 1].radius} m");
        }

        static Vector3 Polar(float azimuth, float radius, float y)
        {
            float a = azimuth * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(a) * radius, y, Mathf.Cos(a) * radius);
        }

        static float Halton(int i, int b)
        {
            float f = 1, r = 0;
            while (i > 0) { f /= b; r += f * (i % b); i /= b; }
            return r;
        }
    }

    // ---- The book (SM_Book_01)
    //
    // Modelled shut, its hinge on the outer apex of the round spine (the model's Z axis), opened by
    // Shaders/BookOpening/BookOpening.hlsl turning each vertex about that axis by Opening x its colour R. As modelled, the
    // cover's U and the page blocks take R 0 (one half) or 1 (the other) and only the bands' outer faces are graded, so
    // opened flat (MemoryBook at 180):
    //   - the spine folds up into the gutter as a ridge whose crest (the apex, on the axis) stays 4 mm (model units) above
    //     the page faces: through the game's paper it shows as a seam with the bands' tips in it;
    //   - the band ends (and the end caps), whose inner corners are R 0/1 and outer corners graded, shear into flaps at the
    //     head and tail;
    //   - between the page blocks (32 mm short of the hinge each side) the gutter is open, looking at the spine's culled
    //     back faces (the sky shows through).
    // The fix, in model space on the shut book, for the spine (y below SpineTop, within SpineReach of the hinge):
    //   1. nothing inside the cover: points above the cover's outer surface (the bands' inner corners) are put on it;
    //   2. the spine's middle lowered by Sag (a smooth bump HingeReach wide), so its crest stays under the page faces at
    //      every opening (opened flat it lies Sag below the hinge, the page faces PageFace);
    //   3. one smooth R across the hinge for every layer (cover, bands, caps), so they bend together and nothing shears;
    //   4. each page face carried on down to the hinge (a strip of the page's own surface, with its half), so opened flat
    //      the two meet under the paper and the gutter is closed.
    // Shut, the changes are inside the book or a few millimetres on the spine's underside.
    public static class BookMeshFix
    {
        public const string SourcePath = ShapeOfMemoryBuilder.Root + "/Art/Art Assets/SM_Book_01.fbx";
        public const string FixedPath = ShapeOfMemoryBuilder.Root + "/Art/Art Assets/Meshes/SM_Book_01_SpineFixed.asset";
        const float PageFace = 0.004f, PageStart = 0.032f, SpineTop = 0.03f, SpineReach = 0.06f, HingeReach = 0.045f, Sag = 0.008f;
        const float StripBelow = 0.0015f;   // the page strips overlap this far past the hinge, so no crack opens between them
        const float CoverSliceZ = 0.08f;     // a cut through the spine between two bands: the bare cover

        [MenuItem("MetaHackthone/Pico Final/Art/Rebuild the fixed book mesh")]
        static void RebuildMenu() { Rebuild(); Use(); }

        public static Mesh Fixed => AssetDatabase.LoadAssetAtPath<Mesh>(FixedPath);

        // Puts the fixed mesh on the open scenes' book models (MemoryGame.BookModel).
        public static void Use()
        {
            var mesh = Fixed;
            if (!mesh) return;
            foreach (var game in Object.FindObjectsByType<MemoryGame>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (game.BookModel) Use(game.BookModel, mesh);
        }

        public static void Use(Transform model, Mesh mesh = null)
        {
            mesh = mesh ? mesh : Fixed;
            if (!model || !mesh) return;
            foreach (var filter in model.GetComponentsInChildren<MeshFilter>(true))
            {
                if (!filter.sharedMesh || filter.sharedMesh == mesh || !filter.sharedMesh.name.StartsWith("SM_Book_01")) continue;
                Undo.RecordObject(filter, "Use the fixed book mesh");
                filter.sharedMesh = mesh;
                PrefabUtility.RecordPrefabInstancePropertyModifications(filter);
                Debug.Log($"SHAPE_OF_MEMORY_MESH {filter.name} in {filter.gameObject.scene.name} uses {mesh.name}");
            }
        }

        public static Mesh Rebuild()
        {
            Mesh source = null;
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(SourcePath)) if (o is Mesh m) { source = m; break; }
            if (!source) { Debug.LogError("SHAPE_OF_MEMORY_MESH no mesh in " + SourcePath); return null; }
            var positions = new List<Vector3>(source.vertices);
            var normals = new List<Vector3>(source.normals);
            var colours = new List<Color>(source.colors);
            var uv0 = new List<Vector2>(); source.GetUVs(0, uv0);
            var uv1 = new List<Vector2>(); source.GetUVs(1, uv1);
            var tangents = new List<Vector4>(source.tangents);
            var triangles = new List<int>(source.triangles);
            var cover = CoverProfile(source);

            // 1-3: the spine
            int spine = 0;
            for (int i = 0; i < positions.Count; i++)
            {
                var p = positions[i];
                if (p.y >= SpineTop || Mathf.Abs(p.x) >= SpineReach) continue;
                spine++;
                p.y = Mathf.Min(p.y, cover(p.x));
                float u = Mathf.Abs(p.x) / HingeReach;
                if (u < 1) p.y -= Sag * (1 - u * u) * (1 - u * u);
                positions[i] = p;
                var c = colours[i]; c.r = Weight(p.x); colours[i] = c;
            }

            // 4: the page faces down to the hinge
            int strips = 0;
            foreach (int side in new[] { -1, 1 })
            {
                // the page face's vertices along its edge nearest the hinge (it is one quad, that edge a little slanted)
                var edge = new List<int>();
                for (int i = 0; i < positions.Count; i++)
                {
                    var p = positions[i];
                    if (Mathf.Abs(p.x - side * PageFace) < 0.001f && p.y > SpineTop && p.y < PageStart + 0.01f && normals[i].x * -side > 0.9f) edge.Add(i);
                }
                edge.Sort((a, b) => positions[a].z.CompareTo(positions[b].z));
                // one vertex per z along the edge
                var row = new List<int>();
                foreach (int i in edge) if (row.Count == 0 || positions[i].z - positions[row[row.Count - 1]].z > 1e-4f) row.Add(i);
                if (row.Count < 2) { Debug.LogError($"SHAPE_OF_MEMORY_MESH book: no page edge found on side {side}"); continue; }
                var top = new int[row.Count]; var bottom = new int[row.Count];
                for (int k = 0; k < row.Count; k++)
                {
                    var p = positions[row[k]];
                    top[k] = Add(p, row[k], side);
                    bottom[k] = Add(new Vector3(p.x, -StripBelow, p.z), row[k], side);
                }
                for (int k = 0; k + 1 < row.Count; k++) { Quad(top[k], top[k + 1], bottom[k + 1], bottom[k], side); strips++; }
                // (Carrying them on past the page blocks' ends, in the cover's lining, to close the hollow between the
                // turn-ins at the head and tail was tried: the tabs showed as flaps with the book shut. The hollow stays.)
            }

            // A new vertex like source vertex like (its UVs and colour), on side's half, facing the other half.
            int Add(Vector3 at, int like, int side)
            {
                positions.Add(at);
                normals.Add(new Vector3(-side, 0, 0));
                var c = colours[like]; c.r = side < 0 ? 0 : 1; colours.Add(c);
                uv0.Add(uv0[like]);
                if (uv1.Count > 0) uv1.Add(uv1[like]);
                if (tangents.Count > 0) tangents.Add(tangents[like]);
                return positions.Count - 1;
            }
            // Two triangles over a, b, c, d (in order round the quad), facing -side (toward the other half, as the page face does).
            void Quad(int a, int b, int c, int d, int side)
            {
                var n = Vector3.Cross(positions[b] - positions[a], positions[c] - positions[a]);
                if (n.x * -side > 0) triangles.AddRange(new[] { a, b, c, a, c, d });
                else triangles.AddRange(new[] { a, c, b, a, d, c });
            }

            var mesh = Fixed;
            bool made = !mesh;
            if (made) mesh = new Mesh { name = "SM_Book_01_SpineFixed" };
            mesh.Clear();
            mesh.indexFormat = positions.Count > 65535 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.SetVertices(positions);
            mesh.SetNormals(normals);
            if (tangents.Count == positions.Count) mesh.SetTangents(tangents);
            mesh.SetColors(colours);
            mesh.SetUVs(0, uv0);
            if (uv1.Count == positions.Count) mesh.SetUVs(1, uv1);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            if (made) AssetDatabase.CreateAsset(mesh, FixedPath); else EditorUtility.SetDirty(mesh);
            AssetDatabase.SaveAssets();
            Debug.Log($"SHAPE_OF_MEMORY_MESH book: {spine} spine vertices reweighted, {strips} page strip quads; open flat the spine's top is {OpenTop(mesh) * 1000:0.0} mm (model) from the hinge plane, the page faces at {-PageFace * 1000:0.0}");
            return mesh;
        }

        // One weight across the hinge: 0 / 1 outside HingeReach, smooth between.
        static float Weight(float x)
        {
            float t = Mathf.Clamp01((x + HingeReach) / (2 * HingeReach));
            return t * t * (3 - 2 * t);
        }

        // The cover's outer surface (y over x): the lowest of a cut through the bare spine, below the page blocks.
        static System.Func<float, float> CoverProfile(Mesh mesh)
        {
            var v = mesh.vertices; var t = mesh.triangles;
            var segments = new List<(Vector2 a, Vector2 b)>();
            var cut = new List<Vector2>(3);
            for (int k = 0; k < t.Length; k += 3)
            {
                cut.Clear();
                for (int e = 0; e < 3; e++)
                {
                    var a = v[t[k + e]]; var b = v[t[k + (e + 1) % 3]];
                    if ((a.z - CoverSliceZ) * (b.z - CoverSliceZ) >= 0) continue;
                    var q = Vector3.Lerp(a, b, (CoverSliceZ - a.z) / (b.z - a.z));
                    cut.Add(new Vector2(q.x, q.y));
                }
                if (cut.Count == 2 && cut[0].y < PageStart && cut[1].y < PageStart) segments.Add((cut[0], cut[1]));
            }
            return x =>
            {
                float low = float.MaxValue;
                foreach (var (a, b) in segments)
                {
                    float x0 = Mathf.Min(a.x, b.x), x1 = Mathf.Max(a.x, b.x);
                    if (x < x0 || x > x1) continue;
                    low = Mathf.Min(low, x1 - x0 < 1e-6f ? Mathf.Min(a.y, b.y) : Mathf.Lerp(a.y, b.y, (x - a.x) / (b.x - a.x)));
                }
                return low;
            };
        }

        // Opened flat, the highest point of everything but the page faces and strips (model x; the page faces are at -PageFace).
        public static float OpenTop(Mesh mesh)
        {
            var v = mesh.vertices; var c = mesh.colors; float top = float.MinValue;
            for (int i = 0; i < v.Length; i++)
            {
                if (Mathf.Abs(Mathf.Abs(v[i].x) - PageFace) < 0.0006f) continue;
                float a = -Mathf.PI * Mathf.Clamp01(c[i].r);
                float x = Mathf.Cos(a) * v[i].x - Mathf.Sin(a) * v[i].y;
                top = Mathf.Max(top, x);
            }
            return top;
        }
    }
}
