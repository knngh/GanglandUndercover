# M10.3 CI 测试自动化配置

## 概述

本文件为 GanglandUndercover 封测阶段的 CI 配置模板。
使用 GitHub Actions，在 PR 合并到 main 时自动运行 Unity 验证。

---

## 发布门槛

| 条件 | 要求 |
|------|------|
| 编辑模式测试 | 全部通过（0 failures） |
| 播放模式测试 | 全部通过（0 failures） |
| API 编译 | 无错误（0 errors） |
| 封测周期 | 72 小时无 P0/P1 级 bug |
| P0 定义 | 无法进入房间、游戏崩溃、联机断连无法恢复 |
| P1 定义 | 关键功能失效（无法投票/无法完成任务/无法会议） |

---

## GitHub Actions 工作流

将以下文件保存为 `.github/workflows/ci.yml`：

```yaml
name: Unity CI

on:
  push:
    branches: [main, develop]
  pull_request:
    branches: [main]

jobs:
  test:
    name: Test (${{ matrix.targetPlatform }})
    runs-on: ubuntu-latest
    strategy:
      fail-fast: false
      matrix:
        targetPlatform: [StandaloneOSX, StandaloneWindows64]

    steps:
      - name: Checkout
        uses: actions/checkout@v4
        with:
          lfs: true

      - name: Cache Library
        uses: actions/cache@v4
        with:
          path: Library
          key: Library-${{ matrix.targetPlatform }}-${{ hashFiles('Assets/**', 'Packages/**', 'ProjectSettings/**') }}
          restore-keys: |
            Library-${{ matrix.targetPlatform }}-

      - name: Run Editor Tests
        uses: game-ci/unity-test-runner@v4
        env:
          UNITY_LICENSE: ${{ secrets.UNITY_LICENSE }}
          UNITY_EMAIL: ${{ secrets.UNITY_EMAIL }}
          UNITY_PASSWORD: ${{ secrets.UNITY_PASSWORD }}
        with:
          projectPath: .
          testMode: EditMode
          artifactsPath: EditMode-${{ matrix.targetPlatform }}-artifacts

      - name: Run PlayMode Tests
        uses: game-ci/unity-test-runner@v4
        env:
          UNITY_LICENSE: ${{ secrets.UNITY_LICENSE }}
          UNITY_EMAIL: ${{ secrets.UNITY_EMAIL }}
          UNITY_PASSWORD: ${{ secrets.UNITY_PASSWORD }}
        with:
          projectPath: .
          testMode: PlayMode
          artifactsPath: PlayMode-${{ matrix.targetPlatform }}-artifacts

      - name: Upload Test Results
        uses: actions/upload-artifact@v4
        if: always()
        with:
          name: TestResults-${{ matrix.targetPlatform }}
          path: |
            EditMode-${{ matrix.targetPlatform }}-artifacts
            PlayMode-${{ matrix.targetPlatform }}-artifacts

  build-verify:
    name: Build Verification
    runs-on: ubuntu-latest
    needs: test
    strategy:
      matrix:
        targetPlatform: [StandaloneOSX, StandaloneWindows64]

    steps:
      - name: Checkout
        uses: actions/checkout@v4
        with:
          lfs: true

      - name: Cache Library
        uses: actions/cache@v4
        with:
          path: Library
          key: Library-${{ matrix.targetPlatform }}-${{ hashFiles('Assets/**', 'Packages/**', 'ProjectSettings/**') }}

      - name: Build
        uses: game-ci/unity-builder@v4
        env:
          UNITY_LICENSE: ${{ secrets.UNITY_LICENSE }}
          UNITY_EMAIL: ${{ secrets.UNITY_EMAIL }}
          UNITY_PASSWORD: ${{ secrets.UNITY_PASSWORD }}
        with:
          projectPath: .
          targetPlatform: ${{ matrix.targetPlatform }}
          buildMethod: BuildScript.Build
          versioning: Semantic

      - name: Upload Build
        uses: actions/upload-artifact@v4
        with:
          name: Build-${{ matrix.targetPlatform }}
          path: Builds/
```

---

## 配置步骤

1. 将此文件保存到 `.github/workflows/ci.yml`（需手动操作，.github 目录在沙盒保护下）
2. 在 GitHub 仓库 Settings → Secrets and variables → Actions 中添加：
   - `UNITY_LICENSE` — Unity 许可证文件内容
   - `UNITY_EMAIL` — Unity 账号邮箱
   - `UNITY_PASSWORD` — Unity 账号密码
3. 首次 push 后观察 CI 运行状态
4. 获得绿色 CI 状态后，视为 M10 封测准备就绪

---

## 封测 Bug 看板模板

| 等级 | 定义 | 响应时间 | 示例 |
|------|------|----------|------|
| P0 | 阻止核心玩法 | 立即修复 | 无法进入房间 / 崩溃 |
| P1 | 关键功能缺失 | 24 小时内 | 无法投票 / 任务无法完成 |
| P2 | 体验问题 | 下个里程碑 | UI 错位 / 文本缺失 |
| P3 | 优化建议 | 排入 backlog | 加载时间优化 |
