# M6 美术管线搭建 — 完成报告

## 交付概览

搭建了「Kenney 3D 资产 → 2D 俯视 Sprite → 游戏房间装饰」的完整管线。

---

## 新建文件

| 文件 | 说明 |
|---|---|
| `Editor/KenneySpriteBaker.cs` | Unity Editor 工具，菜单栏 `Tools → Bake Kenney Sprites`。逐个渲染 FBX → 256×256 PNG，自动配置 TextureImporter (Sprite/Point/Transparent) |
| `Online/Map/KenneySpriteCatalog.cs` | `[CreateAssetMenu]` ScriptableObject。6 个分类列表 (Buildings/Details/LowPoly/Characters/Accessories/Roads)。`MatchRoom()` 方法根据房间名称语义匹配建筑+细节 Sprite |
| `Online/Map/KenneySpriteDecorator.cs` | 运行时装饰器。接收 ShipRooms → 调用 Catalog 匹配 → 用 `OnlineWorldBuilder.CreateShapeProp` 叠加 Sprite，不影响碰撞/步行区 |
| `Sprites/Kenney/` 目录结构 | `Buildings/`, `Buildings/Details/`, `Buildings/LowPoly/`, `Characters/`, `Characters/Accessories/`, `Roads/` |

## 修改文件

| 文件 | 变更 |
|---|---|
| `OnlineMatchController.cs` | 新增 `kenneyCatalog` (KenneySpriteCatalog SerializeField) + `kenneyMode` (bool) + `DecorateWithKenneySprites()` 方法，在灰盒建造后、霓虹灯前调用 |

---

## 使用步骤

1. **Unity 中打开** → 菜单 `Tools → Bake Kenney Sprites` → 点击 `Bake All Kits`
2. 等待渲染完成（约 84+26+40 ≈ 150 个 FBX 模型）
3. **右键 `Assets/_Project/Data/` → Create → Gangland → Kenney Sprite Catalog**
4. 把烘焙好的 PNG 从 `Assets/_Project/Sprites/Kenney/` 各子目录拖入 Catalog 对应列表
5. **在场景中选中 OnlineMatchController** → 勾选 `Kenney Mode` → 拖入 Catalog 引用
6. 运行场景，房间被 Kenney 建筑 Sprite 覆盖

## 房间语义映射 (MatchRoom)

| 港区房间 | Kenney 模型匹配 |
|---|---|
| 西码头货柜场 | building-skyscraper-c + detail-overhang |
| 海关查验区 | building-k + detail-parasol-a |
| 监控室 | building-i |
| 茶餐厅 | building-b + detail-awning |
| 夜市主街 | building-g + detail-awning-wide |
| 金融楼 | building-skyscraper-d |
| 电房 | building-m |
| 天台通道 | low-detail-building-n |
| 指挥车广场 | building-skyscraper-e |
| 证物库 | building-f |
| 后巷排档 | building-c + detail-overhang-wide |
| 地下诊所 | building-a |

## 注意事项

- 灰盒模式 (`useGreyboxMode`) 和 Kenney 模式 (`kenneyMode`) 独立开关，可同时开启
- Kenney 装饰层纯视觉——不修改碰撞、walkable、任务点
- 如果部分模型在俯视角度下效果不佳（侧面细节丢失），可调整 `CameraHeight`/`OrthoSize` 参数重新烘焙
