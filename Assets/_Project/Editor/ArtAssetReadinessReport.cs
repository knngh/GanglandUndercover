using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using GanglandUndercover.Art;
using GanglandUndercover.Online;
using UnityEditor;

namespace GanglandUndercover.Editor
{
    /// <summary>
    /// Audits runtime art coverage before large-scale art replacement work.
    /// The scope is runtime 2D sprites under Assets/_Project/Resources/Sprites.
    /// </summary>
    public static class ArtAssetReadinessReport
    {
        public const string DefaultReportPath = "output/art_readiness_current.md";
        public const string ResourceRoot = "Assets/_Project/Resources";
        public const string SpriteRoot = SpriteResourceImportSettings.SpriteResourcesRoot;
        public const int RequiredCharacterFrameWidth = 64;
        public const int RequiredCharacterFrameHeight = 64;
        public const int RequiredCharacterAvatarWidth = 32;
        public const int RequiredCharacterAvatarHeight = 32;

        private static readonly string[] CharacterDirections =
        {
            "Back",
            "Front",
            "Left",
            "Right",
        };

        private static readonly string[] CharacterFrameNames =
        {
            "idle",
            "walk_0",
            "walk_1",
            "walk_2",
        };

        private static readonly ExpectedCharacterSprite[] CharacterSpecialSprites =
        {
            new ExpectedCharacterSprite("death", RequiredCharacterFrameWidth, RequiredCharacterFrameHeight),
            new ExpectedCharacterSprite("avatar", RequiredCharacterAvatarWidth, RequiredCharacterAvatarHeight),
        };

        private static readonly ExpectedVfx[] ExpectedVfxFrames =
        {
            new ExpectedVfx("blackout", 12, 96, 96),
            new ExpectedVfx("comms_jam", 8, 64, 64),
            new ExpectedVfx("door_lock", 6, 48, 48),
            new ExpectedVfx("emergency_light", 8, 48, 48),
            new ExpectedVfx("evidence_leak", 12, 48, 48),
            new ExpectedVfx("hit", 4, 32, 32),
            new ExpectedVfx("kill", 10, 128, 128),
            new ExpectedVfx("patrol_alert", 4, 64, 64),
        };

        private static readonly string[] RequiredUiResourcePaths =
        {
            "Sprites/UI/Buttons/buttonSquare_beige",
            "Sprites/UI/Buttons/buttonSquare_blue",
            "Sprites/UI/Buttons/buttonSquare_grey",
            "Sprites/UI/Buttons/button_round_gloss",
        };

        private static readonly string[] RequiredRuntimeMapPropResourcePaths =
        {
            Sprite2DAssetCache.HarbourPropCrateWoodPath,
            Sprite2DAssetCache.HarbourPropBarrelOilPath,
            Sprite2DAssetCache.HarbourPropVentBackalleyPath,
            Sprite2DAssetCache.KowloonPropCrateOldPath,
            Sprite2DAssetCache.KowloonPropVentRustPath,
        };

        [MenuItem("Gangland/Art/Validate Runtime Art Readiness")]
        public static void ValidateRuntimeArtReadiness()
        {
            Summary summary = BuildSummary();
            if (summary.IsReady)
            {
                UnityEngine.Debug.Log("[Gangland] Runtime art readiness is complete.");
                return;
            }

            UnityEngine.Debug.LogError("[Gangland] Runtime art readiness has gaps:\n" + string.Join("\n", summary.Issues));
        }

        [MenuItem("Gangland/Art/Write Runtime Art Readiness Report")]
        public static void WriteRuntimeArtReadinessReport()
        {
            WriteMarkdownReport(DefaultReportPath);
        }

        public static Summary BuildSummary()
        {
            Summary summary = new Summary();

            CheckSpritePairs(summary);
            CheckSpriteImporters(summary);
            CheckCharacterCoverage(summary);
            CheckVfxCoverage(summary);
            CheckUiCoverage(summary);
            CheckRuntimeMapPropCoverage(summary);
            CheckRuntimePathConstants(summary);

            return summary;
        }

