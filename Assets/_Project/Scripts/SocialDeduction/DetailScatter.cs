using UnityEngine;
using System.Collections.Generic;

namespace GanglandUndercover.SocialDeduction
{
    /// <summary>
    /// 街头装饰散布系统
    /// 管理墙面海报、涂鸦、电线、空调外机、水坑、烟蒂碎屑等街头写实元素
    /// </summary>
    public class DetailScatter : MonoBehaviour
    {
        [Header("Posters & Graffiti")]
        [SerializeField] private DetailType posterType;
        [SerializeField] private int posterCount = 15;
        [SerializeField] private Vector2 posterSizeRange = new Vector2(0.5f, 1.5f);

        [Header("Graffiti")]
        [SerializeField] private int graffitiCount = 8;
        [SerializeField] private Vector2 graffitiSizeRange = new Vector2(0.8f, 2.5f);

        [Header("Power Lines")]
        [SerializeField] private int powerLineSegments = 20;
        [SerializeField] private float powerLineHeight = 6f;
        [SerializeField] private float powerLineSag = 0.5f;
        [SerializeField] private float powerLineThickness = 0.02f;

        [Header("Air Conditioner Units")]
        [SerializeField] private int acUnitCount = 10;
        [SerializeField] private Vector3 acUnitSize = new Vector3(0.8f, 0.5f, 0.3f);
        [SerializeField] private float acUnitWallOffset = 0.15f;

        [Header("Puddles")]
        [SerializeField] private int puddleCount = 8;
        [SerializeField] private float puddleMaxSize = 2f;
        [SerializeField] private Color puddleColor = new Color(0.2f, 0.2f, 0.25f, 0.4f);

        [Header("Debris")]
        [SerializeField] private int cigaretteButtCount = 30;
        [SerializeField] private int trashCount = 12;

        [Header("Placement Zones")]
        [SerializeField] private Vector3 scatterArea = new Vector3(150f, 0f, 150f);
        [SerializeField] private Vector3 scatterCenter = Vector3.zero;
        [SerializeField] private LayerMask placementLayer = ~0;
        [SerializeField] private float maxSlope = 30f;

        private List<GameObject> scatteredObjects = new List<GameObject>();
        private System.Random rng;

        public enum DetailType
        {
            Poster,
            Graffiti,
            PowerLine,
            ACUnit,
            Puddle,
            CigaretteButt,
            Trash
        }

        private void Awake()
        {
            rng = new System.Random(42);
        }

        /// <summary>
        /// 散布所有装饰细节
        /// </summary>
        public void ScatterAllDetails()
        {
            ClearAllDetails();

            ScatterPosters();
            ScatterGraffiti();
            ScatterPowerLines();
            ScatterACUnits();
            ScatterPuddles();
            ScatterDebris(cigaretteButtCount, "CigaretteButt", 0.02f, 0.05f, Color.white);
            ScatterDebris(trashCount, "Trash", 0.05f, 0.15f, new Color(0.5f, 0.5f, 0.4f));

            Debug.Log($"[DetailScatter] Scattered {scatteredObjects.Count} detail objects across scene");
        }

        private void ClearAllDetails()
        {
            foreach (var obj in scatteredObjects)
            {
                if (obj != null) DestroyImmediate(obj);
            }
            scatteredObjects.Clear();
        }

        /// <summary>
        /// 散布墙上海报
        /// </summary>
        private void ScatterPosters()
        {
            string[] posterNames = new string[]
            {
                "Wanted_Fugitive", "NightClub_Ad", "Liquor_Ad",
                "MartialArts_Film", "Cigarette_Ad", "Gambling_Den"
            };

            for (int i = 0; i < posterCount; i++)
            {
                Vector3 wallPoint = FindWallPoint();
                if (wallPoint == Vector3.zero) continue;

                GameObject poster = CreateQuad("Poster_" + i);
                float size = RandomRange(posterSizeRange.x, posterSizeRange.y);
                poster.transform.position = wallPoint + Vector3.up * RandomRange(1.5f, 3.5f);
                poster.transform.rotation = Quaternion.Euler(0f, RandomRange(0f, 360f), 0f);
                poster.transform.localScale = new Vector3(size, size * 1.4f, 1f);

                MeshRenderer mr = poster.GetComponent<MeshRenderer>();
                mr.material = CreatePosterMaterial(RandomElement(posterNames) + "_" + i);

                scatteredObjects.Add(poster);
            }
        }

