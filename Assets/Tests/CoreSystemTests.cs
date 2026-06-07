using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using UnityEngine;

namespace GanglandUndercover.Tests
{
    /// <summary>
    /// M1-M4 regression tests. The project runtime still lives in Unity's
    /// predefined Assembly-CSharp assembly, so this test asmdef accesses it
    /// through reflection instead of creating a large runtime asmdef migration.
    /// </summary>
    public class CoreSystemTests
    {
        private const string RuntimeAssemblyName = "Assembly-CSharp";
        private static readonly Dictionary<ulong, int> RoleLookup = new Dictionary<ulong, int>();

        [Test]
        public void EmergencyMeetingLimit_ClampsWithinRange()
        {
            object ruleSet = CreateRuleSet();

            Assert.AreEqual(1, InvokeInt(ruleSet, "EmergencyMeetingLimitFor", 3));
            Assert.AreEqual(2, InvokeInt(ruleSet, "EmergencyMeetingLimitFor", 6));
            Assert.AreEqual(3, InvokeInt(ruleSet, "EmergencyMeetingLimitFor", 9));
            Assert.AreEqual(3, InvokeInt(ruleSet, "EmergencyMeetingLimitFor", 15));
        }

        [Test]
        public void EmergencyMeetingLimit_FloorIsOne()
        {
            object ruleSet = CreateRuleSet();

            Assert.AreEqual(1, InvokeInt(ruleSet, "EmergencyMeetingLimitFor", 1));
            Assert.AreEqual(1, InvokeInt(ruleSet, "EmergencyMeetingLimitFor", 2));
        }

        [Test]
        public void RoleDistribution_5Players_Returns1Gang1Undercover()
        {
            object dist = GetRoleDistribution(5);

            Assert.AreEqual(1, FieldInt(dist, "gang"));
            Assert.AreEqual(1, FieldInt(dist, "undercover"));
            Assert.AreEqual(0, FieldInt(dist, "mole"));
            Assert.AreEqual(3, PropertyInt(dist, "PoliceCount"));
        }

        [Test]
        public void RoleDistribution_8Players_Returns2Gang1Undercover1Mole()
        {
            object dist = GetRoleDistribution(8);

            Assert.AreEqual(2, FieldInt(dist, "gang"));
            Assert.AreEqual(1, FieldInt(dist, "undercover"));
            Assert.AreEqual(1, FieldInt(dist, "mole"));
            Assert.AreEqual(4, PropertyInt(dist, "PoliceCount"));
        }

        [Test]
        public void RoleDistribution_10Players_Returns3Gang2Undercover1Mole()
        {
            object dist = GetRoleDistribution(10);

            Assert.AreEqual(3, FieldInt(dist, "gang"));
            Assert.AreEqual(2, FieldInt(dist, "undercover"));
            Assert.AreEqual(1, FieldInt(dist, "mole"));
            Assert.AreEqual(4, PropertyInt(dist, "PoliceCount"));
        }

        [Test]
        public void RoleDistribution_OutOfRange_UsesNearestPreset()
        {
            object low = GetRoleDistribution(4);
            object high = GetRoleDistribution(12);

            Assert.AreEqual(1, FieldInt(low, "gang"));
            Assert.AreEqual(1, FieldInt(low, "undercover"));
            Assert.AreEqual(3, FieldInt(high, "gang"));
            Assert.AreEqual(2, FieldInt(high, "undercover"));
        }

        [Test]
        public void TotalTaskCount_AndEvidenceTarget_ScaleByPlayerCount()
        {
            object ruleSet = CreateRuleSet();

            Assert.AreEqual(24, InvokeInt(ruleSet, "TotalTaskCount", 8, 2));
            Assert.AreEqual(44, InvokeInt(ruleSet, "ScaledEvidenceTarget", 8));
            Assert.AreEqual(34, InvokeInt(ruleSet, "ScaledEvidenceTarget", 5));
        }

