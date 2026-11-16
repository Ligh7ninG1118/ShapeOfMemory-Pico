using System.IO;
using UnityEditor;
using UnityEngine;

namespace ShapeOfMemory.ArtTools
{
    // Builds the sky from four painted panels (Art/Sky/Source, made in Flow): each panel is a view toward the horizon
    // covering a quarter of the way round. They are projected back onto the sphere as the pictures they are
    // (rectilinear, horizon on the given row), crossfaded where neighbours overlap, and the sky above and below the
    // band is filled with a gradient from each column's painted edge to a shared zenith / nadir colour, with sparse soft
    // stars overhead. Output: a 2:1 latitude-longitude PNG imported as a cubemap (Art/Sky/FlowSky.png).
    //
    // The glow panel's centre sits at GlowAzimuth in the sky's own frame. The game turns the dome by
    // MemoryLook.GlowFacing - GlowAzimuth (-20 - 30 = -50), so the glow shows at GlowAzimuth - 50 from the player's forward: GlowAzimuth 30
    // puts it 20 degrees to the front-left, where the environment's key light (Sun) comes from. Replace a panel with a
    // file of the same name and run the menu again.
    public static class SkyPanelBuilder
    {
        const string Dir = "Assets/ShadowWorkshop/PicoFinal/Art/Sky/";
        const string Output = Dir + "FlowSky.png";
        // Round the sky to the right (as the player turns right), starting with the glow.
        static readonly string[] Panels =
        {
            "Source/SkyPanel_1_Glow.jpg", "Source/SkyPanel_2_RightOfGlow.jpg", "Source/SkyPanel_3_Back.jpg", "Source/SkyPanel_4_LeftOfGlow.jpg",
        };
        // Where each panel's horizon is, as a fraction of its height from the top.
        static readonly float[] HorizonRow = { 0.53f, 0.52f, 0.505f, 0.50f };
        public const float GlowAzimuth = 30;   // degrees from +Z toward +X, in the sky's own frame (20 left of forward in game)
        const float Quarter = 90;              // azimuth each panel owns
        const float Overlap = 10;              // degrees crossfaded on each side of a seam
        const float KeepAbove = 10f;           // degrees of painting kept above the horizon, then faded over FadeSpan
        const float KeepBelow = 18f;           // and below it
        const float FadeSpan = 8f;
        const float ZenithDepth = 0.6f;        // the zenith: the panels' top rows averaged, times this (deep navy overhead)
        const float TintBlur = 30f;            // degrees of azimuth the sky's tint (warm toward the glow) is averaged over
        const float TintUpTo = 45f;            // degrees from the horizon where that tint has faded out
        const int W = 4096, H = 2048;

        struct Panel { public Color[] px; public int w, h; public float horizon, tanHalfX, tanHalfY; }

