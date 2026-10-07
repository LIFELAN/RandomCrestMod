# RandomCrestMod — 开发交接 / 关键点

> 这份是给后续（重启对话后）的自己和 AI 用的开发笔记，记录**现状、关键决定、坑和待办**。
> 面向玩家的说明见仓库根目录 `README.md` / `README.en.md`。

## 零、版本 & 状态（每次开工先看这里）

> **当前版本 v0.2.0（已发布 Thunderstore + GitHub Release / tag `v0.2.0`）**：在 v0.1.9 基础上新增“收集奖励”四件套，全部只在装备纷乱时生效，均已实测通过：
> 1. 法术概率退丝 `SpellSilkRefund`（详见“五之五”）：除十字绣外每次施放首个扣丝 `3%×已获得法术` 立即退丝；十字绣照旧真格挡必退。
> 2. 免费投掷口径改为“已获得红色工具 × 2% + 曲镰升级 +2%”（上限 40%），工具袋不再提供免费投掷（容量 +25%/级保留）。
> 3. 生质液瓶随机掷到且拥有生质液腺时，蓝血由 +1 提升到 +3（`LifebloodSyringeService` / `LifebloodSyringePatch.cs`，复用原版 `ADD BLUE HEALTH` 连发）。
> 4. 储液针管 / 陷阱设置器装备时豁免随机替换，走原版功能（任务可完成，HUD 显示原图标）。
>
> 新增文件 `SpellSilkRefund.cs`、`LifebloodSyringePatch.cs`；版本号 `Directory.Build.props` / `thunderstore.toml` 均为 `0.2.0`。
> 编译 `dotnet build -c Release` **0 警告 0 错误**；`dotnet tcli build` 产出 `dist/LIFELAN-RandomCrestMod-0.2.0.zip`。
>
> **历史：已发布** v0.1.9（= v0.1.8 + 十字绣真格挡退丝 / 纹章贴图优化 / Glow + 技能弹窗剪影；tag `v0.1.9`，已发 Thunderstore）。
> 更早：v0.1.8（普通档嘲讽也可给 0 念珠，未单独写 changelog）。
> 本版三块内容（均已并入 `## 0.1.9` 更新日志）：
> 1. 十字绣**真格挡退还本次灵丝** `ParrySilkRefund`（详见“五之四”）——真格挡不花丝，自动释放照常花；
> 2. 纹章贴图优化 `crest_icon.png` / `crest_silhouette.png` / `crest_glow.png`（详见“六”）；
> 3. 普通档嘲讽献祭念珠范围 **1~50 → 0~50**：现在有概率消耗碎片但不获得念珠。
>
> 本版发布文件（已提交）：新增 `ParrySilkRefund.cs`、`SkillGetMsgPatches.cs`、`Assets/crest_glow.png`；
> 修改 `ParryClashTrigger.cs`（`NotifyAttacked` 只认一次）、`RandomCrestModPlugin.cs`（`EnableParrySilkRefund` + 注册）、
> `Assets/crest_icon.png`、`Assets/crest_silhouette.png`、`CHANGELOG.md`、`Directory.Build.props`、`thunderstore.toml`。
>
> 版本号已 bump：`Directory.Build.props` 与 `thunderstore.toml` 均为 `0.1.9`。
> 编译现状：`dotnet build -c Release` **0 警告 0 错误**；`dotnet tcli build` 产出 `dist/LIFELAN-RandomCrestMod-0.1.9.zip`。
> 发布流程见“十一”。
>
> ✅ **v0.1.9 已全部实测通过**（格挡退丝数值、美术修复、电枢球标枪形态、嘲讽三形态均已进游戏确认）。

> 以下 0.1~0.6 为 v0.1.7 的功能细节，保留作背景。

### 0.1 随机工具「连投 / 弹幕」

- 新增 `ThrowToolBarragePatch`（patch `HeroController.ThrowTool`，在 `RandomCrestModPlugin` 注册），
  配合 `RandomToolService.BeforeThrow / AfterThrow / NotifyToolConsumed`。
- **复用游戏自己的 `queuedAutoThrowTool` 循环**（它已负责等投掷动画），每次链式投掷重新掷一个新工具，
  不自己写计时循环。链上重掷用 `PickRedForUse(requireThrowPrefab: true)`，只从带 `ThrowPrefab` 的
  `ThrowablePool` 里选，避免非投射工具打断链。
- 单次按键额外投掷数 `ExtraThrowsPerPress = 工具袋等级 + (装备 Quick Sling 额外 +1)`。
- `BeforeThrow` 抓取 `willThrowTool` 并复位 `_throwConsumed`；`NotifyToolConsumed` 在
  `DidUseAttackTool` postfix 里标记"真的投出去了"；`AfterThrow` 负责武装/递减/结束链。
- **只在真实生成的投掷**（`_throwConsumed && hero.cState.isToolThrowing`，即 ThrowPrefab 路径）才武装链；
  FSM 事件类工具（如电枢球标枪）绝不武装。
- `EndChain` 只在自己确实持有链时才清游戏的 `queuedAutoThrowTool` / `willThrowTool`，其它纹章/无链帧
  完全不碰原版状态。`Tick` 会在死亡 / 危险重生 / 场景切换 / 预算耗尽时中止链。

### 0.2 工具袋（Tool Pouch）成长

- `UsesPerBench` 由固定值改为：`基础配置值 × (1 + 0.25 × PlayerData.ToolPouchUpgrades)`，四舍五入到整数
  （基础配置默认从 **20 改为 16**；`RandomCrestModPlugin` 里的说明同步更新）。
- 新增 `RollFreeThrow`：每级工具袋 **+8% 免费投掷概率，上限 40%**；命中免费投掷时把未扣的预算镜像回
  所有工具（`ApplyUsesToAllRedTools`），不消耗共享次数。

### 0.3 随机嘲讽调整

- 三形态由均等（各 1/3）改为**加权**：普通 **80%**、野兽 **14%**、投掷环 **6%**。
- `ApplyTauntHold` 改为返回 `bool`：配置被别的动作占用（如冲刺/滑步刚结束仍持有）时返回 false，
  `RandomTauntService.Roll` 在这种情况**把野兽降级为普通**，修掉"冲刺后第一次嘲讽声音与动作对不上"。
- 投掷环判定加固：`CheckIfToolEquipped` / `GetToolEquipInfo` / `IntTestToBool` 的覆盖条件由
  `IsRings` 改为 `IsActive`，并**双向作答**（Rings 才报装备，非 Rings 时把真实装备的投掷环报成未装备），
  避免玩家真的带了投掷环时把普通/野兽 roll 劫持成 RINGS 分支。