        public static void WriteMarkdownReport(string reportPath)
        {
            Summary summary = BuildSummary();
            string directory = Path.GetDirectoryName(reportPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(reportPath, ToMarkdown(summary), Encoding.UTF8);
            AssetDatabase.Refresh();
        }

        public static string ToMarkdown(Summary summary)
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("# Gangland Undercover Runtime Art Readiness");
            builder.AppendLine();
            builder.AppendLine("## Status");
            builder.AppendLine();
            builder.AppendLine(summary.IsReady ? "READY" : "BLOCKED");
            builder.AppendLine();
            builder.AppendLine("## Runtime Coverage");
            builder.AppendLine();
            builder.AppendLine("| Area | Count |");
            builder.AppendLine("|---|---:|");
            builder.AppendLine("| Runtime sprite PNG | " + summary.RuntimeSpritePngCount + " |");
            builder.AppendLine("| Runtime sprite PNG meta | " + summary.RuntimeSpriteMetaCount + " |");
            builder.AppendLine("| Misconfigured runtime sprite importer | " + summary.MisconfiguredSpriteImportCount + " |");
            builder.AppendLine("| Character professions | " + summary.CharacterProfessionCount + " |");
            builder.AppendLine("| Character PNG | " + summary.CharacterPngCount + " |");
            builder.AppendLine("| Character special PNG | " + summary.CharacterSpecialPngCount + " |");
            builder.AppendLine("| Character sprite dimension mismatch | " + summary.CharacterSpriteDimensionMismatchCount + " |");
            builder.AppendLine("| VFX effects | " + summary.VfxEffectCount + " |");
            builder.AppendLine("| VFX frames | " + summary.VfxFrameCount + " |");
            builder.AppendLine("| VFX frame dimension mismatch | " + summary.VfxFrameDimensionMismatchCount + " |");
            builder.AppendLine("| Runtime UI sprites | " + summary.UiSpriteCount + " |");
            builder.AppendLine("| Runtime map prop sprites | " + summary.RuntimeMapPropSpriteCount + " |");
            builder.AppendLine("| Runtime map prop sprite dimension mismatch | " + summary.RuntimeMapPropSpriteDimensionMismatchCount + " |");
            builder.AppendLine("| Sprite2DAssetCache public path constants | " + summary.RuntimePathConstantCount + " |");
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
            builder.AppendLine("## Next Art Slices");
            builder.AppendLine();
            builder.AppendLine("1. VFX polish pass 2: capture blackout, comms jam, door lock, evidence leak, patrol alert, kill, and hit effects in motion; tune scale, FPS, opacity, and sorting.");
            builder.AppendLine("2. UI polish: expand the 4 runtime button sprites into a complete panel, card, icon, and disabled-state skin.");
            builder.AppendLine("3. Character polish pass 3: review gameplay screenshots and replace procedural silhouettes with final hand-authored variants.");
            builder.AppendLine("4. Map polish pass 2: extend authored prop replacement to police-station rooms and remaining high-traffic corridors.");
            builder.AppendLine("5. Verification: rerun this report and Unity EditMode before each large art import batch.");
            return builder.ToString();
        }

        private static void CheckSpritePairs(Summary summary)
        {
            if (!Directory.Exists(SpriteRoot))
            {
                summary.AddIssue("Missing sprite root: " + SpriteRoot);
                return;
            }

            string[] pngs = Directory.GetFiles(SpriteRoot, "*.png", SearchOption.AllDirectories);
            string[] metas = Directory.GetFiles(SpriteRoot, "*.png.meta", SearchOption.AllDirectories);
            Array.Sort(pngs, StringComparer.Ordinal);
            Array.Sort(metas, StringComparer.Ordinal);

            summary.RuntimeSpritePngCount = pngs.Length;
            summary.RuntimeSpriteMetaCount = metas.Length;

            for (int i = 0; i < pngs.Length; i++)
            {
                string metaPath = pngs[i] + ".meta";
                if (!File.Exists(metaPath))
                {
                    summary.AddIssue("Missing meta for runtime sprite: " + Normalize(pngs[i]));
                }
            }

            for (int i = 0; i < metas.Length; i++)
            {
                string assetPath = metas[i].Substring(0, metas[i].Length - ".meta".Length);
                if (!File.Exists(assetPath))
                {
                    summary.AddIssue("Orphan runtime sprite meta: " + Normalize(metas[i]));
                }
            }
        }

        private static void CheckSpriteImporters(Summary summary)
        {
            List<string> invalid = SpriteResourceImportSettings.FindMisconfiguredSpritePngs();
            summary.MisconfiguredSpriteImportCount = invalid.Count;

            for (int i = 0; i < invalid.Count; i++)
            {
                summary.AddIssue("Misconfigured runtime sprite importer: " + invalid[i]);
            }
        }

