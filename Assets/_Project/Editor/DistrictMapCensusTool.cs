using System.Collections.Generic;
using System.IO;
using System.Text;
using GanglandUndercover.Online;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace GanglandUndercover.Editor
{
    /// <summary>
    /// 批处理地图 dressing 普查：构建完整街区地图后统计全部 sprite 的
    /// 名字前缀 × 亮度/饱和度 × 尺寸，量化"彩色小条/白块"噪声预算。
    /// 只统计程序化 sprite（名字以 "Runtime " 开头的几何色块）为噪声候选；
    /// LimeZu/atlas 实物贴图（Color.white 的真美术）单独计数不进噪声。
    /// 命令行（勿加 -nographics；必须带 -quit）:
    ///   Unity -batchmode -quit -projectPath &lt;proj&gt; -executeMethod GanglandUndercover.Editor.DistrictMapCensusTool.RunCensusAllDistrictMaps
    /// 输出: output/art_batch2b_census/district-census-&lt;map&gt;.txt
    /// </summary>
    public static class DistrictMapCensusTool
    {
        private class PrefixStats
        {
            public int total;
            public int noiseCount;      // 程序化色块 且 亮白/高饱和
            public float noiseArea;     // noiseCount × maxDim² 面积预算
            public float maxDimension;
            public readonly StringBuilder samples = new StringBuilder();
        }

        [MenuItem("Gangland/Art/Census District Map (All)")]
        public static void RunCensusAllDistrictMaps()
        {
            RunCensus(OnlineMapService.OnlineMapType.HarbourDistrict);
            RunCensus(OnlineMapService.OnlineMapType.PoliceStation);
            RunCensus(OnlineMapService.OnlineMapType.KowloonWalledCity);
        }

        private static void RunCensus(OnlineMapService.OnlineMapType mapType)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            GameObject mapHost = new GameObject("District Census MapService");
            OnlineMapService mapService = mapHost.AddComponent<OnlineMapService>();
            mapService.ActiveMapType = mapType;

            GameObject worldRoot = new GameObject("District Census World");
            var builder = new OnlineWorldBuilder();
            builder.Initialize(
                worldRoot,
                mapService,
                new List<Rect>(),
                new List<Rect>(),
                new List<TextMesh>(),
                8);
            builder.EnsureRuntimeSprites();
            builder.BuildDistrictMap();

            var stats = new Dictionary<string, PrefixStats>();
            int totalCount = 0;
            int proceduralCount = 0;
            int artCount = 0;
            int noiseCount = 0;
            float totalNoiseArea = 0f;

            foreach (SpriteRenderer renderer in worldRoot.GetComponentsInChildren<SpriteRenderer>())
            {
                if (renderer == null || renderer.sprite == null) continue;
                string propName = renderer.gameObject.name;
                Color color = renderer.color;
                Vector3 lossy = renderer.transform.lossyScale;
                float maxDimension = Mathf.Max(lossy.x, lossy.y);
                Texture2D spriteTexture = renderer.sprite.texture;
                bool isProcedural = spriteTexture != null
                    && spriteTexture.name.IndexOf("Runtime ", System.StringComparison.Ordinal) == 0;

                totalCount++;
                if (isProcedural) proceduralCount++; else artCount++;

                float hue;
                float saturation;
                float value;
                Color.RGBToHSV(color, out hue, out saturation, out value);
                bool isBright = value >= 0.55f && saturation <= 0.25f;   // 白块/亮灰块
                bool isSaturated = saturation >= 0.5f && value >= 0.35f; // 彩色小条
                if (!isProcedural || !(isBright || isSaturated)) continue;

                noiseCount++;
                totalNoiseArea += maxDimension * maxDimension;

                string prefix = NormalizePrefix(propName);
                if (!stats.TryGetValue(prefix, out PrefixStats prefixStats))
                {
                    prefixStats = new PrefixStats();
                    stats[prefix] = prefixStats;
                }

                prefixStats.total++;
                prefixStats.noiseCount++;
                prefixStats.noiseArea += maxDimension * maxDimension;
                if (maxDimension > prefixStats.maxDimension) prefixStats.maxDimension = maxDimension;
                if (prefixStats.noiseCount <= 3)
                {
                    if (prefixStats.samples.Length > 0) prefixStats.samples.Append(" | ");
                    string colorInfo = string.Format("v={0:F2} s={1:F2} d={2:F2}", value, saturation, maxDimension);
                    prefixStats.samples.Append(propName).Append("(").Append(colorInfo).Append(")");
                }
            }

            string absolutePath = Path.GetFullPath(Path.Combine(
                "output", "art_batch2b_census", "district-census-" + mapType + ".txt"));
            Directory.CreateDirectory(Path.GetDirectoryName(absolutePath));

            var report = new StringBuilder();
            report.AppendLine("# District dressing census: " + mapType);
            report.AppendLine("total=" + totalCount
                + " procedural=" + proceduralCount
                + " texturedArt=" + artCount
                + " noiseSprites=" + noiseCount
                + " noiseArea=" + totalNoiseArea.ToString("F1"));
            report.AppendLine();
            report.AppendLine("prefix\tnoiseCount\tnoiseArea\tmaxDim\tsamples");

            var sorted = new List<KeyValuePair<string, PrefixStats>>(stats);
            sorted.Sort((a, b) => b.Value.noiseArea.CompareTo(a.Value.noiseArea));

            foreach (KeyValuePair<string, PrefixStats> pair in sorted)
            {
                PrefixStats s = pair.Value;
                report.AppendLine(string.Format("{0}\t{1}\t{2:F2}\t{3:F2}\t{4}",
                    pair.Key, s.noiseCount, s.noiseArea, s.maxDimension, s.samples));
            }

            File.WriteAllText(absolutePath, report.ToString());
            Debug.Log("[DistrictMapCensusTool] " + mapType + " -> " + absolutePath
                + " total=" + totalCount + " noise=" + noiseCount + " noiseArea=" + totalNoiseArea.ToString("F1"));
        }

        /// <summary>名字尾随编号归一："主廊线缆 3"→"主廊线缆"，保留区分层归属的中文前缀。</summary>
        private static string NormalizePrefix(string propName)
        {
            string name = propName.TrimEnd();
            int cut = name.Length;
            while (cut > 0 && (char.IsDigit(name[cut - 1]) || name[cut - 1] == ' '))
            {
                cut--;
            }
            return cut > 0 ? name.Substring(0, cut) : name;
        }
    }
}
