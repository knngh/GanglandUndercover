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
    /// 批次 6e 前置普查：全图 sprite 的 texel/像素 档位分布（2026-11-16）。
    /// 6a 已把相机吸附到整数 texel/px（1080p=3），但 sprite 自身局部缩放仍可能
    /// 非整数——本工具量化"离整数档有多远"及其视觉面积权重，产出拍板依据。
    /// 输出: output/art_batch6e_census_20261116.md
    /// </summary>
    public static class SpriteScaleCensusTool
    {
        // 与 OnlineCameraRig 一致：1080p 吸附档 k=4 → 3 texel/px
        private const float ScreenPixelsPerUnit = 192f; // 1080 / (2*2.8125)

        public static void Census()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            GameObject mapHost = new GameObject("Census MapService");
            OnlineMapService mapService = mapHost.AddComponent<OnlineMapService>();
            mapService.ActiveMapType = OnlineMapService.OnlineMapType.HarbourDistrict;

            GameObject worldRoot = new GameObject("Census World");
            var builder = new OnlineWorldBuilder();
            builder.Initialize(worldRoot, mapService,
                new List<Rect>(), new List<Rect>(), new List<TextMesh>(), 8);
            builder.EnsureRuntimeSprites();
            builder.BuildDistrictMap();

            int total = 0, integerAligned = 0, nearHalf = 0, fractional = 0;
            double weightedTotal = 0, weightedOff = 0;
            // 分层统计（v2）：地板铺量 / LimeZu 实物件 / 其它像素件
            var layers = new Dictionary<string, (int count, int off, double area, double areaOff)>
            {
                { "地板铺量", (0, 0, 0, 0) },
                { "LimeZu 实物件", (0, 0, 0, 0) },
                { "其它像素件", (0, 0, 0, 0) },
            };
            var layerAreas = new Dictionary<string, (double area, double areaOff)>();
            var offenders = new List<(string name, float texelPerPixel, float area)>();

            foreach (SpriteRenderer r in worldRoot.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (r == null || r.sprite == null) continue;
                string texName = r.sprite.texture != null ? r.sprite.texture.name : "";
                if (texName.StartsWith("Runtime ")) continue; // 程序化软图层= Bilinear 渐变，无档位概念

                float worldWidth = r.bounds.size.x;
                if (worldWidth <= 0.001f) continue;
                float texelsPerUnit = r.sprite.rect.width / worldWidth;
                float texelPerPixel = texelsPerUnit / ScreenPixelsPerUnit;
                if (texelPerPixel <= 0.01f) continue; // 远小于像素网格，视觉无感

                float area = r.bounds.size.x * r.bounds.size.y;
                // 干净档判定 v2：texel/px = k（每像素 k texel，k 整数，缩小时）
                // 或 = 1/k（每 texel k 屏幕像素，k 整数，放大时）——两者都是均匀像素网格
                float distToInteger = Mathf.Abs(texelPerPixel - Mathf.Round(texelPerPixel));
                float invStep = texelPerPixel > 0.005f ? 1f / texelPerPixel : 999f;
                float snappedInv = Mathf.Round(invStep);
                float distToInvInteger = snappedInv >= 1f
                    ? Mathf.Abs(texelPerPixel - 1f / snappedInv)
                    : 999f;
                float bestDist = Mathf.Min(distToInteger, distToInvInteger);
                float distToHalf = Mathf.Abs(texelPerPixel - Mathf.Round(texelPerPixel * 2f) / 2f);

                string layer = r.gameObject.name.Contains("地板纹理") || r.gameObject.name.Contains("_tile_")
                    ? "地板铺量"
                    : r.gameObject.name.Contains("LimeZu") ? "LimeZu 实物件" : "其它像素件";

                bool off = false;
                total++;
                weightedTotal += area;
                if (bestDist <= 0.005f)
                {
                    integerAligned++;
                }
                else if (Mathf.Min(distToHalf, distToInvInteger * 2f) <= 0.005f)
                {
                    nearHalf++;
                    off = true;
                }
                else
                {
                    fractional++;
                    off = true;
                }

                var prev = layers[layer];
                layers[layer] = (prev.count + 1, prev.off + (off ? 1 : 0), prev.area + area, prev.areaOff + (off ? area : 0));
                if (off)
                {
                    weightedOff += area;
                    offenders.Add((r.gameObject.name, texelPerPixel, area));
                }
            }

            offenders.Sort((a, b) => b.area.CompareTo(a.area));

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("# 批次 6e 前置普查：sprite texel/像素档位分布（2026-11-16 v2 分层版）");
            sb.AppendLine();
            sb.AppendLine($"口径：港区整图 · 像素素材 sprite（排除程序化软图层） · 屏幕档 1080p={ScreenPixelsPerUnit:F0} 像素/单位");
            sb.AppendLine();
            sb.AppendLine("## 总览");
            sb.AppendLine();
            sb.AppendLine($"| 指标 | 值 |");
            sb.AppendLine($"|---|---|");
            sb.AppendLine($"| 统计 sprite 总数 | {total} |");
            sb.AppendLine($"| 整数档对齐（±0.05） | {integerAligned} ({Pct(integerAligned, total)}) |");
            sb.AppendLine($"| 半档对齐（±0.05，如 2.5） | {nearHalf} ({Pct(nearHalf, total)}) |");
            sb.AppendLine($"| 非整数档 | {fractional} ({Pct(fractional, total)}) |");
            sb.AppendLine($"| **面积加权偏离比** | **{weightedOff / System.Math.Max(1.0, weightedTotal) * 100.0:F1}%** |");
            sb.AppendLine();
            sb.AppendLine("## 分层（拍板依据：先改面积占比大的层）");
            sb.AppendLine();
            sb.AppendLine("| 层 | 数量 | 偏离数 | 面积占比 | 层内面积加权偏离 |");
            sb.AppendLine("|---|---|---|---|---|");
            foreach (var kv in layers)
            {
                double areaShare = kv.Value.area / System.Math.Max(1.0, weightedTotal) * 100.0;
                double layerOff = kv.Value.area == 0 ? 0 : kv.Value.areaOff / kv.Value.area * 100.0;
                sb.AppendLine($"| {kv.Key} | {kv.Value.count} | {kv.Value.off} | {areaShare:F1}% | {layerOff:F1}% |");
            }
            sb.AppendLine();
            sb.AppendLine("## 面积 Top 20 偏离档位者（拍板参考：改这些收益最大）");
            sb.AppendLine();
            sb.AppendLine("| 名字 | texel/px | 面积(单位²) |");
            sb.AppendLine("|---|---|---|");
            int shown = Mathf.Min(20, offenders.Count);
            for (int i = 0; i < shown; i++)
            {
                sb.AppendLine($"| {offenders[i].name} | {offenders[i].texelPerPixel:F2} | {offenders[i].area:F2} |");
            }

            Directory.CreateDirectory("output");
            File.WriteAllText("output/art_batch6e_census_20261116.md", sb.ToString());
            Debug.Log("[SpriteScaleCensus] total=" + total + " aligned=" + integerAligned
                + " half=" + nearHalf + " frac=" + fractional
                + " weightedOff=" + (weightedOff / System.Math.Max(1.0, weightedTotal) * 100.0).ToString("F1")
                + "% → output/art_batch6e_census_20261116.md");
        }

        private static string Pct(int part, int whole)
        {
            return whole == 0 ? "0%" : (part * 100f / whole).ToString("F1") + "%";
        }
    }
}
