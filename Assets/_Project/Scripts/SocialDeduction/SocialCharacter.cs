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
        private Color visualColor;
        private Material material;

        public string CharacterName { get; private set; }
        public SocialRole Role { get; private set; }
        public bool IsPlayer { get; private set; }
        public bool IsAlive { get; private set; } = true;
        public Vector2 BotDirection { get; set; }
        public Vector3 BotTarget { get; set; }
        public float BotDecisionTimer { get; set; }
        public float BotActionCooldown { get; set; }
        public bool HasBotTarget { get; set; }

        public void Bind(string characterName, SocialRole role, bool isPlayer)
        {
            CharacterName = characterName;
            Role = role;
            IsPlayer = isPlayer;
            IsAlive = true;
            visualColor = isPlayer ? GetPlayerColor(role) : GetCivilianColor(characterName);
            meshRenderer = GetComponent<MeshRenderer>();
            material = new Material(FindColorShader());
            meshRenderer.sharedMaterial = material;
            label = GetComponentInChildren<TextMesh>();
            RefreshVisual();
        }

        public void Kill()
        {
            IsAlive = false;
            HasBotTarget = false;
            RefreshVisual();
        }

        public void RefreshVisual()
        {
            if (material != null)
            {
                material.color = IsAlive ? visualColor : DeadColor;
            }

            if (label != null)
            {
                label.text = CharacterName;
                label.color = IsPlayer ? new Color(1f, 0.9f, 0.35f, 1f) : Color.white;
            }
        }

        private static Color GetPlayerColor(SocialRole role)
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

        private static Color GetCivilianColor(string characterName)
        {
            switch (characterName)
            {
                case "巡警陈":
                    return new Color(0.18f, 0.42f, 0.68f, 1f);
                case "技侦周":
                    return new Color(0.32f, 0.48f, 0.42f, 1f);
                case "线人林":
                    return new Color(0.74f, 0.54f, 0.22f, 1f);
                case "疤脸":
                    return new Color(0.52f, 0.24f, 0.42f, 1f);
                default:
                    return new Color(0.52f, 0.52f, 0.48f, 1f);
            }
        }

        private static Shader FindColorShader()
        {
            return Shader.Find("Universal Render Pipeline/Lit")
                ?? Shader.Find("Standard")
                ?? Shader.Find("Unlit/Color")
                ?? Shader.Find("Sprites/Default");
        }

        private void OnDestroy()
        {
            if (material == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(material);
            }
            else
            {
                DestroyImmediate(material);
            }
        }
    }
}
