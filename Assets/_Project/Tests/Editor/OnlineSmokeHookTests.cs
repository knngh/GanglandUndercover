using System.Collections.Generic;
using GanglandUndercover.Online;
using NUnit.Framework;
using UnityEngine;

namespace GanglandUndercover.Tests
{
    [TestFixture]
    public class OnlineSmokeHookTests
    {
        [Test]
        public void EditorTriggerTask_DoesNotMutateRosterOrEndMatch()
        {
            GameObject root = new GameObject("Online Smoke Hook Test");

            try
            {
                OnlineMatchController controller = root.AddComponent<OnlineMatchController>();
                controller.EditorSimulateLocalMatch();
                controller.EditorSkipOpeningForSmokeTest();
                controller.Players.Clear();

                controller.EditorTriggerTaskForSmokeTest(2, true);

                Assert.AreEqual(OnlineMatchPhase.Action, controller.Phase);
                Assert.IsTrue(controller.MatchStarted);
                Assert.AreEqual(0, controller.PlayerCount);
                Assert.Greater(controller.BlackoutTimer, 0f);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void EditorTriggerTask_AllowsNearbyPlayersWithoutMutatingRoster()
        {
            GameObject root = new GameObject("Online Nearby Sabotage Test");

            try
            {
                OnlineMatchController controller = root.AddComponent<OnlineMatchController>();
                controller.EditorSimulateLocalMatch();
                controller.EditorSkipOpeningForSmokeTest();
                Vector3 taskPosition = controller.MapService.TaskPositionFor(2);
                List<ulong> playerIds = new List<ulong>(controller.Players.Keys);

                foreach (ulong playerId in playerIds)
                {
                    OnlinePlayerState state = controller.Players[playerId];
                    state.Position = taskPosition;
                    controller.Players[playerId] = state;
                }

                int playerCount = controller.PlayerCount;
                controller.EditorTriggerTaskForSmokeTest(2, true);

                Assert.AreEqual(OnlineMatchPhase.Action, controller.Phase);
                Assert.AreEqual(playerCount, controller.PlayerCount);
                Assert.Greater(controller.BlackoutTimer, 0f);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
