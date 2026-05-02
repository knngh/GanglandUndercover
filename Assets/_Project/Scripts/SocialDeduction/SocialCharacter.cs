using UnityEngine;

namespace GanglandUndercover.SocialDeduction
{
    public sealed class SocialCharacter : MonoBehaviour
    {
        private static readonly Color GangColor = new Color(0.55f, 0.12f, 0.1f, 1f);
        private static readonly Color PoliceColor = new Color(0.1f, 0.28f, 0.62f, 1f);
        private static readonly Color UndercoverColor = new Color(0.88f, 0.66f, 0.22f, 1f);
        private static readonly Color DeadColor = new Color(0.12f, 0.12f, 0.12f, 1f);

        private MeshRenderer meshRenderer;
        private TextMesh label;

        public string CharacterName { get; private set; }
        public SocialRole Role { get; private set; }
        public bool IsPlayer { get; private set; }
        public bool IsAlive { get; private set; } = true;
        public Vector2 BotDirection { get; set; }
        public float BotDecisionTimer { get; set; }

        public void Bind(string characterName, SocialRole role, bool isPlayer)
        {
            CharacterName = characterName;
            Role = role;
            IsPlayer = isPlayer;
            IsAlive = true;
            meshRenderer = GetComponent<MeshRenderer>();
            label = GetComponentInChildren<TextMesh>();
            RefreshVisual();
        }

        public void Kill()
        {
            IsAlive = false;
            RefreshVisual();
        }

        public void RefreshVisual()
        {
            if (meshRenderer != null)
            {
                meshRenderer.material.color = IsAlive ? GetRoleColor(Role) : DeadColor;
            }

            if (label != null)
            {
                label.text = CharacterName;
                label.color = IsPlayer ? new Color(1f, 0.9f, 0.35f, 1f) : Color.white;
            }
        }

        private static Color GetRoleColor(SocialRole role)
        {
            switch (role)
            {
                case SocialRole.Gang:
                    return GangColor;
                case SocialRole.Police:
                    return PoliceColor;
                default:
                    return UndercoverColor;
            }
        }
    }
}
