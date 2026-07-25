using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using GanglandUndercover.Editor;

namespace GanglandUndercover.Tests
{
    [TestFixture]
    public class SpriteResourceImportSettingsTests
    {
        [Test]
        public void ResourceSprites_AreConfiguredForSpriteRuntimeUse()
        {
            List<string> paths = SpriteResourceImportSettings.GetSpritePngAssetPaths();
            Assert.Greater(paths.Count, 0, "Expected runtime sprite PNG assets under " + SpriteResourceImportSettings.SpriteResourcesRoot);

            List<string> invalid = SpriteResourceImportSettings.FindMisconfiguredSpritePngs();
            Assert.IsEmpty(invalid, "Misconfigured sprite imports:\n" + string.Join("\n", invalid));
        }

        [Test]
        public void ResourceSpriteScan_DoesNotIncludeLegacy3DTextures()
        {
            List<string> paths = SpriteResourceImportSettings.GetSpritePngAssetPaths();
            Assert.IsFalse(paths.Contains("Assets/_Project/Resources/Quaternius/ModularSciFiMegaKit/Textures/T_Decals.png"));
        }

        [Test]
        public void UiButtonResourceSprites_AreLoadableAsSpriteAssets()
        {
            AssertLoadableSprite("Assets/_Project/Resources/Sprites/UI/Buttons/buttonSquare_beige.png");
            AssertLoadableSprite("Assets/_Project/Resources/Sprites/UI/Buttons/button_round_gloss.png");
        }

        private static void AssertLoadableSprite(string assetPath)
        {
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
            Assert.IsNotNull(sprite, assetPath + " must import as a Sprite for Resources.Load<Sprite> UI paths.");
        }
    }
}
