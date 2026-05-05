using GanglandUndercover.Core;
using GanglandUndercover.Gameplay;
using UnityEngine;

namespace GanglandUndercover.World
{
    public sealed class DistrictNode : MonoBehaviour
    {
        private static readonly Color GangColor = new Color(0.54f, 0.15f, 0.1f, 1f);
        private static readonly Color PoliceColor = new Color(0.1f, 0.25f, 0.55f, 1f);
        private static readonly Color ContestedColor = new Color(0.48f, 0.39f, 0.2f, 1f);
        private static readonly Color SelectedColor = new Color(0.95f, 0.78f, 0.33f, 1f);

        private GameController controller;
        private DistrictState district;
        private DistrictType districtType;
        private MeshRenderer meshRenderer;
        private TextMesh label;
        private Material material;

        public void Bind(GameController gameController, DistrictState districtState, TextMesh nodeLabel)
        {
            controller = gameController;
            district = districtState;
            districtType = districtState.Type;
            label = nodeLabel;
            meshRenderer = GetComponent<MeshRenderer>();
            material = CreateMaterial();
            meshRenderer.material = material;
            Refresh();
        }

        public void Refresh()
        {
            if (controller == null || material == null)
            {
                return;
            }

            district = controller.State.GetDistrict(districtType);
            Color controllerColor = GetControllerColor(district.Controller);
            bool isSelected = controller != null && controller.SelectedDistrict == district.Type;
            material.color = isSelected ? Color.Lerp(controllerColor, SelectedColor, 0.45f) : controllerColor;
            transform.localScale = isSelected ? new Vector3(1.35f, 1.35f, 0.25f) : new Vector3(1.12f, 1.12f, 0.2f);

            if (label != null)
            {
                label.text = district.DisplayName + "\nG " + district.GangInfluence + " / P " + district.PolicePresence;
                label.color = isSelected ? SelectedColor : new Color(0.94f, 0.9f, 0.78f, 1f);
            }
        }

        private void OnMouseDown()
        {
            if (controller != null)
            {
                controller.SelectDistrict(districtType);
            }
        }

        private static Color GetControllerColor(Faction faction)
        {
            switch (faction)
            {
                case Faction.Gang:
                    return GangColor;
                case Faction.Police:
                    return PoliceColor;
                default:
                    return ContestedColor;
            }
        }

        private static Material CreateMaterial()
        {
            Shader shader = Shader.Find("Unlit/Color");

            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            return new Material(shader);
        }
    }
}
