# 版本状态记录：0.2.3 / 0.2.4

> 快照时间：2026-10-11。本文档是两版状态的长期记录，供日后 bug 反馈对照使用。
> 维护：主仓库这份为准；v2 工程目录里有一份同步副本。

## 一、总览

| 项 | 0.2.3 | 0.2.4（v2 重构版） |
|---|---|---|
| 状态 | **已发布** | **已发布（v2 重构版）** |
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

- 相对 0.2.3 的**bug 修复**（用户复现并验证）：
  - **疾风步踩墙沿丢控制权 / 飞出去**（长期存在）：墙 / mantle 状态下不再误释放配置（`Attacks.TickAttack` 用
    `IsSprintOrSkid(hero)` 判断忙），还原时静默（`ConfigLease.Release` 在冲刺状态下不发 `HC CONFIG UPDATED`），
    Sprint FSM 不再被取消。
  - **落地后第一时间嘲讽声音 / 动作不符**：非野兽嘲讽时若当前配置是 Warrior（野兽），先放掉持有者
    （`Taunt.Roll`），动作与声音一致。

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
    嘲讽不抢占、雕像封顶 20 生效；**卡死修复（Bug1）与嘲讽声音/动作修复（Bug2）用户已实测通过**。

- 发布前待办：
  1. 关 `RandomCrestModPlugin.DebugLogging`（当前测试用 `true`）。
  2. 合并进主仓库（保留 git / CI / 打包脚本）；`docs/DEV_NOTES.md` 以线上版为主，另存 `docs/REWRITE.md`。
  3. CHANGELOG 0.2.4、README（雕像上限已改）、确认版本号。
  4. `dotnet tcli build` → 推 `v0.2.4` tag（触发 Thunderstore CI）+ 建 GitHub Release。

## 四、发布决策

- 2026-10-11：用户复现并确认修复了上述两个 bug（疾风步卡死、嘲讽声音/动作），触发条件满足，
  **0.2.4 已发布**（原计划为“不主动发布，除非 0.2.3 反馈的 bug 在 0.2.4 已修”）。
- 后续版本判断依据不变：正常游玩手感与 0.2.3 基本无差（唯一差异是雕像封顶 20）；0.2.4 的价值在
  代码结构与抗异常能力。

## 五、资产位置

- 0.2.4 发布 DLL：Thunderstore `LIFELAN/RandomCrestMod` 0.2.4 + GitHub tag `v0.2.4`
- 0.2.3 发布 DLL 备份：`C:/Users/fuenlai/Desktop/rc_backup_0.2.3/RandomCrestMod.dll`
- v2 重写说明：`docs/REWRITE.md`（仓库内）

## 六、0.2.5（配置分节修复）

- 三个配置项从**无名分组 `[]`** 改到命名分组 **`[General]`**。
- 原因：r2modman 的配置解析会丢掉分节名为空的分组（`sections.filter(s => s.sectionName !== "")`），
  导致设置无法在 r2modman 里管理；游戏内 ConfigurationManager 则能显示空分节，所以之前只在游戏内可见。
- 代价：游戏内 ConfigurationManager 会多显示一个 `General` 标题。
- 兼容性：旧 `[]` / `[Tools]` 里的值会变成孤儿，需重新设置（数值默认不变）。
- 纯配置分节改动，无玩法变化。
