using UnityEngine;

namespace GanglandUndercover.Online
{
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
}
