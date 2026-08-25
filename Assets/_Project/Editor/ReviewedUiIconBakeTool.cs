using System;
using System.IO;
using GanglandUndercover.Online;
using UnityEditor;
using UnityEngine;

namespace GanglandUndercover.Editor
{
    public static class ReviewedUiIconBakeTool
    {
        public const string OutputRoot = "Assets/_Project/Resources/Sprites/UI/Icons";
        private const int OutputSize = 64;
        private const int MaxContentSize = 44;
        private const string LimeZuRoot = "Assets/_Project/Resources/Sprites/Tilesets/LimeZu/";
        private const string PoliceStationRoot = "Assets/_Project/Resources/Sprites/Tilesets/PoliceStation/";

        private static readonly IconSpec[] Specs =
        {
            new IconSpec("sabotage_blackout_clean", LimeZuRoot + "Exteriors/room-props/24_Additional_Houses_Post_Apocalyptic_House_Generator_2_48x48.png", new Color32(238, 184, 48, 255)),
            new IconSpec("sabotage_lockdown_clean", LimeZuRoot + "Interiors/room-props/Modern_Interiors_48x48_JailLockerFull.png", new Color32(218, 62, 57, 255)),
            new IconSpec("sabotage_commjam_clean", LimeZuRoot + "Exteriors/room-props/ME_Singles_Subway_and_Train_Station_48x48_Monitor.png", new Color32(52, 171, 202, 255)),
            new IconSpec("sabotage_evidence_clean", LimeZuRoot + "Interiors/room-props/Modern_Interiors_48x48_SafeBucks.png", new Color32(212, 81, 126, 255)),
            new IconSpec("sabotage_patrol_clean", LimeZuRoot + "Office/room-props/Modern_Office_Singles_48x48_276_CctvCameraRig.png", new Color32(231, 111, 43, 255)),
            new IconSpec("task_wire_clean", PoliceStationRoot + "decorations/circuit-board.png", new Color32(55, 192, 124, 255)),
            new IconSpec("task_keypad_clean", LimeZuRoot + "Exteriors/room-props/ME_Singles_Subway_and_Train_Station_48x48_SOS_Box.png", new Color32(230, 162, 48, 255)),
            new IconSpec("task_scan_clean", LimeZuRoot + "Interiors/room-props/Modern_Interiors_48x48_SecurityCameraWallRight.png", new Color32(54, 194, 204, 255)),
            new IconSpec("task_download_clean", LimeZuRoot + "Office/room-props/Modern_Office_Singles_48x48_176_ServerRack.png", new Color32(70, 137, 219, 255)),
            new IconSpec("task_memory_clean", LimeZuRoot + "Exteriors/room-props/ME_Singles_Subway_and_Train_Station_48x48_Control_Big_Monitor.png", new Color32(150, 105, 203, 255)),
            new IconSpec("task_swipe_clean", LimeZuRoot + "Interiors/room-props/Modern_Interiors_48x48_TicketMachine.png", new Color32(69, 184, 153, 255)),
        };

        [MenuItem("Gangland/Art/Bake Reviewed UI Icons")]
        public static void BakeReviewedUiIcons()
        {
            Directory.CreateDirectory(OutputRoot);

            for (int i = 0; i < Specs.Length; i++)
            {
                Bake(Specs[i]);
            }

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            for (int i = 0; i < Specs.Length; i++)
            {
                ConfigureImporter(OutputPath(Specs[i]));
            }

            Debug.Log("Reviewed UI icon bake complete: " + Specs.Length + " icons written to " + OutputRoot + ".");
        }

        [MenuItem("Gangland/Art/Preview Reviewed Sabotage Task")]
        public static void PreviewReviewedSabotageTask()
        {
            OnlineMatchController controller = UnityEngine.Object.FindAnyObjectByType<OnlineMatchController>();
            if (controller == null)
            {
                Debug.LogWarning("Reviewed task preview requires Gangland/Play Online Demo to be running.");
                return;
            }

            controller.EditorForceActionPreviewForSmokeTest();
            controller.EditorTriggerTaskForSmokeTest(2, true);
            controller.EditorOpenTaskPanelForSmokeTest(2);
            controller.EditorRefreshWorldVisualsForSmokeTest();
            Selection.activeGameObject = controller.gameObject;
        }

        [MenuItem("Gangland/Art/Preview Reviewed Sabotage Task", true)]
        private static bool CanPreviewReviewedSabotageTask()
        {
            return EditorApplication.isPlaying;
        }

