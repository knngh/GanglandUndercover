using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using GanglandUndercover.Art;
using UnityEditor;
using UnityEngine;

namespace GanglandUndercover.Editor
{
    public static class VfxMotionPreviewReport
    {
        public const string DefaultMarkdownPath = "output/vfx_motion_preview.md";
        public const string DefaultContactSheetPath = "output/vfx_contact_sheet.png";
        public const string DefaultGameplayContextSheetPath = "output/vfx_gameplay_context_sheet.png";
        public const int ContactSheetCellSize = 144;
        public const int GameplayContextCellWidth = 176;
        public const int GameplayContextCellHeight = 136;
        public const int GameplayContextSamplesPerEffect = 4;
        public const int ContactSheetGap = 8;
        public const int ContactSheetMargin = 12;

        [MenuItem("Gangland/Art/Write VFX Motion Preview")]
        public static void WriteDefaultPreview()
        {
            WritePreview(DefaultMarkdownPath, DefaultContactSheetPath, DefaultGameplayContextSheetPath);
        }

        public static Summary BuildSummary()
        {
            Summary summary = new Summary();
            for (int i = 0; i < VfxEffectProfile.All.Count; i++)
            {
                summary.Effects.Add(BuildEffectPreview(VfxEffectProfile.All[i], summary));
            }

            return summary;
        }

        public static void WritePreview(string markdownPath, string contactSheetPath)
        {
            WritePreview(markdownPath, contactSheetPath, DefaultGameplayContextSheetPath);
        }

        public static void WritePreview(string markdownPath, string contactSheetPath, string gameplayContextSheetPath)
        {
            Summary summary = BuildSummary();
            WriteTexturePng(contactSheetPath, BuildContactSheetTexture(summary));
            WriteTexturePng(gameplayContextSheetPath, BuildGameplayContextSheetTexture(summary));

            string markdownDirectory = Path.GetDirectoryName(markdownPath);
            if (!string.IsNullOrEmpty(markdownDirectory))
            {
                Directory.CreateDirectory(markdownDirectory);
            }

            File.WriteAllText(markdownPath, ToMarkdown(summary, contactSheetPath, gameplayContextSheetPath), Encoding.UTF8);
            AssetDatabase.Refresh();
        }

        public static string ToMarkdown(Summary summary, string contactSheetPath)
        {
            return ToMarkdown(summary, contactSheetPath, DefaultGameplayContextSheetPath);
        }

        public static string ToMarkdown(Summary summary, string contactSheetPath, string gameplayContextSheetPath)
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
            builder.AppendLine("## Gameplay Context Preview");
            builder.AppendLine();
            builder.AppendLine(gameplayContextSheetPath);
            builder.AppendLine();
            builder.AppendLine("## Motion Profiles");
            builder.AppendLine();
            builder.AppendLine("| Effect | Runtime Use | Frames | Size | FPS | Duration | Layer | Mode |");
            builder.AppendLine("|---|---|---:|---:|---:|---:|---:|---|");

            for (int i = 0; i < summary.Effects.Count; i++)
            {
                EffectPreview effect = summary.Effects[i];
                builder.Append("| ");
                builder.Append(effect.Name);
                builder.Append(" | ");
                builder.Append(effect.RuntimeUse);
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
                builder.Append(" | ");
                builder.Append(effect.DurationSeconds.ToString("0.##"));
                builder.Append("s | ");
                builder.Append(effect.SortingOrder);
                builder.Append(" | ");
                builder.Append(effect.PlaybackMode);
                builder.AppendLine(" |");
            }

