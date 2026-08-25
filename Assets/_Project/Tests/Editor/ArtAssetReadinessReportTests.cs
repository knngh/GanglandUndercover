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
            Assert.AreEqual(392, summary.RuntimeSpritePngCount);
            Assert.AreEqual(summary.RuntimeSpritePngCount, summary.RuntimeSpriteMetaCount);
            Assert.AreEqual(0, summary.MisconfiguredSpriteImportCount);
            Assert.AreEqual(5, summary.RuntimeMapPropSpriteCount);
            Assert.AreEqual(0, summary.RuntimeMapPropSpriteDimensionMismatchCount);
            Assert.AreEqual(144, summary.CharacterPngCount);
            Assert.AreEqual(16, summary.CharacterSpecialPngCount);
            Assert.AreEqual(0, summary.CharacterSpriteDimensionMismatchCount);
            Assert.AreEqual(64, summary.VfxFrameCount);
            Assert.AreEqual(0, summary.VfxFrameDimensionMismatchCount);
            Assert.AreEqual(20, summary.UiSpriteCount);
            StringAssert.Contains("Quarantined watermarked draft PNG | 59", ArtAssetReadinessReport.ToMarkdown(summary));
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
            StringAssert.Contains("blackout, comms jam, door lock, patrol alert", markdown);
            StringAssert.Contains("UI polish", markdown);
            StringAssert.Contains("VFX frame dimension mismatch", markdown);
            StringAssert.Contains("watermarked draft", markdown);
        }

        [Test]
        public void RuntimeUiSkin_UsesCleanNoirAssets()
        {
            UIArtCache.ClearCache();
            UIArtCache.Ensure();

            Assert.IsNotNull(UIArtCache.ButtonNormal);
            Assert.IsNotNull(UIArtCache.PanelFrame);
            Assert.IsNotNull(UIArtCache.MeetingTableBg);
            Assert.IsNotNull(UIArtCache.VoteCard);
            Assert.IsNotNull(UIArtCache.ProgressBar);
            Assert.AreEqual("button_noir_clean", UIArtCache.ButtonNormal.texture.name);
            Assert.AreEqual("panel_noir_clean", UIArtCache.PanelFrame.texture.name);
            Assert.AreEqual("meeting_panel_clean", UIArtCache.MeetingTableBg.texture.name);
            Assert.AreEqual("vote_card_clean", UIArtCache.VoteCard.texture.name);
            Assert.AreEqual("progress_clean", UIArtCache.ProgressBar.texture.name);
            Assert.AreEqual(UnityEngine.FilterMode.Point, UIArtCache.PanelFrame.texture.filterMode);
            Assert.AreNotEqual(UnityEngine.Vector4.zero, UIArtCache.PanelFrame.border);
            Assert.AreNotEqual(UnityEngine.Vector4.zero, UIArtCache.ButtonNormal.border);
        }

        [Test]
        public void RuntimeUiIconBatch_UsesReviewedCleanAssets()
        {
            UIArtCache.ClearCache();
            UIArtCache.Ensure();

            UnityEngine.Sprite[] icons =
            {
                UIArtCache.IconSabotageBlackout,
                UIArtCache.IconSabotageLockdown,
                UIArtCache.IconSabotageCommJam,
                UIArtCache.IconSabotageEvidence,
                UIArtCache.IconSabotagePatrol,
                UIArtCache.IconTaskWire,
                UIArtCache.IconTaskKeypad,
                UIArtCache.IconTaskScan,
                UIArtCache.IconTaskDownload,
                UIArtCache.IconTaskMemory,
                UIArtCache.IconTaskSwipe,
            };

            Assert.AreEqual(11, icons.Length);
            foreach (UnityEngine.Sprite icon in icons)
            {
                Assert.IsNotNull(icon);
                StringAssert.EndsWith("_clean", icon.texture.name);
                Assert.AreEqual(64, icon.texture.width);
                Assert.AreEqual(64, icon.texture.height);
                Assert.AreEqual(UnityEngine.FilterMode.Point, icon.texture.filterMode);
            }

            Assert.AreSame(UIArtCache.IconSabotageCommJam, UIArtCache.SabotageIcon(SabotageType.Communications.ToString()));
            Assert.AreSame(UIArtCache.IconSabotagePatrol, UIArtCache.SabotageIcon(SabotageType.PatrolAlert.ToString()));
        }
    }
}