        private static void CheckCharacterCoverage(Summary summary)
        {
            OnlineProfession[] professions = (OnlineProfession[])Enum.GetValues(typeof(OnlineProfession));
            summary.CharacterProfessionCount = professions.Length;
            summary.CharacterExpectedPngCount =
                professions.Length * ((CharacterDirections.Length * CharacterFrameNames.Length) + CharacterSpecialSprites.Length);

            for (int p = 0; p < professions.Length; p++)
            {
                string profession = professions[p].ToString();
                for (int d = 0; d < CharacterDirections.Length; d++)
                {
                    string direction = CharacterDirections[d];
                    for (int f = 0; f < CharacterFrameNames.Length; f++)
                    {
                        string assetPath = SpriteRoot + "/Characters/" + profession + "/" + direction + "/" + CharacterFrameNames[f] + ".png";
                        CheckCharacterSprite(summary, assetPath, RequiredCharacterFrameWidth, RequiredCharacterFrameHeight);
                    }
                }

                for (int s = 0; s < CharacterSpecialSprites.Length; s++)
                {
                    ExpectedCharacterSprite expected = CharacterSpecialSprites[s];
                    string assetPath = SpriteRoot + "/Characters/" + profession + "/" + expected.Name + ".png";
                    if (CheckCharacterSprite(summary, assetPath, expected.Width, expected.Height))
                    {
                        summary.CharacterSpecialPngCount++;
                    }
                }
            }

            if (summary.CharacterPngCount != summary.CharacterExpectedPngCount)
            {
                summary.AddIssue("Character sprite count mismatch: expected "
                    + summary.CharacterExpectedPngCount + ", found " + summary.CharacterPngCount);
            }
        }

        private static bool CheckCharacterSprite(Summary summary, string assetPath, int expectedWidth, int expectedHeight)
        {
            if (!File.Exists(assetPath))
            {
                summary.AddIssue("Missing character sprite: " + assetPath);
                return false;
            }

            summary.CharacterPngCount++;
            if (!TryReadPngDimensions(assetPath, out int width, out int height)
                || width != expectedWidth
                || height != expectedHeight)
            {
                summary.CharacterSpriteDimensionMismatchCount++;
                string size = width > 0 && height > 0 ? width + "x" + height : "unreadable";
                summary.AddIssue("Character sprite size mismatch: " + assetPath
                    + " is " + size + ", expected " + expectedWidth + "x" + expectedHeight);
            }

            return true;
        }

        private static bool TryReadPngDimensions(string assetPath, out int width, out int height)
        {
            width = 0;
            height = 0;

            byte[] bytes;
            try
            {
                bytes = File.ReadAllBytes(assetPath);
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }

            if (bytes.Length < 24
                || bytes[0] != 0x89
                || bytes[1] != 0x50
                || bytes[2] != 0x4E
                || bytes[3] != 0x47
                || bytes[4] != 0x0D
                || bytes[5] != 0x0A
                || bytes[6] != 0x1A
                || bytes[7] != 0x0A)
            {
                return false;
            }

            width = ReadBigEndianInt32(bytes, 16);
            height = ReadBigEndianInt32(bytes, 20);
            return width > 0 && height > 0;
        }

        private static int ReadBigEndianInt32(byte[] bytes, int offset)
        {
            return (bytes[offset] << 24)
                | (bytes[offset + 1] << 16)
                | (bytes[offset + 2] << 8)
                | bytes[offset + 3];
        }

        private static void CheckVfxCoverage(Summary summary)
        {
            summary.VfxEffectCount = ExpectedVfxFrames.Length;

            for (int i = 0; i < ExpectedVfxFrames.Length; i++)
            {
                ExpectedVfx expected = ExpectedVfxFrames[i];
                string directory = SpriteRoot + "/VFX/" + expected.Name;
                if (!Directory.Exists(directory))
                {
                    summary.AddIssue("Missing VFX directory: " + directory);
                    continue;
                }

                string[] frames = Directory.GetFiles(directory, "*.png", SearchOption.TopDirectoryOnly);
                Array.Sort(frames, StringComparer.Ordinal);
                summary.VfxFrameCount += frames.Length;

                if (frames.Length != expected.FrameCount)
                {
                    summary.AddIssue("VFX frame count mismatch for " + expected.Name
                        + ": expected " + expected.FrameCount + ", found " + frames.Length);
                }

                for (int f = 0; f < frames.Length; f++)
                {
                    if (!TryReadPngDimensions(frames[f], out int width, out int height)
                        || width != expected.Width
                        || height != expected.Height)
                    {
                        summary.VfxFrameDimensionMismatchCount++;
                        string size = width > 0 && height > 0 ? width + "x" + height : "unreadable";
                        summary.AddIssue("VFX frame size mismatch: " + Normalize(frames[f])
                            + " is " + size + ", expected " + expected.Width + "x" + expected.Height);
                    }
                }
            }
        }