        [MenuItem("Tools/Shape of Memory/Build Sky From Flow Panels")]
        public static void Build()
        {
            var panels = new Panel[Panels.Length];
            for (int i = 0; i < Panels.Length; i++)
            {
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                tex.LoadImage(File.ReadAllBytes(Dir + Panels[i]));
                float half = (Quarter * 0.5f + Overlap) * Mathf.Deg2Rad;
                panels[i] = new Panel
                {
                    px = tex.GetPixels(), w = tex.width, h = tex.height, horizon = HorizonRow[i],
                    tanHalfX = Mathf.Tan(half), tanHalfY = Mathf.Tan(half) * tex.height / tex.width,
                };
                Object.DestroyImmediate(tex);
            }

            // Shared zenith and nadir: the panels' top and bottom rows, averaged and deepened a little.
            Color zenith = Color.black, nadir = Color.black;
            foreach (var p in panels)
            {
                for (int x = 0; x < p.w; x += 4) { zenith += p.px[(p.h - 3) * p.w + x]; nadir += p.px[2 * p.w + x]; }
            }
            int n = panels.Length * ((panels[0].w + 3) / 4);
            zenith = (zenith / n) * ZenithDepth; nadir = (nadir / n) * 0.7f; zenith.a = nadir.a = 1;

            // The painting is kept only round the horizon (KeepAbove / KeepBelow degrees, fading out over FadeSpan). Beyond
            // it the panels' own skies disagree, so the sky is one gradient for the whole way round, from the panels'
            // average colour where the painting ends to the zenith (or nadir), tinted gently by what lies below it (the
            // warm side stays warm), the tint averaged over TintBlur degrees and fading out with height.
            float startUp = KeepAbove + FadeSpan, startDown = -(KeepBelow + FadeSpan);
            var tintTop = new Color[W];
            var tintBottom = new Color[W];
            Color meanTop = Color.black, meanBottom = Color.black;
            for (int col = 0; col < W; col++)
            {
                float az = (col + 0.5f) / W * 360f - 180f;
                tintTop[col] = PanelColour(panels, az, startUp); meanTop += tintTop[col];
                tintBottom[col] = PanelColour(panels, az, startDown); meanBottom += tintBottom[col];
            }
            meanTop /= W; meanBottom /= W;
            tintTop = BlurRound(tintTop, Mathf.RoundToInt(W * TintBlur / 360f));
            tintBottom = BlurRound(tintBottom, Mathf.RoundToInt(W * TintBlur / 360f));

            var outPx = new Color32[W * H];
            for (int col = 0; col < W; col++)
            {
                float az = (col + 0.5f) / W * 360f - 180f;      // checked against Unity's lat-long import
                for (int row = 0; row < H; row++)
                {
                    float el = 90f - (row + 0.5f) / H * 180f;
                    Color fill;
                    if (el >= 0)
                    {
                        float t = Mathf.InverseLerp(startUp, 90f, el); t = 1 - (1 - t) * (1 - t);
                        float tint = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(startUp, TintUpTo, el));
                        fill = Color.Lerp(meanTop, zenith, t) + (tintTop[col] - meanTop) * tint;
                        fill += Star(az, el) * Mathf.Clamp01((el - 25f) / 15f);
                    }
                    else
                    {
                        float t = Mathf.InverseLerp(startDown, -75f, el); t = t * t * (3 - 2 * t);
                        float tint = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(startDown, -TintUpTo, el));
                        fill = Color.Lerp(meanBottom, nadir, t) + (tintBottom[col] - meanBottom) * tint;
                    }
                    float keep = el >= 0 ? Mathf.InverseLerp(KeepAbove + FadeSpan, KeepAbove, el) : Mathf.InverseLerp(-KeepBelow - FadeSpan, -KeepBelow, el);
                    keep = keep * keep * (3 - 2 * keep);
                    Color final = fill;
                    if (keep > 0) final = Color.Lerp(fill, PanelColour(panels, az, el), keep);
                    final.a = 1;
                    outPx[(H - 1 - row) * W + col] = final;
                }
            }