### 0.4 随机攻击侧配合

- `RandomAttackService.ApplyTauntHold`：若 `_dashActive` 但英雄已落地且不在疾风/滑步，先释放这个过期持有
  再安装嘲讽 config（避免静默不一致）；拿不到就返回 false。

### 0.5 嘲讽献祭（碎片换念珠）

- 新增：装备纷乱时，**在地面**完成一次嘲讽，若 `PlayerData.ShellShards >= 80`，结束时消耗 80 碎片并按
  **roll 到的原始口味**给念珠（`PlayerData.geo`）：普通随机 0~50、野兽 60、投掷环 80；碎片不足则纯表演、
  不消耗。货币走 `CurrencyManager.TakeShards` / `AddGeo`，HUD 计数器动画 + roll 音效（即“提醒音效”）。
- 关键实现点：
  - `_payoutFlavour` 单独保存 **roll 的原始结果**（不随野兽→普通的视觉回退而变），所以给珠概率仍是 80/14/6。
  - **必须完整播完**：`RandomTauntPatches` 新增 `Tk2dWatchAnimationEvents.OnEnter` postfix，仅当 FSM 进入
    `Taunt End Wait` 时调 `NotifyTauntCompleted()`。三种口味的嘲讽动作结束后都会进这个状态；空中嘲讽或
    被打断（`Taunt Antic`/`Taunt` 中途 CANCEL）都不会进，所以不结算。`_tauntCompleted` 由此置真。
  - `_paid` 保证每次 roll 只结算一次；`Roll()` 开头会先补结算上一轮（防止“回 Idle 同帧再次按下”时
    `Tick` 漏采样）。结算点在 FSM 回到 `Idle`（或 5s 兜底超时）。
  - 开关 `RandomCrestModPlugin.EnableTauntShardOffer`（写死 true）。不改 PlayerData 结构、不新增资源。
- 实测重点：地面完整嘲讽才结算（空中/中途受击打断不给）、碎片不足不结算、三种口味给珠数、
  野兽 roll 被降级时给珠仍按原始 roll。

### 0.6 发布前待办（实测通过后）

- [x] 版本号：`Directory.Build.props` 与 `thunderstore.toml` 已 bump 到 **0.1.7**。
- [x] README.md / README.en.md 已补连投 / 免费投掷概率 / 工具袋成长 / 嘲讽献祭，配置默认改为 16。
- [x] `CHANGELOG.md` 已加 0.1.7 条目，`dotnet build -c Release` + `dotnet tcli build` 出包。
- [x] 实测重点：连投稳定（不哑弹/不卡）、免费投掷概率体感正常、嘲讽三形态与声音动作一致。
- [x] 实测嘲讽献祭：地面完整嘲讽才结算、三种口味给珠数、空中/中途受击打断/不足 80 无作用。

---

## 一、当前状态（截至已发布的 v0.1.9）

- 编译：`dotnet build -c Debug`，**0 警告 0 错误**。
- 已安装：`<游戏>/BepInEx/plugins/lifelan-RandomCrestMod/RandomCrestMod.dll`（构建会自动复制）。
- 仓库：https://github.com/LIFELAN/RandomCrestMod（默认分支 `master`）。
- **已发布 v0.1.9**（十字绣真格挡退丝 / 贴图优化 / Glow + 技能弹窗剪影）。

最近提交：
```
4d8b254 Release v0.1.9: parry silk refund, crest texture polish, taunt 0-50
8bf08ea Release v0.1.8: standard taunt can roll 0 rosaries
4655786 Release v0.1.7: tool barrage, pouch growth, taunt offering
221dcbd Make the Chaos crest's Cross Stitch auto-counter; drop Delver's Drill
5823200 Let a random Shaman bind cross scene gates
0f7931b Randomize the R3 taunt and Voltvessels forms; drop Rosary Cannon
```

## 二、模组基本定义

- 插件：BepInEx 5，GUID `io.github.lifelan.randomcrestmod`，类 `RandomCrestModPlugin`。
- 纹章内部 id：**`RandomCrest`**（不要改，存档 `CurrentCrestID` / 资源名都靠它）。
- 显示名：中文 **纷乱**，英文 **Chaos**；说明：`你永远不知道下一秒会发生什么。` / `You never know what will happen next.`
- 创建方式：`CrestService` 克隆猎手(Hunter)纹章，运行时 `Add` 进 `ToolItemManager.crestList`。
- 槽位（`CrestService.Slots`）：6 个，全部 `IsLocked=false`：
  - Skill / Neutral (0,-0.9)、Red / Up (0,0.9)、Blue (2.5,-1.8)、Blue (-2.5,-1.8)、Yellow (-1,-2.7)、Yellow (1,-2.7)。

## 三、功能开关与门控（重要）

- `RandomCrestModPlugin` 里，除 `ToolUsesPerBench` 外全是 **写死的 static readonly**：
  `EnableRandomAttacks / EnableRandomBind / EnableRandomTools / EnableRandomSpells / EnableCustomHudFrame / EnableCustomSaveSpool / EnableRandomIcons / EnableRandomTaunt / OnlyOnRandomCrest(true) / DebugLogging(false)`。
- **所有随机效果只在装备纷乱时生效**：
  - 攻击/缚丝：`RandomCrestModPlugin.OnlyOnRandomCrest && CrestService.IsRandomCrestEquipped()`；
  - 工具/法术：`RandomToolService.GateOpen`（同上）；
  - 嘲讽：`RandomTauntService.Roll` / `Tick` 里的 `IsRandomCrestEquipped()`；
  - 随机图标：`RandomIconService.For` 走 `RandomToolsActive/RandomSpellsActive`；
  - HUD 外框：`HudFrameService.Tick` 检查 `IsRandomCrestEquipped()`。
- 唯一配置项：`[Tools] ToolUsesPerBench = 16`（坐椅子补满的基准次数，每级工具袋 +25%，补充免费）。
- **诅咒缚丝（cursed bind）功能已整体移除**，不要再加回来（见“八、历史坑”）。

## 四、随机攻击 / 缚丝（`RandomAttackService` / `RandomBindService`）

- 攻击入口：`HeroController.Attack` prefix 标记 → `HeroController.UpdateConfig` postfix 里 `ApplyGroup`（见 `RandomAttackPatches`）。
- 随机池 `GetPool(hero)`：取自 `HeroController.configs`，**排除 Cloakless**，共 **7 个均匀**：
  `Default(Hunter) / Warrior / Reaper / Wanderer / Whip(Witch) / Toolmaster / Shaman`。
  （Hunter 的 v2/v3 共用 `Default` 配置，所以猎手没有被加权；没有重复项。）
