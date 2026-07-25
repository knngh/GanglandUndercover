using NUnit.Framework;
using GanglandUndercover.Online;

namespace GanglandUndercover.Tests
{
    /// <summary>
    /// OnlineMatchUtils 静态数据表测试。
    /// 验证任务/破坏/证据等核心数值表的正确性与一致性。
    /// </summary>
    [TestFixture]
    public class OnlineMatchUtilsTests
    {
        // ─── TaskRequiredProgress ──────────────────────────────

        [Test]
        public void TaskRequiredProgress_ReturnsThree_ForAllTaskIds()
        {
            for (int id = 0; id < 28; id++)
            {
                Assert.AreEqual(3, OnlineMatchUtils.TaskRequiredProgress(id),
                    $"TaskRequiredProgress({id}) should be 3");
            }
        }

        // ─── SabotageForTask ───────────────────────────────────

        [Test]
        public void SabotageForTask_MapsCorrectly()
        {
            Assert.AreEqual(SabotageType.None, OnlineMatchUtils.SabotageForTask(0));
            Assert.AreEqual(SabotageType.Blackout, OnlineMatchUtils.SabotageForTask(2));
            Assert.AreEqual(SabotageType.Blackout, OnlineMatchUtils.SabotageForTask(14));
            Assert.AreEqual(SabotageType.Lockdown, OnlineMatchUtils.SabotageForTask(7));
            Assert.AreEqual(SabotageType.Lockdown, OnlineMatchUtils.SabotageForTask(12));
            Assert.AreEqual(SabotageType.Communications, OnlineMatchUtils.SabotageForTask(6));
            Assert.AreEqual(SabotageType.Communications, OnlineMatchUtils.SabotageForTask(27));
            Assert.AreEqual(SabotageType.EvidenceLeak, OnlineMatchUtils.SabotageForTask(3));
            Assert.AreEqual(SabotageType.EvidenceLeak, OnlineMatchUtils.SabotageForTask(22));
            Assert.AreEqual(SabotageType.PatrolAlert, OnlineMatchUtils.SabotageForTask(4));
            Assert.AreEqual(SabotageType.PatrolAlert, OnlineMatchUtils.SabotageForTask(26));
        }

        // ─── SabotageEvidencePenalty ───────────────────────────

        [Test]
        public void SabotageEvidencePenalty_ReturnsExpectedValues()
        {
            foreach (SabotageType type in new[]
            {
                SabotageType.Blackout,
                SabotageType.Lockdown,
                SabotageType.Communications,
                SabotageType.EvidenceLeak,
            })
            {
                int penalty = OnlineMatchUtils.SabotageEvidencePenalty(type);
                Assert.Greater(penalty, 0, $"SabotageEvidencePenalty({type}) should be positive");
            }

            Assert.AreEqual(0, OnlineMatchUtils.SabotageEvidencePenalty(SabotageType.PatrolAlert));
        }

        // ─── TaskEvidenceValue ─────────────────────────────────

        [Test]
        public void TaskEvidenceValue_ReturnsPositive_ForAllTaskIds()
        {
            for (int id = 0; id < 28; id++)
            {
                int value = OnlineMatchUtils.TaskEvidenceValue(id);
                Assert.Greater(value, 0, $"TaskEvidenceValue({id}) should be positive");
            }
        }

        // ─── SabotageName ──────────────────────────────────────

        [Test]
        public void SabotageName_ReturnsNonEmpty_ForAllTypes()
        {
            foreach (SabotageType type in new[]
            {
                SabotageType.Blackout,
                SabotageType.Lockdown,
                SabotageType.Communications,
                SabotageType.EvidenceLeak,
                SabotageType.PatrolAlert,
            })
            {
                string name = OnlineMatchUtils.SabotageName(type);
                Assert.IsFalse(string.IsNullOrEmpty(name), $"SabotageName({type}) should not be empty");
            }
        }

        // ─── PhaseName ─────────────────────────────────────────

        [Test]
        public void PhaseName_ReturnsNonEmpty_ForAllPhases()
        {
            foreach (OnlineMatchPhase phase in new[]
            {
                OnlineMatchPhase.Lobby,
                OnlineMatchPhase.Opening,
                OnlineMatchPhase.Action,
                OnlineMatchPhase.Meeting,
                OnlineMatchPhase.Voting,
                OnlineMatchPhase.Result,
            })
            {
                string name = OnlineMatchUtils.PhaseName(phase);
                Assert.IsFalse(string.IsNullOrEmpty(name), $"PhaseName({phase}) should not be empty");
            }
        }

        // ─── RoleName ──────────────────────────────────────────

        [Test]
        public void RoleName_ReturnsNonEmpty_ForAllRoles()
        {
            foreach (OnlineRole role in new[]
            {
                OnlineRole.Unassigned, OnlineRole.Police, OnlineRole.Gang,
                OnlineRole.Undercover, OnlineRole.Mole
            })
            {
                string name = OnlineMatchUtils.RoleName(role);
                Assert.IsFalse(string.IsNullOrEmpty(name), $"RoleName({role}) should not be empty");
            }
        }
    }
}
