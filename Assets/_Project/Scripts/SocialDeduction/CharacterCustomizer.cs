using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using Unity.Collections;

namespace GanglandUndercover.SocialDeduction
{
    /// <summary>
    /// 角色外观自定义系统。
    /// 管理6种部位（帽子、上衣、下装、配饰、肤色、身高）的外观选择，
    /// 支持本地持久化（PlayerPrefs / JSON 文件）与联机同步（Unity Netcode CustomMessage）。
    /// </summary>
    public sealed class CharacterCustomizer : NetworkBehaviour
    {
        private const string PlayerPrefsKey = "Gangland_Customization";
        private const string CustomMessageName = "GanglandCharacterCustom";
        private const float MinHeightScale = 0.75f;
        private const float MaxHeightScale = 1.25f;
        private const float BaseSpeed = 2.5f;
        private const float HeightSpeedMin = 2.0f;
        private const float HeightSpeedMax = 3.2f;

        [Header("Data")]
        [Tooltip("装扮数据库 ScriptableObject 引用")]
        [SerializeField] private WardrobeData wardrobeData;

        [Header("Visual Targets")]
        [Tooltip("角色头部 Transform，用于帽子/发型定位")]
        [SerializeField] private Transform headBone;

        [Tooltip("角色身体根 Transform，用于上衣/下装定位")]
        [SerializeField] private Transform bodyBone;

        [Tooltip("角色整体缩放根 Transform")]
        [SerializeField] private Transform scaleRoot;

        [Tooltip("角色 SkinnedMeshRenderer 列表，用于肤色着色")]
        [SerializeField] private SkinnedMeshRenderer[] skinRenderers;

        private SocialCharacter socialChar;
        private readonly Dictionary<WardrobePart, string> currentSelection = new Dictionary<WardrobePart, string>();
        private readonly List<GameObject> spawnedAttachments = new List<GameObject>();
        private bool initialized;

        // ── 公开属性 ──

        /// <summary>
        /// 当前各部位选择的装扮 ID。
        /// </summary>
        public IReadOnlyDictionary<WardrobePart, string> CurrentSelection => currentSelection;

        /// <summary>
        /// 当自定义数据变更时触发（本地或远程）。
        /// 参数为完整的选择字典的浅拷贝。
        /// </summary>
        public event Action<IReadOnlyDictionary<WardrobePart, string>> OnCustomizationChanged;

        // ── 生命周期 ──

        private void Awake()
        {
            socialChar = GetComponent<SocialCharacter>();
            if (wardrobeData == null)
            {
                wardrobeData = Resources.Load<WardrobeData>("Wardrobe/WardrobeData");
            }

            InitializeDefaults();

            if (socialChar == null)
                Debug.LogWarning("[CharacterCustomizer] 未找到 SocialCharacter 组件，身高→移动速度联动将不可用。");
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            if (IsServer || IsOwner)
            {
                LoadFromPrefs();
                ApplyAllVisuals();
            }

            if (NetworkManager.Singleton != null && NetworkManager.Singleton.CustomMessagingManager != null)
            {
                NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler(
                    CustomMessageName, OnCustomMessageReceived);
            }
        }

        public override void OnNetworkDespawn()
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.CustomMessagingManager != null)
            {
                NetworkManager.Singleton.CustomMessagingManager.UnregisterNamedMessageHandler(CustomMessageName);
            }

