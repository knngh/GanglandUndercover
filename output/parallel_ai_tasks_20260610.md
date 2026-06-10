# Gangland Undercover — 另一个 AI 可做任务

> 日期: 2026-06-10
> 原则: 给另一个 AI 分配低风险、长流程、可独立验证的工作。不要让它改核心 Relay/Netcode 代码，除非有明确复现和单一修复目标。

---

## 推荐任务

| 优先级 | 任务 | 输入 | 输出 | 风险 |
|--------|------|------|------|------|
| P0 | 跑朋友测试脚本并填结果 | `output/friend_remote_test_runbook_20260610.md` | 一份按步骤填写的测试记录 | 低 |
| P0 | 整理朋友反馈 | 截图、录屏、问题记录模板 | `output/friend_feedback_triage_20260610.md` | 低 |
| P1 | 更新旧文档日期和测试数字 | `output/test_coverage_matrix_20260609.md`, `output/progress_20260609.md` | 新建 2026-06-10 版本，不覆盖历史 | 低 |
| P1 | 审查 UI 文案和截图清单 | `output/ui_*_20260609.md`, `output/screenshot_plan_20260609.md` | 文案差异表和截图缺口表 | 低 |
| P1 | 复核分发包和安装说明 | `Builds/FriendTest-20260610/GanglandUndercover-FriendTest-macOS-20260610.zip` | macOS 打开/权限步骤和截图 | 低 |
| P2 | 补 QA 证据索引 | `Logs/*.xml`, `output/*.md` | `output/qa_evidence_index_20260610.md` | 低 |

---

## 不建议交给另一个 AI 的任务

- 不要让它重构 `OnlineMatchController` 或 `OnlineMatchHud`。
- 不要让它改 Relay/Lobby 加入流程。
- 不要让它删除资源、移动 `.meta`、清理 `Assets/Resources`。
- 不要让它修改测试期望来“让测试通过”。
- 不要让它提交构建产物或日志。

---

## 可直接复制给另一个 AI 的提示

```text
你在 /Users/zhugehao/projects/GanglandUndercover 工作。
不要改核心 C# 运行时代码，不要删除资源，不要提交 Builds/Logs/Library。

任务:
1. 阅读 output/friend_remote_test_runbook_20260610.md 和 output/remote_test_closure_20260610.md。
2. 基于这些文档，新建 output/friend_feedback_triage_20260610.md。
3. 文档里准备一个问题登记表，字段包括: 编号、步骤、Host/Client、现象、截图/录屏、是否阻塞、复现次数、建议归类。
4. 不覆盖 2026-06-09 的历史文档；如需更新测试数字，新建 2026-06-10 版本。
5. 完成后运行 git diff --check，并汇报改了哪些文件。
```
