# RandomCrestMod 重写说明（v2）

> 本目录是重写版，原目录 `RandomCrestMod` 未改动。
> 构建产物：`bin/Release/netstandard2.1/RandomCrestMod.dll`（**不会**自动复制到游戏目录，需手动安装）。

## 一、结构

```
Core/                 纯规则，无 Unity / 游戏类型，可单独测试
  Rules.cs            次数、免费投掷、退丝、碎片、念珠、嘲讽权重与发放

Runtime/
  Gate.cs             唯一的"功能是否生效"判断
  ConfigLease.cs      招式配置的唯一写入者（所有权 + 伪装）
  ConfigPool.cs       招式池、冲刺/滑步判断、FSM 查找
  SpoofPatches.cs     ToolCrest.IsEquipped / CheckIfCrestEquipped 伪装

Features/
  Attacks.cs          普通/上/下/墙斩、蓄力斩、冲刺斩
  Bind.cs             随机缚丝、诅咒缚丝、Shaman 落水与场景门桥接
  Taunt.cs            随机嘲讽（三形态）
  Tools.cs            工具：ToolPool / ToolBudget / ThrowBarrage / VoltToggle
  ToolPatches.cs      工具相关 Harmony 补丁（只做转发）
  Spells.cs           法术退丝
  Rewards.cs          奖励账本（碎片、念珠、雕像）+ 针管奖励
  Parry/              十字绣（沿用原实现，仅改引用）
  LifebloodSyringePatch.cs / RuneRageDamagePatch.cs / ReaperPayoutPatch.cs / StatuePickupService.cs

Presentation/         HUD、纹章美术、存档界面、动画兜底（沿用原实现，仅改引用）

RandomCrestModPlugin.cs   插件入口、配置、驱动器（Runner）
tests/CoreTests/          Core 规则的控制台测试（dotnet run）
```

## 二、相对旧版的主要改动

### 结构
1. **配置所有权**：旧版用 `_active / _nailArtActive / _bindActive / _dashActive / _tauntHold / _pending / IsSpoofing` 等十多个标志维护同一件事。新版只有 `ConfigLease` 一个写入者，伪装由"当前持有者"推导，两者不可能不一致。
2. **共享次数单一写入点**：所有 `AmountLeft` 写入都经过 `ToolBudget.SyncAll()`。
3. **纯规则独立**：次数、免费投掷、退丝、念珠等数值在 `Core/Rules.cs`，有 38 项测试。
4. **奖励统一**：所有碎片、念珠、雕像发放经过 `RewardLedger`。
5. **门控统一**：所有功能只问 `Gate`。`OnlyOnRandomCrest` 开关已删除（原本一直为 true）。
6. **补丁只转发**：Harmony 补丁不再直接读写状态。

### 修复（对应上一轮分析）
- **第 1 项（异常残留）**：`ConfigLease.Restore` 先清状态再调用游戏方法，异常也不会留下伪装。`Abort` 统一在死亡/切场景时调用。
- **第 2 项（`_pending` 与 Restore 交叉）**：斩击请求只在提出它的那一帧有效；还原期间（`ConfigLease.Restoring`）忽略请求。
- **第 4 项（守卫不一致）**：所有配置占用都走同一套优先级（Dash < Attack < NailArt < Bind < Taunt）。
- **第 6 项（雕像上限）**：`RewardLedger.StatueRespectsCap = true`，达到 20 个上限后不再发放。改成 `false` 可恢复旧行为。
- **第 8 项（免费补充状态丢失）**：`TryReplenishTools` 改用 Harmony finalizer 还原，异常时也会恢复；`BeginFreeRefill` 不再用已被改过的值覆盖原值。

### 有意的行为变化（需要你确认）
- **配置分节变了**：`ToolUsesPerBench` 在 `[Tools]`，`CursedBind` 与 `ParryAlwaysSucceed` 在 `[Gameplay]`。旧 cfg 里空分节的值不会被读取，新文件会用默认值生成。如果你改过这些值，需要重新设置。
- **抢占规则变化**：缚丝、蓄力斩、嘲讽可以抢占正在进行的普通斩击或冲刺。旧版是拒绝（缚丝）或降级（嘲讽）。
- **冲刺中的普通斩击**：静默安装，并等待 Sprint FSM 空闲后再还原（保留了旧版的保护）。
- **雕像上限**：见上。
- **不再自动复制 DLL** 到 `BepInEx/plugins`。