            base.OnNetworkDespawn();
        }

        private void OnDestroy()
        {
            ClearAttachments();
        }

        // ── 初始化 ──

        /// <summary>
        /// 将所有部位初始化为各自第一个默认选项。
        /// </summary>
        private void InitializeDefaults()
        {
            if (wardrobeData == null)
                return;

            WardrobePart[] parts = {
                WardrobePart.Hat, WardrobePart.Top, WardrobePart.Bottom,
                WardrobePart.Accessory, WardrobePart.SkinTone, WardrobePart.Height
            };

            foreach (var part in parts)
            {
                var defaults = wardrobeData.GetDefaultIdsByPart(part);
                if (defaults.Count > 0)
                {
                    currentSelection[part] = defaults[0];
                }
            }

            initialized = true;
        }

        // ── 选择与生效 ──

        /// <summary>
        /// 为指定部位选择装扮项。
        /// </summary>
        public void Select(WardrobePart part, string itemId)
        {
            if (wardrobeData == null)
            {
                Debug.LogError("[CharacterCustomizer] WardrobeData 为空，无法切换装扮。");
                return;
            }

            var item = wardrobeData.FindById(itemId);
            if (item == null)
            {
                Debug.LogWarning($"[CharacterCustomizer] 未找到装扮项: {itemId}");
                return;
            }

            if (item.part != part)
            {
                Debug.LogWarning($"[CharacterCustomizer] 装扮项 {itemId} 属于 {item.part}，而非 {part}");
                return;
            }

            currentSelection[part] = itemId;
            ApplyVisual(part, item);
            SaveToPrefs();

            if (IsSpawned)
                BroadcastCustomData();

            OnCustomizationChanged?.Invoke(new Dictionary<WardrobePart, string>(currentSelection));
        }

        /// <summary>
        /// 对外观选择进行整体应用，通常用于刚从存档恢复时。
        /// </summary>
        public void ApplyAllVisuals()
        {
            if (wardrobeData == null)
                return;

            foreach (var kv in currentSelection)
            {
                var item = wardrobeData.FindById(kv.Value);
                if (item != null)
                    ApplyVisual(kv.Key, item);
            }
        }

        /// <summary>
        /// 将单个装扮项应用到角色模型。
        /// </summary>
        private void ApplyVisual(WardrobePart part, WardrobeItem item)
        {
            switch (part)
            {
                case WardrobePart.Hat:
                    ApplyAttachment(item);
                    break;
                case WardrobePart.Top:
                case WardrobePart.Bottom:
                case WardrobePart.Accessory:
                    ApplyAttachment(item);
                    break;
                case WardrobePart.SkinTone:
                    ApplySkinTone(item);
                    break;
                case WardrobePart.Height:
                    ApplyHeightScale(item);
                    break;
            }
        }

        /// <summary>
        /// 挂载装扮预制体（帽子/上衣/下装/配饰）。
        /// 先从 Resources 加载预制体再实例化到对应骨骼上。
        /// </summary>
        private void ApplyAttachment(WardrobeItem item)
        {
            // 移除同部位旧附件
            RemoveAttachmentsByPart(item.part);

            // 无装扮项（如 hat_none / acc_none）
            if (string.IsNullOrEmpty(item.iconPath) || item.id.EndsWith("_none"))
                return;

            // 尝试从 Resources 加载预制体
            string resourcePath = item.iconPath.Replace("Icons/Wardrobe/", "Models/Wardrobe/");
            var prefab = Resources.Load<GameObject>(resourcePath);
            if (prefab == null)
            {
                // 图标路径可能复用为模型路径，二次尝试
                prefab = Resources.Load<GameObject>(item.iconPath);
                if (prefab == null)
                    return; // 无对应模型预制体时静默跳过
            }

            Transform parent = GetAttachmentParent(item.part);
            var instance = Instantiate(prefab, parent, false);
            instance.name = $"Wardrobe_{item.part}_{item.id}";
            spawnedAttachments.Add(instance);
        }

        private void RemoveAttachmentsByPart(WardrobePart part)
        {
            for (int i = spawnedAttachments.Count - 1; i >= 0; i--)
            {
                if (spawnedAttachments[i] != null && spawnedAttachments[i].name.Contains($"_{part}_"))
                {
                    if (Application.isPlaying)
                        Destroy(spawnedAttachments[i]);
                    else
                        DestroyImmediate(spawnedAttachments[i]);

                    spawnedAttachments.RemoveAt(i);
                }
            }
        }

        private void ClearAttachments()
        {
            foreach (var go in spawnedAttachments)
            {
                if (go != null)
                {
                    if (Application.isPlaying)
                        Destroy(go);
                    else
                        DestroyImmediate(go);
                }
            }
            spawnedAttachments.Clear();
        }

        private Transform GetAttachmentParent(WardrobePart part)
        {
            switch (part)
            {
                case WardrobePart.Hat:
                    return headBone != null ? headBone : transform;
                case WardrobePart.Top:
                case WardrobePart.Bottom:
                    return bodyBone != null ? bodyBone : transform;
                case WardrobePart.Accessory:
                    return bodyBone != null ? bodyBone : transform;
                default:
                    return transform;
            }
        }

        /// <summary>
        /// 应用肤色到 SkinnedMeshRenderer。
        /// </summary>
        private void ApplySkinTone(WardrobeItem item)
        {
            Color skinColor = item.GetColor();
            if (skinRenderers == null)
                return;

            foreach (var smr in skinRenderers)
            {
                if (smr == null)
                    continue;

                // 使用 MaterialPropertyBlock 避免创建新材质实例
                var block = new MaterialPropertyBlock();
                smr.GetPropertyBlock(block);
                block.SetColor("_BaseColor", skinColor);
                block.SetColor("_Color", skinColor);
                smr.SetPropertyBlock(block);
            }
        }

        /// <summary>
        /// 应用身高缩放，并联动 SocialCharacter.MoveSpeed。
        /// </summary>
        private void ApplyHeightScale(WardrobeItem item)
        {
            float scale = Mathf.Clamp(item.scaleFactor, MinHeightScale, MaxHeightScale);

            if (scaleRoot != null)
            {
                scaleRoot.localScale = new Vector3(scale, scale, scale);
            }
            else
            {
                transform.localScale = new Vector3(scale, scale, scale);
            }

            // 身高 → 移动速度联动
            if (socialChar != null)
            {
                float t = Mathf.InverseLerp(MinHeightScale, MaxHeightScale, scale);
                float speed = Mathf.Lerp(HeightSpeedMin, HeightSpeedMax, t);
                socialChar.MoveSpeed = speed;
                socialChar.SetMoveSpeed(speed);
            }
        }

        /// <summary>
        /// 根据当前身高缩放计算 MoveSpeed 并刷新。
        /// 供外部（如 SocialCharacter 初始化后）调用以同步。
        /// </summary>
        public void RefreshMoveSpeedFromHeight()
        {
            if (!currentSelection.TryGetValue(WardrobePart.Height, out string heightId))
                return;
            if (wardrobeData == null)
                return;

            var item = wardrobeData.FindById(heightId);
            if (item != null)
                ApplyHeightScale(item);
        }

        // ── 持久化：PlayerPrefs ──

        /// <summary>
        /// 将当前自定义配置保存到 PlayerPrefs（JSON 格式）。
        /// </summary>
        public void SaveToPrefs()
        {
            var data = new CharacterCustomData();
            foreach (var kv in currentSelection)
            {
                // 使用 Switch 而非字典以兼容旧版 .NET 的 Unity 环境
                switch (kv.Key)
                {
                    case WardrobePart.Hat:       data.hat = kv.Value; break;
                    case WardrobePart.Top:       data.top = kv.Value; break;
                    case WardrobePart.Bottom:    data.bottom = kv.Value; break;
                    case WardrobePart.Accessory: data.accessory = kv.Value; break;
                    case WardrobePart.SkinTone:  data.skinTone = kv.Value; break;
                    case WardrobePart.Height:    data.height = kv.Value; break;
                }
            }

            string json = JsonUtility.ToJson(data);
            PlayerPrefs.SetString(PlayerPrefsKey, json);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// 从 PlayerPrefs 加载自定义配置。
        /// </summary>
        public void LoadFromPrefs()
        {
            if (!PlayerPrefs.HasKey(PlayerPrefsKey))
                return;

            string json = PlayerPrefs.GetString(PlayerPrefsKey);
            if (string.IsNullOrEmpty(json))
                return;

            try
            {
                var data = JsonUtility.FromJson<CharacterCustomData>(json);
                if (data != null)
                {
                    ApplyFromData(data);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[CharacterCustomizer] PlayerPrefs 数据解析失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 将自定义配置保存到本地 JSON 文件。
        /// </summary>
        public void SaveToFile(string filePath = null)
        {
            if (string.IsNullOrEmpty(filePath))
                filePath = System.IO.Path.Combine(Application.persistentDataPath, "character_custom.json");

            var data = new CharacterCustomData();
            foreach (var kv in currentSelection)
            {
                switch (kv.Key)
                {
                    case WardrobePart.Hat:       data.hat = kv.Value; break;
                    case WardrobePart.Top:       data.top = kv.Value; break;
                    case WardrobePart.Bottom:    data.bottom = kv.Value; break;
                    case WardrobePart.Accessory: data.accessory = kv.Value; break;
                    case WardrobePart.SkinTone:  data.skinTone = kv.Value; break;
                    case WardrobePart.Height:    data.height = kv.Value; break;
                }
            }

            string json = JsonUtility.ToJson(data, true);
            System.IO.File.WriteAllText(filePath, json);
        }

        /// <summary>
        /// 从本地 JSON 文件加载自定义配置。
        /// </summary>
        public void LoadFromFile(string filePath = null)
        {
            if (string.IsNullOrEmpty(filePath))
                filePath = System.IO.Path.Combine(Application.persistentDataPath, "character_custom.json");

            if (!System.IO.File.Exists(filePath))
                return;

            try
            {
                string json = System.IO.File.ReadAllText(filePath);
                var data = JsonUtility.FromJson<CharacterCustomData>(json);
                if (data != null)
                    ApplyFromData(data);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[CharacterCustomizer] JSON 文件加载失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 将反序列化后的数据填入 currentSelection 并应用。
        /// </summary>
        private void ApplyFromData(CharacterCustomData data)
        {
            if (!string.IsNullOrEmpty(data.hat))       currentSelection[WardrobePart.Hat] = data.hat;
            if (!string.IsNullOrEmpty(data.top))       currentSelection[WardrobePart.Top] = data.top;
            if (!string.IsNullOrEmpty(data.bottom))    currentSelection[WardrobePart.Bottom] = data.bottom;
            if (!string.IsNullOrEmpty(data.accessory)) currentSelection[WardrobePart.Accessory] = data.accessory;
            if (!string.IsNullOrEmpty(data.skinTone))  currentSelection[WardrobePart.SkinTone] = data.skinTone;
            if (!string.IsNullOrEmpty(data.height))    currentSelection[WardrobePart.Height] = data.height;

            ApplyAllVisuals();
        }

        // ── 重置 ──

        /// <summary>
        /// 将所有部位重置为默认选项。
        /// </summary>
        public void ResetToDefaults()
        {
            if (wardrobeData == null)
                return;

            WardrobePart[] parts = {
                WardrobePart.Hat, WardrobePart.Top, WardrobePart.Bottom,
                WardrobePart.Accessory, WardrobePart.SkinTone, WardrobePart.Height
            };

            foreach (var part in parts)
            {
                var defaults = wardrobeData.GetDefaultIdsByPart(part);
                if (defaults.Count > 0)
                    currentSelection[part] = defaults[0];
            }

            ApplyAllVisuals();
            SaveToPrefs();

            if (IsSpawned)
                BroadcastCustomData();

            OnCustomizationChanged?.Invoke(new Dictionary<WardrobePart, string>(currentSelection));
        }

        /// <summary>
        /// 删除 PlayerPrefs 中的自定义数据。
        /// </summary>
        public void ClearSavedData()
        {
            PlayerPrefs.DeleteKey(PlayerPrefsKey);
            PlayerPrefs.Save();
        }

        // ── 网络同步（Unity Netcode CustomMessage） ──

        /// <summary>
        /// 将当前自定义数据广播至所有客户端。
        /// 格式：NetworkObjectId (8B) + JSON (UTF-8)。
        /// </summary>
        private void BroadcastCustomData()
        {
            if (!IsSpawned || NetworkManager.Singleton == null)
                return;

            var manager = NetworkManager.Singleton.CustomMessagingManager;
            if (manager == null)
                return;

            string json = SerializeCurrentSelection();
            byte[] jsonBytes = System.Text.Encoding.UTF8.GetBytes(json);

            int totalSize = 8 + jsonBytes.Length;
            using var writer = new FastBufferWriter(totalSize, Allocator.Temp);
            writer.WriteValueSafe(NetworkObjectId);
            for (int i = 0; i < jsonBytes.Length; i++)
                writer.WriteByteSafe(jsonBytes[i]);

            manager.SendNamedMessage(CustomMessageName, NetworkManager.ServerClientId, writer,
                NetworkDelivery.ReliableSequenced);
        }

        /// <summary>
        /// 接收远程自定义数据并应用。
        /// </summary>
        private void OnCustomMessageReceived(ulong senderId, FastBufferReader reader)
        {
            reader.ReadValueSafe(out ulong objectId);
            if (objectId != NetworkObjectId)
                return; // 不属于本对象，忽略

            int remainingBytes = reader.Length - (int)reader.Position;
            if (remainingBytes <= 0)
                return;

            byte[] jsonBytes = new byte[remainingBytes];
            reader.ReadBytesSafe(ref jsonBytes, remainingBytes);

            string json = System.Text.Encoding.UTF8.GetString(jsonBytes);
            if (string.IsNullOrEmpty(json))
                return;

            try
            {
                var data = JsonUtility.FromJson<CharacterCustomData>(json);
                if (data != null)
                {
                    ApplyFromData(data);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[CharacterCustomizer] 网络数据解析失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 序列化当前选择为 JSON。
        /// </summary>
        private string SerializeCurrentSelection()
        {
            var data = new CharacterCustomData();
            foreach (var kv in currentSelection)
            {
                switch (kv.Key)
                {
                    case WardrobePart.Hat:       data.hat = kv.Value; break;
                    case WardrobePart.Top:       data.top = kv.Value; break;
                    case WardrobePart.Bottom:    data.bottom = kv.Value; break;
                    case WardrobePart.Accessory: data.accessory = kv.Value; break;
                    case WardrobePart.SkinTone:  data.skinTone = kv.Value; break;
                    case WardrobePart.Height:    data.height = kv.Value; break;
                }
            }
            return JsonUtility.ToJson(data);
        }

        /// <summary>
        /// 将当前自定义数据序列化后提供给外部（如 OnlineMatchController 集成到 ClientProfileMessage 中）。
        /// </summary>
        public string GetCustomDataJson()
        {
            return SerializeCurrentSelection();
        }

        /// <summary>
        /// 从 JSON 字符串应用自定义数据（供 OnlineMatchController 集成使用）。
        /// </summary>
        public void ApplyCustomDataJson(string json)
        {
            if (string.IsNullOrEmpty(json))
                return;

            try
            {
                var data = JsonUtility.FromJson<CharacterCustomData>(json);
                if (data != null)
                    ApplyFromData(data);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[CharacterCustomizer] ApplyCustomDataJson 解析失败: {ex.Message}");
            }
        }

        // ── 内部数据结构 ──

        /// <summary>
        /// 角色自定义数据的可序列化结构，用于 JSON 持久化与网络传输。
        /// </summary>
        [Serializable]
        private class CharacterCustomData
        {
            public string hat;
            public string top;
            public string bottom;
            public string accessory;
            public string skinTone;
            public string height;
        }
    }
}
