using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace GanglandUndercover.Editor
{
    /// <summary>
    /// Stage 2: 一键创建 GanglandUndercover 角色 AnimatorController。
    /// 菜单：Gangland > Setup Character Animator Controller
    /// </summary>
    public static class StageTwoCharacterAnimationSetup
    {
        private const string ControllerPath = "Assets/_Project/Art/Animators/GanglandCharacter.controller";
        private const string OverrideControllerPath = "Assets/_Project/Art/Animators/GanglandCharacter_Override.controller";

        private static readonly (string name, string guid, float defaultSpeed)[] MaleClips =
        {
            ("idle",        "40d3a309d10a3904db79282f9b6d90e3", 0f),
            ("walk",        "6599ecd6d50f5cd488a2a18812b7174c", 0.5f),
            ("jog",         "66fd17140cb1f434599cd0c4b33ab1aa", 1f),
            ("phoneTalk",   "3975160a5f0e2cd47b1bc61842d9c278", 0f),
        };

        private static readonly (string name, string guid, float defaultSpeed)[] FemaleClips =
        {
            ("idle",        "0cac3c50a88d6a74782b4161b4b42dc1", 0f),
            ("walk",        "a9ce97153f7113649bd17bbb3de7a81e", 0.5f),
            ("jog",         "89b462e947c35b8458ea34cd26887038", 1f),
        };

        private static readonly int SpeedParam = Animator.StringToHash("Speed");
        private static readonly int DeadParam  = Animator.StringToHash("Dead");
        private static readonly int ActionParam = Animator.StringToHash("Action");

        [MenuItem("Gangland/Setup Character Animator Controller")]
        public static void CreateAnimatorController()
        {
            EnsureDirectory(ControllerPath);

            // 1. Create main controller
            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            controller.name = "GanglandCharacter";

            AnimatorControllerLayer baseLayer = controller.layers[0];
            AnimatorStateMachine sm = baseLayer.stateMachine;
            sm.entryPosition = new Vector3(200, 200, 0);
            sm.anyStatePosition = new Vector3(50, 0, 0);

            // 2. Add parameters
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("Dead",  AnimatorControllerParameterType.Bool);
            controller.AddParameter("Action", AnimatorControllerParameterType.Trigger);

            // 3. Create states
            AnimatorState idleState   = sm.AddState("Idle",   new Vector3(300, 200, 0));
            AnimatorState walkState   = sm.AddState("Walk",   new Vector3(300, 100, 0));
            AnimatorState jogState    = sm.AddState("Jog",    new Vector3(300, 0, 0));
            AnimatorState deadState   = sm.AddState("Dead",   new Vector3(500, 100, 0));
            AnimatorState actionState = sm.AddState("Action", new Vector3(500, 200, 0));

            sm.defaultState = idleState;

            // 4. Assign motion clips (try male first, editor can override)
            AssignMotion(idleState,   MaleClips[0].guid);
            AssignMotion(walkState,   MaleClips[1].guid);
            AssignMotion(jogState,    MaleClips[2].guid);
            AssignMotion(actionState, MaleClips[3].guid);

            // Dead is a no-motion state (empty clip or use idle)
            deadState.writeDefaultValues = false;

            // 5. Transitions
            // Idle → Walk: Speed > 0.1
            AnimatorStateTransition idleToWalk = idleState.AddTransition(walkState);
            idleToWalk.hasExitTime = false;
            idleToWalk.exitTime = 0f;
            idleToWalk.duration = 0.15f;
            idleToWalk.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");

            // Walk → Idle: Speed ≤ 0.1
            AnimatorStateTransition walkToIdle = walkState.AddTransition(idleState);
            walkToIdle.hasExitTime = false;
            walkToIdle.exitTime = 0f;
            walkToIdle.duration = 0.2f;
            walkToIdle.AddCondition(AnimatorConditionMode.Less, 0.05f, "Speed");

            // Walk → Jog: Speed > 0.7
            AnimatorStateTransition walkToJog = walkState.AddTransition(jogState);
            walkToJog.hasExitTime = false;
            walkToJog.exitTime = 0f;
            walkToJog.duration = 0.12f;
            walkToJog.AddCondition(AnimatorConditionMode.Greater, 0.7f, "Speed");

            // Jog → Walk: Speed ≤ 0.7
            AnimatorStateTransition jogToWalk = jogState.AddTransition(walkState);
            jogToWalk.hasExitTime = false;
            jogToWalk.exitTime = 0f;
            jogToWalk.duration = 0.15f;
            jogToWalk.AddCondition(AnimatorConditionMode.Less, 0.7f, "Speed");

            // Idle → Jog: Speed > 0.7 (direct for fast start)
            AnimatorStateTransition idleToJog = idleState.AddTransition(jogState);
            idleToJog.hasExitTime = false;
            idleToJog.exitTime = 0f;
            idleToJog.duration = 0.1f;
            idleToJog.AddCondition(AnimatorConditionMode.Greater, 0.7f, "Speed");

            // AnyState → Dead: Dead == true
            AnimatorStateTransition anyToDead = sm.AddAnyStateTransition(deadState);
            anyToDead.hasExitTime = false;
            anyToDead.exitTime = 0f;
            anyToDead.duration = 0.08f;
            anyToDead.canTransitionToSelf = false;
            anyToDead.AddCondition(AnimatorConditionMode.If, 1f, "Dead");

            // Dead → Idle: Dead == false (revive / reset)
            AnimatorStateTransition deadToIdle = deadState.AddTransition(idleState);
            deadToIdle.hasExitTime = false;
            deadToIdle.exitTime = 0f;
            deadToIdle.duration = 0.2f;
            deadToIdle.AddCondition(AnimatorConditionMode.IfNot, 0f, "Dead");

            // AnyState → Action: Action trigger
            AnimatorStateTransition anyToAction = sm.AddAnyStateTransition(actionState);
            anyToAction.hasExitTime = false;
            anyToAction.exitTime = 0f;
            anyToAction.duration = 0.05f;
            anyToAction.canTransitionToSelf = false;
            anyToAction.AddCondition(AnimatorConditionMode.If, 0f, "Action");

            // Action → Idle: exit time
            actionState.AddTransition(idleState).hasExitTime = true;

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // 6. Create override controller
            AnimatorOverrideController overrideController =
                new AnimatorOverrideController(controller);
            AssetDatabase.CreateAsset(overrideController, OverrideControllerPath);
            AssetDatabase.SaveAssets();

            Debug.Log(
                $"[Gangland] AnimatorController created at {ControllerPath}\n" +
                $"[Gangland] OverrideController created at {OverrideControllerPath}\n" +
                "[Gangland] Parameters: Speed(Float), Dead(Bool), Action(Trigger)\n" +
                "[Gangland] States: Idle → Walk → Jog, AnyState→Dead, AnyState→Action");
        }

        private static void AssignMotion(AnimatorState state, string motionGuid)
        {
            string path = AssetDatabase.GUIDToAssetPath(motionGuid);
            if (string.IsNullOrEmpty(path))
            {
                Debug.LogWarning($"[Gangland] Motion GUID {motionGuid} not found for state '{state.name}'");
                return;
            }

            Motion clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip == null)
            {
                clip = AssetDatabase.LoadAssetAtPath<BlendTree>(path);
            }

            if (clip != null)
            {
                state.motion = clip;
            }
            else
            {
                Debug.LogWarning($"[Gangland] Could not load motion at {path} for state '{state.name}'");
            }
        }

        private static void EnsureDirectory(string assetPath)
        {
            string dir = System.IO.Path.GetDirectoryName(assetPath);
            if (!string.IsNullOrEmpty(dir) && !AssetDatabase.IsValidFolder(dir))
            {
                string parent = System.IO.Path.GetDirectoryName(dir);
                string folder = System.IO.Path.GetFileName(dir);
                if (!string.IsNullOrEmpty(parent))
                {
                    AssetDatabase.CreateFolder(parent, folder);
                }
            }
        }
    }
}