### 未改动
- 所有 FSM 状态名、事件名、Harmony 目标、数值（次数 16、免费投掷 2%/件 上限 40%、嘲讽 80/14/6、念珠 10/格、十字绣 50% 等）。
- 全部美术、HUD、存档界面、动画兜底、十字绣实现。

### 游戏内测试后的修复
- **疾风步停不下来、冲出场景**：重写时漏掉了旧版 `FSMUtility.SendEventToGameObject` 前缀补丁，静默换招时 "HC CONFIG UPDATED" 没有被吞掉，取消了 Sprint FSM。已在 `Runtime/SpoofPatches.cs` 的 `QuietSwapPatches` 中补回，只在 `ConfigLease.Quiet` 期间生效。
- **缚丝配置启动过晚**：漏掉了 `HeroController.CanBind` 后置补丁，已在 `Features/Bind.cs` 的 `BindStartPatches` 中补回。
- **冲刺中的普通斩击**：补上了旧版的保护。静默安装的斩击会等 Sprint FSM 空闲后再还原。

- **连续缚丝总是同一个动作**：缚丝尝试在按住期间不会重新抽取，且 `BeginAttempt` 遇到已持有的配置会直接返回。现改为每次按下缚丝键（`WasPressed` 上升沿）都重新抽取，并替换旧配置；正在进行的缚丝或冲刺时用静默换招。

- **单次缚丝女巫与野兽同时出现**：替换已持有的招式时，`ConfigLease.Acquire` 直接装上新招式，没有先还原旧招式，旧招式的根节点（女巫）仍激活。现在替换前先静默还原（`Restore`），保证同一时间只有一套招式的根节点是激活的。
- **同一次按键抽取两次**：`CanBind` 和 `Tick` 同一帧各抽取一次，已加按帧守卫，每帧只抽取一次。
- **滑步中野兽缚丝偶尔不生效**：和上面同一个替换问题（残留的根节点状态干扰野兽根节点）。此项是根据代码推断的，**还需要日志确认**，见下文。

> 教训：旧版补丁清单应该逐条对照。下次改动前先列出所有 `PatchAll` 的目标，逐一核对。

## 三、验证状态

| 项目 | 状态 |
|---|---|
| 编译（Release，0 错误，0 警告） | ✅ |
| Core 规则测试（38 项） | ✅ |
| 游戏内行为 | ⚠️ **未测试**，需要你在游戏里验证 |

## 四、游戏内验证清单

安装 DLL 后，建议按这个顺序测：

1. **启动与纹章**：纹章出现在长椅菜单，六个槽位可用，卸下纷乱后其它纹章无变化。
2. **随机斩击**：普通/上/下/蹬墙斩击能切换招式，方向保持不变。
3. **冲刺斩**：冲刺中按攻击，冲刺斩能随机，且不卡住、不漂浮（重点：冲刺中途抢占）。
4. **蓄力斩**：能随机，结束后恢复。
5. **随机缚丝**：普通、疾风步、滑步中缚丝都能正常结束。
6. **诅咒缚丝**：触发后序列正常播放，念珠增加，结束后操作正常。
7. **落水与场景门**：随机萨满空中缚丝落水不穿出场景，能穿过场景门。
8. **嘲讽**：三种形态与声音/动作一致；地面完整嘲讽后按 80 碎片结算。
9. **十字绣**：真格挡退丝；自动反击 50%；卸下纷乱后原版。
10. **工具**：随机投掷、雕像连投、共享次数、坐椅子补满、免费补充。
11. **雕像**：钟心镇拾取一次；针管奖励在 20 个上限后停止。
12. **异常恢复（第 1 项）**：死亡、切场景后招式都能回到真实纹章，没有残留伪装。
13. **存档**：读档、切换纹章后 HUD 与存档界面正常。

## 五、需要验证的旧问题（未在本版修复）

- **卸载后的存档**：卸载模组后，存档里的 `RandomCrest` 纹章 ID 会怎样，需要实测。模组自己无法在卸载后修复存档。
- **`CanNailArt` 的调用方**：未确认它的调用频率。
- **SilkCurseMod 兼容**：`ToolCrest.IsEquipped` 仍是全局伪装点，兼容方案未实现。