            var outTex = new Texture2D(W, H, TextureFormat.RGBA32, false);
            outTex.SetPixels32(outPx);
            File.WriteAllBytes(Output, outTex.EncodeToPNG());
            Object.DestroyImmediate(outTex);
            AssetDatabase.ImportAsset(Output);
            var imp = (TextureImporter)AssetImporter.GetAtPath(Output);
            imp.textureShape = TextureImporterShape.TextureCube;
            var s = new TextureImporterSettings();
            imp.ReadTextureSettings(s);
            s.cubemapConvolution = TextureImporterCubemapConvolution.None;
            s.generateCubemap = TextureImporterGenerateCubemap.Cylindrical;
            s.seamlessCubemap = true;
            imp.SetTextureSettings(s);
            imp.sRGBTexture = true;
            imp.mipmapEnabled = true;
            imp.maxTextureSize = 4096;
            imp.textureCompression = TextureImporterCompression.CompressedHQ;
            imp.SaveAndReimport();
            Debug.Log("[SkyPanelBuilder] wrote " + Output);
        }

        // The panels' colour in a direction, crossfaded where neighbours overlap.
        static Color PanelColour(Panel[] panels, float az, float el)
        {
            Color acc = Color.black; float wsum = 0;
            float ce = Mathf.Cos(el * Mathf.Deg2Rad), y = Mathf.Sin(el * Mathf.Deg2Rad);
            for (int i = 0; i < panels.Length; i++)
            {
                float rel = Mathf.DeltaAngle(GlowAzimuth + i * Quarter, az);
                float wh = Mathf.Clamp01((Quarter * 0.5f + Overlap - Mathf.Abs(rel)) / (2f * Overlap));
                if (wh <= 0) continue;
                wh = wh * wh * (3 - 2 * wh);
                float x = ce * Mathf.Sin(rel * Mathf.Deg2Rad), z = ce * Mathf.Cos(rel * Mathf.Deg2Rad);
                if (z <= 0.05f) continue;
                var p = panels[i];
                float u = Mathf.Clamp01(0.5f + 0.5f * (x / z) / p.tanHalfX);
                float v = Mathf.Clamp01(p.horizon - 0.5f * (y / z) / p.tanHalfY);
                acc += Sample(p, u, 1 - v) * wh; wsum += wh;
            }
            return wsum > 0 ? acc / wsum : Color.black;
        }

        static Color[] BlurRound(Color[] src, int radius)
        {
            int n = src.Length; var dst = new Color[n]; Color sum = Color.black;
            for (int k = -radius; k <= radius; k++) sum += src[(k % n + n) % n];
            for (int i = 0; i < n; i++)
            {
                dst[i] = sum / (2 * radius + 1);
                sum += src[(i + radius + 1) % n] - src[((i - radius) % n + n) % n];
            }
            return dst;
        }

        static Color Sample(Panel p, float u, float vFromBottom)
        {
            float fx = Mathf.Clamp(u * p.w - 0.5f, 0, p.w - 1.001f), fy = Mathf.Clamp(vFromBottom * p.h - 0.5f, 0, p.h - 1.001f);
            int x0 = (int)fx, y0 = (int)fy; float tx = fx - x0, ty = fy - y0;
            Color a = p.px[y0 * p.w + x0], b = p.px[y0 * p.w + x0 + 1], c = p.px[(y0 + 1) * p.w + x0], d = p.px[(y0 + 1) * p.w + x0 + 1];
            return Color.Lerp(Color.Lerp(a, b, tx), Color.Lerp(c, d, tx), ty);
        }

        // Sparse, soft stars on a grid of directions, so they keep their size near the zenith.
        static Color Star(float az, float el)
        {
            float ce = Mathf.Cos(el * Mathf.Deg2Rad);
            var d = new Vector3(ce * Mathf.Sin(az * Mathf.Deg2Rad), Mathf.Sin(el * Mathf.Deg2Rad), ce * Mathf.Cos(az * Mathf.Deg2Rad));
            const float cells = 110f;
            var p = d * cells; var cell = new Vector3(Mathf.Floor(p.x), Mathf.Floor(p.y), Mathf.Floor(p.z));
            float h1 = Hash(cell), h2 = Hash(cell + new Vector3(17, 3, 5)), h3 = Hash(cell + new Vector3(5, 29, 11)), h4 = Hash(cell + new Vector3(41, 7, 23));
            if (h4 > 0.06f) return Color.clear;
            var sp = (cell + new Vector3(0.2f + 0.6f * h1, 0.2f + 0.6f * h2, 0.2f + 0.6f * h3)).normalized;
            float ang = Mathf.Acos(Mathf.Clamp(Vector3.Dot(d, sp), -1, 1)) * Mathf.Rad2Deg;
            float s = Mathf.Clamp01(1f - ang / 0.16f);
            return new Color(0.9f, 0.88f, 1f) * (s * s * (0.35f + 0.5f * h1));
        }

        static float Hash(Vector3 c)
        {
            float h = Mathf.Sin(Vector3.Dot(c, new Vector3(127.1f, 311.7f, 74.7f))) * 43758.5453f;
            return h - Mathf.Floor(h);
        }
    }
}
