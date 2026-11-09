// Import rules for prop folders laid out as
//   Assets/Scenes/<scene>/PropImport.json   rules for this folder (see the _doc field in any of them)
//   Assets/Scenes/<scene>/Props/*.fbx       models
//   Assets/Scenes/<scene>/Textures/*        base-colour maps
//   Assets/Scenes/<scene>/Materials/*.mat   URP Lit materials, one per prop
//
// Two parts:
// - PropModelPostprocessor runs on every import of a model in Props/. It enforces the import flags and bakes
//   the file's node transforms, the target size and the pivot into the mesh, so the prefab root is always
//   identity. Editing PropImport.json reimports that folder's models.
// - Tools > Scene Props > Sync applies everything that lives in .meta or .mat files: texture import settings,
//   one material per prop with its base map, and the model -> material remap. Run it after adding props or
//   editing PropImport.json. Textures have no postprocessor on purpose: adding one would reimport and
//   recompress every texture in the project (~2,400 including packages).
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace ScenePropImport
{
    [Serializable]
    public class PropRule
    {
        public string model;        // file name in Props/, no extension
        public string texture;      // file name in Textures/, no extension
        public string material;     // file name in Materials/, no extension
        public float size;          // longest side in metres after import; 0 keeps the file's own scale
        public string pivot;        // bottom | center | keep; empty uses defaults.pivot
        public int maxTextureSize;  // 0 uses defaults.maxTextureSize
        public float showcaseYaw;   // Y rotation in the showcase scene only, to turn the prop's front to the camera
    }

    [Serializable]
    public class PropDefaults
    {
        public string pivot = "bottom";
        public int maxTextureSize = 1024;
        public float smoothness = 0.15f;  // used only when Sync creates a material
        public bool readable;             // Read/Write on the meshes
    }

    [Serializable]
    public class PropConfig
    {
        public PropDefaults defaults = new PropDefaults();
        public PropRule[] props = new PropRule[0];
    }

    public static class PropRules
    {
        public const string ScenesRoot = "Assets/Scenes";
        public const string ConfigName = "PropImport.json";

        public static string ConfigPath(string sceneDir) => sceneDir + "/" + ConfigName;

        /// Splits Assets/Scenes/<scene>/<kind>/<file> and checks the folder has a PropImport.json.
        public static bool TryParse(string assetPath, out string sceneDir, out string kind, out string name)
        {
            sceneDir = kind = name = null;
            var path = assetPath.Replace('\\', '/');
            if (!path.StartsWith(ScenesRoot + "/", StringComparison.OrdinalIgnoreCase)) return false;
            var parts = path.Substring(ScenesRoot.Length + 1).Split('/');
            if (parts.Length != 3) return false;
            sceneDir = ScenesRoot + "/" + parts[0];
            kind = parts[1];
            name = Path.GetFileNameWithoutExtension(parts[2]);
            return File.Exists(ConfigPath(sceneDir));
        }

        public static PropConfig Load(string sceneDir)
        {
            var config = JsonUtility.FromJson<PropConfig>(File.ReadAllText(ConfigPath(sceneDir))) ?? new PropConfig();
            if (config.defaults == null) config.defaults = new PropDefaults();
            if (string.IsNullOrEmpty(config.defaults.pivot)) config.defaults.pivot = "bottom";
            if (config.defaults.maxTextureSize <= 0) config.defaults.maxTextureSize = 1024;
            if (config.props == null) config.props = new PropRule[0];
            return config;
        }

        public static PropRule FindByModel(PropConfig config, string name) =>
            config.props.FirstOrDefault(p => string.Equals(p.model, name, StringComparison.OrdinalIgnoreCase));

        public static PropRule FindByTexture(PropConfig config, string name) =>
            config.props.FirstOrDefault(p => string.Equals(p.texture, name, StringComparison.OrdinalIgnoreCase));

        public static string PivotOf(PropConfig config, PropRule rule) =>
            string.IsNullOrEmpty(rule.pivot) ? config.defaults.pivot : rule.pivot;

        public static int TextureSizeOf(PropConfig config, PropRule rule) =>
            rule != null && rule.maxTextureSize > 0 ? rule.maxTextureSize : config.defaults.maxTextureSize;

        public static void ApplyModelSettings(ModelImporter importer, PropConfig config)
        {
            importer.useFileScale = true;
            importer.globalScale = 1f;            // size comes from PropImport.json, baked by the postprocessor
            importer.bakeAxisConversion = false;
            importer.importAnimation = false;     // the files carry empty takes; avoids an Animator per prop
            importer.animationType = ModelImporterAnimationType.None;
            importer.importBlendShapes = false;
            importer.importVisibility = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.addCollider = false;
            importer.isReadable = config.defaults.readable;
            importer.importNormals = ModelImporterNormals.Import;
            importer.importTangents = ModelImporterTangents.None;  // base colour only, no normal maps
            importer.preserveHierarchy = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
        }

        public static void ApplyTextureSettings(TextureImporter importer, int maxSize)
        {
            importer.textureType = TextureImporterType.Default;
            importer.textureShape = TextureImporterShape.Texture2D;
            importer.sRGBTexture = true;
            importer.alphaSource = TextureImporterAlphaSource.None;
            importer.mipmapEnabled = true;
            importer.isReadable = false;
            importer.maxTextureSize = maxSize;
            importer.textureCompression = TextureImporterCompression.Compressed;
            foreach (var platform in new[] { "Android", "Standalone" })
            {
                var settings = importer.GetPlatformTextureSettings(platform);
                if (settings.overridden && settings.maxTextureSize != maxSize)
                {
                    settings.maxTextureSize = maxSize;
                    importer.SetPlatformTextureSettings(settings);
                }
            }
        }
    }

    public class PropModelPostprocessor : AssetPostprocessor
    {
        // Bump when the bake changes so every prop model reimports.
        public override uint GetVersion() => 1;

        void OnPreprocessModel()
        {
            if (!PropRules.TryParse(assetPath, out var sceneDir, out var kind, out _) || kind != "Props") return;
            context.DependsOnSourceAsset(PropRules.ConfigPath(sceneDir));
            PropRules.ApplyModelSettings((ModelImporter)assetImporter, PropRules.Load(sceneDir));
        }

        void OnPostprocessModel(GameObject root)
        {
            if (!PropRules.TryParse(assetPath, out var sceneDir, out var kind, out var name) || kind != "Props") return;
            var config = PropRules.Load(sceneDir);
            var rule = PropRules.FindByModel(config, name);
            if (rule == null)
            {
                context.LogImportWarning($"[PropImport] {name} has no entry in {PropRules.ConfigPath(sceneDir)}; imported as is.");
                return;
            }
            string error;
            var result = PropBaker.Bake(root, rule.size, PropRules.PivotOf(config, rule), out error);
            if (error != null) context.LogImportWarning($"[PropImport] {assetPath}: {error}");
            else Debug.Log($"[PropImport] {assetPath}: {result}");
        }
    }

    public static class PropBaker
    {
        /// Bakes every node transform, the size and the pivot into the meshes and resets all transforms.
        public static string Bake(GameObject root, float size, string pivot, out string error)
        {
            error = null;
            if (root.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length > 0) { error = "skinned mesh, left as is"; return null; }
            var filters = root.GetComponentsInChildren<MeshFilter>(true).Where(f => f.sharedMesh).ToArray();
            if (filters.Length == 0) { error = "no meshes"; return null; }
            if (filters.Select(f => f.sharedMesh).Distinct().Count() != filters.Length) { error = "a mesh is shared by several nodes, left as is"; return null; }

            var matrices = filters.Select(f => f.transform.localToWorldMatrix).ToArray();
            var vertices = new Vector3[filters.Length][];
            var min = Vector3.positiveInfinity;
            var max = Vector3.negativeInfinity;
            for (int i = 0; i < filters.Length; i++)
            {
                var v = filters[i].sharedMesh.vertices;
                for (int j = 0; j < v.Length; j++)
                {
                    v[j] = matrices[i].MultiplyPoint3x4(v[j]);
                    min = Vector3.Min(min, v[j]);
                    max = Vector3.Max(max, v[j]);
                }
                vertices[i] = v;
            }
            var extent = max - min;
            var longest = Mathf.Max(extent.x, Mathf.Max(extent.y, extent.z));
            if (longest < 1e-6f) { error = "empty bounds"; return null; }

            var scale = size > 0f ? size / longest : 1f;
            Vector3 origin;
            switch ((pivot ?? "bottom").ToLowerInvariant())
            {
                case "keep": origin = Vector3.zero; break;
                case "center": origin = (min + max) * 0.5f; break;
                default: origin = new Vector3((min.x + max.x) * 0.5f, min.y, (min.z + max.z) * 0.5f); break;
            }

            for (int i = 0; i < filters.Length; i++)
            {
                var mesh = filters[i].sharedMesh;
                var matrix = matrices[i];
                var v = vertices[i];
                for (int j = 0; j < v.Length; j++) v[j] = (v[j] - origin) * scale;
                mesh.vertices = v;

                var normals = mesh.normals;
                if (normals != null && normals.Length == v.Length)
                {
                    var normalMatrix = matrix.inverse.transpose;
                    for (int j = 0; j < normals.Length; j++) normals[j] = normalMatrix.MultiplyVector(normals[j]).normalized;
                    mesh.normals = normals;
                }
                if (matrix.determinant < 0f)
                {
                    for (int s = 0; s < mesh.subMeshCount; s++)
                    {
                        var triangles = mesh.GetTriangles(s);
                        for (int t = 0; t < triangles.Length; t += 3) { var a = triangles[t + 1]; triangles[t + 1] = triangles[t + 2]; triangles[t + 2] = a; }
                        mesh.SetTriangles(triangles, s, false);
                    }
                }
                if (mesh.tangents != null && mesh.tangents.Length == v.Length) mesh.RecalculateTangents();
                mesh.RecalculateBounds();
            }

            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                t.localPosition = Vector3.zero;
                t.localRotation = Quaternion.identity;
                t.localScale = Vector3.one;
            }
            return $"{Format(extent)} m -> {Format(extent * scale)} m (x{scale:0.####}), pivot {pivot}";
        }

        public static string Format(Vector3 v) => $"{v.x:0.###} x {v.y:0.###} x {v.z:0.###}";
    }

    public static class PropSync
    {
        const string LitShader = "Universal Render Pipeline/Lit";

        [MenuItem("Tools/Scene Props/Sync Materials and Import Settings")]
        static void Menu() => Debug.Log(Run());

        /// Applies texture settings, creates/links materials and remaps every configured folder. Returns a report.
        public static string Run()
        {
            var report = new StringBuilder("[PropImport] Sync\n");
            foreach (var sceneDir in AssetDatabase.GetSubFolders(PropRules.ScenesRoot))
            {
                if (!File.Exists(PropRules.ConfigPath(sceneDir))) continue;
                var config = PropRules.Load(sceneDir);
                report.AppendLine($"== {sceneDir}");
                SyncTextures(sceneDir, config, report);
                SyncModels(sceneDir, config, report);
                Measure(sceneDir, config, report);
            }
            AssetDatabase.SaveAssets();
            return report.ToString();
        }

        static void SyncTextures(string sceneDir, PropConfig config, StringBuilder report)
        {
            foreach (var path in FilesIn(sceneDir + "/Textures"))
            {
                if (!(AssetImporter.GetAtPath(path) is TextureImporter importer)) continue;
                var rule = PropRules.FindByTexture(config, Path.GetFileNameWithoutExtension(path));
                if (rule == null) report.AppendLine($"  ! texture {Path.GetFileName(path)} is not used by any prop");
                PropRules.ApplyTextureSettings(importer, PropRules.TextureSizeOf(config, rule));
                importer.SaveAndReimport();
            }
        }

        static void SyncModels(string sceneDir, PropConfig config, StringBuilder report)
        {
            var known = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var rule in config.props)
            {
                known.Add(rule.model);
                var modelPath = FindFile(sceneDir + "/Props", rule.model);
                if (modelPath == null) { report.AppendLine($"  ! {rule.model}: model not found in Props/"); continue; }
                var importer = AssetImporter.GetAtPath(modelPath) as ModelImporter;
                if (importer == null) { report.AppendLine($"  ! {rule.model}: not a model"); continue; }

                var material = string.IsNullOrEmpty(rule.material) ? null : EnsureMaterial(sceneDir, rule, config, report);
                PropRules.ApplyModelSettings(importer, config);
                if (material)
                {
                    var sourceNames = new HashSet<string>(AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<Material>().Select(m => m.name));
                    foreach (var pair in importer.GetExternalObjectMap())
                        if (pair.Key.type == typeof(Material)) sourceNames.Add(pair.Key.name);
                    foreach (var sourceName in sourceNames)
                        importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), sourceName), material);
                    if (sourceNames.Count == 0) report.AppendLine($"  ! {rule.model}: no material slot found to remap");
                }
                importer.SaveAndReimport();
            }
            foreach (var path in FilesIn(sceneDir + "/Props"))
                if (!known.Contains(Path.GetFileNameWithoutExtension(path)))
                    report.AppendLine($"  ! model {Path.GetFileName(path)} has no entry in {PropRules.ConfigName}");
        }

        static Material EnsureMaterial(string sceneDir, PropRule rule, PropConfig config, StringBuilder report)
        {
            var folder = sceneDir + "/Materials";
            if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder(sceneDir, "Materials");
            var path = folder + "/" + rule.material + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material)
            {
                material = new Material(Shader.Find(LitShader)) { name = rule.material };
                material.SetFloat("_Smoothness", config.defaults.smoothness);
                material.SetFloat("_Metallic", 0f);
                AssetDatabase.CreateAsset(material, path);
                report.AppendLine($"  + created {path}");
            }
            if (string.IsNullOrEmpty(rule.texture)) return material;
            var texturePath = FindFile(sceneDir + "/Textures", rule.texture);
            var texture = texturePath == null ? null : AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            if (!texture) { report.AppendLine($"  ! {rule.model}: texture {rule.texture} not found in Textures/"); return material; }
            if (material.HasProperty("_BaseMap") && material.GetTexture("_BaseMap") != texture) { material.SetTexture("_BaseMap", texture); EditorUtility.SetDirty(material); }
            if (material.HasProperty("_MainTex") && material.GetTexture("_MainTex") != texture) { material.SetTexture("_MainTex", texture); EditorUtility.SetDirty(material); }
            return material;
        }

        static void Measure(string sceneDir, PropConfig config, StringBuilder report)
        {
            foreach (var rule in config.props)
            {
                var modelPath = FindFile(sceneDir + "/Props", rule.model);
                var prefab = modelPath == null ? null : AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
                if (!prefab) continue;
                var filters = prefab.GetComponentsInChildren<MeshFilter>(true).Where(f => f.sharedMesh).ToArray();
                if (filters.Length == 0) continue;
                var bounds = filters[0].sharedMesh.bounds;
                foreach (var f in filters.Skip(1)) bounds.Encapsulate(f.sharedMesh.bounds);
                var tris = filters.Sum(f => Enumerable.Range(0, f.sharedMesh.subMeshCount).Sum(s => (long)f.sharedMesh.GetIndexCount(s)) / 3);
                var renderer = prefab.GetComponentInChildren<MeshRenderer>(true);
                var mat = renderer ? renderer.sharedMaterial : null;
                var tex = mat && mat.HasProperty("_BaseMap") ? mat.GetTexture("_BaseMap") : null;
                var rootOk = prefab.transform.localRotation == Quaternion.identity && prefab.transform.localScale == Vector3.one;
                report.AppendLine($"  {rule.model,-52} {PropBaker.Format(bounds.size)} m  min.y {bounds.min.y:0.###}  {tris,6} tris  " +
                                  $"mat {(mat ? mat.name : "-")}  tex {(tex ? tex.name : "-")}{(rootOk ? "" : "  ! root transform not identity")}");
            }
        }

        internal static IEnumerable<string> FilesIn(string folder)
        {
            if (!AssetDatabase.IsValidFolder(folder)) yield break;
            foreach (var guid in AssetDatabase.FindAssets("", new[] { folder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetDatabase.IsValidFolder(path)) continue;
                if (Path.GetDirectoryName(path).Replace('\\', '/') == folder) yield return path;
            }
        }

        internal static string FindFile(string folder, string name) =>
            FilesIn(folder).FirstOrDefault(p => string.Equals(Path.GetFileNameWithoutExtension(p), name, StringComparison.OrdinalIgnoreCase));
    }

    /// Builds <scene>/<Name>Showcase.unity: every prop of the folder laid out in rows on a plain floor, with a
    /// key light and a framing camera. Props over 0.8 m wide or 1 m tall go in the back row. Re-run after adding
    /// props or changing sizes; it overwrites the showcase scene (its GUID is kept).
    public static class PropShowcase
    {
        const float Gap = 0.15f;
        const string MenuPath = "Tools/Scene Props/Build Showcase Scene for Selected Folder";

        class Item
        {
            public string name;
            public GameObject prefab;
            public float yaw;
            public Bounds bounds;   // after yaw, in prop space
            public Vector3 position;
        }

        [MenuItem(MenuPath)]
        static void Menu() => Debug.Log("[PropImport] " + Build(SelectedSceneDir()));

        [MenuItem(MenuPath, true)]
        static bool MenuValidate() => SelectedSceneDir() != null;

        static string SelectedSceneDir()
        {
            var path = Selection.activeObject ? AssetDatabase.GetAssetPath(Selection.activeObject).Replace('\\', '/') : "";
            if (!path.StartsWith(PropRules.ScenesRoot + "/", StringComparison.OrdinalIgnoreCase)) return null;
            var dir = PropRules.ScenesRoot + "/" + path.Substring(PropRules.ScenesRoot.Length + 1).Split('/')[0];
            return File.Exists(PropRules.ConfigPath(dir)) ? dir : null;
        }

        public static string ScenePathFor(string sceneDir)
        {
            var folder = Path.GetFileName(sceneDir);
            var stem = folder.EndsWith("Scene", StringComparison.Ordinal) ? folder.Substring(0, folder.Length - 5) : folder;
            return $"{sceneDir}/{char.ToUpperInvariant(stem[0])}{stem.Substring(1)}Showcase.unity";
        }

        public static string Build(string sceneDir)
        {
            if (sceneDir == null) return "no prop folder selected";
            var scenePath = ScenePathFor(sceneDir);
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
                if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).path == scenePath)
                    return $"{scenePath} is open; close it and run again";

            var items = Collect(sceneDir);
            if (items.Count == 0) return $"{sceneDir} has no props";
            Layout(items);

            var previous = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            var scene = UnityEditor.SceneManagement.EditorSceneManager.NewScene(
                UnityEditor.SceneManagement.NewSceneSetup.EmptyScene, UnityEditor.SceneManagement.NewSceneMode.Additive);
            try
            {
                UnityEngine.SceneManagement.SceneManager.SetActiveScene(scene);
                var props = new GameObject("Props");
                var layout = new Bounds(Vector3.zero, Vector3.zero);
                foreach (var item in items)
                {
                    var go = (GameObject)PrefabUtility.InstantiatePrefab(item.prefab, scene);
                    go.name = item.name;
                    go.transform.SetParent(props.transform, false);
                    go.transform.SetPositionAndRotation(item.position, Quaternion.Euler(0f, item.yaw, 0f));
                    layout.Encapsulate(new Bounds(item.position + item.bounds.center, item.bounds.size));
                }

                var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
                floor.name = "Floor";
                floor.transform.position = new Vector3(layout.center.x, 0f, layout.center.z);
                floor.transform.localScale = new Vector3((layout.size.x + 2f) / 10f, 1f, (layout.size.z + 2f) / 10f);
                floor.GetComponent<MeshRenderer>().sharedMaterial = FloorMaterial(sceneDir);

                var light = new GameObject("Key Light").AddComponent<Light>();
                light.type = LightType.Directional;
                light.shadows = LightShadows.Soft;
                light.intensity = 1f;
                light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

                var camera = new GameObject("Showcase Camera") { tag = "MainCamera" }.AddComponent<Camera>();
                camera.gameObject.AddComponent<AudioListener>();
                camera.nearClipPlane = 0.05f;
                camera.fieldOfView = 40f;
                Frame(camera, layout, 16f / 9f);

                UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene, scenePath);
            }
            finally
            {
                if (previous.IsValid()) UnityEngine.SceneManagement.SceneManager.SetActiveScene(previous);
                UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene, true);
            }
            return $"built {scenePath} with {items.Count} props";
        }

        static List<Item> Collect(string sceneDir)
        {
            var config = PropRules.Load(sceneDir);
            var items = new List<Item>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var path in config.props.Select(r => PropSync.FindFile(sceneDir + "/Props", r.model)).Where(p => p != null)
                         .Concat(PropSync.FilesIn(sceneDir + "/Props")))
            {
                if (!seen.Add(path)) continue;
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var filters = prefab ? prefab.GetComponentsInChildren<MeshFilter>(true).Where(f => f.sharedMesh).ToArray() : null;
                if (filters == null || filters.Length == 0) continue;
                var name = Path.GetFileNameWithoutExtension(path);
                var rule = PropRules.FindByModel(config, name);
                var yaw = rule != null ? rule.showcaseYaw : 0f;
                var rotation = Quaternion.Euler(0f, yaw, 0f);
                var bounds = new Bounds(rotation * filters[0].sharedMesh.bounds.center, Vector3.zero);
                foreach (var f in filters)
                {
                    var b = f.sharedMesh.bounds;
                    for (int c = 0; c < 8; c++)
                        bounds.Encapsulate(rotation * (b.center + Vector3.Scale(b.extents, new Vector3((c & 1) == 0 ? -1 : 1, (c & 2) == 0 ? -1 : 1, (c & 4) == 0 ? -1 : 1))));
                }
                items.Add(new Item { name = name, prefab = prefab, yaw = yaw, bounds = bounds });
            }
            return items;
        }

        /// Big props in a back row, the rest tallest-first in rows toward the camera (-Z), each row centred on X.
        static void Layout(List<Item> items)
        {
            var big = items.Where(i => Mathf.Max(i.bounds.size.x, i.bounds.size.z) > 0.8f || i.bounds.size.y > 1f).ToList();
            var small = items.Where(i => !big.Contains(i)).OrderByDescending(i => i.bounds.size.y).ToList();
            var rowWidth = Mathf.Max(1.6f, big.Sum(i => i.bounds.size.x) + Gap * Mathf.Max(0, big.Count - 1));
            var rows = new List<List<Item>>();
            if (big.Count > 0) rows.Add(big);
            var row = new List<Item>();
            var width = 0f;
            foreach (var item in small)
            {
                if (row.Count > 0 && width + Gap + item.bounds.size.x > rowWidth) { rows.Add(row); row = new List<Item>(); width = 0f; }
                width += (row.Count > 0 ? Gap : 0f) + item.bounds.size.x;
                row.Add(item);
            }
            if (row.Count > 0) rows.Add(row);

            var back = 0f;
            foreach (var r in rows)
            {
                var depth = r.Max(i => i.bounds.size.z);
                var z = back - depth * 0.5f;
                var x = -(r.Sum(i => i.bounds.size.x) + Gap * (r.Count - 1)) * 0.5f;
                foreach (var item in r)
                {
                    var target = new Vector3(x + item.bounds.size.x * 0.5f, 0f, z);
                    item.position = target - new Vector3(item.bounds.center.x, item.bounds.min.y, item.bounds.center.z);
                    x += item.bounds.size.x + Gap;
                }
                back -= depth + Gap * 2f;
            }
        }

        /// Pulls the camera back along its view direction until every corner of the layout is inside the frustum.
        static void Frame(Camera camera, Bounds layout, float aspect)
        {
            const float pitch = 35f, margin = 1.08f;
            var rotation = Quaternion.Euler(pitch, 0f, 0f);
            var forward = rotation * Vector3.forward;
            var right = rotation * Vector3.right;
            var up = rotation * Vector3.up;
            var tanV = Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            var tanH = tanV * aspect;
            var target = layout.center;
            var distance = 0f;
            for (int c = 0; c < 8; c++)
            {
                var corner = layout.center + Vector3.Scale(layout.extents, new Vector3((c & 1) == 0 ? -1 : 1, (c & 2) == 0 ? -1 : 1, (c & 4) == 0 ? -1 : 1));
                var p = corner - target;
                var depth = Vector3.Dot(p, forward);
                distance = Mathf.Max(distance, Mathf.Abs(Vector3.Dot(p, right)) * margin / tanH - depth);
                distance = Mathf.Max(distance, Mathf.Abs(Vector3.Dot(p, up)) * margin / tanV - depth);
            }
            camera.transform.SetPositionAndRotation(target - forward * distance, rotation);
        }

        static Material FloorMaterial(string sceneDir)
        {
            var folder = sceneDir + "/Materials";
            if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder(sceneDir, "Materials");
            var path = folder + "/MT_ShowcaseFloor.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material) return material;
            material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "MT_ShowcaseFloor" };
            material.SetColor("_BaseColor", new Color(0.46f, 0.47f, 0.5f));
            material.SetFloat("_Smoothness", 0.1f);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }
    }
}