- `ResolveCrest(config)` 把配置映射到真实 `ToolCrest`，用于 spoof。
- spoof：`RandomAttackService.IsSpoofing` 为真时，`ToolCrest.IsEquipped` 只认 `SpoofCrest`（`RandomAttackPatches.ToolCrest_IsEquipped_Postfix`）。
- 冲刺/滑步保护（`IsSprintOrSkid`）：
  - 判定 = `IsSprintOrDash(cState)` **或 Sprint FSM 非 `Idle`**（覆盖松开疾风步后的 Skid 状态）；
  - 换配置会触发 `HC CONFIG UPDATED` → 全局取消 Sprint FSM → 卡死漂浮，所以：
    - `ApplyIfRequested` / `ApplyForBind` / `EndBind` / `Tick` 全部用 `IsSprintOrSkid` 判断；
    - **滑步中缚丝用“静默换配置”（`ApplyGroup(hero, group, quiet:true)`）**，不触发 `HC CONFIG UPDATED`，Sprint FSM 继续跑；
    - 静默换配置不通知 Sprint FSM，所以它缓存的冲刺劈砍对象/数值（`Dash Stab`、`Attack Speed/Time/Steps`）不会因这次绑定而刷新；普通劈砍与 Bind 本身不读它们，下一次冲刺劈砍时 `OnAttackCounterForDash` 会重掷并 `SetDashStabVariables` 刷新。
    - 旧文档里“滑步缚丝拿不到 Rage/Reaper”其实是 `TickDash` 提前 `Restore` 的 bug（`Send Bind Event` 在 `End Bind` 之前调用 `BindCompleted`），已在 v0.1.1 修掉（`TickDash` 有 `_active/_nailArtActive/_bindActive` 时不恢复），现在站立/疾风步/滑步缚丝都能拿到。
  - **普通劈砍也能随机了**（本次）：以前 `ApplyIfRequested` 在 `IsSprintOrSkid` 时直接 `return`，导致疾风步/空中疾风步/滑步里的普通、上、下劈砍完全不随机。现在改成同样用**静默换配置**（`ApplyGroup(..., quiet: IsSprintOrSkid(hero))`），并：
    - `Tick` 的 `_active` 分支里，若 `_dashActive && IsSprintOrSkid` 则暂停恢复（5s 超时兜底），避免恢复时发 `HC CONFIG UPDATED` 把 Sprint FSM 取消；“刚起步的冲刺”仍走原来的 `!_dashActive` 提前恢复逻辑；
    - `OnAttackCounterForDash` 加 `_pending` 门禁：普通攻击的 `IncrementAttackCounter` 不再多掷一次（那次会被紧接着的 `UpdateConfig` 覆盖），只有 Sprint FSM 真正的冲刺劈砍才重掷。
- 不要再用 `SPRINT CANCEL` + 延迟 的方案（实测会把大黄蜂弄成"跑出场景、失控"）。
- `ReaperPayoutPatch`：随机缚丝进 Reaper 模式时，临时把 `CurrentCrestID="Reaper"` 让回丝结算生效（`HealthManager.TakeDamage` prefix/postfix）。
- `RandomCrestAnimationLibrary` + `AnimationFallbackPatches`：合并所有纹章的动画库，兜底 `HeroAnimationController.GetClip`，并屏蔽 `Could not resolve animation clip` 日志洪水。

## 五、随机工具 / 法术（`RandomToolService` / `RandomToolPatches`）