        private static void Bake(IconSpec spec)
        {
            if (!File.Exists(spec.SourcePath))
            {
                throw new FileNotFoundException("Reviewed icon source is missing.", spec.SourcePath);
            }

            Texture2D source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            Texture2D output = new Texture2D(OutputSize, OutputSize, TextureFormat.RGBA32, false);

            try
            {
                if (!source.LoadImage(File.ReadAllBytes(spec.SourcePath), false))
                {
                    throw new InvalidDataException("Unable to decode reviewed icon source: " + spec.SourcePath);
                }

                Color32[] sourcePixels = source.GetPixels32();
                Color32[] outputPixels = CreateFrame(spec.Accent);
                RectInt bounds = FindVisibleBounds(sourcePixels, source.width, source.height);
                BlitNearest(sourcePixels, source.width, bounds, outputPixels);
                output.SetPixels32(outputPixels);
                output.Apply(false, false);
                File.WriteAllBytes(OutputPath(spec), output.EncodeToPNG());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(source);
                UnityEngine.Object.DestroyImmediate(output);
            }
        }

        private static Color32[] CreateFrame(Color32 accent)
        {
            Color32[] pixels = new Color32[OutputSize * OutputSize];
            Color32 background = new Color32(9, 16, 22, 246);
            Color32 edge = new Color32(55, 70, 82, 255);

            for (int y = 0; y < OutputSize; y++)
            {
                for (int x = 0; x < OutputSize; x++)
                {
                    bool border = x < 2 || x >= OutputSize - 2 || y < 2 || y >= OutputSize - 2;
                    bool accentBar = y >= 3 && y <= 6 && x >= 3 && x < OutputSize - 3;
                    pixels[y * OutputSize + x] = border ? edge : accentBar ? accent : background;
                }
            }

            return pixels;
        }

        private static RectInt FindVisibleBounds(Color32[] pixels, int width, int height)
        {
            int minX = width;
            int minY = height;
            int maxX = -1;
            int maxY = -1;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (pixels[y * width + x].a <= 8)
                    {
                        continue;
                    }

                    minX = Mathf.Min(minX, x);
                    minY = Mathf.Min(minY, y);
                    maxX = Mathf.Max(maxX, x);
                    maxY = Mathf.Max(maxY, y);
                }
            }

            if (maxX < minX || maxY < minY)
            {
                throw new InvalidDataException("Reviewed icon source contains no visible pixels.");
            }

            return new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1);
        }

        private static void BlitNearest(Color32[] source, int sourceWidth, RectInt bounds, Color32[] destination)
        {
            float scale = Mathf.Min((float)MaxContentSize / bounds.width, (float)MaxContentSize / bounds.height);
            int width = Mathf.Max(1, Mathf.RoundToInt(bounds.width * scale));
            int height = Mathf.Max(1, Mathf.RoundToInt(bounds.height * scale));
            int startX = (OutputSize - width) / 2;
            int startY = 9 + (OutputSize - 12 - height) / 2;

            for (int y = 0; y < height; y++)
            {
                int sourceY = bounds.y + Mathf.Min(bounds.height - 1, y * bounds.height / height);

                for (int x = 0; x < width; x++)
                {
                    int sourceX = bounds.x + Mathf.Min(bounds.width - 1, x * bounds.width / width);
                    Color32 foreground = source[sourceY * sourceWidth + sourceX];

                    if (foreground.a <= 8)
                    {
                        continue;
                    }

                    int destinationIndex = (startY + y) * OutputSize + startX + x;
                    destination[destinationIndex] = AlphaBlend(foreground, destination[destinationIndex]);
                }
            }
        }

        private static Color32 AlphaBlend(Color32 foreground, Color32 background)
        {
            int alpha = foreground.a;
            int inverse = 255 - alpha;
            return new Color32(
                (byte)((foreground.r * alpha + background.r * inverse) / 255),
                (byte)((foreground.g * alpha + background.g * inverse) / 255),
                (byte)((foreground.b * alpha + background.b * inverse) / 255),
                255);
        }

        private static string OutputPath(IconSpec spec)
        {
            return OutputRoot + "/" + spec.OutputName + ".png";
        }

        private static void ConfigureImporter(string path)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                throw new InvalidOperationException("Reviewed icon importer is unavailable: " + path);
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 64f;
            importer.filterMode = FilterMode.Point;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = OutputSize;
            importer.SaveAndReimport();
        }

        private readonly struct IconSpec
        {
            public readonly string OutputName;
            public readonly string SourcePath;
            public readonly Color32 Accent;

            public IconSpec(string outputName, string sourcePath, Color32 accent)
            {
                OutputName = outputName;
                SourcePath = sourcePath;
                Accent = accent;
            }
        }
    }
}
