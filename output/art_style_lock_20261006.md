# 美术风格锁定 v1.1（Art Bible 修订案）

> 日期：2026-10-06
> 状态：权威，修订 `output/art_bible_v1_20260605.md` 的 §2/§3，其余章节继续有效
> 决策人：用户（2026-10-06 拍板）

## 0. 决策记录

| # | 决策 | 内容 | 理由 |
|---|------|------|------|
| D1 | 分辨率基准 = **48×48** | 全线统一到 LimeZu 48px 网格 | LimeZu CC0 套件是现成最大资产池（已导入未接线）；64×64 程序化角色质量已到天花板 |
| D2 | 角色 = **方案 A（CC0 换装）** | 用 CC0 像素角色包按 8 职业换装+配色，沿用 `scripts/import_cc0_characters.py` 管线 | 一次成型、授权干净；AI 生成方案 B 的 4 方向×4 帧一致性成本高，延后 |

## 1. 像素规格修订（替代 Art Bible §2）

| 类别 | v1（废弃） | v1.1（生效） | 说明 |
|------|-----------|-------------|------|
| 角色 sprite | 64×64 | **48×48** | Pivot 脚底 y=0，4 方向 × (idle+walk×3) |
| 角色头像 | 64×64 | **48×48** | 裁剪头部 + 职业色边框 |
| 地板 tile | 32×32 | **48×48** | 无缝拼接 |
| 墙壁 tile | 16×16 | **48×48** | LimeZu 墙件 |
| 道具 sprite | 32×32 | **48×48 基准** | 大件为 48 的倍数（96×48 等） |
| 小地图图标 | 16×16 | 16×16（不变） | |
| UI 图标 | 32/64 | 32/64（不变） | 现有 8 张能力图标已合规 |
| VFX sprite | 64×64 | 64×64（不变） | 8 效果 64 帧管线已跑通，不重做 |

**PPU 规则**：tile PPU=48 → 1 tile = 1 世界单位，与 v1 的 32×32@PPU32 世界占位完全一致。
**角色渲染高度**：目标 ≈ 2 世界单位（约 1.5~2 tile），只通过 SpriteRenderer scale 调整
（现有旋钮：`CharacterAnimController.cs:124` localScale），**不改任何世界/地图逻辑**。

## 2. 硬不变量（每批美术验收前置条件）

1. **服务端权威地图数据零改动**：房间矩形、出生点、任务点、通风管拓扑坐标不动；
   美术只动 PPU / scale / sprite 引用。
2. **sprite 路径约定零破坏**：`Sprite2DAssetCache` 路径常量、`cc0_manifest.json`
   结构（8 职业 × 4 方向 × 4 帧 + death + avatar = 144 PNG）、`tileset_manifest.json` 结构不变。
3. **LimeZu 原始文件不改名、不重切**：一律通过 manifest 映射引用，保持 import 脚本幂等。
4. **Point filter**，新导入 meta 逐张检查（readiness 报告 misconfig 必须 = 0）。

## 3. 角色换装规范（方案 A 落地规则）

- 沿用 Art Bible §3.4 职业识别表与**隐藏阵营原则**：Mole 外观 = Inspector；
  UndercoverAgent 不出现警方色系。
- 可区分度做在**阵营内**（职业道具剪影：警徽/相机/证物袋 vs 链子/酒瓶/扳手），
  不做在阵营间——卧底玩法依赖身份不可读。
- 配色严格取自 `scripts/gangland_palette.py`（Art Bible §4 色板），不新造基色。
- 48×48 比例下身体占比 ≥ 60%，保证缩放后剪影可读。

## 4. 批次风格 Gate（每批提交前逐项打勾）

- [ ] 色板全部出自 `gangland_palette.py`，无新造基色
- [ ] 48px 网格对齐（道具尺寸为 48 的倍数）
- [ ] `ArtAssetReadinessReport` 重跑：misconfig=0、dimension mismatch=0
- [ ] EditMode 测试绿（`-runTests -testPlatform EditMode`）
- [ ] 地图批次：同局 ≥2 客户端截图一致（确定性生成验证）
- [ ] 截图/证据存 `output/`（延续现有证据链纪律）
