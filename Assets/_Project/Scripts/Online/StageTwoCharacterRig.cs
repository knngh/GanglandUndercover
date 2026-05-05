using UnityEngine;

namespace GanglandUndercover.Online
{
    public enum StageTwoCharacterVisualState
    {
        Idle,
        Walk,
        Interact,
        Downed,
        Report,
        Meeting,
        Vote
    }

    public sealed class StageTwoCharacterRig : MonoBehaviour
    {
        public const string RigCatalogAssetPath = "Assets/_Project/Resources/Stage2/Characters/Stage2CharacterRigCatalog.asset";
        public const string RigCatalogResourcePath = "Stage2/Characters/Stage2CharacterRigCatalog";

        [SerializeField] private string rigKey = "runtime";
        [SerializeField] private string sourcePrefabPath = string.Empty;
        [SerializeField] private StageTwoCharacterVisualState currentState = StageTwoCharacterVisualState.Idle;

        public Transform BodyRoot;
        public Transform HeadRoot;
        public Transform LeftArm;
        public Transform RightArm;
        public Transform LeftFoot;
        public Transform RightFoot;
        public Transform StateRoot;

        private StageTwoCharacterVisualState lastAppliedState = (StageTwoCharacterVisualState)(-1);

        public string RigKey => rigKey;
        public string SourcePrefabPath => sourcePrefabPath;
        public StageTwoCharacterVisualState CurrentState => currentState;
        public bool HasStateCatalog => StageTwoCharacterRigCatalog.LoadDefault() != null;

        public bool HasRequiredRuntimeSlots =>
            BodyRoot != null
            && HeadRoot != null
            && LeftArm != null
            && RightArm != null
            && LeftFoot != null
            && RightFoot != null
            && StateRoot != null;

        public void Configure(string key, string sourcePath)
        {
            rigKey = string.IsNullOrWhiteSpace(key) ? "runtime" : key;
            sourcePrefabPath = sourcePath ?? string.Empty;
        }

        public void ApplyRuntimeState(bool alive, bool moving, bool interacting, bool reportNearby, bool inMeeting, bool hasVoted)
        {
            StageTwoCharacterVisualState nextState = StageTwoCharacterVisualState.Idle;

            if (!alive)
            {
                nextState = StageTwoCharacterVisualState.Downed;
            }
            else if (inMeeting && hasVoted)
            {
                nextState = StageTwoCharacterVisualState.Vote;
            }
            else if (inMeeting)
            {
                nextState = StageTwoCharacterVisualState.Meeting;
            }
            else if (reportNearby)
            {
                nextState = StageTwoCharacterVisualState.Report;
            }
            else if (interacting)
            {
                nextState = StageTwoCharacterVisualState.Interact;
            }
            else if (moving)
            {
                nextState = StageTwoCharacterVisualState.Walk;
            }

            ApplyState(nextState);
        }

        public void ApplyState(StageTwoCharacterVisualState state)
        {
            currentState = state;

            if (state == lastAppliedState)
            {
                return;
            }

            lastAppliedState = state;

            ApplyPose(state);
        }

        private void ApplyPose(StageTwoCharacterVisualState state)
        {
            StageTwoCharacterRigCatalog catalog = StageTwoCharacterRigCatalog.LoadDefault();
            StageTwoCharacterPose pose = catalog != null ? catalog.GetPose(state) : StageTwoCharacterPose.DefaultFor(state);

            if (BodyRoot != null)
            {
                BodyRoot.localPosition = new Vector3(0f, pose.BodyYOffset, BodyRoot.localPosition.z);
                BodyRoot.localRotation = Quaternion.Euler(BodyRoot.localEulerAngles.x, BodyRoot.localEulerAngles.y, pose.BodyZRotation);
            }

            if (HeadRoot != null)
            {
                HeadRoot.localPosition = new Vector3(HeadRoot.localPosition.x, pose.HeadYOffset, HeadRoot.localPosition.z);
            }

            if (LeftArm != null)
            {
                LeftArm.localRotation = Quaternion.Euler(LeftArm.localEulerAngles.x, LeftArm.localEulerAngles.y, pose.LeftArmZRotation);
            }

            if (RightArm != null)
            {
                RightArm.localRotation = Quaternion.Euler(RightArm.localEulerAngles.x, RightArm.localEulerAngles.y, pose.RightArmZRotation);
            }
        }

