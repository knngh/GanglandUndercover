using NUnit.Framework;
using UnityEngine;
using GanglandUndercover.Art;
using GanglandUndercover.Editor;

namespace GanglandUndercover.Tests
{
    [TestFixture]
    public class VfxMotionPreviewReportTests
    {
        [Test]
        public void VfxMotionPreview_BuildsCoverageForEveryRuntimeEffect()
        {
            VfxMotionPreviewReport.Summary summary = VfxMotionPreviewReport.BuildSummary();

            Assert.IsTrue(summary.IsReady, VfxMotionPreviewReport.ToMarkdown(summary, VfxMotionPreviewReport.DefaultContactSheetPath));
            Assert.AreEqual(8, summary.EffectCount);
            Assert.AreEqual(64, summary.FrameCount);

            foreach (VfxMotionPreviewReport.EffectPreview effect in summary.Effects)
            {
                Assert.Greater(effect.FrameCount, 0, effect.Name + " should have frames.");
                Assert.Greater(effect.FramesPerSecond, 0f, effect.Name + " should declare a preview FPS.");
                Assert.Greater(effect.DurationSeconds, 0f, effect.Name + " should expose a motion duration.");
                Assert.IsFalse(string.IsNullOrEmpty(effect.RuntimeUse), effect.Name + " should describe its runtime use.");
                Assert.IsFalse(string.IsNullOrEmpty(effect.PlaybackMode), effect.Name + " should describe its playback mode.");
                Assert.IsFalse(string.IsNullOrEmpty(effect.PolishPriority), effect.Name + " should declare a polish priority.");
                Assert.IsFalse(string.IsNullOrEmpty(effect.PolishFocus), effect.Name + " should declare a polish focus.");
                Assert.AreEqual(effect.ExpectedFrameCount, effect.FrameCount, effect.Name + " frame count.");
                Assert.AreEqual(effect.ExpectedWidth, effect.Width, effect.Name + " width.");
                Assert.AreEqual(effect.ExpectedHeight, effect.Height, effect.Name + " height.");
            }

            VfxMotionPreviewReport.EffectPreview kill = summary.Effects.Find(effect => effect.Name == "kill");
            Assert.IsNotNull(kill);
            Assert.AreEqual(504, kill.SortingOrder);
            Assert.AreEqual("OneShot", kill.PlaybackMode);
            Assert.AreEqual(10f / 15f, kill.DurationSeconds, 0.001f);

            VfxMotionPreviewReport.EffectPreview evidenceLeak = summary.Effects.Find(effect => effect.Name == "evidence_leak");
            Assert.IsNotNull(evidenceLeak);
            Assert.AreEqual(499, evidenceLeak.SortingOrder);
            StringAssert.Contains("evidence", evidenceLeak.RuntimeUse.ToLowerInvariant());

            string markdown = VfxMotionPreviewReport.ToMarkdown(
                summary,
                VfxMotionPreviewReport.DefaultContactSheetPath,
                VfxMotionPreviewReport.DefaultGameplayContextSheetPath);
            StringAssert.Contains("blackout", markdown);
            StringAssert.Contains("comms_jam", markdown);
            StringAssert.Contains("door_lock", markdown);
            StringAssert.Contains("kill", markdown);
            StringAssert.Contains("## Polish Priority", markdown);
            StringAssert.Contains("Duration", markdown);
            StringAssert.Contains("Layer", markdown);
            StringAssert.Contains("P1", markdown);
            StringAssert.Contains("P2", markdown);
            StringAssert.Contains(VfxMotionPreviewReport.DefaultContactSheetPath, markdown);
            StringAssert.Contains("## Gameplay Context Preview", markdown);
            StringAssert.Contains(VfxMotionPreviewReport.DefaultGameplayContextSheetPath, markdown);
            StringAssert.Contains("Start with P2 rows", markdown);
        }

        [Test]
        public void VfxMotionPreview_CreatesReadableContactSheetTexture()
        {
            VfxMotionPreviewReport.Summary summary = VfxMotionPreviewReport.BuildSummary();
            Texture2D texture = VfxMotionPreviewReport.BuildContactSheetTexture(summary);

            try
            {
                Assert.IsNotNull(texture);
                Assert.Greater(texture.width, 0);
                Assert.Greater(texture.height, 0);
                Assert.Greater(texture.EncodeToPNG().Length, 0);
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }
        }

        [Test]
        public void VfxMotionPreview_UsesSharedRuntimeProfiles()
        {
            VfxMotionPreviewReport.Summary summary = VfxMotionPreviewReport.BuildSummary();

            Assert.AreEqual(VfxEffectProfile.All.Count, summary.Effects.Count);
            for (int i = 0; i < VfxEffectProfile.All.Count; i++)
            {
                VfxEffectProfile profile = VfxEffectProfile.All[i];
                VfxMotionPreviewReport.EffectPreview effect = summary.Effects[i];

                Assert.AreEqual(profile.Name, effect.Name);
                Assert.AreEqual(profile.FrameCount, effect.ExpectedFrameCount, profile.Name + " frame contract.");
                Assert.AreEqual(profile.Width, effect.ExpectedWidth, profile.Name + " width contract.");
                Assert.AreEqual(profile.Height, effect.ExpectedHeight, profile.Name + " height contract.");
                Assert.AreEqual(profile.FramesPerSecond, effect.FramesPerSecond, profile.Name + " fps contract.");
                Assert.AreEqual(profile.SortingOrder, effect.SortingOrder, profile.Name + " sorting contract.");
                Assert.AreEqual(profile.PlaybackModeName, effect.PlaybackMode, profile.Name + " playback contract.");
            }
        }

        [Test]
        public void VfxMotionPreview_CreatesGameplayContextSheetTexture()
        {
            VfxMotionPreviewReport.Summary summary = VfxMotionPreviewReport.BuildSummary();
            Texture2D texture = VfxMotionPreviewReport.BuildGameplayContextSheetTexture(summary);

            try
            {
                Assert.IsNotNull(texture);
                Assert.Greater(texture.width, 0);
                Assert.Greater(texture.height, 0);
                Assert.Greater(texture.EncodeToPNG().Length, 0);
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }
        }
    }
}
