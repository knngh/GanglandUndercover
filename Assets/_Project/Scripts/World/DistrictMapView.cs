using System.Collections.Generic;
using GanglandUndercover.Core;
using GanglandUndercover.Gameplay;
using UnityEngine;

namespace GanglandUndercover.World
{
    public sealed class DistrictMapView : MonoBehaviour
    {
        private readonly List<DistrictNode> nodes = new List<DistrictNode>();
        private GameController controller;

        public void Bind(GameController gameController)
        {
            controller = gameController;
            controller.Changed += Refresh;
            Build();
            Refresh();
        }

        private void OnDestroy()
        {
            if (controller != null)
            {
                controller.Changed -= Refresh;
            }
        }

        private void Build()
        {
            Vector3 dockyard = new Vector3(-2.8f, 1.2f, 0f);
            Vector3 warehouse = new Vector3(-1.3f, 0.2f, 0f);
            Vector3 market = new Vector3(0.2f, 1.4f, 0f);
            Vector3 precinct = new Vector3(2.2f, 1f, 0f);
            Vector3 clinic = new Vector3(1.7f, -1.35f, 0f);
            Vector3 tenement = new Vector3(-0.5f, -1.7f, 0f);

            CreateLink(dockyard, warehouse);
            CreateLink(warehouse, market);
            CreateLink(market, precinct);
            CreateLink(warehouse, tenement);
            CreateLink(tenement, clinic);
            CreateLink(clinic, precinct);
            CreateLink(market, clinic);

            CreateNode(DistrictType.Dockyard, dockyard);
            CreateNode(DistrictType.WarehouseRow, warehouse);
            CreateNode(DistrictType.NightMarket, market);
            CreateNode(DistrictType.PolicePrecinct, precinct);
            CreateNode(DistrictType.Clinic, clinic);
            CreateNode(DistrictType.TenementBlock, tenement);
        }

        private void CreateNode(DistrictType type, Vector3 position)
        {
            DistrictState district = controller.State.GetDistrict(type);
            GameObject nodeObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            nodeObject.name = district.DisplayName + " Node";
            nodeObject.transform.SetParent(transform, false);
            nodeObject.transform.localPosition = position;

            TextMesh label = CreateLabel(nodeObject.transform);
            DistrictNode node = nodeObject.AddComponent<DistrictNode>();
            node.Bind(controller, district, label);
            nodes.Add(node);
        }

        private void CreateLink(Vector3 start, Vector3 end)
        {
            GameObject linkObject = new GameObject("District Link");
            linkObject.transform.SetParent(transform, false);

            LineRenderer line = linkObject.AddComponent<LineRenderer>();
            line.positionCount = 2;
            line.useWorldSpace = false;
            line.SetPosition(0, start + new Vector3(0f, 0f, 0.2f));
            line.SetPosition(1, end + new Vector3(0f, 0f, 0.2f));
            line.startWidth = 0.05f;
            line.endWidth = 0.05f;
            line.material = CreateLineMaterial();
            line.startColor = new Color(0.52f, 0.47f, 0.34f, 0.75f);
            line.endColor = new Color(0.52f, 0.47f, 0.34f, 0.75f);
        }

        private static Material CreateLineMaterial()
        {
            Shader shader = Shader.Find("Sprites/Default");

            if (shader == null)
            {
                shader = Shader.Find("Unlit/Color");
            }

            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            return new Material(shader);
        }

        private static TextMesh CreateLabel(Transform parent)
        {
            GameObject labelObject = new GameObject("Label");
            labelObject.transform.SetParent(parent, false);
            labelObject.transform.localPosition = new Vector3(0f, 0f, -0.22f);

            TextMesh label = labelObject.AddComponent<TextMesh>();
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.characterSize = 0.18f;
            label.fontSize = 48;
            return label;
        }

        private void Refresh()
        {
            foreach (DistrictNode node in nodes)
            {
                node.Refresh();
            }
        }
    }
}
