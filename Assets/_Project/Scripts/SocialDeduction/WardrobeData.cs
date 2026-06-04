using System;
using System.Collections.Generic;
using UnityEngine;

namespace GanglandUndercover.SocialDeduction
{
    /// <summary>
    /// 角色可自定义的部位类型。
    /// </summary>
    public enum WardrobePart
    {
        Hat,
        Top,
        Bottom,
        Accessory,
        SkinTone,
        Height
    }

    /// <summary>
    /// 装扮稀有度。
    /// </summary>
    public enum WardrobeRarity
    {
        Common,
        Rare,
        Epic,
        Legendary
    }

    /// <summary>
    /// 单个装扮项的数据定义。
    /// </summary>
    [Serializable]
    public class WardrobeItem
    {
        [Tooltip("唯一标识，如 hat_baseball_cap")]
        public string id;

        [Tooltip("UI 显示名称")]
        public string displayName;

        [Tooltip("所属部位")]
        public WardrobePart part;

        [Tooltip("预览图标资源路径，如 Icons/Wardrobe/hat_cap")]
        public string iconPath;

        [Tooltip("稀有度")]
        public WardrobeRarity rarity;

        [Tooltip("是否默认解锁")]
        public bool unlockedByDefault = true;

        [Tooltip("[SkinTone 专属] 肤色十六进制颜色，如 #E8C8A8")]
        public string colorHex;

        [Tooltip("[Height 专属] 缩放因子，基准 1.0")]
        public float scaleFactor = 1f;

        public Color GetColor()
        {
            if (string.IsNullOrEmpty(colorHex) || part != WardrobePart.SkinTone)
                return Color.white;

            if (ColorUtility.TryParseHtmlString(colorHex, out Color result))
                return result;

            return Color.white;
        }

        public bool IsDefault => unlockedByDefault;
    }

    /// <summary>
    /// 装扮数据库 ScriptableObject。
    /// 在 Unity Editor 中通过 Assets > Create > GanglandUndercover > Wardrobe Data 创建。
    /// </summary>
    [CreateAssetMenu(menuName = "GanglandUndercover/Wardrobe Data", fileName = "WardrobeData")]
    public class WardrobeData : ScriptableObject
    {
        [Tooltip("所有装扮项列表")]
        public List<WardrobeItem> items = new List<WardrobeItem>();

        private Dictionary<WardrobePart, List<WardrobeItem>> partCache;

        /// <summary>
        /// 按部位获取所有装扮项。
        /// </summary>
        public List<WardrobeItem> GetItemsByPart(WardrobePart part)
        {
            BuildCacheIfNeeded();
            return partCache.TryGetValue(part, out var list) ? list : new List<WardrobeItem>();
        }

        /// <summary>
        /// 获取指定部位的默认选项 ID 列表。
        /// </summary>
        public List<string> GetDefaultIdsByPart(WardrobePart part)
        {
            var result = new List<string>();
            foreach (var item in GetItemsByPart(part))
            {
                if (item.unlockedByDefault)
                    result.Add(item.id);
            }
            return result;
        }

        /// <summary>
        /// 根据 ID 查找装扮项。
        /// </summary>
        public WardrobeItem FindById(string id)
        {
            foreach (var item in items)
            {
                if (item.id == id)
                    return item;
            }
            return null;
        }

        /// <summary>
        /// 获取默认解锁的所有装扮项。
        /// </summary>
        public List<WardrobeItem> GetDefaultUnlockedItems()
        {
            var result = new List<WardrobeItem>();
            foreach (var item in items)
            {
                if (item.unlockedByDefault)
                    result.Add(item);
            }
            return result;
        }

        private void BuildCacheIfNeeded()
        {
            if (partCache != null)
                return;

            partCache = new Dictionary<WardrobePart, List<WardrobeItem>>();
            foreach (var item in items)
            {
                if (item == null)
                    continue;

                if (!partCache.TryGetValue(item.part, out var list))
                {
                    list = new List<WardrobeItem>();
                    partCache[item.part] = list;
                }
                list.Add(item);
            }
        }

        private void OnValidate()
        {
            partCache = null;
        }

#if UNITY_EDITOR
        /// <summary>
        /// 在 Inspector 中首次创建时填充默认装扮数据。
        /// </summary>
        [ContextMenu("Reset to Default Data")]
        private void ResetToDefault()
        {
            items.Clear();
            PopulateDefaults();
        }

        private void Reset()
        {
            if (items.Count == 0)
                PopulateDefaults();
        }

