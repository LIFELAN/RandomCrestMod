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

## 七、0.2.6（未发布，开发中）

> 基于 v0.2.5（git `master @ f3dd012`，tag `v0.2.5`）。工作区 8 个功能文件改动，**未提交**。
> 全流程测试（含蓄力/疾风步/缚丝/诅咒缚丝/工具）通过，用户确认无新卡死。

### 1. 蓄力斩过场清零（bug，已修）

- 现象：蓄力斩在过场后从 0 重新蓄力；其它纹章保持蓄力。
- 根因：Sprint FSM 有全局 `HC CONFIG UPDATED -> Cancel All -> Reset -> Reset Nail Charge State`；
  `Reset Nail Charge State` 调 `HeroControllerMethods.SetAllowNailChargingWhileRelinquished`（枚举 0），
  清掉“交出控制权时保留蓄力”的开关。旧守卫 `IsSprintOrSkid` 只看冲刺/滑步，覆盖不到
  “蓄力中 / 过场中”时的换配置（`Release` / `Abort` / `OnNailArtConfirmed`）→ 发事件 → 清零。
- 修法：`ConfigPool.ShouldSwapQuietly(hero)` = `IsSprintOrSkid` **或** `cState.nailCharging` **或**
  `cState.transitioning`。`Acquire` 在此状态下强制静默；`Release` 用它；`Abort` 恒静默；
  `OnNailArtConfirmed` 用它。
- 日志证据：`[ConfigLease] forcing quiet release (Attack) nailCharging=True sprint=False` 等；
  `[QuietSwap] swallowed HC CONFIG UPDATED (nailCharging=True ...)`。旧代码在这些时刻会发事件。

### 2. 滑步缚丝后冲刺斩串味（bug，已修）

- 现象：滑步缚丝后立刻冲刺斩，冲刺斩用了缚丝那个纹章。
- 根因：缚丝租约在冲刺期间被 `TickOwner` 故意保留（避免 Sprint FSM 忙时发事件）；
  `OnDashStabStarted` 守卫 `owner != Dash && !(owner == None && fromDashAttack)` 在 `owner == Bind`
  时直接 return → 不重掷、不刷 `SetDashStabVariables`，且 `SpoofCrest` 仍是缚丝纹章。
- 修法：真·冲刺斩（`fromDashAttack && !isBinding`）时 `force` 抢权重掷（owner `None` / `Bind` / `Taunt`），
  和普通攻击 `OnUpdateConfig` 的 `force` 行为一致。日志 `(re-roll, was Bind)`。
- 安全性：抢权要求 `!cState.isBinding`；`BindCompleted()`（给 Rage/Reaper buff）在 `Send Bind Event`，
  严格早于 `End Bind`（才清 `isBinding`），所以 buff 已结算。实测 48 次 `was Bind` 全部发生在
  缚丝 FSM `Cooldown -> Idle` 之后（日志亦见 `Rage Bind ... -> Rage Idle` 正常）。

### 3. 加固：过场期间不装配置

- `Attacks.TryStartSprintMoveset` 与 `Bind.BeginAttempt` 增加 `cState.transitioning` 守卫。
- 原因：`RandomCrestRunner` 在 `transitioning` 时每帧 `Abort`，而 `TryStartSprintMoveset` 又会立刻重新装，
  导致“拆→装”抖动（日志曾出现单次过场 ~100 条连续 `swallowed HC CONFIG UPDATED`）。修复后最长连续 ≤3。

### 4. 配置说明中英双语

- 三个配置项描述改成“中文\nEnglish”，键名/默认值不变。
- BepInEx 无内置本地化；游戏内 ConfigurationManager 字体可能不渲染中文（方框），r2modman 文本编辑器正常。
- 强制工具桥段（飞镖任务）的 HUD 图标：`RandomIconService.For` 在 `ToolItemManager.IsCustomToolOverride`
  为真时返回 null，让 HUD 显示任务工具（直标）的真实图标，而不是纷乱的随机图标。

### 5. 工具：强制工具桥段（飞镖任务）保持原版

- 现象：洛迪（Loddy）房间的飞镖任务里，普通纹章会被游戏强制装备直标（Straight Pin）且不走消耗，
  装备纷乱时却被随机工具替换，HUD 看似不消耗、实际扣了共享次数。
- 根因：游戏用 FSM 动作 `SetCustomToolOverride`（`full.cs:545634`）把攻击工具强制覆盖为直标；
  `ToolItemManager.GetBoundAttackTool(..., Active)` 会直接返回它，而 `ToolItemManager.IsCustomToolOverride`
  （`full.cs:206000`）就是“当前有强制覆盖”的开关。mod 的 `GetBoundAttackToolPatch` 只看 `IsPicking`，没检查它。
