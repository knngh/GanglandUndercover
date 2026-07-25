using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace GanglandUndercover.Editor
{
    public static class VfxMotionPreviewReport
    {
        public const string DefaultMarkdownPath = "output/vfx_motion_preview.md";
        public const string DefaultContactSheetPath = "output/vfx_contact_sheet.png";
        public const int ContactSheetCellSize = 144;
        public const int ContactSheetGap = 8;
        public const int ContactSheetMargin = 12;

        private static readonly VfxSpec[] ExpectedEffects =
        {
            new VfxSpec("blackout", 12, 96, 96, 6f),
            new VfxSpec("comms_jam", 8, 64, 64, 14f),
            new VfxSpec("door_lock", 6, 48, 48, 10f),
            new VfxSpec("emergency_light", 8, 48, 48, 12f),
            new VfxSpec("evidence_leak", 12, 48, 48, 9f),
            new VfxSpec("hit", 4, 32, 32, 18f),
            new VfxSpec("kill", 10, 128, 128, 15f),
            new VfxSpec("patrol_alert", 4, 64, 64, 6f),
        };

        [MenuItem("Gangland/Art/Write VFX Motion Preview")]
        public static void WriteDefaultPreview()
        {
            WritePreview(DefaultMarkdownPath, DefaultContactSheetPath);
        }

        public static Summary BuildSummary()
        {
            Summary summary = new Summary();
            for (int i = 0; i < ExpectedEffects.Length; i++)
            {
                summary.Effects.Add(BuildEffectPreview(ExpectedEffects[i], summary));
            }

            return summary;
        }

        public static void WritePreview(string markdownPath, string contactSheetPath)
        {
            Summary summary = BuildSummary();
            string imageDirectory = Path.GetDirectoryName(contactSheetPath);
            if (!string.IsNullOrEmpty(imageDirectory))
            {
                Directory.CreateDirectory(imageDirectory);
            }

            Texture2D contactSheet = BuildContactSheetTexture(summary);
            try
            {
                File.WriteAllBytes(contactSheetPath, contactSheet.EncodeToPNG());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(contactSheet);
            }

            string markdownDirectory = Path.GetDirectoryName(markdownPath);
            if (!string.IsNullOrEmpty(markdownDirectory))
            {
                Directory.CreateDirectory(markdownDirectory);
            }

            File.WriteAllText(markdownPath, ToMarkdown(summary, contactSheetPath), Encoding.UTF8);
            AssetDatabase.Refresh();
        }

        public static string ToMarkdown(Summary summary, string contactSheetPath)
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("# Gangland Undercover VFX Motion Preview");
            builder.AppendLine();
            builder.AppendLine("## Status");
            builder.AppendLine();
            builder.AppendLine(summary.IsReady ? "READY" : "BLOCKED");
            builder.AppendLine();
            builder.AppendLine("## Contact Sheet");
            builder.AppendLine();
            builder.AppendLine(contactSheetPath);
            builder.AppendLine();
            builder.AppendLine("## Motion Profiles");
            builder.AppendLine();
            builder.AppendLine("| Effect | Frames | Size | FPS |");
            builder.AppendLine("|---|---:|---:|---:|");

            for (int i = 0; i < summary.Effects.Count; i++)
            {
                EffectPreview effect = summary.Effects[i];
                builder.Append("| ");
                builder.Append(effect.Name);
                builder.Append(" | ");
                builder.Append(effect.FrameCount);
                builder.Append("/");
                builder.Append(effect.ExpectedFrameCount);
                builder.Append(" | ");
                builder.Append(effect.Width);
                builder.Append("x");
                builder.Append(effect.Height);
                builder.Append(" | ");
                builder.Append(effect.FramesPerSecond.ToString("0.##"));
                builder.AppendLine(" |");
            }

            builder.AppendLine();
            builder.AppendLine("## Issues");
            builder.AppendLine();
            if (summary.Issues.Count == 0)
            {
                builder.AppendLine("- None");
            }
            else
            {
                for (int i = 0; i < summary.Issues.Count; i++)
                {
                    builder.AppendLine("- " + summary.Issues[i]);
                }
            }

            builder.AppendLine();
            builder.AppendLine("## Next Checks");
            builder.AppendLine();
            builder.AppendLine("1. Inspect row order against the table above: blackout, comms_jam, door_lock, emergency_light, evidence_leak, hit, kill, patrol_alert.");
            builder.AppendLine("2. Tune scale, FPS, opacity, and sorting where contact-sheet motion reads muddy.");
            builder.AppendLine("3. Capture the same effects in a live gameplay scene before replacing another asset batch.");
            return builder.ToString();
        }

        public static Texture2D BuildContactSheetTexture(Summary summary)
        {
            int maxFrames = 1;
            for (int i = 0; i < summary.Effects.Count; i++)
            {
                maxFrames = Mathf.Max(maxFrames, summary.Effects[i].FramePaths.Count);
            }

            int width = ContactSheetMargin * 2
                + maxFrames * ContactSheetCellSize
                + Mathf.Max(0, maxFrames - 1) * ContactSheetGap;
            int height = ContactSheetMargin * 2
                + summary.Effects.Count * ContactSheetCellSize
                + Mathf.Max(0, summary.Effects.Count - 1) * ContactSheetGap;

            Texture2D sheet = new Texture2D(width, height, TextureFormat.RGBA32, false);
            Color32[] pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    bool checker = ((x / 8) + (y / 8)) % 2 == 0;
                    pixels[(y * width) + x] = checker
                        ? new Color32(31, 35, 43, 255)
                        : new Color32(43, 49, 60, 255);
                }
            }

            sheet.SetPixels32(pixels);

            for (int row = 0; row < summary.Effects.Count; row++)
            {
                EffectPreview effect = summary.Effects[row];
                int y = height - ContactSheetMargin - ContactSheetCellSize
                    - row * (ContactSheetCellSize + ContactSheetGap);

                for (int frame = 0; frame < effect.FramePaths.Count; frame++)
                {
                    int x = ContactSheetMargin + frame * (ContactSheetCellSize + ContactSheetGap);
                    Texture2D source = LoadReadableTexture(effect.FramePaths[frame]);
                    if (source == null)
                    {
                        continue;
                    }

                    try
                    {
                        BlitCenteredNearest(source, sheet, x, y, ContactSheetCellSize, ContactSheetCellSize);
                    }
                    finally
                    {
                        UnityEngine.Object.DestroyImmediate(source);
                    }
                }
            }

            sheet.Apply(false, false);
            return sheet;
        }

        private static EffectPreview BuildEffectPreview(VfxSpec spec, Summary summary)
        {
            EffectPreview effect = new EffectPreview(
                spec.Name,
                spec.ExpectedFrameCount,
                spec.Width,
                spec.Height,
                spec.FramesPerSecond);

            for (int i = 0; i < spec.ExpectedFrameCount; i++)
            {
                string path = SpriteResourceImportSettings.SpriteResourcesRoot
                    + "/VFX/" + spec.Name + "/" + spec.Name + "_" + i.ToString("00") + ".png";
                if (!File.Exists(path))
                {
                    summary.Issues.Add("Missing VFX frame: " + path);
                    continue;
                }

                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite == null)
                {
                    summary.Issues.Add("VFX frame is not importable as Sprite: " + path);
                    continue;
                }

                int width = sprite.texture != null ? sprite.texture.width : 0;
                int height = sprite.texture != null ? sprite.texture.height : 0;
                if (width != spec.Width || height != spec.Height)
                {
                    summary.Issues.Add("VFX frame dimension mismatch: " + path + " expected "
                        + spec.Width + "x" + spec.Height + " found " + width + "x" + height);
                }

                effect.FramePaths.Add(path);
                effect.Width = width;
                effect.Height = height;
            }

            if (effect.FrameCount != spec.ExpectedFrameCount)
            {
                summary.Issues.Add("VFX frame count mismatch: " + spec.Name + " expected "
                    + spec.ExpectedFrameCount + " found " + effect.FrameCount);
            }

            return effect;
        }

        private static Texture2D LoadReadableTexture(string path)
        {
            if (!File.Exists(path))
            {
                return null;
            }

            Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!texture.LoadImage(File.ReadAllBytes(path)))
            {
                UnityEngine.Object.DestroyImmediate(texture);
                return null;
            }

            texture.filterMode = FilterMode.Point;
            return texture;
        }

        private static void BlitCenteredNearest(Texture2D source, Texture2D target, int targetX, int targetY, int cellWidth, int cellHeight)
        {
            int maxSourceSide = Mathf.Max(source.width, source.height);
            int scale = Mathf.Max(1, Mathf.FloorToInt((cellWidth - 16) / (float)maxSourceSide));
            scale = Mathf.Min(scale, 4);

            int scaledWidth = source.width * scale;
            int scaledHeight = source.height * scale;
            int offsetX = targetX + (cellWidth - scaledWidth) / 2;
            int offsetY = targetY + (cellHeight - scaledHeight) / 2;

            Color32[] sourcePixels = source.GetPixels32();
            for (int sy = 0; sy < source.height; sy++)
            {
                for (int sx = 0; sx < source.width; sx++)
                {
                    Color32 sourcePixel = sourcePixels[(sy * source.width) + sx];
                    if (sourcePixel.a == 0)
                    {
                        continue;
                    }

                    for (int yy = 0; yy < scale; yy++)
                    {
                        for (int xx = 0; xx < scale; xx++)
                        {
                            int px = offsetX + sx * scale + xx;
                            int py = offsetY + sy * scale + yy;
                            if (px < 0 || px >= target.width || py < 0 || py >= target.height)
                            {
                                continue;
                            }

                            Color32 background = target.GetPixel(px, py);
                            target.SetPixel(px, py, AlphaBlend(sourcePixel, background));
                        }
                    }
                }
            }
        }

        private static Color32 AlphaBlend(Color32 foreground, Color32 background)
        {
            float alpha = foreground.a / 255f;
            byte r = (byte)Mathf.RoundToInt((foreground.r * alpha) + (background.r * (1f - alpha)));
            byte g = (byte)Mathf.RoundToInt((foreground.g * alpha) + (background.g * (1f - alpha)));
            byte b = (byte)Mathf.RoundToInt((foreground.b * alpha) + (background.b * (1f - alpha)));
            return new Color32(r, g, b, 255);
        }

        private readonly struct VfxSpec
        {
            public readonly string Name;
            public readonly int ExpectedFrameCount;
            public readonly int Width;
            public readonly int Height;
            public readonly float FramesPerSecond;

            public VfxSpec(string name, int expectedFrameCount, int width, int height, float framesPerSecond)
            {
                Name = name;
                ExpectedFrameCount = expectedFrameCount;
                Width = width;
                Height = height;
                FramesPerSecond = framesPerSecond;
            }
        }

        public sealed class Summary
        {
            public readonly List<EffectPreview> Effects = new List<EffectPreview>();
            public readonly List<string> Issues = new List<string>();

            public bool IsReady => Issues.Count == 0;
            public int EffectCount => Effects.Count;

            public int FrameCount
            {
                get
                {
                    int count = 0;
                    for (int i = 0; i < Effects.Count; i++)
                    {
                        count += Effects[i].FrameCount;
                    }

                    return count;
                }
            }
        }

        public sealed class EffectPreview
        {
            public readonly string Name;
            public readonly int ExpectedFrameCount;
            public readonly int ExpectedWidth;
            public readonly int ExpectedHeight;
            public readonly float FramesPerSecond;
            public readonly List<string> FramePaths = new List<string>();

            public int Width { get; internal set; }
            public int Height { get; internal set; }
            public int FrameCount => FramePaths.Count;

            public EffectPreview(string name, int expectedFrameCount, int expectedWidth, int expectedHeight, float framesPerSecond)
            {
                Name = name;
                ExpectedFrameCount = expectedFrameCount;
                ExpectedWidth = expectedWidth;
                ExpectedHeight = expectedHeight;
                FramesPerSecond = framesPerSecond;
            }
        }
    }
}
