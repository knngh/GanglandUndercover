---
AIGC:
    Label: "1"
    ContentProducer: 001191440300708461136T1XGW3
    ProduceID: 9470d846c7eff9b24afb94a99a2cb3f0_4fff18995dab11f18d42525400d9a7a1
    ReservedCode1: Ei0j0/WYtyw+eUhBj/yc0wxJuMza1Ni6vjUI2LxTKtV2gyeFAPgSUej0a5hdVuLg885rZD84nKV87bAcJM/m3sz3bPbV7nIoZRs9ouFTEfBgI34Qg7U+8O+nqRRJXY1qTmH/CgikCjiS2jKKm3sy88aJPOHlRjehi7hrdB/jmws8FUX5BdE6SFp1Dy8=
    ContentPropagator: 001191440300708461136T1XGW3
    PropagateID: 9470d846c7eff9b24afb94a99a2cb3f0_4fff18995dab11f18d42525400d9a7a1
    ReservedCode2: Ei0j0/WYtyw+eUhBj/yc0wxJuMza1Ni6vjUI2LxTKtV2gyeFAPgSUej0a5hdVuLg885rZD84nKV87bAcJM/m3sz3bPbV7nIoZRs9ouFTEfBgI34Qg7U+8O+nqRRJXY1qTmH/CgikCjiS2jKKm3sy88aJPOHlRjehi7hrdB/jmws8FUX5BdE6SFp1Dy8=
---

# Gangland Undercover — 第3阶段改动摘要

> 目标：将 SocialPrototypeController 的离线圈子从胶囊体/方块原型升级为 DenysAlmaral/Synty 真实 3D 资源，接入 GanglandCharacter 动画系统。

## 修改文件清单

### 1. SocialCharacter.cs
| 改动 | 说明 |
|------|------|
| 新增 `BindForPrefab()` 方法 | 预制体角色的轻量初始化：保留原始材质引用（不替换为纯色），后续由 CreateCharacter 通过 Tint 着色。存储 materials 数组用于 RefreshVisual 控制 alive/dead 颜色变化。 |

### 2. SocialPrototypeController.cs

#### 2.1 角色资源映射
| 改动 | 说明 |
|------|------|
| `GetPrefabPathForRole()` | Police 映射从 `PolygonStarter/SM_Bean_Cop_01` 改为 `DenysAlmaral/CityPeople/Prefabs/professions/police_Female_A`。Gang (`casual_Male_K`)、Undercover (`casual_Male_G`) 保持不变。 |
| 新增 `GetTaskPropPath()` | 任务名到 AssetStore/Synty/PolygonGeneric 道具预制体的路径映射表（查封货柜→Crate_02, 调取监控→Screen_01, 修复电闸→Switch_01, 扫描证物→Papers_03, 上传档案→Keypad_01）。 |

#### 2.2 CreateCharacter() 角色创建流程
| 改动 | 说明 |
|------|------|
| Animator 配置 | Instantiate 后调用 `ConfigureCharacterAnimator()`：查找/添加 Animator 组件，通过 GUID `1f860609e221b48e6a101a78e9c6f70e` 加载 GanglandCharacter.controller（Editor 走 AssetDatabase，Runtime 回退 Resources）。设置 `applyRootMotion=false`，`cullingMode=CullUpdateTransforms`。 |
| 自适应缩放 | 新增 `FitCharacterToMap()`：遍历所有 Renderer 合并 Bounds，按最大轴 0.82f 基准计算缩放因子（clamp 到 [0.04,0.32]），取代原来的硬编码 `(0.42,0.42,0.82)`。 |
| Tint 着色 | 新增 `TintCharacterModel()`：对每个 Renderer.material 执行 `Color.Lerp(current, roleColor, 0.28f)` 混合着色，保留原始纹理/贴图（替代原 Bind() 的材料替换方式）。 |
| 绑定方式 | 预制体成功时调用 `BindForPrefab()` 而非 `Bind()`；回退胶囊体则仍用 `Bind()`。 |

#### 2.3 CreateTask() 任务站 3D 化
| 改动 | 说明 |
|------|------|
| 道具模型 | 通过 `GetTaskPropPath()` 获取对应道具路径，`Resources.Load` 成功后 Instantiate 挂载为任务站的子对象（localPosition `(0, 0, -0.65)`，旋转 `(-90,0,0)`，缩放 0.52）。资源缺失时静默跳过，不影响原有方块底板。 |

#### 2.4 CreateRoom() 场景 3D 化
| 改动 | 说明 |
|------|------|
| 墙面装饰 | 尝试加载 `AssetStore/Synty/PolygonGeneric/Prefabs/Base/SM_Bld_Base_Wall_Half_02` 预制体，实例化为南北墙装饰（取代原来的纯色 Cube Trim）。资源缺失时静默回退。 |
| 新增 `PlaceRoomDecor()` | 封装预制体实例化/定位/旋转的辅助方法（Euler(-90,0,0) 校正 Synty 模型的默认朝向）。 |
| 移除 | `CreateRoomTrim()` 不再被 CreateRoom 调用（方法保留以防其他引用）。 |

#### 2.5 新增辅助方法汇总
| 方法 | 职责 |
|------|------|
| `FitCharacterToMap(GameObject)` | Bounds 驱动的自适应缩放 |
| `TintCharacterModel(GameObject, Color)` | 材质颜色混合着色 |
| `ConfigureCharacterAnimator(GameObject)` | Animator 组件配置 + 挂载 GanglandCharacter.controller |
| `LoadGanglandCharacterController()` | 通过 AssetDatabase.GUIDToAssetPath 加载动画控制器 |
| `GetTaskPropPath(string)` | 任务名→道具资源路径映射 |
| `PlaceRoomDecor(Transform, GameObject, ...)` | 房间装饰预制体实例化 |

## 资源依赖

| 资源 | 路径 | 用途 |
|------|------|------|
| police_Female_A | `Resources/AssetStore/DenysAlmaral/CityPeople/Prefabs/professions/` | Police 角色模型 |
| casual_Male_G | `Resources/AssetStore/DenysAlmaral/CityPeople/Prefabs/city/` | Undercover 角色模型 |
| casual_Male_K | `Resources/AssetStore/DenysAlmaral/CityPeople/Prefabs/downtown/` | Gang 角色模型 |
| GanglandCharacter.controller | `Art/Animators/` (GUID: 1f860609...) | 动画控制器 |
| SM_Gen_Prop_Crate_02 | `Resources/AssetStore/Synty/PolygonGeneric/Prefabs/Props/` | 查封货柜道具 |
| SM_Gen_Prop_Screen_01 | 同上 | 调取监控道具 |
| SM_Gen_Prop_Switch_01 | 同上 | 修复电闸道具 |
| SM_Gen_Prop_Papers_03 | 同上 | 扫描证物道具 |
| SM_Gen_Prop_Keypad_01 | 同上 | 上传档案道具 |
| SM_Bld_Base_Wall_Half_02 | `Resources/AssetStore/Synty/PolygonGeneric/Prefabs/Base/` | 房间墙面装饰 |

## 安全设计

- 所有 `Resources.Load` 均有 null 检查，失败时静默回退到原有方块/胶囊体逻辑
- `#if UNITY_EDITOR` 保护 AssetDatabase 调用，Runtime 构建不会编译 Editor-only 代码
- 括号平衡验证通过：SocialCharacter.cs (40/40)、SocialPrototypeController.cs (313/313)
*（内容由AI生成，仅供参考）*