        [Test]
        public void TimeLimit_NotReached_ReturnsFalse()
        {
            object bridge = CreateVictoryBridge();
            Array tasks = MakeTasks((true, false), (false, false));

            bool hasResult = TryTimeLimit(bridge, 100f, 300f, 5, 10, tasks, out string result);

            Assert.IsFalse(hasResult);
            Assert.IsEmpty(result);
        }

        [Test]
        public void TimeLimit_EvidenceHigh_PoliceWins()
        {
            object bridge = CreateVictoryBridge();
            Array tasks = MakeTasks((true, false));

            bool hasResult = TryTimeLimit(bridge, 350f, 300f, 9, 10, tasks, out string result);

            Assert.IsTrue(hasResult);
            StringAssert.Contains("警方胜利", result);
        }

        [Test]
        public void TimeLimit_EvidenceLow_GangWins()
        {
            object bridge = CreateVictoryBridge();
            Array tasks = MakeTasks((false, false), (false, false));

            bool hasResult = TryTimeLimit(bridge, 350f, 300f, 2, 10, tasks, out string result);

            Assert.IsTrue(hasResult);
            StringAssert.Contains("黑帮胜利", result);
        }

        [Test]
        public void TimeLimit_TasksHigh_PoliceWins()
        {
            object bridge = CreateVictoryBridge();
            Array tasks = MakeTasks((true, false), (true, false));

            bool hasResult = TryTimeLimit(bridge, 350f, 300f, 3, 10, tasks, out string result);

            Assert.IsTrue(hasResult);
            StringAssert.Contains("警方胜利", result);
        }

        [Test]
        public void DetermineChannel_DeadPlayer_Ghost()
        {
            AssertChannel("Action", false, "Ghost");
            AssertChannel("Meeting", false, "Ghost");
            AssertChannel("Voting", false, "Ghost");
            AssertChannel("Lobby", false, "Ghost");
        }

        [Test]
        public void DetermineChannel_AliveMeetingAndAction()
        {
            AssertChannel("Meeting", true, "Meeting");
            AssertChannel("Voting", true, "Meeting");
            AssertChannel("Action", true, "Proximity");
            AssertChannel("Lobby", true, "Proximity");
        }

        [Test]
        public void Sanitize_RemovesTagsAndTruncates()
        {
            Assert.AreEqual("hello", Sanitize("<b>hello</b>"));
            Assert.AreEqual("text", Sanitize("<script>alert('xss')</script>text"));
            Assert.AreEqual(256, Sanitize(new string('a', 600)).Length);
            Assert.AreEqual("Report body at Dockyard", Sanitize("Report body at Dockyard"));
            Assert.IsNull(Sanitize(null));
            Assert.AreEqual(string.Empty, Sanitize(string.Empty));
        }

        [Test]
        public void Victory_EvidenceClosure_PoliceWins()
        {
            object result = EvaluateVictory(
                evidenceScore: 50,
                evidenceTarget: 44,
                players: MakePlayers((2, "Gang"), (4, "Police"), (1, "Undercover"), (1, "Mole")),
                tasks: MakeTasks(),
                matchStarted: true,
                phaseName: "Action");

            AssertResult(result, true, "警方胜利");
        }

        [Test]
        public void Victory_AllGangDead_PoliceWins()
        {
            object result = EvaluateVictory(
                evidenceScore: 10,
                evidenceTarget: 44,
                players: MakePlayers((0, "Gang"), (3, "Police"), (0, "Undercover"), (0, "Mole")),
                tasks: MakeTasks(),
                matchStarted: true,
                phaseName: "Action");

            AssertResult(result, true, "警方胜利");
        }

        [Test]
        public void Victory_GangOutnumber_GangWins()
        {
            object result = EvaluateVictory(
                evidenceScore: 10,
                evidenceTarget: 44,
                players: MakePlayers((3, "Gang"), (2, "Police"), (1, "Undercover"), (0, "Mole")),
                tasks: MakeTasks(),
                matchStarted: true,
                phaseName: "Action");

            AssertResult(result, true, "黑帮胜利");
        }