- 入口：`HeroController.GetWillThrowTool` → `ToolItemManager.GetBoundAttackTool(binding, Active)`。
- 用 `GetWillThrowTool` 的 prefix/postfix 开一个"窗口"（`IsPicking`），只在投掷路径重掷，避免报告逻辑重复随机。
- 替换点在 `GetBoundAttackTool` 的 `ToolReturn Active` 分支；`ThrowTool` 会用 `GetAttackToolBinding` 反推 binding，所以补丁里对 spoof 工具返回按下的 binding。
- 池：`ToolItemType.Red`（工具）与 `ToolItemType.Skill`（法术，6 个）。
- **硬编码排除**：`Extractor`(Needle Phial)、`Silk Snare`(Snare Setter)、`Rosary Cannon`(念珠炮，使用方式特殊 + 快速连投容易哑弹)、`Screw Attack`(Delver's Drill / 掘洞钻，向下突进的钻头，随机投掷无法正常工作)。
- **特殊处理**：`Lightning Rod`(Voltvessels / 电枢球) 每次抽取时随机掷 `offState`(标枪，FSM 事件
  `LIGHTNING ROD`) / `onState`(流星锤，ThrowPrefab)，即 `RandomToolService.RollToggleState()`。该形态存在
  `PlayerData.LightningToolToggle`，所以 `RollToggleState` 会先快照玩家自己的值，`RestoreToggleState` 在
  投掷后 0.5s（`Tick`）或卸下纷乱时还原，尽量不动存档（`OnSaveLoaded` 清空快照）。**标枪形态随机替换下
  正常出招、共享次数正常扣，已实测通过（0.1.9）**。
- 共享次数：`_usesLeft` 初始 20，所有 Red 工具同步，坐椅子/读档 `ResetUses`；`BeginFreeRefill/EndFreeRefill` 临时把 Red 工具的 `replenishResource=None` 让补充免费（其它纹章不受影响）。
- 计数补丁：`GetToolStorageAmount`、`HeroController.CanThrowTool`、`HeroController.DidUseAttackTool`、`ToolItemManager.TryReplenishTools`。
- 绑定/法术只在装备纷乱时替换（`RandomToolsActive/RandomSpellsActive`）。
- **法术费用**：`PlayerDataSilkSkillCostPatch` 在 `RandomSpellsActive` 时把 `PlayerData.SilkSkillCost` 整体减 `RandomCrestModPlugin.RandomSpellSilkDiscount`（默认 1，下限 1）：原版 4 → 3，满血带蚤母卵（原版 3）→ 2，所以**蚤母卵在纷乱上仍有效**。判定（`CanThrowTool`）/ HUD 图标（`ToolHudIcon`）/ 所有技能 FSM 的 `TakeSilk`（经 `GetPlayerDataVariable` 读该属性）都读它，所以自动一致；缚丝（`SilkSpool.BindCost`）和工具（`Usage.SilkRequired`）完全不受影响。

## 五之二、随机嘲讽（`RandomTauntService` / `RandomTauntPatches`）

- 嘲讽由大黄蜂身上的 **`Silk Specials`** FSM 驱动（只在落地按 R3/V 时生效；空中 Taunt 直接结束）。
- 三种形态的判定：
  - `Silk Check` 状态用 `GetToolEquipInfo`/`IntTestToBool`/`CheckIfToolEquipped` 检查 **`Shakra Ring`（投掷环）**
    是否装备且数量足够；`BoolAllTrue` 全真 → 事件 `RINGS` → `Taunt Antic Rings`/`Taunt Rings`（独立动画）。
  - 否则 `PlayerdataIntCompare`(PlayerData `silk` vs 嘲讽消耗) 分岔：够丝走 `Silk Taunt`（耗丝），
    不够走 `SILKLESS`；两者最后都到 `Voice Type`，用 `CheckIfCrestEquipped` 检查 **`ToolCrest Warrior`
    （内部名，显示名就是「野兽」）** → `Beast`（粗犷音效）或 `Standard`。
- 实现：`ListenForTauntV2.OnUpdate` prefix 在事件发出前 roll 一个 `TauntFlavour`；随后只在
  `Silk Specials` 的 `Silk Check` / `Voice Type` 这两个状态里覆盖对应判定：
  - `CheckIfCrestEquipped.IsTrue`：命中 Beast 纹章时直接 `__result = IsBeast`（同时压过可能残留的攻击 spoof）。
  - `CheckIfToolEquipped.IsTrue` / `GetToolEquipInfo.DoAction` / `IntTestToBool.DoCompare`：Rings 时把
    "Rings Equipped" / "Has Two Rings" 置真（只写 FSM 变量，**不碰 PlayerData / 工具数据**）。
- **动作/特效匹配（已改为真·换 config，之前的手动开 root + 覆写 TauntSlash 不稳定，已弃）**：
  嘲讽的动作不是由 `CheckIfCrestEquipped` 决定的，而是 `Taunt` 状态里 `GetHeroAttackObject(TauntSlash)`
  读的 `HeroController.CurrentConfigGroup.TauntSlash`，再在 `Taunt Slash` 状态激活。野兽（Warrior）领一个
  **独占的** `TauntSlash`（在 `Hero_Hornet/Attacks/Warrior/Taunt Slash`，需要 Warrior 的 ActiveRoot 开着），
  其余纹章共用英雄根下的 `Hero_Hornet/Taunt Slash`。所以 roll 到野兽时：
  - `RandomTauntService.Roll` 调 `RandomAttackService.ApplyTauntHold(Gameplay.WarriorCrest)`；
    后者用 `FindGroup`（先按 `crest.HeroConfig` 引用、再按 config 名字匹配）找到 Warrior 的 ConfigGroup，
    再 `ApplyGroup(..., quiet:true)` 换过去，并置 `_tauntHold`。
  - `RandomAttackService.Tick` 在 `_tauntHold` 时提前 return（不自动 restore，只处理死亡/切场景），
    `Restore` 的守卫也把 `_tauntHold` 算进去。
  - 普通/投掷环调 `ReleaseTauntHold()`（换回真实纹章）——`Tick` 在 FSM 回 `Idle`/卸下纷乱/超时 5s 时
    会 `Clear` 并自动释放。
  - `_tauntHold` 与 `_active/_nailArtActive/_bindActive/_dashActive` 互斥（apply 前会检查，避免抢
    正在进行的攻击 config）。
- 所有覆盖都用 `IsInState()` 校验 `action.Fsm.Name == "Silk Specials"` 且 `ActiveStateName` 匹配，
  所以同样的 PlayMaker Action 在攻击/缚丝等其它地方完全不受影响。
- 其它纹章完全不受影响：没装备纷乱时 `Roll`/`Tick` 直接 `Clear`。

## 五之三、十字绣强化（`ParryAutoCounterService` / `ParryClashEffectPatch` / `HeroHighlight`）

- **只在装备纷乱时生效**（`CrestService.IsRandomCrestEquipped()`），其它纹章完全原版。
- **自动反击**（`ParryAutoCounterService` + `ParryAutoCounterPatch`）：`Silk Specials` FSM 的 `Parry Stance`
  状态里 `FINISHED` 出口从 `Parry Recover` 改到 `Parry Clash`（`ToState` 与 `ToFsmState` 都要改，
  `Fsm.DoTransition` 只读后者）。FSM 实例是所有纹章共享的，所以 `Tick()`（挂在 `RandomCrestRunner.Update`）
  每帧按纹章 apply/restore。
- **去火花**（`ParryClashEffectPatch`）：`ActivateGameObject.OnEnter` 前缀，只跳过对 `Parry Clash Effect`
  的**激活**；`Cancel All` 的取消激活照常放行。
- **后退高光**（`HeroHighlight` + `HeroHighlightPatch`）：只在 `Parry Clash` 期间给英雄本体渲染器写
  `_FlashAmount` / `_FlashColor`（`1` / `FFE0F0`，写死在 `RandomCrestModPlugin`），离开该状态时清 0。
  **只抬不压**，不干扰游戏自己的受击 / 无敌闪白。注意：游戏的 `SpriteFlash` 只在有 flash 时才刷新
  `_FlashAmount`，所以必须显式清 0，否则会残留。
- 配置项仍**只有** `Tools/ToolUsesPerBench`，高光数值等全部写死。

## 五之四、十字绣真格挡退还灵丝（`ParrySilkRefund`，0.1.9）

- 目标：装备纷乱时，十字绣**真实格挡成功**（立场被击中，走 `PARRIED`）就退还**本次施放实际花掉的灵丝**；
  自动释放（立场自然结束）不退还，维持两套释放的价值差。
- 实现：
  - `ParrySilkRefundPatch`：`HeroController.TakeSilk(int)` 的 **postfix**，仅当 `hero.silkSpecialFSM` 处于
    `Parry Start` 且 `RandomToolService.RandomSpellsActive` 时，把 `amount` 记进 `_pending`。
    该状态里 FSM 先 `GetPlayerDataVariable("SilkSkillCost")` 再 `TakeSilk`，所以拿到的正是折扣后的 3 / 2，
    和实际扣费完全一致（不依赖结算时刻再读一次的假设）。
  - `ParrySilkRefund.OnParried()`：在 `ParryClashTrigger.NotifyAttacked()` 里调用；`_pending>0` 且
    `RandomSpellsActive` 时 `HeroController.instance.AddSilk(_pending, heroEffect:false)`，然后清零。
  - 开关：`RandomCrestModPlugin.EnableParrySilkRefund = true`；plugin `Awake` 里
    `_harmony.PatchAll(typeof(ParrySilkRefundPatch))`。
- **关键坑 / 设计点（别踩）**：
  1. `NotifyAttacked` 会被 **PARRIED 的 `SendEvent` prefix** 和 **`CheckParry`/`TakeDamage` 的 postfix 兜底**
     各调一次 → 已在 `NotifyAttacked` 用 `if (Attacked) return;` 保证**只退一次**；`OnParried` 又靠
     `_pending` 清零二次兜底。
  2. **绝不要在 `ParryClashTrigger.Reset()` 里清 `_pending`**：`ParryAutoCounterService.Tick()` 与 FSM 同在
     `Update`，同帧顺序不保证；若 Tick 在 FSM 进入 `Parry Start` 之后才跑，会把刚记录的值清掉。现在用
     “下次 `Capture` 覆盖 + `OnParried` 消费”的语义，最稳。
  3. `OnParried` 里**再 gate 一次 `RandomSpellsActive`**：自动释放会残留 `_pending`，防止残留值在别的纹章
     真格挡时被误消费。
  4. `heroEffect: false`。原版涨丝（`HeroController.SilkGain`）也是 false；`heroEffect:true` 走的是
     `SpriteFlash.flashFocusHeal()`（大黄蜂本体白闪），而格挡时她正在 `Parry Clash` 动画里，看不清，
     还容易和受击/高光混淆。**主反馈是丝轴自动补丝动画**（`AddSilk` 内部必调 `silkSpool.RefreshSilk`）。
  5. 曾试过反射 `SilkChunk.regeneratedSound`（= `ui_silk_chunk_regenerated_option_2d`）播音效，**用户实测后
     要求去掉**，现在是纯丝轴版。不要再默认加音效。
  6. `AddSilk` 自身会 clamp 到 `CurrentSilkMax`、刷新 HUD、重置丝线回复，不需要额外保护。
- 兼容性：只影响「装备纷乱 + 随机法术启用」；其它纹章的十字绣完全原版。
- 桌面参考音（可删，代码不依赖）：`C:\Users\fuenlai\Desktop\Silksong_SilkSounds\`（11 个候选，含
  `ui_silk_chunk_regenerated_option_2d` / `ui_spool_shard_fill_up` / `hornet_bind_ready` 等）。
- 实测重点（0.1.9 已全部实测通过）：①真格挡退 3、带蚤母卵退 2；②自动释放不退；③满丝附近不溢出；
  ④连续多次格挡每次都能退；⑤换成别的纹章后十字绣完全原版。

## 五之五、收集奖励（法术退丝 / 工具免费投掷 / 生质液瓶 / 任务工具豁免，未发布）

> 目标：鼓励收集、让每次收集与任务有回报，并尽量能单一纹章通关。全部只在装备纷乱时生效。

### 1. 法术概率退丝 `SpellSilkRefund`（新文件）

- 除十字绣外的 5 个法术，**每次施放的首个扣丝**掷一次 `3% × 已获得法术数`（`ToolItemManager.GetOwnedToolsCount(Skill)`，0~6 → 0~18%），命中则**立即** `AddSilk(spent, heroEffect:false)`（与十字绣同一套动画/反馈）。
- 捕获点：`HeroController.TakeSilk(int)` postfix，仅当 `silkSpecialFSM` 处于技能扣丝状态（`Start Throw` / `A Sphere Start` / `A Sphere Repeat` / `Silk Bomb Start` / `Silk Bomb Restart` / `Silk Charge Begin` / `BossNeedle Cast`）时记录。
- **一次性**：`_rolledThisCast` 在首次扣丝时置真，`SpellSilkRefund.Tick()` 只在 FSM 回到 `Idle` 才复位，所以丝球延长 / 丝弹续发不会重复掷。
- `Parry Start`（十字绣）和 `Silk Taunt`（嘲讽）都不在技能状态表里；十字绣继续走 `ParrySilkRefund` 的真格挡必退，两者不叠加。`-1` 技能折扣（`RandomSpellSilkDiscount`）保留，退的是实际扣掉的量。
- 开关 `EnableSpellSilkRefund`。

### 2. 免费投掷改由已获得红工具提供 `RandomToolService`

- `RollFreeThrow()`：`ObtainedRedToolCount() × 2%`，曲镰升级（`Gameplay.CurveclawUpgradedTool.SavedData.IsUnlocked`）再 **+2%**，上限仍 **40%**。工具袋等级不再提供免费投掷（容量 +25%/级保留）。
- `ObtainedRedToolCount()`：遍历 `ToolItemManager.GetAllTools()`，取 `Type==Red && SavedData.IsUnlocked`，按 `tool.CountKey` 去重（升级线算 1，如弧爪→曲镰；丝弹三选一），排除 `Extractor`/`Silk Snare`。**用 `SavedData.IsUnlocked` 而不是 `IsUnlockedNotHidden`**，所以被升级替换隐藏起来的弧爪本体仍然计入。

### 3. 生质液瓶 +3 蓝血 `LifebloodSyringeService`（新文件）

- 生质液腺 = `PlayerData.HasLifebloodSyringeGland`；生质液瓶内部名 `Lifeblood Syringe`（`ToolItemStatesLiquid`，红色，在随机池里）。
- 生质液瓶的 `Heal` 状态用 `SendEventToRegister` 发 `ADD BLUE HEALTH`；`Blue Health Control` FSM（在 GameObject `Health` 上，`coremanagers_assets__gamecameras.bundle`，path_id `-1550123635733692519`）的 `Add Blue Health` 状态做 `healthBlue += 1` 并创建 1 个 `Blue HP Prefab`。
- 补丁在 `SendEventToRegister.OnEnter` postfix：事件=`ADD BLUE HEALTH`、`Fsm.ActiveStateName == "Heal"`、`RandomToolsActive` 且拥有生质液腺时，把 `_extraAddsRemaining = 2` 入队；`LifebloodSyringeService.Tick()` 每帧检查 `Blue Health Control` FSM 是否回 `Idle`，回就再发一次 `ADD BLUE HEALTH`，连发 2 次 → 原版一共建 3 个面具、`healthBlue = 3`。**完全复用原版流程，不自己写计数、不发 `UPDATE BLUE HEALTH`。**
- **坑（已踩过）**：`UPDATE BLUE HEALTH` 不是重建蓝血画面的事件，而是“清蓝血”——它触发 `HeroController.UpdateBlueHealth()`（`healthBlue = 0`）。第一版就是发它导致 +3 被立刻清 0。以后想重建蓝血画面，只能让原版 `ADD BLUE HEALTH` 自己跑，不要发 `UPDATE BLUE HEALTH`、也不要直接写 `PlayerData.healthBlue`（写了画面不会建面具）。
- `Blue Health Control` FSM 用 `Resources.FindObjectsOfTypeAll<PlayMakerFSM>()` 按 `Fsm.Name` 找并缓存（`HudFrameService` 同款做法）。
- 不要求生质液瓶有剩余弹药：随机池对红色工具统一镜像共享次数（`SetAmount(pick, _usesLeft)`），所以它总是可投；每次施放无冷却。新类 `LifebloodSyringeService` + `LifebloodSyringePatch`。

### 4. 任务工具豁免替换 `RandomToolService.IsVanillaEquipTool`

- `GetBoundAttackToolPatch.Postfix` 在替换前判断 `__result`（当前装备的工具）是否为 `Extractor` / `Silk Snare`；是则直接 return，走原版。它们本就不在 `RedPool`，所以共享次数 / 连投 / 免费补充都不会碰它们。
- `RandomIconService.For` 对这两个工具返回 null，HUD 显示原版图标。
- 目的：储液针管是任务道具，被随机替换就无法完成医生任务线；陷阱设置器同类型，一并豁免。

### 5. 触发入口

- 框架 Tick（`RandomCrestRunner.Update`）新增 `SpellSilkRefund.Tick()` 与 `LifebloodSyringeService.Tick()`。
- `RandomCrestModPlugin` 新增 `EnableSpellSilkRefund = true`，并注册 `SpellSilkRefundPatch` / `LifebloodSyringePatch`。

## 六、HUD / 存档界面美术

- 资源（`Assets/`，内嵌 DLL）：
  - `crest_icon.png` / `crest_silhouette.png` / `crest_glow.png` — 纹章本体图标 / 剪影 / 装备爆光（发光剪影）；
  - `crest_hud_frame.png` — 左上角缚丝丝轴外框（用户绘制，后期 gamma≈1.35 提亮），**ppu 420**；
  - `crest_save_spool.png` — 存档选择界面 spool（226×173）；
  - `crest_random_tool.png` — HUD 工具固定图标（红色，"兵械库"成就图标再上色 + 圆形柔边遮罩），**ppu 390**；
  - `crest_random_tool_poison.png` — 同上图标的紫色版（花芯囊中毒时），由红版整体 hue-rotate −0.23 生成，**ppu 390**；
  - `crest_random_spell.png` — HUD 法术固定图标（白色，"千丝万缕"成就图标），**ppu 420**。
- `HudFrameService`：**叠加方案**——不碰游戏 tk2d 网格/材质，而是在 `Bind Orb` 下挂一个自己的 MeshFilter/MeshRenderer：
  - 装备纷乱时隐藏游戏外框渲染器 + 显示我们的叠加；否则反过来；
  - 叠加对象的 `layer` 必须等于 `Bind Orb.gameObject.layer`（否则 HUD 相机不渲染）；
  - **读档 / 场景加载会重建 `Bind Orb`**，所以 `CrestPatches` 在 `GameManager.SetLoadedGameData` 和 `HeroController.SceneInit` 里调用 `HudFrameService.Reset()` 强制重新获取；
  - **关键坑（已修）**：游戏自己的 `Bind Orb` FSM 会在 HUD 出场动画里开关外框 `MeshRenderer.enabled`（`Init` 先关，等血量 HUD 出来即 `SHOW HP` 后 `Appear` 再开）。因此**绝不能缓存 `enabled` 再恢复**：获取时它通常是 `false`，恢复这个值会让所有其它纹章的外框永久消失。现在只记录「是不是我们把它关掉的」(`_weHidGameFrame`)，恢复时只在我们关过的情况下置 `true`；
  - `Reset()` 会先 `Restore()` 再丢引用（否则活下来的 HUD 会一直保持关闭），并销毁旧的叠加对象/网格，避免残留 ghost；
  - 首次获取时用 `_gameFrameRevealed` 等游戏先把外框显示一次（`Appear`）再接管，这样纷乱外框和其它 HUD 一样「一点点加载」，而不是进档瞬间就蹦出来。
  - 曾用"直接改游戏 `sharedMesh/sharedMaterial`"方案，会破坏其他纹章外框（已弃用，不要回退）。
- 纹章三张美术对应游戏预制体 `Template Crest` 的三个渲染器，**缩放不同**，做图时必须按同一物理尺寸换算：
  - `Crest Sprite`（`crestSprite`，缩放 ×1）— 本体图标；
  - `Crest Silhouette`（`crestSilhouette`，缩放 ×2）— 切换高亮时的剪影（`InventoryToolCrest.TransitionDisplayState` 在剪影↔图标之间交叉淡入淡出）。所以剪影图元像素≈图标的一半；
  - `Crest Submit Effects/Crest Glow`（`crestGlow`，缩放 ×2.85）— 选中/装备 Burst 的白色爆光。Core 约等于剪影物理尺寸、外圈光晕到约 1.3×。**没有这张图时，克隆自猎手的纷乱会保留猎手的 Glow**，切换/确认纹章时会闪出猎手形状的白色发光剪影。
  - 当前 `crest_silhouette.png` 是**手工描边**后洞填充的（描边原稿在桌面 `纷乱_图标描边.png`）：外轮廓内填实、中心圆与犄角根部小圆环保持镂空、不做形态学处理以保留尖角。
  - 当前 `crest_glow.png` 由 `crest_silhouette.png` 生成（core 缩到 193×195、放进 255×261 画布、高斯模糊半径 15）——想换形状就重跑同款流程。
- 内嵌贴图统一用 `SpriteMeshType.FullRect`（`LoadEmbeddedSprite`）：**Tight 网格会裁掉 Glow 这类软边 alpha**，必须保持 FullRect。
- 技能获取弹窗（`SkillGetMsg`，预制体 `Silk_Skill_Get_Prompt`）只在 `Setup` 里写 `Crest`/`Crest_Glow`，其 `Skill Group/Crest/Pivot/Crest_Silhouette` 预制体里烘焙的是猎手剪影且**脚本从不更新** → 获得法术时会看到猎手剪影叠在纷乱图标上。`SkillGetMsgPatches.SkillGetMsgCrestSilhouettePatch` 在装备纷乱时把它换成 `crest.CrestSilhouette`；其它纹章不碰。
- 随机图标：`RandomIconPatches.ToolHudIconSpritePatch`（按 `RandomIconService.For` 换 sprite；红 binding 在 `tool.PoisonDamageTicks > 0 && Gameplay.PoisonPouchTool.IsEquippedHud` 时用 `PoisonToolIcon` 紫色版）+ `RandomIconPatches.ToolHudIconColourPatch`（强制白、并关掉 `RECOLOUR`/`CAN_HUESHIFT`，防止原版中毒/电枢着色器把专属图标去色）。**注意补丁点在 `ToolHudIcon.SetIconColour` 覆写上，不是 `RadialHudIcon` 基类**——基类在所有关键字开关之前调用，放基类会被原版随后重新开启关键字而失效。
- 存档 spool：`SaveProfileCrestPatch` 改 `SaveProfileHealthBar.ShowHealth`（我们的 id 解析不了它的私有枚举）。

## 七、其它

- `CrestService.EnsureCreated`：**不要用一次性 `_created` 门禁**——`crestList` 是可变 ScriptableObject，换存档/场景会被重置，必须每帧检查 `GetByName(CrestName) == null` 再补注册。
- 本地化：`CrestService.EnsureLocalisationCurrent` 按 `Language.CurrentLanguage()` 注入中/英，语言切换会重注入。
- 完成度：`CrestPatches.PlayerData_CountGameCompletion_Postfix` 扣掉纷乱贡献的那 1 点；`ALL_CRESTS` 成就不动（纷乱始终解锁，分子分母平衡）。
- 伊娃（Crest Upgrader）进度：她的 FSM（`weave_10`）用 PlayMaker 动作 `CountCrestUnlockPoints` 统计**所有非隐藏基础纹章的已解锁槽位总数**（阈值 11/12/20/27/32，判断只用 `Current`）。纷乱常解锁 + 6 槽会被计入，`CrestUpgraderPatches` 给该动作加 Postfix 把纷乱自己的贡献扣回（不写死 6，按同一套规则重算）。**不要删这个补丁。**
- 调试：`DebugLogging` 写死 false；要看日志就把 `RandomCrestModPlugin.DebugLogging` 改成 true 再编译。

## 八、历史坑（不要重犯）

1. **诅咒缚丝** 已删除：它用 `FORCE CURSED BIND` 全局跳转绕过游戏检查，导致过场可触发、丝不足卡死、滑步卡死，为它做的妥协（`TakeSilk` 屏蔽、`CanBind` 门控、滑步/丝量守卫）也一并删了。
2. **滑步 + 换配置**：任何在 Sprint FSM 非 Idle 时触发 `HC CONFIG UPDATED` / `SPRINT CANCEL` 的操作都会让大黄蜂漂浮/跑出场景。用 `IsSprintOrSkid` + 静默换配置规避。
3. **`ToolCrest.IsEquipped` 是共享补丁点**：SilkCurseMod 也 patch 它（当 Cursed 已装备）。两者同时用会互相干扰，兼容性方案**尚未实现**（方向：SilkCurseMod 让路，且不装 RandomCrestMod 时行为不变；粒度建议按 `CurrentCrestID=="RandomCrest"`，无需反射）。
4. **随机萨满空中缚丝落水穿出场景**（`854219a` 的修法不够）：
   - `SurfaceWaterRegion.OnTriggerEnter2D` 里有：`if (hero.cState.isBinding && !SpellCrest.IsEquipped)` 就把英雄往上推 0.2 / 清竖直速度然后 return（不让进水）。这个 reject 只触发一次（trigger enter 只发一次），而 Bind FSM 的 `Shaman Fall` 每帧还会 `SetVelocity2d` 重新给向下速度 → 英雄直接从水面穿下去，掉出场景 → 黑屏 → 回到入口。
   - 正常随机到萨满时 `SpellCrest.IsEquipped` 被 spoof 成 true，本该走进水分支；**但当 spoof 被提前释放（见 `TickDash` 抢跑）时 reject 分支就被触发**，于是穿模。
   - 修法（本次）：
     1. `TickDash` 在 `_dashActive` 的分支里，准备 `Restore` 前也检查 `_active || _nailArtActive || _bindActive`，有其它操作持有 spoof 时绝不恢复（它同时修了野兽/收割者 buff 丢失的根因）；
     2. 安全网：在 `SurfaceWaterRegion.OnTriggerEnter2D` 的 Prefix 里，只要 Bind FSM 处于 `Shaman Air`/`Shaman Fall` 且正在缚丝，就临时把 `SpellCrest.IsEquipped` 报为 true（`ForceSpellCrestForWater`），让水正常接住英雄；进入水后 `EnteredWater` 会发全局 FSM CANCEL，同时现有 `cs.swimming` 的 `CancelBindFsm` 也会发 CANCEL。
   - 已实测通过（0.1.9）：落水不会穿出场景。
5. **随机萨满缚丝穿越场景门卡在边缘**（本次修复）：`TransitionPoint.TryDoTransition` 判断缚丝英雄能否过门用的是 `HeroController.IsShamanCrestEquipped()`，它读的是 `PlayerData.CurrentCrestID == "Spell"`，**不是** `ToolCrest.IsEquipped`。装备纷乱时 `CurrentCrestID == "RandomCrest"`，所以即使随机掷到萨满、`IsEquipped` spoof 成功，这个检查仍为 false：门每帧把英雄推出触发器并清零竖直速度，而 Bind FSM 的 `Shaman Fall` 又每帧重新给向下速度 → 英雄卡在场景边缘动不了（普通/疾风步缚丝都一样）。
   - 修法：`RandomAttackService.SpoofingShaman`（`IsSpoofing && SpoofCrest == Gameplay.SpellCrest`）+ `RandomAttackService.IsShamanCrestEquippedForTransition`（原版逻辑 || spoof）；然后 `RandomAttackPatches` 用 **Transpiler** 把 `TransitionPoint.TryDoTransition` 里对 `IsShamanCrestEquipped` 的 `callvirt` 换成后者。
   - 为何用 Transpiler 而不是 Postfix：`IsShamanCrestEquipped` 是极小的非虚方法，Mono JIT 很可能把它内联进 `TryDoTransition`，那样 Harmony 对该方法的 detour 会被绕过；直接改调用点不受内联影响。
   - 只影响「缚丝中且随机到萨满」这一种情况；其它纹章缚丝仍按原版被挡（它们很快结束，不会卡死），未装备纷乱时行为完全不变。
   - 已实测通过（0.1.9）：装备纷乱、从上层掉入下层场景门、随机掷到萨满时能正常切场景。

## 九、待办 / 待确认

- [x] HUD 外框读档 / 切换纹章后正常显示（`HudFrameService` 不再缓存 `renderer.enabled`，`Reset()` 先恢复再清理，等游戏出场后再接管）。
- [x] 随机萨满空中缚丝落水不再穿出场景（`TickDash` 不再抢跑 + `SurfaceWaterRegion` 的 Shaman 安全网）。
- [x] 随机萨满缚丝穿越上下场景门不再卡在边缘（Transpiler 重定向 `TransitionPoint.TryDoTransition` 里的 `IsShamanCrestEquipped` 调用，让 spoof 被认可）。
- [x] 野兽/收割者疾风步/滑步缚丝正常获得 Rage/Reaper buff（同一根因：`TickDash` 提前 `Restore`）。
- [x] 疾风步/空中疾风步/滑步里的普通、上、下劈砍也随机（`ApplyIfRequested` 改为静默换配置）。
- [x] 纷乱法术费用降为 3 格（`PlayerDataSilkSkillCostPatch`）；满血带蚤母卵再叠到 2 格。
- [x] 纷乱自带 Glow（`crest_glow.png`）+ 技能获取弹窗剪影补丁（`SkillGetMsgPatches`），切换/确认纹章与获得法术时不再闪猎手形状。
- [x] 十字绣真格挡退还本次灵丝（`ParrySilkRefund`，`EnableParrySilkRefund`）；自动释放不退；去掉本体白闪与音效，只用丝轴补丝动画。
- [x] **实测格挡退丝（0.1.9）**：真格挡退 3 / 带蚤母卵 2；自动释放不退；满丝不溢出；连续格挡都生效；换纹章后原版。
- [x] **实测上述美术修复**：装备纷乱后 ①在铁匠铺切换/确认纹章时爆光是纷乱形状；②获得法术时弹窗里是纷乱剪影而不是猎手剪影。
- [x] **实测花芯囊中毒图标（本版）**：装备纷乱 + 花芯囊 + 可中毒红工具时，HUD 随机工具图标变专属紫色
  （`crest_random_tool_poison.png`，hue −0.18）；卸下花芯囊恢复红色；不再出现之前的灰色。
- [x] **实测电枢球（Voltvessels）标枪形态**：随机掷到 `offState`（FSM 事件 `LIGHTNING ROD`）时能正常
  出招、共享次数正常扣（0.1.9 通过；`RollToggleState` 保留双形态）。
- [x] **实测嘲讽三形态**：普通 / 野兽吼叫 / 投掷环都能正确触发；野兽形态独占 `TauntSlash` 动作随声音
  一起出现（Warrior root 开关正确、`Taunt Slash` 变量被覆写），Rings 的 `BoolAllTrue` 只依赖我们已覆盖的 bool。
- [ ] （可选）滑步缚丝时 Sprint FSM 的缓存冲刺劈砍对象不会随 bind 刷新；目前靠下一次冲刺劈砍重掷兜底，未发现可见问题。
- [ ] （可选）SilkCurseMod 兼容：让 SilkCurseMod 在装备纷乱时让路。
- [ ] （可选）随机结果临时日志，验证 7 纹章均匀分布。
- [ ] （可选）把法术费用 / 其它写死参数做成配置项（目前按设计全写死）。
- [x] Release 打包：本机可用 Git Credential Manager 里存的 `github.com` 凭据调 REST API 建 Release 并上传附件（不需额外 token），已发至 v0.1.9。

## 十、环境 / 路径 / 分析资料

- 游戏：`C:\Program Files (x86)\Steam\steamapps\common\Hollow Knight Silksong`
- 配置：`<游戏>/BepInEx/config/io.github.lifelan.randomcrestmod.cfg`（旧配置有孤儿键时可直接删文件让其重建）
- 分析资料（同机 `E:\Agent\Pi\tmpwork`）：
  - `acs/` — `Assembly-CSharp.dll` 的反编译源码；
  - `fsm_Bind.txt` / `fsm_Sprint.txt` / `fsm_CrestAttacks.txt` 等 — PlayMaker FSM 状态机 dump；
  - `src/` — 手工整理的关键类；
  - `crestspools/`、`hudtk/`、`sil_all/` — 提取的游戏美术；
  - 各种 `dump_*.py` / `extract_*.py` — UnityPy 提取脚本（本地化需 `TeamCherry.SharedUtils` 的固定密钥 AES-ECB 解密）。
- 构建需要 `SilksongPath.props`（已被 git 忽略），指向游戏目录。

## 十一、Thunderstore 发布

- 包标识：团队（namespace）**LIFELAN**，包名 **RandomCrestMod**，社区 **hollow-knight-silksong**。
  （若 Thunderstore 上的团队名不是 `LIFELAN`，改 `thunderstore.toml` 的 `namespace`。）
- 依赖：`BepInEx-BepInExPack_Silksong-5.4.2304`（Silksong 社区几乎所有 mod 都用这个版本）。
- 安装布局：Silksong 的 install rule 把包**根目录**的 `.dll` 装到 `BepInEx/plugins/<包名>/`，
  所以 `RandomCrestMod.dll` 直接放 zip 根目录，**不要**再套 `BepInEx/plugins/...`。
- 打包工具：仓库已固定 `tcli`（`.config/dotnet-tools.json`）。本地：
  ```sh
  dotnet tool restore
  dotnet build -c Release
  dotnet tcli build          # 产出 dist/LIFELAN-RandomCrestMod-<ver>.zip
  ```
  或 `powershell -File tools/package.ps1`。
- 首次上传（网页）：登录 thunderstore.io → 进入社区 `hollow-knight-silksong` → **Upload** →
  选 `dist/LIFELAN-RandomCrestMod-<ver>.zip` 即可（zip 内已含 `manifest.json` / `icon.png` / `README.md`）。
- 自动发布：`.github/workflows/publish-thunderstore.yml`，推 `v*` tag 触发。
  需要在仓库 Secrets 里配置 `TCLI_AUTH_TOKEN`：
  Thunderstore → **Settings → Teams → LIFELAN → Service Accounts → Add service account**，
  复制 token（只显示一次）。没有 token 时 CI 只构建产物、不发包。
- 版本号来源：`Directory.Build.props` 的 `<Version>`；`thunderstore.toml` 的 `versionNumber` 需手动保持一致。
- `icon.png`（仓库根，256×256）由 `tmpwork/crest_icon_on_dark.png` 生成，仅用于商店图标。