        /// <summary>
        /// 散布墙面涂鸦
        /// </summary>
        private void ScatterGraffiti()
        {
            string[] graffitiTags = new string[] { "Dragon", "Tiger", "Dagger", "Skull", "Serpent", "Phoenix" };

            for (int i = 0; i < graffitiCount; i++)
            {
                Vector3 wallPoint = FindWallPoint();
                if (wallPoint == Vector3.zero) continue;

                GameObject graffiti = CreateQuad("Graffiti_" + i);
                float size = RandomRange(graffitiSizeRange.x, graffitiSizeRange.y);
                graffiti.transform.position = wallPoint + Vector3.up * RandomRange(0.5f, 4f);
                graffiti.transform.rotation = Quaternion.Euler(0f, RandomRange(0f, 360f), 0f);
                graffiti.transform.localScale = new Vector3(size, size * RandomRange(0.6f, 1.5f), 1f);

                MeshRenderer mr = graffiti.GetComponent<MeshRenderer>();
                mr.material = CreateGraffitiMaterial(RandomElement(graffitiTags) + "_" + i);
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

                scatteredObjects.Add(graffiti);
            }
        }

        /// <summary>
        /// 散布电线
        /// </summary>
        private void ScatterPowerLines()
        {
            for (int i = 0; i < powerLineSegments; i++)
            {
                Vector3 start = new Vector3(
                    scatterCenter.x + RandomRange(-scatterArea.x * 0.4f, scatterArea.x * 0.4f),
                    powerLineHeight,
                    scatterCenter.z + RandomRange(-scatterArea.z * 0.4f, scatterArea.z * 0.4f)
                );

                Vector3 end = start + new Vector3(RandomRange(5f, 15f), -powerLineSag, RandomRange(-2f, 2f));

                GameObject line = new GameObject("PowerLine_" + i);
                LineRenderer lr = line.AddComponent<LineRenderer>();
                lr.positionCount = 2;
                lr.SetPosition(0, start);
                lr.SetPosition(1, end);
                lr.startWidth = powerLineThickness;
                lr.endWidth = powerLineThickness;
                lr.material = new Material(Shader.Find("Unlit/Color"));
                lr.material.color = new Color(0.1f, 0.1f, 0.1f, 0.8f);
                lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

                line.transform.SetParent(transform);
                scatteredObjects.Add(line);
            }
        }

        /// <summary>
        /// 散布空调外机
        /// </summary>
        private void ScatterACUnits()
        {
            for (int i = 0; i < acUnitCount; i++)
            {
                Vector3 wallPoint = FindWallPoint();
                if (wallPoint == Vector3.zero) continue;

                GameObject acUnit = GameObject.CreatePrimitive(PrimitiveType.Cube);
                acUnit.name = "ACUnit_" + i;
                acUnit.transform.position = wallPoint + Vector3.up * RandomRange(2f, 4.5f) +
                    Vector3.forward * acUnitWallOffset;
                acUnit.transform.localScale = acUnitSize;
                acUnit.transform.rotation = Quaternion.Euler(0f, RandomRange(0f, 360f), 0f);

                MeshRenderer mr = acUnit.GetComponent<MeshRenderer>();
                mr.material = new Material(Shader.Find("Standard"));
                mr.material.color = new Color(0.8f, 0.8f, 0.75f);
                mr.material.SetFloat("_Metallic", 0.7f);

                acUnit.transform.SetParent(transform);
                scatteredObjects.Add(acUnit);
            }
        }

        /// <summary>
        /// 散布地面水坑
        /// </summary>
        private void ScatterPuddles()
        {
            for (int i = 0; i < puddleCount; i++)
            {
                Vector3 groundPoint = FindGroundPoint();
                if (groundPoint == Vector3.zero) continue;

                GameObject puddle = CreateQuad("Puddle_" + i);
                float size = RandomRange(0.5f, puddleMaxSize);
                puddle.transform.position = groundPoint + Vector3.up * 0.01f;
                puddle.transform.rotation = Quaternion.Euler(90f, RandomRange(0f, 360f), 0f);
                puddle.transform.localScale = new Vector3(size, size * RandomRange(0.5f, 1f), 1f);

                MeshRenderer mr = puddle.GetComponent<MeshRenderer>();
                mr.material = new Material(Shader.Find("Unlit/Transparent"));
                mr.material.color = puddleColor;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

                scatteredObjects.Add(puddle);
            }
        }