- 修法：`IsCustomToolOverride` 为真时，整条随机工具链让路：`GetBoundAttackToolPatch` 不重掷、
  `GetAttackToolBindingPatch` 不套假绑定、`DidUseAttackToolPatch` 不扣共享预算、
  `ThrowBarrage.AfterThrow`/`Tick` 不武装/继续连投。
- 好处：不需要硬编码具体 NPC；任何强制指定工具的桥段（任务 / 教程 / 小游戏）都自动回到原版。

### 6. 工具：储液针管（Extractor）绕过共享预算

- 现象：装备储液针管（Extractor）使用时消耗共享次数，消耗完后坐椅子补不回来，
  要切到别的工具补一下再切回来才能用。
- 根因：`EnsureBuilt` 把所有 `ToolItemType.Red` 收进 `RedPool`，Extractor 也是红的，于是
  `GetToolStorageAmountPatch` 报 `Capacity`、`SyncAll`/`Mirror` 写它的 `AmountLeft`、`Consume` 扣 `_left`。
  但原版 Extractor 的 FSM（`Ex Grnd Check / Ex Charge / Ex Stab …`，`_fsm_init_full.txt:719+`）没有任何
  `CustomToolUsage`，而 `DidUseAttackTool` 只在 `!IsCustomUsage` 时才扣 `AmountLeft`（`full.cs:50082`）——
  即原版用它基本不消耗。mod 却把它的真实 `AmountLeft` 覆盖掉、并让用它的行为扣共享次数。
- 修法：`IsRed` 对 `VanillaEquipTools` 返回 false；`SyncAll` / `Mirror` / `BeginFreeRefill` 跳过它们。
  这样 `GetToolStorageAmount` / `Consume` / `IsOutOfUses` / `CanThrowToolPatch` 自动不动 Extractor。
  `RedPool` 仍保留它，随机掷到照常可用（它不靠 `AmountLeft` 工作）。
- **安全性**（用 UnityPy 读资产确认）：Extractor 是 `ToolItemBasic`，`baseStorageAmount = 0`、`isCustomUsage = 0`、
  usage 固定为 FSM 事件 `EXTRACTOR`。因为 `baseStorageAmount == 0`，`CheckIfToolEquipped` 的
  `RequiredAmountLeft` 判断被短路（直接返回 true），`ToolItemBasic.Usage` 也不随 `AmountLeft` 变，所以
  本改动不可能影响它的 FSM / 控制权。

### 7. 工具：念珠炮充能与共享次数联动（手动充能回灌）

- 现象1：共享次数花光后 HUD 显示炮是满的，但道具用不了。
- 现象2：坐椅子手动给炮充能，花念珠但充能进度不涨。
- 根因：`Tick` 每帧把 `AmountLeft` 直接写成 `Capacity`/`_left`，既和 `IsOutOfUses`（看 `_left`）打架，
  又把手动充上去的值立刻覆盖。
- 修法（只在 `Tick` 一处）：
  - 若炮自身 `AmountLeft > _left`（说明玩家手动充能了）→ `_left = Math.Min(炮的 AmountLeft, Capacity)`
    （把手动充能**回灌**为共享次数，封顶共享容量，避免其他纹章的高充能灌进来）；
  - 然后 `SetAmount(炮, _left)`（炮始终跟随共享次数）。
  `IsOutOfUses` / `SyncAll` / `Mirror` 恢复原样（不再特殊化念珠炮）。
- 效果：炮与共享次数联动（用道具会一起降、门控一致、`Shoot Loop` 长按连发保留、坐椅子 `Reset()` 补 `_left`）；
  手动充能（花念珠）会把手动值提升成共享次数，因此不再被覆盖。HUD 环 = `AmountLeft / Capacity`。

### 8. 发布前清单

1. `RandomCrestModPlugin.DebugLogging = false`、`ConfigSwapDiagnostics = false`（现均为 `true`，测试用）。
2. `Directory.Build.props` / `thunderstore.toml` 版本号 → `0.2.6`；CHANGELOG `Unreleased` → `0.2.6`。
3. `dotnet build -c Release`（应 0 警告 0 错误）→ `dotnet tcli build`。
4. 推 `v0.2.6` tag + GitHub Release（触发 Thunderstore CI）。

### 9. 未解 / 观察项

- 液体工具（跳蚤秘酿）**储备量**：随机投掷不扣；但“免费坐椅补给”在**该工具被装备**时会给它补瓶并扣储备
  （`BeginFreeRefill` 只免货币、不免液体 `reserveCost`；生质液瓶有液腺时无限不扣）。用户评估与道具丢失无关，暂不修。
- 红工具 `alternateUnlockedTest` 全为空 → `SyncAll` 写 `AmountLeft` 不会解锁任何工具（确认无副作用）。