        [Test]
        public void Victory_NoChange_WhenBalanced()
        {
            object result = EvaluateVictory(
                evidenceScore: 10,
                evidenceTarget: 44,
                players: MakePlayers((2, "Gang"), (4, "Police"), (1, "Undercover"), (1, "Mole")),
                tasks: MakeTasks(),
                matchStarted: true,
                phaseName: "Action");

            Assert.IsFalse(PropertyBool(result, "HasResult"));
        }

        [Test]
        public void Victory_UndercoverSoloWins()
        {
            object result = EvaluateVictory(
                evidenceScore: 5,
                evidenceTarget: 44,
                players: MakePlayers((0, "Gang"), (0, "Police"), (1, "Undercover"), (0, "Mole")),
                tasks: MakeTasks(),
                matchStarted: true,
                phaseName: "Action");

            AssertResult(result, true, "卧底胜利");
        }

        [Test]
        public void Victory_NotStarted_ReturnsNoChange()
        {
            object result = EvaluateVictory(
                evidenceScore: 100,
                evidenceTarget: 44,
                players: MakePlayers((2, "Gang"), (4, "Police"), (1, "Undercover"), (1, "Mole")),
                tasks: MakeTasks(),
                matchStarted: false,
                phaseName: "Lobby");

            Assert.IsFalse(PropertyBool(result, "HasResult"));
        }

        private static object CreateRuleSet()
        {
            return ScriptableObject.CreateInstance(RuntimeType("GanglandUndercover.Online.OnlineRuleSet"));
        }

        private static object CreateVictoryBridge()
        {
            return Activator.CreateInstance(RuntimeType("GanglandUndercover.Online.OnlineVictoryBridge"));
        }

        private static object GetRoleDistribution(int playerCount)
        {
            return Invoke(CreateRuleSet(), "GetRoleDistribution", playerCount);
        }

        private static bool TryTimeLimit(object bridge, float elapsed, float limit, int evidence, int target, Array tasks, out string result)
        {
            object[] args = { elapsed, limit, evidence, target, tasks, string.Empty };
            bool hasResult = (bool)Invoke(bridge, "TryTimeLimitEvaluation", args);
            result = (string)args[5];
            return hasResult;
        }

        private static void AssertChannel(string phaseName, bool alive, string expected)
        {
            object phase = Enum.Parse(RuntimeType("GanglandUndercover.Online.OnlineMatchPhase"), phaseName);
            object channel = InvokeStatic(RuntimeType("GanglandUndercover.Online.ChatSystem"), "DetermineChannel", phase, alive);
            Assert.AreEqual(expected, channel.ToString());
        }

        private static string Sanitize(string input)
        {
            return (string)InvokeStatic(RuntimeType("GanglandUndercover.Online.ChatSystem"), "Sanitize", input);
        }

        private static object EvaluateVictory(int evidenceScore, int evidenceTarget, object players, Array tasks, bool matchStarted, string phaseName)
        {
            object bridge = CreateVictoryBridge();
            Type roleType = RuntimeType("GanglandUndercover.Online.OnlineRole");
            object phase = Enum.Parse(RuntimeType("GanglandUndercover.Online.OnlineMatchPhase"), phaseName);

            return Invoke(bridge, "Evaluate",
                evidenceScore,
                evidenceTarget,
                players,
                MakeRoleResolver(),
                tasks,
                matchStarted,
                phase,
                Enum.Parse(roleType, "Police"));
        }

        private static object MakePlayers(params (int Count, string RoleName)[] groups)
        {
            RoleLookup.Clear();

            Type stateType = RuntimeType("GanglandUndercover.Online.OnlinePlayerState");
            Type roleType = RuntimeType("GanglandUndercover.Online.OnlineRole");
            Type professionType = RuntimeType("GanglandUndercover.Online.OnlineProfession");
            Type dictType = typeof(Dictionary<,>).MakeGenericType(typeof(ulong), stateType);
            object dict = Activator.CreateInstance(dictType);
            MethodInfo add = dictType.GetMethod("Add");
            ConstructorInfo stateCtor = stateType.GetConstructor(new[]
            {
                typeof(ulong), typeof(string), typeof(Vector3), typeof(bool), typeof(bool),
                roleType, professionType, typeof(int), typeof(bool)
            });

