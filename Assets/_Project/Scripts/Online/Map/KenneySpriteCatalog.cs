using System;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace GanglandUndercover.Online.Map
{
    /// <summary>
    /// Kenney Sprite 目录表 — ScriptableObject，存储所有烘焙后 Sprite 的引用。
    ///
    /// 使用方式：
    /// 1. 运行 Tools → Bake Kenney Sprites 生成 PNG
    /// 2. 把 PNG 拖入此 ScriptableObject 对应槽位
    /// 3. OnlineMatchController 的 KenneyMode 自动从目录读取
    ///
    /// 默认位置：Assets/_Project/Data/KenneySpriteCatalog.asset
    /// </summary>
    [CreateAssetMenu(fileName = "KenneySpriteCatalog", menuName = "Gangland/Kenney Sprite Catalog")]
    public class KenneySpriteCatalog : ScriptableObject
    {
        [Header("建筑主体")]
        public List<NamedSprite> Buildings = new List<NamedSprite>();

        [Header("建筑细节（雨棚、遮阳棚等）")]
        public List<NamedSprite> BuildingDetails = new List<NamedSprite>();

        [Header("低面建筑")]
        public List<NamedSprite> LowPolyBuildings = new List<NamedSprite>();

        [Header("角色")]
        public List<NamedSprite> Characters = new List<NamedSprite>();

        [Header("角色配件（眼镜、轮椅等）")]
        public List<NamedSprite> Accessories = new List<NamedSprite>();

        [Header("道路")]
        public List<NamedSprite> Roads = new List<NamedSprite>();

        // ══════════════════════════════════════════════════════
        // 查询 API
        // ══════════════════════════════════════════════════════

        /// <summary>按名称查找任意分类中的 Sprite</summary>
        public Sprite FindByName(string name)
        {
            foreach (var ns in Buildings)
                if (ns.Name == name) return ns.Sprite;
            foreach (var ns in BuildingDetails)
                if (ns.Name == name) return ns.Sprite;
            foreach (var ns in LowPolyBuildings)
                if (ns.Name == name) return ns.Sprite;
            foreach (var ns in Characters)
                if (ns.Name == name) return ns.Sprite;
            foreach (var ns in Accessories)
                if (ns.Name == name) return ns.Sprite;
            foreach (var ns in Roads)
                if (ns.Name == name) return ns.Sprite;
            return null;
        }

        /// <summary>按名称模糊匹配（忽略大小写、下划线）</summary>
        public Sprite FindFuzzy(string name)
        {
            string normalized = name.ToLowerInvariant().Replace("-", "").Replace("_", "");
            foreach (var ns in AllSprites())
            {
                string n = ns.Name.ToLowerInvariant().Replace("-", "").Replace("_", "");
                if (n == normalized) return ns.Sprite;
            }
            return null;
        }

        /// <summary>按类别获取随机 Sprite</summary>
        public Sprite RandomBuilding()
        {
            if (Buildings.Count == 0) return null;
            return Buildings[UnityEngine.Random.Range(0, Buildings.Count)].Sprite;
        }

        /// <summary>按类别获取随机 Sprite</summary>
        public Sprite RandomCharacter()
        {
            if (Characters.Count == 0) return null;
            return Characters[UnityEngine.Random.Range(0, Characters.Count)].Sprite;
        }

        /// <summary>匹配包(Bag)：按房间名称语义匹配建筑 + 细节</summary>
        public SpriteBag MatchRoom(string roomLabel)
        {
            var bag = new SpriteBag();
            string lower = roomLabel.ToLowerInvariant();

            // ── M8.1 警署房间映射 ──
            if (lower.Contains("lobby") || lower.Contains("大厅"))
            {
                bag.Main = FindFuzzy("building-skyscraper-e") ?? FindFuzzy("building-d");
                bag.Detail = FindFuzzy("detail-overhang");
            }
            else if (lower.Contains("introom") || lower.Contains("审讯"))
            {
                bag.Main = FindFuzzy("building-i") ?? FindFuzzy("building-f");
                bag.Detail = null;
            }
            else if (lower.Contains("evidence") || lower.Contains("证物"))
            {
                bag.Main = FindFuzzy("building-f") ?? FindFuzzy("building-c");
                bag.Detail = null;
            }
            else if (lower.Contains("armory") || lower.Contains("监控"))
            {
                bag.Main = FindFuzzy("building-i") ?? FindFuzzy("building-f");
                bag.Detail = null;
            }
            else if (lower.Contains("cells") || lower.Contains("拘留"))
            {
                bag.Main = FindFuzzy("building-a") ?? FindFuzzy("building-b");
                bag.Detail = null;
            }
            else if (lower.Contains("briefing") || lower.Contains("简报"))
            {
                bag.Main = FindFuzzy("building-j") ?? FindFuzzy("building-h");
                bag.Detail = FindFuzzy("detail-awning");
            }

            // ── 港区房间映射（原有 12 个） ──
            else if (lower.Contains("货柜") || lower.Contains("码头"))
            {
                bag.Main = FindFuzzy("building-skyscraper-c") ?? FindFuzzy("building-n");
                bag.Detail = FindFuzzy("detail-overhang");
            }
            else if (lower.Contains("海关") || lower.Contains("查验"))
            {
                bag.Main = FindFuzzy("building-k") ?? FindFuzzy("building-e");
                bag.Detail = FindFuzzy("detail-parasol-a");
            }
            else if (lower.Contains("监控"))
            {
                bag.Main = FindFuzzy("building-i") ?? FindFuzzy("building-f");
                bag.Detail = null;
            }
            else if (lower.Contains("茶餐厅") || lower.Contains("茶"))
            {
                bag.Main = FindFuzzy("building-b") ?? RandomBuilding();
                bag.Detail = FindFuzzy("detail-awning");
            }
            else if (lower.Contains("夜市") || lower.Contains("情报"))
            {
                bag.Main = FindFuzzy("building-g") ?? FindFuzzy("building-h");
                bag.Detail = FindFuzzy("detail-awning-wide");
            }
            else if (lower.Contains("金融") || lower.Contains("洗钱") || lower.Contains("账房"))
            {
                bag.Main = FindFuzzy("building-skyscraper-d") ?? FindFuzzy("building-j");
                bag.Detail = null;
            }
            else if (lower.Contains("电房") || lower.Contains("电力") || lower.Contains("机房"))
            {
                bag.Main = FindFuzzy("building-m") ?? FindFuzzy("building-l");
                bag.Detail = null;
            }
            else if (lower.Contains("天台"))
            {
                bag.Main = FindFuzzy("low-detail-building-n") ?? FindFuzzy("building-a");
                bag.Detail = null;
            }
            else if (lower.Contains("指挥") || lower.Contains("广场"))
            {
                bag.Main = FindFuzzy("building-skyscraper-e") ?? FindFuzzy("building-d");
                bag.Detail = null;
            }
            else if (lower.Contains("证物"))
            {
                bag.Main = FindFuzzy("building-f") ?? FindFuzzy("building-c");
                bag.Detail = null;
            }
            else if (lower.Contains("排档") || lower.Contains("后巷") || lower.Contains("黑市"))
            {
                bag.Main = FindFuzzy("building-c") ?? FindFuzzy("building-a");
                bag.Detail = FindFuzzy("detail-overhang-wide");
            }
            else if (lower.Contains("诊所") || lower.Contains("地下"))
            {
                bag.Main = FindFuzzy("building-a") ?? FindFuzzy("building-b");
                bag.Detail = null;
            }
            else
            {
                // 兜底：随机建筑
                bag.Main = RandomBuilding();
                bag.Detail = null;
            }

            return bag;
        }

        // ══════════════════════════════════════════════════════

        private IEnumerable<NamedSprite> AllSprites()
        {
            foreach (var ns in Buildings) yield return ns;
            foreach (var ns in BuildingDetails) yield return ns;
            foreach (var ns in LowPolyBuildings) yield return ns;
            foreach (var ns in Characters) yield return ns;
            foreach (var ns in Accessories) yield return ns;
            foreach (var ns in Roads) yield return ns;
        }
    }

    // ══════════════════════════════════════════════════════════
    // 辅助类型
    // ══════════════════════════════════════════════════════════

    [Serializable]
    public struct NamedSprite
    {
        public string Name;
        public Sprite Sprite;

        public NamedSprite(string name, Sprite sprite)
        {
            Name = name;
            Sprite = sprite;
        }
    }

    /// <summary>
    /// Sprite 包：一个房间可能需要主体建筑 + 装饰细节
    /// </summary>
    public struct SpriteBag
    {
        public Sprite Main;    // 主体建筑 sprite
        public Sprite Detail;  // 装饰（雨棚、遮阳棚等），可为 null
    }
}
