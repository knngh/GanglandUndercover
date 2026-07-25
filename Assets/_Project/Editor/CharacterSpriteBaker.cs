using System;
using System.IO;
using GanglandUndercover.Art;
using GanglandUndercover.Online;
using UnityEditor;
using UnityEngine;

namespace GanglandUndercover.Editor
{
    /// <summary>
    /// Bakes the runtime procedural profession sprites into Resources so art readiness
    /// can verify the exact assets the game loads.
    /// </summary>
    public static class CharacterSpriteBaker
    {
        public const int FrameSize = 64;
        public const int AvatarSize = 32;
        public const string CharacterRoot = SpriteResourceImportSettings.SpriteResourcesRoot + "/Characters";

        [MenuItem("Gangland/Art/Bake Procedural Character Sprites")]
        public static void BakeProceduralCharacterSprites()
        {
            int written = BakeAllCharacterFrames();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            int importersChanged = SpriteResourceImportSettings.ConfigureAllSpritePngs();
            Debug.Log("[Gangland] Procedural character sprites baked: " + written
                + " PNG, importers updated: " + importersChanged);
        }

        public static int BakeAllCharacterFrames()
        {
            int written = 0;
            foreach (OnlineProfession profession in Enum.GetValues(typeof(OnlineProfession)))
            {
                Sprite2DAssetCache.ProfSpriteSet set = Sprite2DAssetCache.CreateProceduralCharacterSet(profession);
                written += WriteDirection(profession, "Front", set.Front_Frame0, set.Front_Frame1, set.Front_Frame2);
                written += WriteDirection(profession, "Back", set.Back_Frame0, set.Back_Frame1, set.Back_Frame2);
                written += WriteDirection(profession, "Left", set.Left_Frame0, set.Left_Frame1, set.Left_Frame2);
                written += WriteDirection(profession, "Right", set.Right_Frame0, set.Right_Frame1, set.Right_Frame2);
                written += WriteFrame(profession, "death", set.Dead, FrameSize);
                written += WriteFrame(profession, "avatar", set.Avatar, AvatarSize);
            }

            AssetDatabase.SaveAssets();
            return written;
        }

        private static int WriteDirection(OnlineProfession profession, string direction, Sprite idle, Sprite walk0, Sprite walk1)
        {
            int written = 0;
            written += WriteFrame(profession, direction, "idle", idle, FrameSize);
            written += WriteFrame(profession, direction, "walk_0", walk0, FrameSize);
            written += WriteFrame(profession, direction, "walk_1", walk1, FrameSize);
            written += WriteFrame(profession, direction, "walk_2", idle, FrameSize);
            return written;
        }

        private static int WriteFrame(OnlineProfession profession, string direction, string frameName, Sprite sprite, int expectedSize)
        {
            if (sprite == null || sprite.texture == null)
            {
                throw new InvalidOperationException("Missing baked sprite for "
                    + profession + "/" + direction + "/" + frameName);
            }

            Texture2D texture = sprite.texture;
            if (texture.width != expectedSize || texture.height != expectedSize)
            {
                throw new InvalidOperationException("Baked sprite has invalid dimensions for "
                    + profession + "/" + direction + "/" + frameName + ": "
                    + texture.width + "x" + texture.height);
            }

            byte[] png = EncodeAsUprightPng(texture, expectedSize, expectedSize);
            if (png == null || png.Length == 0)
            {
                throw new InvalidOperationException("Failed to encode baked sprite for "
                    + profession + "/" + direction + "/" + frameName);
            }

            string directory = CharacterRoot + "/" + profession + "/" + direction;
            Directory.CreateDirectory(directory);
            File.WriteAllBytes(directory + "/" + frameName + ".png", png);
            return 1;
        }

        private static int WriteFrame(OnlineProfession profession, string frameName, Sprite sprite, int expectedSize)
        {
            if (sprite == null || sprite.texture == null)
            {
                throw new InvalidOperationException("Missing baked sprite for " + profession + "/" + frameName);
            }

            Texture2D texture = sprite.texture;
            if (texture.width != expectedSize || texture.height != expectedSize)
            {
                throw new InvalidOperationException("Baked sprite has invalid dimensions for "
                    + profession + "/" + frameName + ": " + texture.width + "x" + texture.height);
            }

            byte[] png = EncodeAsUprightPng(texture, expectedSize, expectedSize);
            if (png == null || png.Length == 0)
            {
                throw new InvalidOperationException("Failed to encode baked sprite for "
                    + profession + "/" + frameName);
            }

            string directory = CharacterRoot + "/" + profession;
            Directory.CreateDirectory(directory);
            File.WriteAllBytes(directory + "/" + frameName + ".png", png);
            return 1;
        }

        private static byte[] EncodeAsUprightPng(Texture2D source, int width, int height)
        {
            Color32[] sourcePixels = source.GetPixels32();
            Color32[] flippedPixels = new Color32[sourcePixels.Length];

            for (int y = 0; y < height; y++)
            {
                int sourceRow = y * width;
                int targetRow = (height - 1 - y) * width;
                for (int x = 0; x < width; x++)
                {
                    flippedPixels[targetRow + x] = sourcePixels[sourceRow + x];
                }
            }

            Texture2D output = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point
            };
            output.SetPixels32(flippedPixels);
            output.Apply();
            byte[] bytes = output.EncodeToPNG();
            UnityEngine.Object.DestroyImmediate(output);
            return bytes;
        }
    }
}
