using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Orbiters.Logger.Editor
{
    /// <summary>
    /// The Logger's logo: an "L" whose foot is the first of four log lines. Drawn from its vector paths, filled in one
    /// colour, so it takes the tint of wherever it sits (the green brand tile, a source chip) and stays crisp at any size.
    /// The window tab, which only takes a texture, gets it rasterized in code at the screen's pixel density and in the
    /// grey of Unity's own tab icons for the current skin: no image file, import settings or GUID to lose.
    /// </summary>
    internal static class LoggerLogo
    {
        /// <summary>The logo's box (its SVG viewBox).</summary>
        public static readonly Vector2 Size = new Vector2(328f, 407f);

        // Each path: its start point, then cubic segments (x1, y1, x2, y2, x, y); lines are cubics with their controls on
        // their ends. From the logo's SVG, without its hairline outline (invisible below 400 px, muddy on a tint).
        private static readonly float[][] Paths =
        {
            new[]
            {
                129.795f, 311.5f,
                129.795f, 311.5f, 291.63f, 311.5f, 291.63f, 311.5f,
                311.328f, 311.5f, 327.296f, 327.468f, 327.296f, 347.166f,
                327.296f, 363.293f, 316.517f, 377.393f, 300.853f, 381.23f,
                263.086f, 390.483f, 194.047f, 406f, 155.795f, 406f,
                101.295f, 406f, 65.487f, 405f, 29.295f, 367f,
                -6.896f, 329f, 1.295f, 278.5f, 1.295f, 278.5f,
                1.295f, 278.5f, 19.564f, 30.758f, 19.564f, 30.758f,
                20.822f, 13.699f, 35.03f, 0.5f, 52.135f, 0.5f,
                70.173f, 0.5f, 84.795f, 15.122f, 84.795f, 33.16f,
                84.795f, 33.16f, 84.795f, 266.5f, 84.795f, 266.5f,
                84.795f, 291.353f, 104.942f, 311.5f, 129.795f, 311.5f
            },
            new[]
            {
                193.988f, 278.5f,
                193.988f, 278.5f, 151.69f, 278.5f, 151.69f, 278.5f,
                134.904f, 278.5f, 121.297f, 264.892f, 121.297f, 248.107f,
                121.297f, 231.832f, 134.116f, 218.445f, 150.376f, 217.742f,
                150.376f, 217.742f, 192.635f, 215.913f, 192.635f, 215.913f,
                210.44f, 215.143f, 225.296f, 229.37f, 225.296f, 247.192f,
                225.296f, 264.483f, 211.279f, 278.5f, 193.988f, 278.5f
            },
            new[]
            {
                256.438f, 179.751f,
                256.438f, 179.751f, 157.481f, 184.327f, 157.481f, 184.327f,
                137.773f, 185.238f, 121.297f, 169.506f, 121.297f, 149.777f,
                121.297f, 132.66f, 133.816f, 118.118f, 150.743f, 115.575f,
                150.743f, 115.575f, 248.705f, 100.852f, 248.705f, 100.852f,
                272.706f, 97.245f, 294.295f, 115.832f, 294.295f, 140.102f,
                294.295f, 161.31f, 277.623f, 178.771f, 256.438f, 179.751f
            },
            new[]
            {
                227.197f, 73.866f,
                227.197f, 73.866f, 157.628f, 83.48f, 157.628f, 83.48f,
                138.422f, 86.134f, 121.297f, 71.212f, 121.297f, 51.823f,
                121.297f, 37.452f, 130.889f, 24.849f, 144.741f, 21.021f,
                144.741f, 21.021f, 212.434f, 2.313f, 212.434f, 2.313f,
                235.751f, -4.131f, 258.796f, 13.409f, 258.796f, 37.601f,
                258.796f, 55.884f, 245.308f, 71.364f, 227.197f, 73.866f
            }
        };

        // Unity's tab icon greys: light on the dark skin, dark on the light one.
        private static readonly Color DarkSkinTab = new Color(0.78f, 0.78f, 0.78f);
        private static readonly Color LightSkinTab = new Color(0.27f, 0.27f, 0.27f);
        private const float TabPoints = 16f;

        private static Texture2D tabIcon;
        private static bool tabIconProSkin;
        private static int tabIconPixels;

        // The texture is the domain's own: it goes before a script reload, which would otherwise leak it.
        static LoggerLogo() => AssemblyReloadEvents.beforeAssemblyReload += ReleaseTabIcon;

        private static void ReleaseTabIcon()
        {
            if (tabIcon != null)
            {
                UnityEngine.Object.DestroyImmediate(tabIcon);
            }

            tabIcon = null;
        }

        /// <summary>Fills the logo in <paramref name="color"/>, as large as fits <paramref name="box"/>, centred.</summary>
        public static void Fill(Painter2D painter, Rect box, Color color)
        {
            float scale = Mathf.Min(box.width / Size.x, box.height / Size.y);
            if (scale <= 0f)
            {
                return;
            }

            var origin = box.center - Size * (scale * 0.5f);
            float lineScale = LineScale(Size.y * scale);
            painter.fillColor = color;
            for (int index = 0; index < Paths.Length; index++)
            {
                var path = Paths[index];
                float middle = Middle(path), squash = index == 0 ? 1f : lineScale;
                Vector2 P(int i) => origin + new Vector2(path[i], middle + (path[i + 1] - middle) * squash) * scale;
                painter.BeginPath();
                painter.MoveTo(P(0));
                for (int i = 2; i + 5 < path.Length; i += 6)
                {
                    painter.BezierCurveTo(P(i), P(i + 2), P(i + 4));
                }

                painter.ClosePath();
                painter.Fill();
            }
        }

        // Optical size: below 40 px tall the log lines get up to a quarter thinner, so the gaps between them (under
        // half a pixel at 16 px) stay open. The "L" keeps its shape.
        private static float LineScale(float height) => 1f - 0.24f * Mathf.Clamp01((40f - height) / 24f);

        // Halfway between a path's highest and lowest points (its control points: close enough for these shapes).
        private static float Middle(float[] path)
        {
            float top = float.MaxValue, bottom = float.MinValue;
            for (int i = 1; i < path.Length; i += 2)
            {
                top = Mathf.Min(top, path[i]);
                bottom = Mathf.Max(bottom, path[i]);
            }

            return (top + bottom) * 0.5f;
        }

        /// <summary>
        /// The window tab's icon: 16 points at the current pixel density, in the skin's tab icon grey. Cached; a new one
        /// comes back after a skin or display scale change.
        /// </summary>
        public static Texture2D TabIcon()
        {
            bool proSkin = EditorGUIUtility.isProSkin;
            int pixels = Mathf.Max(16, Mathf.RoundToInt(TabPoints * EditorGUIUtility.pixelsPerPoint));
            if (tabIcon != null && tabIconProSkin == proSkin && tabIconPixels == pixels)
            {
                return tabIcon;
            }

            ReleaseTabIcon();
            // One point of margin, like Unity's own 16 px icons.
            tabIcon = Rasterize(pixels, pixels / TabPoints, proSkin ? DarkSkinTab : LightSkinTab);
            tabIcon.name = "Logger tab icon";
            tabIconProSkin = proSkin;
            tabIconPixels = pixels;
            return tabIcon;
        }

        /// <summary>
        /// The logo in a square texture of <paramref name="pixels"/>, <paramref name="margin"/> pixels from its top and
        /// bottom: exact horizontal coverage over 16 sub-rows per pixel, non-zero winding, at the same optical size as
        /// <see cref="Fill"/>.
        /// </summary>
        internal static Texture2D Rasterize(int pixels, float margin, Color color)
        {
            float scale = (pixels - 2f * margin) / Size.y;
            var origin = new Vector2((pixels - Size.x * scale) * 0.5f, margin);
            var edges = Flatten(origin, scale, LineScale(Size.y * scale));

            const int subRows = 16;
            var coverage = new float[pixels * pixels];
            var crossings = new List<(float x, int winding)>();
            for (int row = 0; row < pixels * subRows; row++)
            {
                float y = (row + 0.5f) / subRows;
                crossings.Clear();
                foreach (var (a, b) in edges)
                {
                    if ((a.y <= y) == (b.y <= y))
                    {
                        continue;
                    }

                    float t = (y - a.y) / (b.y - a.y);
                    crossings.Add((a.x + (b.x - a.x) * t, b.y > a.y ? 1 : -1));
                }

                crossings.Sort((l, r) => l.x.CompareTo(r.x));
                int winding = 0;
                int line = row / subRows * pixels;
                for (int i = 0; i < crossings.Count - 1; i++)
                {
                    winding += crossings[i].winding;
                    if (winding != 0)
                    {
                        AddSpan(coverage, line, pixels, crossings[i].x, crossings[i + 1].x, 1f / subRows);
                    }
                }
            }

            var texture = new Texture2D(pixels, pixels, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            var colors = new Color32[pixels * pixels];
            for (int y = 0; y < pixels; y++)
            {
                for (int x = 0; x < pixels; x++)
                {
                    // Textures count rows from the bottom.
                    float alpha = Mathf.Clamp01(coverage[y * pixels + x]) * color.a;
                    colors[(pixels - 1 - y) * pixels + x] = new Color(color.r, color.g, color.b, alpha);
                }
            }

            texture.SetPixels32(colors);
            texture.Apply(false, false);
            return texture;
        }

        // Adds the part of [from, to) that falls in each pixel of a row.
        private static void AddSpan(float[] coverage, int line, int pixels, float from, float to, float weight)
        {
            from = Mathf.Clamp(from, 0f, pixels);
            to = Mathf.Clamp(to, 0f, pixels);
            for (int x = (int)from; x < pixels && x < to; x++)
            {
                float covered = Mathf.Min(to, x + 1f) - Mathf.Max(from, x);
                if (covered > 0f)
                {
                    coverage[line + x] += covered * weight;
                }
            }
        }

        // Every path as straight edges in pixel space, each curve cut into 16 lines; the log lines (every path but the
        // "L") scaled by lineScale in height around their middle.
        private static List<(Vector2 a, Vector2 b)> Flatten(Vector2 origin, float scale, float lineScale)
        {
            const int steps = 16;
            var edges = new List<(Vector2, Vector2)>();
            for (int index = 0; index < Paths.Length; index++)
            {
                var path = Paths[index];
                float middle = Middle(path), squash = index == 0 ? 1f : lineScale;
                Vector2 P(int i) => origin + new Vector2(path[i], middle + (path[i + 1] - middle) * squash) * scale;
                var current = P(0);
                for (int i = 2; i + 5 < path.Length; i += 6)
                {
                    Vector2 c1 = P(i), c2 = P(i + 2), end = P(i + 4);
                    for (int s = 1; s <= steps; s++)
                    {
                        float t = s / (float)steps, u = 1f - t;
                        var next = u * u * u * current + 3f * u * u * t * c1 + 3f * u * t * t * c2 + t * t * t * end;
                        edges.Add((s == 1 ? current : edges[edges.Count - 1].Item2, next));
                    }

                    current = end;
                }
            }

            return edges;
        }
    }
}