        private static void CheckUiCoverage(Summary summary)
        {
            summary.UiExpectedSpriteCount = RequiredUiResourcePaths.Length;

            for (int i = 0; i < RequiredUiResourcePaths.Length; i++)
            {
                string assetPath = ResourceAssetPath(RequiredUiResourcePaths[i]);
                if (File.Exists(assetPath))
                {
                    summary.UiSpriteCount++;
                }
                else
                {
                    summary.AddIssue("Missing runtime UI sprite: " + assetPath);
                }
            }
        }

        private static void CheckRuntimeMapPropCoverage(Summary summary)
        {
            summary.RuntimeMapPropExpectedSpriteCount = RequiredRuntimeMapPropResourcePaths.Length;

            for (int i = 0; i < RequiredRuntimeMapPropResourcePaths.Length; i++)
            {
                string assetPath = ResourceAssetPath(RequiredRuntimeMapPropResourcePaths[i]);
                if (!File.Exists(assetPath))
                {
                    summary.AddIssue("Missing runtime map prop sprite: " + assetPath);
                    continue;
                }

                summary.RuntimeMapPropSpriteCount++;
                if (!TryReadPngDimensions(assetPath, out int width, out int height)
                    || width != 32
                    || height != 32)
                {
                    summary.RuntimeMapPropSpriteDimensionMismatchCount++;
                    string size = width > 0 && height > 0 ? width + "x" + height : "unreadable";
                    summary.AddIssue("Runtime map prop sprite size mismatch: " + assetPath
                        + " is " + size + ", expected 32x32");
                }
            }
        }

        private static void CheckRuntimePathConstants(Summary summary)
        {
            FieldInfo[] fields = typeof(Sprite2DAssetCache).GetFields(BindingFlags.Public | BindingFlags.Static);
            Array.Sort(fields, (a, b) => string.Compare(a.Name, b.Name, StringComparison.Ordinal));

            for (int i = 0; i < fields.Length; i++)
            {
                FieldInfo field = fields[i];
                if (!field.IsLiteral || field.FieldType != typeof(string) || !field.Name.EndsWith("Path", StringComparison.Ordinal))
                {
                    continue;
                }

                string resourcePath = field.GetRawConstantValue() as string;
                if (string.IsNullOrEmpty(resourcePath))
                {
                    continue;
                }

                summary.RuntimePathConstantCount++;
                string assetPath = ResourceAssetPath(resourcePath);
                if (!File.Exists(assetPath))
                {
                    summary.AddIssue("Missing Sprite2DAssetCache resource path " + field.Name + ": " + assetPath);
                    continue;
                }

                TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                if (importer == null || !SpriteResourceImportSettings.IsConfigured(importer))
                {
                    summary.AddIssue("Sprite2DAssetCache resource path is not Sprite-configured: " + assetPath);
                }
            }
        }

        private static string ResourceAssetPath(string resourcePath)
        {
            return ResourceRoot + "/" + resourcePath + ".png";
        }

        private static string Normalize(string path)
        {
            return path.Replace(Path.DirectorySeparatorChar, '/');
        }

        private sealed class ExpectedVfx
        {
            public readonly string Name;
            public readonly int FrameCount;
            public readonly int Width;
            public readonly int Height;

            public ExpectedVfx(string name, int frameCount, int width, int height)
            {
                Name = name;
                FrameCount = frameCount;
                Width = width;
                Height = height;
            }
        }

        private sealed class ExpectedCharacterSprite
        {
            public readonly string Name;
            public readonly int Width;
            public readonly int Height;

            public ExpectedCharacterSprite(string name, int width, int height)
            {
                Name = name;
                Width = width;
                Height = height;
            }
        }

        public sealed class Summary
        {
            public readonly List<string> Issues = new List<string>();
            public int RuntimeSpritePngCount;
            public int RuntimeSpriteMetaCount;
            public int MisconfiguredSpriteImportCount;
            public int CharacterProfessionCount;
            public int CharacterExpectedPngCount;
            public int CharacterPngCount;
            public int CharacterSpecialPngCount;
            public int CharacterSpriteDimensionMismatchCount;
            public int VfxEffectCount;
            public int VfxFrameCount;
            public int VfxFrameDimensionMismatchCount;
            public int UiExpectedSpriteCount;
            public int UiSpriteCount;
            public int RuntimeMapPropExpectedSpriteCount;
            public int RuntimeMapPropSpriteCount;
            public int RuntimeMapPropSpriteDimensionMismatchCount;
            public int RuntimePathConstantCount;

            public bool IsReady
            {
                get { return Issues.Count == 0; }
            }

            public void AddIssue(string issue)
            {
                Issues.Add(issue);
            }
        }
    }
}
