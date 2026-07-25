using NUnit.Framework;
using UnityEngine;
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
                Assert.AreEqual(effect.ExpectedFrameCount, effect.FrameCount, effect.Name + " frame count.");
                Assert.AreEqual(effect.ExpectedWidth, effect.Width, effect.Name + " width.");
                Assert.AreEqual(effect.ExpectedHeight, effect.Height, effect.Name + " height.");
            }

            string markdown = VfxMotionPreviewReport.ToMarkdown(summary, VfxMotionPreviewReport.DefaultContactSheetPath);
            StringAssert.Contains("blackout", markdown);
            StringAssert.Contains("comms_jam", markdown);
            StringAssert.Contains("door_lock", markdown);
            StringAssert.Contains("kill", markdown);
            StringAssert.Contains(VfxMotionPreviewReport.DefaultContactSheetPath, markdown);
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
    }
}
