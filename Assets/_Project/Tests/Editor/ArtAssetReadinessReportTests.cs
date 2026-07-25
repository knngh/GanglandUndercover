using NUnit.Framework;
using GanglandUndercover.Art;
using GanglandUndercover.Editor;
using GanglandUndercover.Online;

namespace GanglandUndercover.Tests
{
    [TestFixture]
    public class ArtAssetReadinessReportTests
    {
        [Test]
        public void RuntimeArtReadiness_IsCompleteForCurrentAssetSet()
        {
            ArtAssetReadinessReport.Summary summary = ArtAssetReadinessReport.BuildSummary();

            Assert.IsTrue(summary.IsReady, ArtAssetReadinessReport.ToMarkdown(summary));
            Assert.AreEqual(373, summary.RuntimeSpritePngCount);
            Assert.AreEqual(summary.RuntimeSpritePngCount, summary.RuntimeSpriteMetaCount);
            Assert.AreEqual(0, summary.MisconfiguredSpriteImportCount);
            Assert.AreEqual(5, summary.RuntimeMapPropSpriteCount);
            Assert.AreEqual(0, summary.RuntimeMapPropSpriteDimensionMismatchCount);
            Assert.AreEqual(144, summary.CharacterPngCount);
            Assert.AreEqual(16, summary.CharacterSpecialPngCount);
            Assert.AreEqual(0, summary.CharacterSpriteDimensionMismatchCount);
            Assert.AreEqual(64, summary.VfxFrameCount);
            Assert.AreEqual(0, summary.VfxFrameDimensionMismatchCount);
            Assert.AreEqual(4, summary.UiSpriteCount);
        }

        [Test]
        public void CharacterSpecialSprites_LoadIntoRuntimeCharacterSets()
        {
            Sprite2DAssetCache.Ensure();

            foreach (OnlineProfession profession in System.Enum.GetValues(typeof(OnlineProfession)))
            {
                Assert.IsTrue(Sprite2DAssetCache.CharacterSets.TryGetValue(profession, out Sprite2DAssetCache.ProfSpriteSet set));
                Assert.IsNotNull(set.Dead, profession + " must load a downed character sprite.");
                Assert.IsNotNull(set.Avatar, profession + " must load a meeting avatar sprite.");
                Assert.AreEqual(64, set.Dead.texture.width, profession + " downed sprite width.");
                Assert.AreEqual(64, set.Dead.texture.height, profession + " downed sprite height.");
                Assert.AreEqual(32, set.Avatar.texture.width, profession + " avatar sprite width.");
                Assert.AreEqual(32, set.Avatar.texture.height, profession + " avatar sprite height.");
            }
        }

        [Test]
        public void RuntimePathConstants_AreIncludedInReadinessScan()
        {
            ArtAssetReadinessReport.Summary summary = ArtAssetReadinessReport.BuildSummary();

            Assert.GreaterOrEqual(summary.RuntimePathConstantCount, 50);
            Assert.IsTrue(summary.IsReady, ArtAssetReadinessReport.ToMarkdown(summary));
        }

        [Test]
        public void MarkdownReport_ListsNextArtSlices()
        {
            ArtAssetReadinessReport.Summary summary = ArtAssetReadinessReport.BuildSummary();
            string markdown = ArtAssetReadinessReport.ToMarkdown(summary);

            StringAssert.Contains("## Next Art Slices", markdown);
            StringAssert.Contains("Character polish", markdown);
            StringAssert.Contains("Map polish", markdown);
            StringAssert.Contains("VFX polish", markdown);
            StringAssert.Contains("UI polish", markdown);
            StringAssert.Contains("VFX frame dimension mismatch", markdown);
        }
    }
}