        /// <summary>
        /// 散布烟蒂和碎屑
        /// </summary>
        private void ScatterDebris(int count, string baseName, float minSize, float maxSize, Color baseColor)
        {
            for (int i = 0; i < count; i++)
            {
                Vector3 groundPoint = FindGroundPoint();
                if (groundPoint == Vector3.zero) continue;

                GameObject debris = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                debris.name = $"{baseName}_{i}";
                float size = RandomRange(minSize, maxSize);
                debris.transform.position = groundPoint + new Vector3(
                    RandomRange(-0.15f, 0.15f), size * 0.5f, RandomRange(-0.15f, 0.15f)
                );
                debris.transform.rotation = Random.rotation;
                debris.transform.localScale = new Vector3(size, size * RandomRange(0.3f, 1f), size);

                DestroyImmediate(debris.GetComponent<CapsuleCollider>());

                MeshRenderer mr = debris.GetComponent<MeshRenderer>();
                mr.material = new Material(Shader.Find("Standard"));
                mr.material.color = baseColor + new Color(
                    RandomRange(-0.1f, 0.1f),
                    RandomRange(-0.1f, 0.1f),
                    RandomRange(-0.1f, 0.1f)
                );

                debris.transform.SetParent(transform);
                scatteredObjects.Add(debris);
            }
        }

        // -- 工具方法 --

        private Vector3 FindWallPoint()
        {
            for (int attempt = 0; attempt < 20; attempt++)
            {
                Vector3 origin = new Vector3(
                    scatterCenter.x + RandomRange(-scatterArea.x * 0.45f, scatterArea.x * 0.45f),
                    5f,
                    scatterCenter.z + RandomRange(-scatterArea.z * 0.45f, scatterArea.z * 0.45f)
                );

                // 向四周射线找墙面
                for (int dir = 0; dir < 4; dir++)
                {
                    Vector3 direction = Quaternion.Euler(0f, dir * 90f, 0f) * Vector3.forward;
                    if (Physics.Raycast(origin, direction, out RaycastHit hit, 30f, placementLayer))
                    {
                        return hit.point;
                    }
                }
            }
            return Vector3.zero;
        }

        private Vector3 FindGroundPoint()
        {
            for (int attempt = 0; attempt < 15; attempt++)
            {
                Vector3 origin = new Vector3(
                    scatterCenter.x + RandomRange(-scatterArea.x * 0.45f, scatterArea.x * 0.45f),
                    10f,
                    scatterCenter.z + RandomRange(-scatterArea.z * 0.45f, scatterArea.z * 0.45f)
                );

                if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 30f, placementLayer))
                {
                    if (Vector3.Angle(hit.normal, Vector3.up) <= maxSlope)
                    {
                        return hit.point;
                    }
                }
            }
            return Vector3.zero;
        }

        private GameObject CreateQuad(string name)
        {
            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = name;
            DestroyImmediate(quad.GetComponent<MeshCollider>());
            quad.transform.SetParent(transform);
            return quad;
        }

        private Material CreatePosterMaterial(string id)
        {
            Material mat = new Material(Shader.Find("Standard"));
            Color[] posterColors = new Color[]
            {
                new Color(0.9f, 0.2f, 0.1f),
                new Color(0.1f, 0.3f, 0.8f),
                new Color(0.8f, 0.7f, 0.1f),
                new Color(0.2f, 0.1f, 0.1f),
                new Color(0.3f, 0.5f, 0.2f),
            };
            mat.color = RandomElement(posterColors);
            mat.SetFloat("_Glossiness", 0.1f);
            return mat;
        }

        private Material CreateGraffitiMaterial(string id)
        {
            Material mat = new Material(Shader.Find("Standard"));
            Color[] grfColors = new Color[]
            {
                new Color(1f, 0.1f, 0.1f, 0.7f),
                new Color(0.1f, 1f, 0.1f, 0.7f),
                new Color(0.1f, 0.3f, 1f, 0.7f),
                new Color(1f, 0.8f, 0f, 0.7f),
                new Color(0.8f, 0.1f, 0.8f, 0.7f),
            };
            mat.color = RandomElement(grfColors);
            mat.SetFloat("_Glossiness", 0f);
            return mat;
        }

        private float RandomRange(float min, float max)
        {
            return (float)(min + rng.NextDouble() * (max - min));
        }

        private T RandomElement<T>(T[] array)
        {
            return array[rng.Next(array.Length)];
        }

        private void OnDestroy()
        {
            ClearAllDetails();
        }
    }
}