        public static string StateName(StageTwoCharacterVisualState state)
        {
            switch (state)
            {
                case StageTwoCharacterVisualState.Walk:
                    return "Walk";
                case StageTwoCharacterVisualState.Interact:
                    return "Interact";
                case StageTwoCharacterVisualState.Downed:
                    return "Downed";
                case StageTwoCharacterVisualState.Report:
                    return "Report";
                case StageTwoCharacterVisualState.Meeting:
                    return "Meeting";
                case StageTwoCharacterVisualState.Vote:
                    return "Vote";
                default:
                    return "Idle";
            }
        }
    }

    [CreateAssetMenu(menuName = "Gangland/Stage2 Character Rig Catalog")]
    public sealed class StageTwoCharacterRigCatalog : ScriptableObject
    {
        public StageTwoCharacterPose[] Poses;

        public StageTwoCharacterPose GetPose(StageTwoCharacterVisualState state)
        {
            if (Poses != null)
            {
                for (int i = 0; i < Poses.Length; i++)
                {
                    if (Poses[i].State == state)
                    {
                        return Poses[i];
                    }
                }
            }

            return StageTwoCharacterPose.DefaultFor(state);
        }

        public static StageTwoCharacterRigCatalog LoadDefault()
        {
            return Resources.Load<StageTwoCharacterRigCatalog>(StageTwoCharacterRig.RigCatalogResourcePath);
        }
    }

    [System.Serializable]
    public struct StageTwoCharacterPose
    {
        public StageTwoCharacterVisualState State;
        public float BodyYOffset;
        public float HeadYOffset;
        public float BodyZRotation;
        public float LeftArmZRotation;
        public float RightArmZRotation;

        public static StageTwoCharacterPose DefaultFor(StageTwoCharacterVisualState state)
        {
            switch (state)
            {
                case StageTwoCharacterVisualState.Walk:
                    return new StageTwoCharacterPose { State = state, BodyYOffset = -0.03f, HeadYOffset = 0.23f, BodyZRotation = -5f, LeftArmZRotation = 22f, RightArmZRotation = -22f };
                case StageTwoCharacterVisualState.Interact:
                    return new StageTwoCharacterPose { State = state, BodyYOffset = -0.04f, HeadYOffset = 0.22f, BodyZRotation = -4f, LeftArmZRotation = 4f, RightArmZRotation = -46f };
                case StageTwoCharacterVisualState.Downed:
                    return new StageTwoCharacterPose { State = state, BodyYOffset = -0.22f, HeadYOffset = -0.18f, BodyZRotation = 82f, LeftArmZRotation = 74f, RightArmZRotation = 100f };
                case StageTwoCharacterVisualState.Report:
                    return new StageTwoCharacterPose { State = state, BodyYOffset = -0.06f, HeadYOffset = 0.26f, BodyZRotation = 0f, LeftArmZRotation = -24f, RightArmZRotation = -62f };
                case StageTwoCharacterVisualState.Meeting:
                    return new StageTwoCharacterPose { State = state, BodyYOffset = -0.12f, HeadYOffset = 0.2f, BodyZRotation = 0f, LeftArmZRotation = 8f, RightArmZRotation = -8f };
                case StageTwoCharacterVisualState.Vote:
                    return new StageTwoCharacterPose { State = state, BodyYOffset = -0.1f, HeadYOffset = 0.22f, BodyZRotation = 0f, LeftArmZRotation = -18f, RightArmZRotation = -36f };
                default:
                    return new StageTwoCharacterPose { State = state, BodyYOffset = -0.08f, HeadYOffset = 0.2f, BodyZRotation = 0f, LeftArmZRotation = 12f, RightArmZRotation = -12f };
            }
        }
    }
}