            ulong id = 0;
            foreach ((int count, string roleName) in groups)
            {
                for (int i = 0; i < count; i++)
                {
                    id++;
                    object role = Enum.Parse(roleType, roleName);
                    object profession = Enum.Parse(professionType, ProfessionFor(roleName));
                    object state = stateCtor.Invoke(new object[]
                    {
                        id, roleName + i, Vector3.zero, true, true, role, profession, 0, false
                    });

                    add.Invoke(dict, new object[] { id, state });
                    RoleLookup[id] = Convert.ToInt32(role);
                }
            }

            return dict;
        }

        private static string ProfessionFor(string roleName)
        {
            switch (roleName)
            {
                case "Gang": return "Enforcer";
                case "Undercover": return "UndercoverAgent";
                case "Mole": return "Enforcer";
                default: return "Inspector";
            }
        }

        private static Delegate MakeRoleResolver()
        {
            Type roleType = RuntimeType("GanglandUndercover.Online.OnlineRole");
            Type delegateType = typeof(Func<,>).MakeGenericType(typeof(ulong), roleType);
            ParameterExpression id = Expression.Parameter(typeof(ulong), "id");
            MethodInfo resolver = typeof(CoreSystemTests).GetMethod(nameof(ResolveRoleValue), BindingFlags.Static | BindingFlags.NonPublic);
            UnaryExpression body = Expression.Convert(Expression.Call(resolver, id), roleType);
            return Expression.Lambda(delegateType, body, id).Compile();
        }

        private static int ResolveRoleValue(ulong clientId)
        {
            return RoleLookup.TryGetValue(clientId, out int role) ? role : 1;
        }

        private static Array MakeTasks(params (bool Completed, bool Sabotaged)[] states)
        {
            Type taskType = RuntimeType("GanglandUndercover.Online.OnlineTaskState");
            Array array = Array.CreateInstance(taskType, states.Length);
            ConstructorInfo ctor = taskType.GetConstructor(new[]
            {
                typeof(int), typeof(string), typeof(Vector3), typeof(int), typeof(int), typeof(bool), typeof(bool)
            });

            for (int i = 0; i < states.Length; i++)
            {
                object task = ctor.Invoke(new object[]
                {
                    i, "Task" + i, Vector3.zero, states[i].Completed ? 1 : 0, 1, states[i].Completed, states[i].Sabotaged
                });
                array.SetValue(task, i);
            }

            return array;
        }

        private static void AssertResult(object result, bool hasResult, string expectedText)
        {
            Assert.AreEqual(hasResult, PropertyBool(result, "HasResult"));
            StringAssert.Contains(expectedText, PropertyString(result, "ResultText"));
        }

        private static int FieldInt(object target, string fieldName)
        {
            return Convert.ToInt32(target.GetType().GetField(fieldName).GetValue(target));
        }

        private static int PropertyInt(object target, string propertyName)
        {
            return Convert.ToInt32(target.GetType().GetProperty(propertyName).GetValue(target));
        }

        private static bool PropertyBool(object target, string propertyName)
        {
            return (bool)target.GetType().GetProperty(propertyName).GetValue(target);
        }

        private static string PropertyString(object target, string propertyName)
        {
            return (string)target.GetType().GetProperty(propertyName).GetValue(target);
        }

        private static int InvokeInt(object target, string methodName, params object[] args)
        {
            return Convert.ToInt32(Invoke(target, methodName, args));
        }

        private static object Invoke(object target, string methodName, params object[] args)
        {
            return target.GetType().GetMethod(methodName).Invoke(target, args);
        }

        private static object InvokeStatic(Type type, string methodName, params object[] args)
        {
            return type.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static).Invoke(null, args);
        }

        private static Type RuntimeType(string fullName)
        {
            return Type.GetType(fullName + ", " + RuntimeAssemblyName, throwOnError: true);
        }
    }
}