        private void PopulateDefaults()
        {
            // ── Hat ──
            AddItem("hat_none",          "不戴帽子",   WardrobePart.Hat,        "",                      WardrobeRarity.Common,    true);
            AddItem("hat_baseball_cap",  "棒球帽",     WardrobePart.Hat,        "Icons/Wardrobe/hat_cap", WardrobeRarity.Common,    true);
            AddItem("hat_beret",         "贝雷帽",     WardrobePart.Hat,        "Icons/Wardrobe/hat_beret", WardrobeRarity.Rare,    false);
            AddItem("hat_flat_cap",      "鸭舌帽",     WardrobePart.Hat,        "Icons/Wardrobe/hat_flat", WardrobeRarity.Common,    true);
            AddItem("hat_bandana",       "头巾",       WardrobePart.Hat,        "Icons/Wardrobe/hat_bandana", WardrobeRarity.Rare,  false);
            AddItem("hat_hood",          "兜帽",       WardrobePart.Hat,        "Icons/Wardrobe/hat_hood", WardrobeRarity.Epic,     false);

            // ── Top ──
            AddItem("top_tshirt",        "T恤",        WardrobePart.Top,        "Icons/Wardrobe/top_tshirt", WardrobeRarity.Common,  true);
            AddItem("top_jacket",        "夹克",       WardrobePart.Top,        "Icons/Wardrobe/top_jacket", WardrobeRarity.Common,  true);
            AddItem("top_hoodie",        "卫衣",       WardrobePart.Top,        "Icons/Wardrobe/top_hoodie", WardrobeRarity.Rare,    false);
            AddItem("top_shirt",         "衬衫",       WardrobePart.Top,        "Icons/Wardrobe/top_shirt", WardrobeRarity.Common,   true);
            AddItem("top_vest",          "背心",       WardrobePart.Top,        "Icons/Wardrobe/top_vest", WardrobeRarity.Common,    true);
            AddItem("top_coat",          "风衣",       WardrobePart.Top,        "Icons/Wardrobe/top_coat", WardrobeRarity.Epic,     false);

            // ── Bottom ──
            AddItem("bottom_pants",      "长裤",       WardrobePart.Bottom,     "Icons/Wardrobe/bottom_pants", WardrobeRarity.Common, true);
            AddItem("bottom_jeans",      "牛仔裤",     WardrobePart.Bottom,     "Icons/Wardrobe/bottom_jeans", WardrobeRarity.Common, true);
            AddItem("bottom_shorts",     "短裤",       WardrobePart.Bottom,     "Icons/Wardrobe/bottom_shorts", WardrobeRarity.Common, true);
            AddItem("bottom_cargo",      "工装裤",     WardrobePart.Bottom,     "Icons/Wardrobe/bottom_cargo", WardrobeRarity.Rare,  false);
            AddItem("bottom_sweatpants", "运动裤",     WardrobePart.Bottom,     "Icons/Wardrobe/bottom_sweat", WardrobeRarity.Common, true);

            // ── Accessory ──
            AddItem("acc_none",          "无配饰",     WardrobePart.Accessory,  "",                            WardrobeRarity.Common,    true);
            AddItem("acc_necklace",      "项链",       WardrobePart.Accessory,  "Icons/Wardrobe/acc_necklace", WardrobeRarity.Rare,  false);
            AddItem("acc_watch",         "手表",       WardrobePart.Accessory,  "Icons/Wardrobe/acc_watch", WardrobeRarity.Common,   true);
            AddItem("acc_sunglasses",    "墨镜",       WardrobePart.Accessory,  "Icons/Wardrobe/acc_sunglass", WardrobeRarity.Rare,  false);
            AddItem("acc_earring",       "耳环",       WardrobePart.Accessory,  "Icons/Wardrobe/acc_earring", WardrobeRarity.Epic,   false);
            AddItem("acc_ring",          "戒指",       WardrobePart.Accessory,  "Icons/Wardrobe/acc_ring",  WardrobeRarity.Common,    true);

            // ── SkinTone ──
            AddItem("skin_light",        "浅色",       WardrobePart.SkinTone,   "",                            WardrobeRarity.Common,    true,  "#E8C8A8");
            AddItem("skin_med_light",    "中等浅",     WardrobePart.SkinTone,   "",                            WardrobeRarity.Common,    true,  "#D1A88A");
            AddItem("skin_medium",       "中等",       WardrobePart.SkinTone,   "",                            WardrobeRarity.Common,    true,  "#A67B5B");
            AddItem("skin_med_dark",     "中等深",     WardrobePart.SkinTone,   "",                            WardrobeRarity.Common,    true,  "#734C33");
            AddItem("skin_dark",         "深色",       WardrobePart.SkinTone,   "",                            WardrobeRarity.Common,    true,  "#472A1E");

            // ── Height ──
            AddItem("height_xs",         "偏矮",       WardrobePart.Height,     "",                            WardrobeRarity.Common,    true,  null, 0.85f);
            AddItem("height_s",          "稍矮",       WardrobePart.Height,     "",                            WardrobeRarity.Common,    true,  null, 0.93f);
            AddItem("height_m",          "标准",       WardrobePart.Height,     "",                            WardrobeRarity.Common,    true,  null, 1.00f);
            AddItem("height_l",          "稍高",       WardrobePart.Height,     "",                            WardrobeRarity.Common,    true,  null, 1.07f);
            AddItem("height_xl",         "偏高",       WardrobePart.Height,     "",                            WardrobeRarity.Common,    true,  null, 1.15f);
        }

        private void AddItem(string id, string displayName, WardrobePart part, string iconPath,
            WardrobeRarity rarity, bool unlockedByDefault, string colorHex = null, float scaleFactor = 1f)
        {
            items.Add(new WardrobeItem
            {
                id = id,
                displayName = displayName,
                part = part,
                iconPath = iconPath,
                rarity = rarity,
                unlockedByDefault = unlockedByDefault,
                colorHex = colorHex,
                scaleFactor = scaleFactor
            });
        }
#endif
    }
}
