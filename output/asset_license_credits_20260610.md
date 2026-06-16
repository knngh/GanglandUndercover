# Gangland Undercover — 第三方资产授权目录

> 版本: v0.2.0-dev | 更新: 2026-06-10
> 用途: 游戏内 Credits 页面 / Steam 商店页法律声明 / 发行前合规检查

---

## 运行时使用的资产

### LimeZu — 2D 环境 Tile 精灵

| 资产 | 来源 | 许可 | 用途 |
|------|------|------|------|
| LimeZu Interiors | itch.io / LimeZu | CC0 | 任务站内饰 tile |
| LimeZu Exteriors | itch.io / LimeZu | CC0 | 九龙港区街道 tile |

> 引用路径: `Assets/_Project/Resources/Sprites/Tilesets/LimeZu/`
> 加载方式: `Sprite2DAssetCache` → `Resources.Load`

### Kenney — 2D 精灵

| 资产 | 来源 | 许可 | 用途 |
|------|------|------|------|
| Kenney Buildings | kenney.nl | CC0 1.0 | 建筑 sprite |
| Kenney Roads | kenney.nl | CC0 1.0 | 道路 sprite |
| Kenney Characters | kenney.nl | CC0 1.0 | 角色相关 sprite |

> 引用路径: `Assets/_Project/Sprites/Kenney/`

### Quaternius — 3D 模型/贴图

| 资产 | 来源 | 许可 | 用途 |
|------|------|------|------|
| Modular SciFi Mega Kit | quaternius.com | CC0 | 3D 模型贴图（Resources 中，可能在用） |
| Modular Lowpoly Streets Free | quaternius.com | CC0 | 3D 街景模型（Legacy3D 中，未引用） |

> 引用路径: `Assets/_Project/Resources/Quaternius/`、`Assets/_Project/Art/ThirdParty/Quaternius/`

### Unity Asset Store — "Free Pack"

| 资产 | 来源 | 许可 | 用途 |
|------|------|------|------|
| Free Pack | Unity Asset Store | 待确认 | Resources 中，用途待查 |

---

## 未被构建引用的资产

| 资产 | 位置 | 大小 | 说明 |
|------|------|------|------|
| ModularLowpolyStreetsFree | Legacy3D/ | ~100 MB | 未引用，不进构建 |
| FreePackUnused | Legacy3D/ | ~100 MB | 未引用，不进构建 |

---

## 音频

| 资产 | 来源 | 许可 | 数量 |
|------|------|------|------|
| Kenney SFX | kenney.nl | CC0 1.0 | 游戏音效 cue |
| 自录音效 | 自制 | 自有 | SFX 系列 |

---

## 字体

| 字体 | 来源 | 许可 | 用途 |
|------|------|------|------|
| CJKPixelFallback | Resources/Fonts/ | 待确认 | CJK 像素回退字体 |

---

## Steam 商店页 Credits 模板

```
Art Assets:
  LimeZu — itch.io/limezu (CC0)
  Kenney — kenney.nl (CC0 1.0)
  Quaternius — quaternius.com (CC0)

Audio:
  Kenney — kenney.nl (CC0 1.0)

Engine:
  Unity 6000 — unity.com

Fonts:
  [待确认]

"No AI-generated assets were used in the creation of this game's art, music, or writing."
```

---

## 合规待办

| 事项 | 状态 |
|------|------|
| 确认 "Free Pack" (AssetStore) 的确切许可 | ☐ |
| 确认 CJKPixelFallback 字体许可（开源？商业？） | ☐ |
| 如果用了 pixabay/incompetech BGM，补充 BGM 授权 | ☐ |
| 确认 Quaternius 3D 资源是否实际进入构建 | ☐ |
| 准备游戏内 Credits 面板 UI | ☐ |