            builder.AppendLine();
            builder.AppendLine("## Polish Priority");
            builder.AppendLine();
            builder.AppendLine("| Priority | Effect | Focus | First Adjustment |");
            builder.AppendLine("|---|---|---|---|");
            for (int i = 0; i < summary.Effects.Count; i++)
            {
                EffectPreview effect = summary.Effects[i];
                builder.Append("| ");
                builder.Append(effect.PolishPriority);
                builder.Append(" | ");
                builder.Append(effect.Name);
                builder.Append(" | ");
                builder.Append(effect.PolishFocus);
                builder.Append(" | ");
                builder.Append(effect.FirstAdjustment);
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
            builder.AppendLine("1. Inspect row order against the motion table: blackout, comms_jam, door_lock, emergency_light, evidence_leak, hit, kill, patrol_alert.");
            builder.AppendLine("2. Start with P2 rows, then verify blackout, comms_jam, door_lock, and patrol_alert against busy gameplay backgrounds.");
            builder.AppendLine("3. Compare the gameplay context preview against a live scene capture before replacing another asset batch.");
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

        public static Texture2D BuildGameplayContextSheetTexture(Summary summary)
        {
            int width = ContactSheetMargin * 2
                + GameplayContextSamplesPerEffect * GameplayContextCellWidth
                + Mathf.Max(0, GameplayContextSamplesPerEffect - 1) * ContactSheetGap;
            int height = ContactSheetMargin * 2
                + summary.Effects.Count * GameplayContextCellHeight
                + Mathf.Max(0, summary.Effects.Count - 1) * ContactSheetGap;

            Texture2D sheet = new Texture2D(width, height, TextureFormat.RGBA32, false);
            FillTexture(sheet, new Color32(22, 25, 32, 255));

            for (int row = 0; row < summary.Effects.Count; row++)
            {
                EffectPreview effect = summary.Effects[row];
                int y = height - ContactSheetMargin - GameplayContextCellHeight
                    - row * (GameplayContextCellHeight + ContactSheetGap);

                for (int sample = 0; sample < GameplayContextSamplesPerEffect; sample++)
                {
                    int x = ContactSheetMargin + sample * (GameplayContextCellWidth + ContactSheetGap);
                    DrawGameplayContextBackground(sheet, x, y, GameplayContextCellWidth, GameplayContextCellHeight, row, sample);

                    if (effect.FramePaths.Count == 0)
                    {
                        continue;
                    }

                    int frameIndex = Mathf.RoundToInt(sample * (effect.FramePaths.Count - 1) / (float)Mathf.Max(1, GameplayContextSamplesPerEffect - 1));
                    frameIndex = Mathf.Clamp(frameIndex, 0, effect.FramePaths.Count - 1);
                    Texture2D source = LoadReadableTexture(effect.FramePaths[frameIndex]);
                    if (source == null)
                    {
                        continue;
                    }

                    try
                    {
                        BlitCenteredNearest(source, sheet, x, y, GameplayContextCellWidth, GameplayContextCellHeight);
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

        private static EffectPreview BuildEffectPreview(VfxEffectProfile profile, Summary summary)
        {
            EffectPreview effect = new EffectPreview(
                profile.Name,
                profile.FrameCount,
                profile.Width,
                profile.Height,
                profile.FramesPerSecond,
                profile.RuntimeUse,
                profile.PlaybackModeName,
                profile.SortingOrder,
                profile.PolishPriority,
                profile.PolishFocus,
                profile.FirstAdjustment);

            for (int i = 0; i < profile.FrameCount; i++)
            {
                string path = SpriteResourceImportSettings.SpriteResourcesRoot
                    + "/VFX/" + profile.Name + "/" + profile.Name + "_" + i.ToString("00") + ".png";
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
                if (width != profile.Width || height != profile.Height)
                {
                    summary.Issues.Add("VFX frame dimension mismatch: " + path + " expected "
                        + profile.Width + "x" + profile.Height + " found " + width + "x" + height);
                }

                effect.FramePaths.Add(path);
                effect.Width = width;
                effect.Height = height;
            }

            if (effect.FrameCount != profile.FrameCount)
            {
                summary.Issues.Add("VFX frame count mismatch: " + profile.Name + " expected "
                    + profile.FrameCount + " found " + effect.FrameCount);
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

        private static void WriteTexturePng(string path, Texture2D texture)
        {
            string imageDirectory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(imageDirectory))
            {
                Directory.CreateDirectory(imageDirectory);
            }

            try
            {
                File.WriteAllBytes(path, texture.EncodeToPNG());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        private static void FillTexture(Texture2D texture, Color32 color)
        {
            Color32[] pixels = new Color32[texture.width * texture.height];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = color;
            }

            texture.SetPixels32(pixels);
        }

        private static void DrawGameplayContextBackground(Texture2D target, int targetX, int targetY, int width, int height, int row, int sample)
        {
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    bool floorTile = ((x / 16) + (y / 16) + row) % 2 == 0;
                    bool seam = x % 16 == 0 || y % 16 == 0;
                    Color32 color = floorTile
                        ? new Color32(38, 44, 54, 255)
                        : new Color32(31, 37, 47, 255);
                    if (seam)
                    {
                        color = new Color32(24, 29, 38, 255);
                    }

                    target.SetPixel(targetX + x, targetY + y, color);
                }
            }

            int phaseOffset = sample * 7;
            DrawFilledRect(target, targetX + 14 + phaseOffset, targetY + 18, 48, 10, new Color32(74, 57, 45, 255));
            DrawFilledRect(target, targetX + width - 58, targetY + height - 34 - (row % 2) * 12, 38, 22, new Color32(52, 61, 68, 255));
            DrawFilledRect(target, targetX + width - 54, targetY + height - 30 - (row % 2) * 12, 30, 14, new Color32(77, 88, 92, 255));
            DrawCharacterSilhouette(target, targetX + width / 2 - 13, targetY + height / 2 - 25);
            DrawBodyReadableBase(target, targetX + width / 2 + 17, targetY + height / 2 - 10);
        }

        private static void DrawCharacterSilhouette(Texture2D target, int x, int y)
        {
            DrawFilledRect(target, x + 9, y + 0, 10, 10, new Color32(202, 212, 220, 255));
            DrawFilledRect(target, x + 5, y + 11, 18, 28, new Color32(45, 111, 186, 255));
            DrawFilledRect(target, x + 2, y + 18, 5, 18, new Color32(33, 76, 124, 255));
            DrawFilledRect(target, x + 22, y + 18, 5, 18, new Color32(33, 76, 124, 255));
            DrawFilledRect(target, x + 7, y + 39, 6, 13, new Color32(20, 24, 30, 255));
            DrawFilledRect(target, x + 16, y + 39, 6, 13, new Color32(20, 24, 30, 255));
        }

        private static void DrawBodyReadableBase(Texture2D target, int x, int y)
        {
            DrawFilledRect(target, x, y + 9, 26, 13, new Color32(96, 24, 22, 255));
            DrawFilledRect(target, x + 4, y + 4, 12, 12, new Color32(120, 36, 32, 255));
            DrawFilledRect(target, x + 9, y + 19, 22, 6, new Color32(54, 25, 28, 255));
        }

        private static void DrawFilledRect(Texture2D target, int x, int y, int width, int height, Color32 color)
        {
            for (int yy = 0; yy < height; yy++)
            {
                int py = y + yy;
                if (py < 0 || py >= target.height)
                {
                    continue;
                }

                for (int xx = 0; xx < width; xx++)
                {
                    int px = x + xx;
                    if (px < 0 || px >= target.width)
                    {
                        continue;
                    }

                    target.SetPixel(px, py, color);
                }
            }
        }

        private static void BlitCenteredNearest(Texture2D source, Texture2D target, int targetX, int targetY, int cellWidth, int cellHeight)
        {
            int maxSourceSide = Mathf.Max(source.width, source.height);
            int scale = Mathf.Max(1, Mathf.FloorToInt((Mathf.Min(cellWidth, cellHeight) - 16) / (float)maxSourceSide));
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
            public readonly string RuntimeUse;
            public readonly string PlaybackMode;
            public readonly int SortingOrder;
            public readonly string PolishPriority;
            public readonly string PolishFocus;
            public readonly string FirstAdjustment;
            public readonly List<string> FramePaths = new List<string>();

            public int Width { get; internal set; }
            public int Height { get; internal set; }
            public int FrameCount => FramePaths.Count;
            public float DurationSeconds => FramesPerSecond > 0f ? FrameCount / FramesPerSecond : 0f;

            public EffectPreview(
                string name,
                int expectedFrameCount,
                int expectedWidth,
                int expectedHeight,
                float framesPerSecond,
                string runtimeUse,
                string playbackMode,
                int sortingOrder,
                string polishPriority,
                string polishFocus,
                string firstAdjustment)
            {
                Name = name;
                ExpectedFrameCount = expectedFrameCount;
                ExpectedWidth = expectedWidth;
                ExpectedHeight = expectedHeight;
                FramesPerSecond = framesPerSecond;
                RuntimeUse = runtimeUse;
                PlaybackMode = playbackMode;
                SortingOrder = sortingOrder;
                PolishPriority = polishPriority;
                PolishFocus = polishFocus;
                FirstAdjustment = firstAdjustment;
            }
        }
    }
}
