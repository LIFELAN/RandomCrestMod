# 版本状态记录：0.2.3 / 0.2.4

> 快照时间：2026-10-11。本文档是两版状态的长期记录，供日后 bug 反馈对照使用。
> 维护：主仓库这份为准；v2 工程目录里有一份同步副本。

## 一、总览

| 项 | 0.2.3 | 0.2.4（v2 重构版） |
|---|---|---|
| 状态 | **已发布** | **已完成、已测试通过，未发布** |
| 位置 | 主仓库 `E:/Agent/Pi/RandomCrestMod`（git `master @ f288ae9`，tag `v0.2.3`） | `C:/Users/fuenlai/Desktop/RandomCrestMod-v2`（无 git） |
| 版本号 | `0.2.3` | `0.2.4` |
| 架构 | 平铺 `.cs`，一堆 `_active/_bindActive/_dashActive/...` 标志 | `Core / Runtime / Features / Presentation` + 单元测试 |
| DLL md5 | `9db579d4d9d35a8754872d9fb737a56b` | `1e07b47bc28684a77addb8222788bc29` |
| 发布渠道 | Thunderstore `LIFELAN/RandomCrestMod` 0.2.3 + GitHub tag `v0.2.3` | — |

## 二、0.2.3 状态

- 内容：冲刺斩随机、滑步/连续缚丝每次重掷、HUD 出现/消失动画、纷乱↔基础猎手过渡、
  配置项（无名分组：`ToolUsesPerBench` / `CursedBind` / `ParryAlwaysSucceed`）、纹章选择红槽 `0.9 → 0.95`。
- 已知**潜在问题**（仅异常 / 边界路径才可能发作，正常游玩不可见，故当时未修）：
  1. `Restore()` 无 `try/finally`：`UpdateConfig` 抛异常时伪装可能残留。
  2. `_pending` 布尔残留：极端情况下会被之后的无关 `UpdateConfig` 消费。
  3. `TryReplenishTools` 免费补充无 finalizer：原方法抛异常则 `replenishResource` 停在 `None`。
  4. 针管雕像奖励不封顶（当时按“彩蛋无上限”处理）。
- 这几点正是 0.2.4 修复的对象。

## 三、0.2.4（v2 重构版）状态

- 结构：
  - `Core/Rules.cs`：所有数值与纯规则（无 Unity 依赖），带 38 项断言测试。
  - `Runtime/`：`ConfigLease`（招式配置的唯一持有者 + 伪装）、`Gate`（统一门控）、
    `ConfigPool`（招式池 / 冲刺判定）、`SpoofPatches`（`IsEquipped` / `CheckIfCrestEquipped`）。
  - `Features/`：`Attacks` / `Bind` / `Taunt` / `Tools`+`ToolPatches` / `Spells` / `Rewards` / `Parry/*`。
  - `Presentation/`：HUD / 纹章美术 / 存档界面 / 动画兜底（沿用 0.2.3，字节级一致）。

- 相对 0.2.3 的**可感知**差异（正常游玩唯一能感觉到的变化）：
  - **针管雕像奖励封顶 20**（`RewardLedger.StatueRespectsCap = true`）。理由：不破坏游戏原有消耗品上限机制。
    （该奖励本身是彩蛋性质，需装备纷乱时用随机抽到的针管造成伤害才触发。）

- 相对 0.2.3 的**结构性改进**（正常游玩不可感知，只提升鲁棒性 / 可维护性）：
  - `ConfigLease` 先清状态再 `Restore` → 异常不再残留伪装。
  - 攻击请求按帧失效（`_requestFrame` + `Restoring`）→ 无 `_pending` 残留。
  - `TryReplenishTools` 用 `[HarmonyFinalizer]` 还原 → 异常也恢复。
  - `ToolBudget.SyncAll` 作为工具次数唯一写入点。
  - 纯规则可单元测试（`tests/CoreTests`，`dotnet run`）。

- 复刻 0.2.3 的行为决策（v2 重写时曾回归，测试中已修回）：
  - **攻击始终强制重掷**（不被缚丝 / 嘲讽的所有权挡住）。
  - **缚丝抢占并持有自己的纹章**，`cState.isBinding` 期间不重掷（野兽缚丝特效完整）。
  - **嘲讽配置忙时降级为普通、不抢占**（冲刺中游戏本身禁止嘲讽，属于不可达场景）。

- 测试状态（2026-10-11）：
  - 编译 0 警告 0 错误；`CoreTests` 38/38 通过。
  - 游戏内已确认：冲刺斩随机、连续/滑步缚丝每次随机、野兽缚丝特效完整、攻击始终随机、
    嘲讽不抢占、雕像封顶 20 生效。用户继续跑完整流程测试。

- 发布前待办：
  1. 关 `RandomCrestModPlugin.DebugLogging`（当前测试用 `true`）。
  2. 合并进主仓库（保留 git / CI / 打包脚本）；`docs/DEV_NOTES.md` 以线上版为主，另存 `docs/REWRITE.md`。
  3. CHANGELOG 0.2.4、README（雕像上限已改）、确认版本号。
  4. `dotnet tcli build` → 推 `v0.2.4` tag（触发 Thunderstore CI）+ 建 GitHub Release。

## 四、发布决策（用户 2026-10-11 定）

- **不主动发布 0.2.4。**
- 若收到 **0.2.3 的 bug 反馈**，且该问题在 **0.2.4 已修复** → 再考虑发布 0.2.4。
- 若无反馈 → 保持 0.2.3 在线；0.2.4 仅作为本地优化分支保留。
- 判断依据：两版正常游玩手感基本无差（唯一差异是雕像封顶），0.2.4 的价值在代码结构与抗异常能力，
  因此以“是否修掉了 0.2.3 的反馈 bug”作为是否发布的触发条件。

## 五、资产位置

- 0.2.3 发布 DLL 备份：`C:/Users/fuenlai/Desktop/rc_backup_0.2.3/RandomCrestMod.dll`
- 0.2.4 测试 DLL：已装到 `BepInEx/plugins/lifelan-RandomCrestMod/`（md5 `1e07b47b…`）
- v2 重写说明：`C:/Users/fuenlai/Desktop/RandomCrestMod-v2/docs/REWRITE.md`
- 0.2.3 备份也可从 GitHub tag `v0.2.3` / Thunderstore 0.2.3 重新获取。
