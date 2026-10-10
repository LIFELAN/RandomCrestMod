# RandomCrestMod — 开发交接 / 关键点

> 这份是给后续（重启对话后）的自己和 AI 用的开发笔记，记录**现状、关键决定、坑和待办**。
> 面向玩家的说明见仓库根目录 `README.md` / `README.en.md`。

## 零、版本 & 状态（每次开工先看这里）

> **当前：v0.2.3 已发布（Thunderstore + GitHub tag）。** 本轮改动（冲刺斩 / 滑步缚丝修复、HUD 出现消失动画、
> 新增配置项）见下方与“五之十”“五之十一”。

### 本轮改动（v0.2.3，已发布）

1. **冲刺斩总用猎手** → 修。根因：`CheckIfCrestEquipped` 读的 `ToolCrest.IsEquipped` 太小被 Mono 内联，
   `ToolCrest.IsEquipped` 的 postfix 对这些 FSM 分支不生效。新增 `RandomAttackPatches.CheckIfCrestEquipped_IsTrue_Prefix`，
   并给 `RandomAttackService.OnAttackCounterForDash` 加同帧兜底。详见“五之十”。
2. **纷乱 HUD 出现/消失动画** → 按方向等比揭示（圆盘固定、三个突出一起长）；`HudFramePatches` 三个 hook
   跟随游戏 `FrameAppear/FrameDisappear`；场景/读档直接显示（方案 B）。详见“五之十”。
3. **纷乱 ↔ 基础猎手（未升级）过渡** → 两者共用 `defaultFrameAnims`，游戏 `DoChangeFrame` 判定“没变”而跳过；
   新增 `HudFramePatches.DoChangeFrame_Prefix` 绕过。详见“五之十”。
4. **纹章选择界面红色工具槽** `0.9 → 0.95`（`CrestService.Slots`）。
5. **配置项**（同一个空名字段，ConfigurationManager 里三行）：`ToolUsesPerBench=16` / `CursedBind=true` /
   `ParryAlwaysSucceed=false`。
6. **累积未发布**（更早实现、多为实测通过）：诅咒缚丝 5%（现可关）、大黄蜂雕像多投开关、十字绣自动反击
   50%（现可改必中）、储液针管奖励、符文之怒翻倍、BellTown 雕像拾取、工具袋升级补碎片。
   细节见“五之七”“五之八”“五之九”。
7. **冲刺缚丝复用同一纹章** → 修。`ApplyForBind` 曾被 `_bindActive` 挡住不重掷（冲刺时 `Restore` 被推迟），
   该状态自 v0.1.1 起一直存在。已去掉 `ApplyForBind` 守卫里的 `_bindActive`。详见“五之十一”。

> **开工**：`dotnet build -c Release`（应 0 警告 0 错误）→ 覆盖 `BepInEx/plugins/lifelan-RandomCrestMod/RandomCrestMod.dll`。
> **发布**见“十一”。临时排查日志（`[DashLog]`/`[HudLog]`/`[HudDbg]`）已全部删除；需要时把
> `RandomCrestModPlugin.DebugLogging` 改 true 或临时加 `LogInfo`。
> **工作区改动文件**：`CrestService.cs`、`HudFrameService.cs`、`HudFramePatches.cs`(新)、`RandomAttackPatches.cs`、
> `RandomAttackService.cs`、`RandomCrestModPlugin.cs`、`ParryAutoCounterService.cs`、`CursedBindService.cs`、`docs/DEV_NOTES.md`。

> **当前版本 v0.2.1（已发布 Thunderstore + GitHub Release / tag `v0.2.1`）**：在 v0.2.0 基础上：①蓝血 HUD 染色修复 + 更换高清 HUD 外框（对齐原版 cloakless 盘）；②所有红工具**始终**进随机池（`AllToolsRandom` 配置移除，`Extractor` 装备时仍豁免、`Silk Snare` 改为遵循随机）；③念珠炮始终充能 + 长按连发（`IsToolEquippedPatch`）；④符文之怒改动整体退回（删除 `RuneRageRadiusService`）。详情见“五之六”；版本号 `Directory.Build.props` / `thunderstore.toml` 均为 `0.2.1`。
>
> **v0.2.0（已发布 Thunderstore + GitHub Release / tag `v0.2.0`）**：在 v0.1.9 基础上新增“收集奖励”四件套，全部只在装备纷乱时生效，均已实测通过：
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

## 一、当前状态（截至已发布的 v0.2.1）

- 编译：`dotnet build -c Release`，**0 警告 0 错误**。
- 已安装：`<游戏>/BepInEx/plugins/lifelan-RandomCrestMod/RandomCrestMod.dll`（构建会自动复制）。
- 仓库：https://github.com/LIFELAN/RandomCrestMod（默认分支 `master`）。
- **已发布 v0.2.1**（Thunderstore + GitHub Release / tag `v0.2.1`）：蓝血 HUD 染色、高清 HUD 外框、全道具始终随机、念珠炮长按连发。
- **本地工作区代码与 tag `v0.2.1` 完全一致**（仅 DEV_NOTES 的“已发布”标注不同）。HUD 偏移/缩放曾短暂做过 `[Hud]` 配置，已整体移除并写死回发布值（`-0.84 / 0.16 / 0.9125`）。下次直接改代码从 `master` 最新提交继续即可。

最近提交（从新到旧）：
```
f84b878 Revert HUD frame tuning to the published v0.2.1 values
a327c1d Bake HUD frame offset/scale values, drop their config entries
75f14af Add HUD frame scale config (internal tuning)
a0c6fff Add HUD frame offset config (internal tuning)
0466caf Docs: mark v0.2.1 as published
58fd4f1 Release v0.2.1: lifeblood HUD tint, high-res frame, all-tools random, cannon barrage
```

## 二、模组基本定义

- 插件：BepInEx 5，GUID `io.github.lifelan.randomcrestmod`，类 `RandomCrestModPlugin`。
- 纹章内部 id：**`RandomCrest`**（不要改，存档 `CurrentCrestID` / 资源名都靠它）。
- 显示名：中文 **纷乱**，英文 **Chaos**；说明：`你永远不知道下一秒会发生什么。` / `You never know what will happen next.`
- 创建方式：`CrestService` 克隆猎手(Hunter)纹章，运行时 `Add` 进 `ToolItemManager.crestList`。
- 槽位（`CrestService.Slots`）：6 个，全部 `IsLocked=false`：
  - Skill / Neutral (0,-0.9)、Red / Up (0,0.95)、Blue (2.5,-1.8)、Blue (-2.5,-1.8)、Yellow (-1,-2.7)、Yellow (1,-2.7)。
  - 红色槽 2026-10-10 由 0.9 微调到 0.95（纹章选择界面位置），运行时 `ApplySlots` 烘焙，重进游戏/重开档生效。

## 三、功能开关与门控（重要）

- `RandomCrestModPlugin` 里，除配置项外全是 **写死的 static readonly / 只读属性**：
  `EnableRandomAttacks / EnableRandomBind / EnableRandomTools / EnableRandomSpells / EnableCustomHudFrame / EnableCustomSaveSpool / EnableRandomIcons / EnableRandomTaunt / OnlyOnRandomCrest(true) / DebugLogging(false)`。
- **配置项**（`Config.Bind`，可在 ConfigurationManager 里热改；**全部同一个空名字段**，这样 Manager
  不画分组标题，列表就是三行）：
  - `ToolUsesPerBench = 16` — 坐椅子补满的基准次数，每级工具袋 +25%，补充免费。
  - `CursedBind = true` — 诅咒缚丝开关；`false` 时 `CursedBindService.Roll()` 直接返回 false，随机缚丝永不出诅咒。
  - `ParryAlwaysSucceed = false` — 十字绣**自动反击**（姿态自然结束、未被击中）是否必中；`false`=50%（当前默认），`true`=必中。真实格挡永远反击，不受影响。
- **所有随机效果只在装备纷乱时生效**：
  - 攻击/缚丝：`RandomCrestModPlugin.OnlyOnRandomCrest && CrestService.IsRandomCrestEquipped()`；
  - 工具/法术：`RandomToolService.GateOpen`（同上）；
  - 嘲讽：`RandomTauntService.Roll` / `Tick` 里的 `IsRandomCrestEquipped()`；
  - 随机图标：`RandomIconService.For` 走 `RandomToolsActive/RandomSpellsActive`；
  - HUD 外框：`HudFrameService.Tick` 检查 `IsRandomCrestEquipped()`。
- 旧的 `FORCE CURSED BIND` 实现已移除（见“八、历史坑”）；现版本是 Bind FSM `Do Bind` 分支覆盖（见“五之七”），
  由 `[CursedBind] Enabled` 控制开关。

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
- **所有红工具都在池里（v0.2.1 起）**：原 `DefaultExcluded`（`Extractor` / `Silk Snare` / `Rosary Cannon` / `Screw Attack`）与 `AllToolsRandom` 配置已删除；`Extractor` 始终在装备时豁免（保持原版功能 / 图标）。
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

## 五之六、本轮改动（v0.2.1）

### 1. 蓝血状态 HUD 染色（`HudFrameService`）

- **问题**：进入蓝血（lifeblood）状态后，游戏会给自己的 `Bind Orb` frame sprite 染蓝——`BindOrbHudFrame.RefreshLifebloodTint()`（`acs/BindOrbHudFrame.cs:264`）执行 `tk2dSprite.color = lifebloodTint; EnableKeyword("RECOLOUR")`，`lifebloodTint` 预制体里是 `(0.557, 0.890, 1.0, 1.0)`。我们的叠加层是独立 mesh + 复制材质，从不复刻这一步，而装备纷乱时又把游戏 renderer 藏了，所以框保持银色，看起来像“钢魂 HUD”。
- **修法**：`HudFrameService.Acquire()` 用 `AccessTools.FieldRefAccess<BindOrbHudFrame, Color>("lifebloodTint")` 读游戏实际 tint，并先给复制材质 `DisableKeyword("RECOLOUR")` + 白色归零；`Apply()` 每帧调 `RefreshLifebloodTint()`：`HeroController.instance.IsInLifebloodState` 为真时给 quad 顶点色写 tint 并 `EnableKeyword("RECOLOUR")`，否则回白 + `DisableKeyword`。只在状态切换时写；重建 quad 时把 `_tinted` 复位。
- **依据**：`RECOLOUR` 走顶点色（`PoisonTintTk2dSprite.Colour => sprite.color`），材质是 `Sprites/Default-ColorFlash`。

### 2. 全道具随机（始终生效，无配置项）

- `RandomToolService.EnsurePools` 现在把**所有红色工具**都收进 `RedPool`（原 `DefaultExcluded` / `Excluded` 已删除）；`AllToolsRandom` 配置项已移除，不再可配。
- `VanillaEquipTools` 只留 `{ "Extractor" }`：主动装备储液针管仍是针管本尊（任务/图标），陷阱设置器改为和普通红工具一样遵循随机（装备时会被替换、HUD 显示随机图标）。
- 免费投掷（+2%/件）继续排除 `Extractor` / `Silk Snare`（`FreeThrowExcludedTools` 不变），所以掘洞钻和念珠炮各 +2%，另两件不计。
- **念珠炮始终充能 + 长按连发**：`Rosary Cannon` 是 `ToolItemLerpStates` + `isCustomUsage`，弹药走自己的 `SavedData.AmountLeft`；`EnsurePools` 缓存 `_rosaryCannon`，`Tick()` 每帧 `KeepRosaryCannonCharged()` 把它同步到 `UsesPerBench`。另外，`Shoot Loop` 的 `GetToolEquipInfo` 的 `Tool` 参数是**直接引用念珠炮本体**（FSM 模板里 `value=PPtr(...,-329254137206601981)`），而随机工具并未真正装备，`IsEquipped` 为 false 会让循环一发就结束；新增 `IsToolEquippedPatch`（`ToolItemManager.IsToolEquipped` postfix）在 `readSource==Active && IsSpoofed(tool)` 时报 true（HUD 读取不动），所以按住可一直发。`Shoot` 的 `CustomToolUsage` 也是同一个 `Tool` 引用。
- 注意：四件里 `Silk Snare` / `Rosary Cannon` / `Screw Attack` 都是 FSM 事件工具（`ThrowPrefab == null`），不会进连投链（`AfterThrow` 的 `!_throwConsumed` 分支会结束链），属预期。

（原「符文之怒只加半径」改动已整体退回；现改为**伤害翻倍**，见“五之九”。）

## 五之七、诅咒缚丝（已实现，未发布，用户实测中）

> 目标：给「纷乱」的随机缚丝补回**诅咒缚丝（被拒绝的缚丝）**这个结果，同时不复现旧版那套崩溃。

### 1. 现状（已实现、已部署、已验证「基本实现」）

- **概率固定**：`RandomCrestModPlugin.CursedBindChance = 0.05f`（写死，不可配置）。用户按 5% 全流程实测。
- **新增文件**：
  - `CursedBindService.cs` — 掷点、`Arm/Clear/Reset/Tick`、`ShouldOverride`、`ResolveCursedEvent`。
  - `CursedBindPatches.cs` — 只补一个点：`PlayerDataVariableTest.OnEnter` 的 prefix。
- **修改文件**（各几行）：
  - `RandomBindService.cs`：`BeginAttempt` 里 `CursedBindService.Arm(CursedBindService.Roll())`；`End()` 里 `Clear()`。
  - `RandomCrestModPlugin.cs`：`EnableCursedBind = true`、`CursedBindChance = 0.05f` 固定值、注册 `CursedBindPatches`、框架 Tick 加 `CursedBindService.Tick()`、新增常开 `LogInfo`。
  - `CrestPatches.cs`：`SetLoadedGameData` / `SceneInit` 里 `CursedBindService.Reset()`。
- **日志**（常开，不受 `DebugLogging` 影响）：`[CursedBind] cursed bind rolled.` / `[CursedBind] Diverted the bind into the Cursed branch.`

### 2. 机制（关键事实，别再踩）

- Bind FSM 的**运行时名字是 `Bind`**（在 `heroloading_assets_all.bundle`，path_id `-8018744589059483442`），但它的**模板名是 `Spell Control`**（`fsmtemplates_assets_shared.bundle`，path_id `-3690107984273115179`）。`FSMUtility.LocateFSM(hero.gameObject, "Bind")` 按运行时名匹配，所以 `IsInState` 用 `fsm.Name == "Bind"` 是对的。
- `Do Bind` 状态最后一个动作是 `PlayerDataVariableTest(VariableName="IsAnyCursed", ExpectedValue=true)`；其 `IsExpectedEvent` 序列化就是字符串 **`CURSED`**（`byteData` 尾部 `67 85 82 83 69 68`），`IsNotExpectedEvent` 为空。转场 `CURSED → Cursed Bind Start`，`FINISHED → Set Bind Anims`。
- **不要再用 `FORCE CURSED BIND` 全局跳转**（旧版 `ba96154` 删除）：它直接进 `Cursed Bind Start`，绕过 `Can Bind?` 的丝量 / `CanBind()` / 落地 / 冲刺 / 过场判断 → 旧版三大崩溃。新方案只覆盖 `Do Bind` 的那一个测试，前面所有门照常走。
- **不要全局 spoof `PlayerData.IsAnyCursed`**：它会 `CurrentSilkMaxBasic` 里 `if (IsAnyCursed) return 3;`，把丝线上限压到 3、丝线外观/死亡茧全变。只覆盖那一个 FSM 动作。
- 诅咒动画 clip 由现有 `AnimationFallbackPatches`（`HeroAnimationController.GetClip` postfix → `RandomCrestAnimationLibrary` 合并库）兜底；少数 `ActivateGameObject` 目标可能挂诅咒/女巫 ActiveRoot 下，目前随机装 config，未特意开那个 root（实测通过说明没问题）。

### 3. 诅咒路径扣丝真相（方向 1 的依据）

路径：`Do Bind ──CURSED──> Cursed Bind Start → Start pt2 → Mid → Cursed Damage → Witch Binding? → Remove Silk? → Cancel All / Cursed Bind End`。

- **`Cursed Damage`（主扣丝点）**：
  - `GetSilk(StoreAmount="Current Silk Amount")` → `TakeSilkV2(Amount="Current Silk Amount")` = **扣光当前全部灵丝**（用户观察到的「扣除全部灵丝」）。
  - `CallMethodProper(HeroController.DamageSelf, 1)` = **1 格 ENEMY 类型自伤**（`DamageSelf` → `TakeDamage(..., HazardType.ENEMY)`）。
  - `SendEventToRegister("SILK CURSED UPDATE")`；`HeroController.UpdateSilkCursed()` 会 `ResetSilkRegen` + 发同名事件（丝轴 HUD 变诅咒外观）。
- **`Remove Silk?`（次扣丝点）**：`TakeSilk(amount=<变量>)` + `CheckIfToolEquipped`（备用缚丝道具 Reserve Bind）。原版因 #1 已清空丝，这里扣到 0；**若只跳过 #1、不动这里，就会扣掉正常缚丝费**。
- **`Can Bind?`**：`PlayerdataIntCompare(silk, …)`，`IsAnyCursed` 经 `ConvertBoolToInt` 影响比较值。我们**不覆盖** `Can Bind?`，所以进入诅咒缚丝前走的是**普通满丝判定**。

### 4. 方向 1（诅咒缚丝不扣灵丝）— 可行性 & 风险（用户已听，待拍板）

- **可行**，两种做法：
  - **方案 A（推荐，精准）**：仿 `Do Bind`，用 `IsInState` 限定：`Cursed Damage` 的 `TakeSilkV2` 把 `Amount` 置 0（或跳过）；`Remove Silk?` 的 `TakeSilk` 把 `amount` 置 0（或跳过）。只影响诅咒路径。
  - **方案 B（广谱，旧写法）**：诅咒窗口内 prefix `HeroController.TakeSilk(int)` / `TakeSilk(int, SilkTakeSource)` 返回 false（= 已删除的 `ShouldSuppressSilk`）。一处覆盖两处扣丝，但会吞窗口内所有扣丝。
- **风险**：
  1. 「不扣丝」≠「无代价」：`DamageSelf(1)` 仍在。**需用户决定是否保留自伤**（作者建议保留）。
  2. 满丝门槛仍在：不覆盖 `Can Bind?` 就不会低丝触发（低丝触发是另一个特性，风险更高，建议分开）。
  3. `SILK CURSED UPDATE` 视觉 / `ResetSilkRegen` 仍会触发，可能短暂显示诅咒丝轴外观，需实测；不好看可连事件一起拦。
  4. 方案 B 窗口风险：窗口内任何扣丝被吞，必须严格清理（死亡/切场景/被打断）；方案 A 无此问题。
  5. 扣丝后分支依赖：从 dump 看 `Witch Binding?` / `Remove Silk?` / `Cancel All` 不读丝，风险低，全流程留意。
- **待用户拍板两点**：① 是否保留 `DamageSelf(1)`；② 是否保留「必须满丝才能触发」门槛。确认后按方案 A 实现。

### 5. 方向 2（扣光丝但给补偿）— 已采纳并实现

- 实现文件：`CursedBindRewardPatch.cs`（新）。patch `HeroController.TakeSilk(int, SilkSpool.SilkTakeSource)`
  的 **postfix**，只在 `source == SilkSpool.SilkTakeSource.Curse` 且
  `RandomCrestModPlugin.EnableCursedBind` 且 `CursedBindService.Active` 时生效，给
  `amount * 10` 枚念珠（`CurrencyManager.AddGeo`，HUD 计数器会动）。
- **为什么是干净信号**：`Cursed Damage` 里 `GetSilk` 存 “Current Silk Amount”，
  `TakeSilkV2` 调 `HeroController.TakeSilk(amount, Curse)`（`SilkTakeSource.Curse`），所以 hook 这个
  重载拿到的 `amount` 就是实际扣除量。单参重载 `TakeSilk(int)` 走的是 `Normal`，与
  `ParrySilkRefundPatch` 不冲突。
- **只补偿模组自己掷出的诅咒缚丝**：`CursedBindService.Active` 只有随机缚丝 `Arm(cursed)` 时才为 true；
  真正戴 Cursed 纹章的玩家（`OnlyOnRandomCrest` 下）不会触发。
- **自伤 `DamageSelf(1)` 保留**：用户要求“扣除全部灵丝这点不动”；注意自伤也仍在。
- 日志：`[CursedBind] refused bind took N silk -> +M rosaries.`
- 待实测：命中一次诅咒缚丝后念珠增量是否 = 扣丝格数 × 10。

### 6. 测试清单（用户正在走）

- 诅咒序列能否正常播 / 有无卡死；丝线上限是否照常（不应变 3）；结束后能否正常操作。
- 滑步 / 空中 / 满丝 / 被打断 / 切场景 / 死亡 各种时刻。
- 日志两条是否成对出现（只有第一条 = `Do Bind` 没走到）。

### 7. 回退（如需）

```sh
cd E:/Agent/Pi/RandomCrestMod
git checkout -- CrestPatches.cs RandomBindService.cs RandomCrestModPlugin.cs
rm CursedBindPatches.cs CursedBindService.cs
dotnet build -c Release
```
（cfg 里 `[Bind]` 段留着不影响其它功能。）

## 五之八、大黄蜂雕像多投开关 + 十字绣 50% + 储液针管奖励（本轮，未发布）

### 1. 大黄蜂雕像 = 多投开关

- 物品是游戏原版 `CollectableItemBasic`，资产名 **`Fixer Idol`**（显示键 `INV_NAME_FIXER_IDOL` = 大黄蜂雕像），
  存在 `PlayerData.instance.Collectables.GetData("Fixer Idol").Amount`。
- **不唯一**：`customMaxAmount=0`、`useQuestForCap` 为空，`IsAtMax()` 回退到 `GlobalSettings.Gameplay.ConsumableItemCap = 20`；
  自带 `useResponses`（消耗给 60 碎片）。所以**消耗雕像会关闭多投**，这是预期。
- `RandomToolService.StatueHeld`（新属性）+ `ExtraThrowsPerPress` 开头 `if (!StatueHeld) return 0;`；
  `Tick` 链清理条件加 `|| !StatueHeld`，让消耗后立即断链。
- 口径（用户拍板的 B 方案）：**雕像=总开关；投掷次数=工具袋等级（+ Quick Sling）**；工具袋=0 时自然 0 次额外投掷。

### 2. 世界拾取点（`StatuePickupService.cs`，新）

- 场景 `Belltown`（钟心镇），坐标 **(97.39893, 22.56768)**（用户调试模组实测；地图师区域靠入口，最近对象 `Mapper Call Pole` ~1.36）。
- 一次性：`SceneData.instance.PersistentBools`，key `("Belltown", "RandomCrestMod_HornetStatue")`；
  `MarkTaken` 绑在 `CollectableItemPickup.OnPickup`。**不检测纹章**。
- 生成：`Instantiate(Gameplay.CollectableItemPickupPrefab)` + `SetItem(CollectableItemManager.GetItemByName("Fixer Idol"))`，
  反射私有 `spriteRenderer` 换雕像图标；每帧 `Tick` 重试直到成功；`!GameManager.CanPickupsExist()` 时跳过。
- 注意：目标点 0.13~0.32 内有墙片，若实机嵌墙，把 `PickupPosition` 沿入口方向微调 0.3~0.5。

### 3. 储液针管奖励（`ExtractorRewardPatch.cs`，新）

- `HealthManager.TakeDamage(HitInstance)` postfix：`hitInstance.AttackType == AttackTypes.ExtractMoss`
  （储液针管伤害对象 `Extractor Hit` 实测值为 14=ExtractMoss）+ `RandomToolService.RandomToolsActive`。
- **按“一次使用”结算**：`PlayMakerFSM.SendEvent("EXTRACTOR")` prefix 重置 `_awardedThisUse`；
  同一段 stab 的多段/多目标伤害只发 1 枚；多次使用累加，**无上限**。
- 发放 `CollectableItemManager.AddItem(item, 1)` + `CollectableUIMsg.Spawn(item)`（不走 `Collect`，不会强开背包）。
- 注意：**不要用 `EXTRACTOR DMG` 动画事件**当判据（空挥也触发）。
- **主动装备的储液针管不奖励**：`ToolItemManager.IsToolEquipped("Extractor")`（字符串重载，直读真实
  工具槽，不受随机 spoof / custom-use override 影响）为 true 时直接 return。储液针管是 `VanillaEquipTool`，
  装备时不会被随机替换，所以“装备 = 玩家主动用”；只有随机抽到时 `IsToolEquipped` 才是 false。

### 4. 十字绣 50%（`ParryAutoCounterService.cs` 改）

- 进入 `Parry Clash` 且 `!ParryClashTrigger.Attacked`（自动路径）时，状态跃迁帧掷一次固定 **50%**（`AutoCounterSuccessChance`），存 `_autoCounterHits`。
- `SetRedirect` 新增对 `Parry Clash` 的 `FINISHED` 转场：`redirect && !_autoCounterHits` → `Parry Recover`；否则恢复 `Change Facing?`。
- **失败用 `Parry Recover`（不是 `Parry End`）**：`Parry End` 会调 `HeroController.CrossStitchInvuln()` 给无敌，白嫖不合适；`Parry Recover` 不送无敌、播收招动画。
- 真格挡不掷骰，永远反击；HUD 高光仍绑在 `Parry Clash`，失败照常播。
- 待实测：失败是否顺畅收招、真格挡不受影响、红蓝场景切换后转场复位。

### 5. 注册 / 驱动

- `RandomCrestModPlugin.Awake` 注册 `ExtractorRewardPatch`、`CursedBindRewardPatch`。
- `RandomCrestRunner.Update` 加 `StatuePickupService.Tick()`、`ToolPouchShardBonus.Tick()`。

### 6. 工具袋升级补碎片（`ToolPouchShardBonus.cs`，新）

- 每帧轮询 `PlayerData.ToolPouchUpgrades`，增量 `gained * 800` 走 `CurrencyManager.AddShards`
  （游戏自身按碎片上限封顶，上限本身每级工具袋 +25%）。用限量的 `AddShards` 而不是直写 `ShellShards`，
  避免超上限污染 HUD。
- `Reset()`（baseline=-1）挂在 `RandomToolSaveLoadedPatch`（`GameManager.SetLoadedGameData` 的 postfix，和
  `RandomToolService.OnSaveLoaded()` 同位），避免读档把存档已有等级误发一次。
- **仅装备纷乱时**：`CrestService.IsRandomCrestEquipped()` 为 false 时跳过（baseline 已在前面推进，
  所以非纷乱期间的升级不会被补发）。

## 五之九、符文之怒伤害翻倍（本轮，未发布）

> 目标：装备纷乱时让随机到的**符文之怒**（Silk Bomb / 丝弹）伤害**提高 1 倍**（×2）。

### 1. 弹体与伤害来源（tmpwork 反序列化确认）

- `Silk Specials` FSM 的 `Sonar Cast Effects` / `Do Explosions` 会从全局池生成弹体；`Blast Prefab`
  由 `Zap Variant` 决定：
  - 普通弹 `Weaver Bomb Blast`（`herodynamic_assets_all.bundle`，path_id `-2546894702494613118`）；
  - Zap 弹 `Weaver Bomb Blast Zap`（`localpoolprefabs_assets_shared.bundle`，path_id `-3940014353810078904`）。
- 两者根节点都挂 `HeroShamanRuneEffect`（`damager` 字段指向子节点 `Blast/damager` 的 `DamageEnemies`）。
  子 `damager` 的 `DamageEnemies`：`useNailDamage=1`、`damageDealt=15`、
  `nailDamageMultiplier` 普通 `1` / Zap `2.1`。
- 伤害最终值 = 钉伤 × `nailDamageMultiplier` × `DamageMultiplier`（`DamageEnemies.DoDamage` 内
  `tempDamageStack.AddMultiplier(DamageMultiplier)`），所以**改 `DamageMultiplier` 只影响伤害，不影响击退**。
- 原版 `HeroShamanRuneEffect.Refresh()` 会把 `damager.DamageMultiplier` 设为
  `initialDamageMult × (SpellCrest 装备 ? SpellCrestRuneDamageMult : 1)`；纷乱是 Hunter 克隆，
  所以 `SpellCrest.IsEquipped` 为 false，基值为 `initialDamageMult`。

### 2. 实现（`RuneRageDamagePatch.cs`，新）

- `[HarmonyPatch(typeof(HeroShamanRuneEffect), nameof(HeroShamanRuneEffect.Refresh))]` **postfix**：
  `RandomToolService.RandomSpellsActive` 为真、且 `effect.gameObject.name` 以 `Weaver Bomb Blast`
  开头时，`damager.DamageMultiplier *= 2`。
- **为什么安全**：`Refresh()` 每次都用 `initialDamageMult` 赋值（绝对值），所以即使池化对象反复
  `OnEnable` 调 `Refresh`，我们也只是把基值 ×2，不会连乘；不装备纷乱时 `Refresh` 会把它重置回基值，
  不会泄漏到其它纹章。
- **为什么按名字过滤**：`HeroShamanRuneEffect` 还被 Needle Throw、Silk Charge、Cross Slash、Parry 等
  非符文之怒对象复用，不能整类都翻倍。`StartsWith` 兼容运行时 `(Clone)` 后缀，也覆盖 `... Zap`。
- 仅乘 `DamageMultiplier`，不动 `nailDamageMultiplier`，所以 Zap 形态的 2.1 倍加成仍然保留（最终 ×4.2）。
- `RandomCrestModPlugin.Awake` 注册 `RuneRageDamagePatch`。

### 3. 测试重点

- 纷乱上符文之怒命中伤害约为平时 **2 倍**；带 Zap 形态（若曾 equipped 对应工具）仍保留其额外倍率。
- 换其它纹章放符文之怒，伤害与原版一致；纷乱卸下后不回弹、不残留。

## 五之十、冲刺斩修复 + HUD 出现/消失动画（本轮，未发布）

### 1. 冲刺斩（Dash Stab）总是猎手：根因与修复

- 现象：冲刺瞬间按攻击，冲刺斩永远走猎手（`Set Attack Single`）分支。
- 根因：Sprint FSM 用 PlayMaker `CheckIfCrestEquipped` 选分支，它读
  `ToolCrest.IsEquipped`（`PlayerData.instance.CurrentCrestID == name`）。这个方法极小，Mono JIT 会把它
  **内联**进 `CheckIfCrestEquipped.IsTrue`，于是挂在 `ToolCrest.IsEquipped` 上的 postfix 对这些 FSM
  分支不生效 → 永远按真实纹章（纷乱）判定 → 落到 default 猎手分支。同 `IsShamanCrestEquipped` 那个老坑。
- 修复：`RandomAttackPatches` 新增 `CheckIfCrestEquipped.IsTrue` getter 的 **prefix**（虚属性走虚表，
  不会被内联）：`IsSpoofing` 时直接 `__result = ReferenceEquals(crest, SpoofCrest)`。
- `RandomAttackService.OnAttackCounterForDash` 另加 `sprintFSM.ActiveStateName == "Start Attack"` 兜底
  （冲刺起步同帧时 `_dashActive` 还没置位），此时补 `_dashActive/_activateTime`。

### 2. HUD 纷乱外框出现/消失动画（`HudFrameService` + 新 `HudFramePatches`）

- 目标：纷乱外框像原版一样「旧纹章消失 → 纷乱出现 / 纷乱消失 → 新纹章出现」。
- 机制：`BindOrbHudFrame.DoChangeFrame` 按纹章身份选 `BasicFrameAnims`；纷乱不认识 → 用
  `defaultFrameAnims`（猎手）。切换流程是 `FrameDisappear(旧) → FrameAppear(新)`。
- `HudFramePatches` postfix 三个方法（注册在 `RandomCrestModPlugin.Awake`）：
  - `FrameAppear`：游戏开始播新帧 appear（旧纹章 disappear 已完）→ 纷乱时接管 overlay 播 appear；
    否则（新纹章不是纷乱）`ReleaseNow()` 交还游戏帧。
  - `FrameDisappear`：旧帧开始消失 → 若 overlay 正在显示（旧纹章是纷乱）就播纷乱收起。
  - `AlreadyAppeared`：instant 路径（跳过 appear）→ 纷乱瞬间顶上 / 否则释放。
- **overlay 用「按方向等比揭示」，不是整体缩放**（缩放会让圆盘也变大，且针太长会拖后）：
  - 遮罩 mesh 是 `MaskGrid=160` 的网格，每帧只改顶点 alpha。
  - `BuildQuad` 先扫描贴图，按 `MaskAngleBins=720` 算每个方向的**最远不透明像素**；每个顶点
    `norm = (d - DiskRevealFrac) / (该方向最远 - DiskRevealFrac)`，盘内 `norm=0`。
  - `ApplyMask(progress)`：`alpha = 1 - SmoothStep(0,1, InverseLerp(progress, progress+MaskSoftness, norm))`。
    这样圆盘固定、所有突出**同时**到各自终点（不会针拖后）。
  - 常量：`DiskRevealFrac=0.1464`（盘半径/图高，源图盘直径 640px）、`MaskSoftness=0.01`、
    `AppearDuration=0.5s`（ease-in）、`DisappearDuration=0.15s`（ease-out）。`_reveal` 是进度 0..1。
- **场景/读档 vs 纹章切换**（方案 B）：
  - `Reset()`（`SceneInit` / `SetLoadedGameData`）→ `_sceneReset`；新场景里游戏帧一上屏就**直接显示
    完整帧**（不播生长）。因为游戏自己的 HUD 出场动画已经在演，我们再来一次「整体长大」很突兀。
  - 只有**纹章切换**走 `FrameDisappear → FrameAppear` 时播生长/收回。
  - **`_sceneReset` 必须有寿命**：场景 HUD 可见后 ~1s 过期（`_sceneResetSince`，只用 `HudVisible()` 判断）。
    否则「装别的纹章 → 进出场景 → 切回纷乱」会被场景加载的「直接显示」抢走（椅子菜单里 `CurrentCrestID`
    先变、游戏的 `FrameDisappear` 后到，切回时仍有生长被笛）。实际踩的坑：过期判断里带了
    `_gameRenderer != null && _gameRenderer.enabled`，而装别的纹章时我们从不 `Acquire()`，`_gameRenderer`
    一直是 null → 过期从未跑过。真实换纹章的 `FrameDisappear` 也会清 `_sceneReset`（游戏事件在 Update、
    我们在 LateUpdate，正常换纹章会先到），但不能只靠它。
- **纷乱 ↔ 基础猎手（未升级）修好**：两者都用 `defaultFrameAnims`，`DoChangeFrame` 会因
  「Idle clip 相同」提前返回 → 整个过渡（旧消失 / 新出现 / 切换音效）被跳过，换回默认纹章既没特效
  也没声音。修法：`HudFramePatches` 新增 `DoChangeFrame` **prefix**，装备是纷乱或 `Gameplay.HunterCrest`
  时把私有 `currentFrameAnims` 置空，使下次 `basicFrameAnims == null` 绕过该提前返回；只对这两个
  纹章做，同纹章刷新仍在身份检查处提前返回。已实测通过。
- 关键点：
  - 换纹章时**不能立刻 `Restore()`**（原来菜单里就撤了，退出菜单游戏才过渡 → 只看到猎手帧）。
    overlay 还在时先挂着，等 `FrameDisappear` hook 再收；0.75s 超时只在 HUD 可见时计时。
  - 收完后保持 `_reveal=AppearStartReveal`（只剩圆盘）压着游戏帧，直到新纹章 `FrameAppear` 才交还。
  - 场景加载时游戏会先报一次 `FrameAppear chaos=False` 再补 `FrameDisappear`+`FrameAppear chaos=True`；
    `_suppressDisappearUntil` 会吞掉这次补播的 `FrameDisappear`；非纷乱的 `FrameAppear`/`AlreadyAppeared`
    也**不会**在 `_animMode==Appear` 时把 overlay 拽走。
  - 防重放：`_shown` 或正在 appear 时再来 `FrameAppear` 只 `Apply` 不重播。1.5s 兜底防卡死。
  - **`Mathf.SmoothStep(from,to,t)` 的第三个参数是 0–1 插值系数，不是距离**（踩过两次，满进度
    alpha 变负 → 整块透明）。距离要先 `InverseLerp` 归一化。
- 纹章选择界面红色工具槽：`CrestService.Slots` 里 Red 的位置 `(0, 0.9)` → `(0, 0.95)`（用户要求上移一点点），
  只影响纷乱在纹章选择界面的槽位布局。

### 3. 纹章模板问题（结论）

- 功能层面：新纹章实际上必须克隆一个现有纹章（本项目克隆 Hunter）。`ToolCrest → HeroControllerConfig`
  挂着整套招式对象/动画库/参数，从零造等于把模板资产全复制一遍。
- HUD 帧是唯一例外：`BindOrbHudFrame` 把每纹章的 appear/idle/disappear 写死按纹章身份，没有新纹章槽位，
  所以新纹章永远拿 `defaultFrameAnims`（猎手）；要有自己的帧动画只能 patch `BindOrbHudFrame` 或用 overlay。

### 4. 状态

- 已实测通过；临时 `[DashLog]` / `[HudLog]` 常开日志已删除；**暂不发布**。

## 五之十一、冲刺缚丝复用同一纹章（v0.1.1 起，已修）

- 现象：连续冲刺缚丝时每次都出现**同一个纹章 / 同一个动作**，直到落地 / 停下才换一个，然后继续重复。
- 根因需要两个条件同时成立：
  1. `ApplyForBind` 的守卫 `if (_active || _nailArtActive || _bindActive || hero == null) return;` 在 `_bindActive`
     未清时直接早退，**不重掷纹章**。该守卫从 v0.1.0 首个提交 `fd73bee` 起就在。
  2. `_bindActive` 在冲刺期间不会被清：`EndBind` 与 `Tick` 的缚丝分支都因 `IsSprintOrSkid(hero)` 暂缓 `Restore`。
- **定位：v0.1.1（`eb44157`）**。v0.1.0 的 `TickDash` 在冲刺短暂中断后有一个兜底 `Restore`，且**没有**检查 `_bindActive`：
  ```csharp
  if (sprintActive) { _dashLastActive = Time.time; return; }
  // v0.1.0：这里没有 _bindActive 检查
  if (Time.time - _dashLastActive > 0.25f || Time.time - _activateTime > 10f) Restore(hero);
  ```
  它会顺手把 `_bindActive` 清掉，所以下一次缚丝能重掷。v0.1.1 为修「萨满空中缚丝穿水 / 野兽·收割者 buff 丢失」
  在兜底前加了 `if (_active || _nailArtActive || _bindActive) return;`，从此刻起冲刺期间没人再清 `_bindActive`
  → `ApplyForBind` 一直早退 → 复用第一次掷到的纹章。v0.1.7 又给 `EndBind` / `Tick` 加 `IsBindFsmBusy`，
  只是让它卡得更久，不是起点。**v0.1.0 → v0.2.2 一直存在。**
- 日志佐证（`BepInEx/LogOutput.log`，PureNeedleMod 的 bind 诊断）：连续 11 次非萨满 `Bind Air` 后落地一次
  （此时才 `Restore` 清掉），接着连掷到萨满并卡住 12 次 `Shaman Air`。
- 修法（本轮）：`RandomAttackService.ApplyForBind` 守卫去掉 `_bindActive`（一次尝试已由 `RandomBindService._attemptActive`
  串行化，能进来就一定是新缚丝，必须重掷）；同时在重掷时补 `_bindCancelSent = false;`，让新缚丝重新拥有落水安全网。
  **不要**去改 `TickDash` 里那句 `_bindActive` 守卫——它是 v0.1.1 修穿水 / buff 的关键，删了会退回旧 bug。
- 待用户完整测试。

## 六、HUD / 存档界面美术

- 已解包：`C:\Users\fuenlai\Desktop\Silksong_HUD_Frames\` 下每个纹章一张 idle frame（`hunter` / `cloakless` / `hunter_v2` / `hunter_v3` / `warrior_beast` / `reaper` / `wanderer` / `witch_cursed` / `witch` / `toolmaster_architect` / `spell_shaman`），源脚本 `tmpwork/extract_crest_hud_frames.py`（从 `hud_assets_all.bundle` 的 atlas0 按 UV 裁剪）。游戏里没有单独的钢魂 HUD 贴图，钢魂外观更可能是 desaturate/黑色染。

- 资源（`Assets/`，内嵌 DLL）：
  - `crest_icon.png` / `crest_silhouette.png` / `crest_glow.png` — 纹章本体图标 / 剪影 / 装备爆光（发光剪影）；
  - `crest_hud_frame.png` — 左上角缚丝丝轴外框（用户绘制），**ppu 420**；
    **不要提亮**：v0.1.0 发布后曾用 gamma≈1.35 提亮过一版（提交 `c20c9fd`），结果蓝血状态下框整体偏亮、很像钢魂外观。
    用户 2026-10-08 给了 cloakless 对齐参考（331×303，md5 `178661e7`）帮定位：盘心在原版 cloakless 盘位置、盘直径 89px = 原版。但该低清图进游戏被放大 ~6.5x 会有毛边，所以最终用回高清 `新版hud.png`（2374×2186，md5 `1f5756cd`，与参考版同一美术，只是整体缩放/平移差）。
    对齐常量：`HudFrameService.SpoolFrac = (0.14764, 0.52745)`（高清图盘心 pixel (350.5,1153.0)），`RandomCrestModPlugin.HudFrameScale = 0.9125`（让 640px 盘渲染成原版 1.390625 世界单位）；偏移 `HudFrameOffsetX/Y = (-0.84, 0.16)` 即原版 cloakless 盘在 Bind Orb 下的局部位置，不用动。换图后重新量盘心 pixel 与盘直径即可同步这三个值。
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
6. **冲刺缚丝复用同一纹章**（v0.1.1 → v0.2.2，已修）：`ApplyForBind` 被 `_bindActive` 挡住不重掷，而冲刺期间
   `EndBind` / `Tick` 都因 `IsSprintOrSkid` 推迟 `Restore`，`_bindActive` 不会清 → 复用第一次掷到的纹章。
   起点是 v0.1.1（`eb44157`）给 `TickDash` 加 `_bindActive` 守卫（修穿水 / buff 必需，**不能删**）。
   修法是让 `ApplyForBind` 不再看 `_bindActive`。详见“五之十一”。

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
- [x] **实测通过（v0.2.3）**：冲刺时连续缚丝，每次都重掷成不同的纹章（修 `ApplyForBind` 的 `_bindActive` 守卫，见“五之十一”）。
- [ ] （可选）滑步缚丝时 Sprint FSM 的缓存冲刺劈砍对象不会随 bind 刷新；目前靠下一次冲刺劈砍重掷兜底，未发现可见问题。
- [ ] （可选）SilkCurseMod 兼容：让 SilkCurseMod 在装备纷乱时让路。
- [ ] （可选）随机结果临时日志，验证 7 纹章均匀分布。
- [ ] （可选）把法术费用 / 其它写死参数做成配置项（目前按设计全写死）。
- [x] Release 打包：本机可用 Git Credential Manager 里存的 `github.com` 凭据调 REST API 建 Release 并上传附件（不需额外 token），已发至 v0.1.9。

## 十、环境 / 路径 / 分析资料

- 游戏：`C:\Program Files (x86)\Steam\steamapps\common\Hollow Knight Silksong`
- 配置：`<游戏>/BepInEx/config/io.github.lifelan.randomcrestmod.cfg`（旧配置有孤儿键时可直接删文件让其重建；当前三项 `ToolUsesPerBench` / `CursedBind` / `ParryAlwaysSucceed` 都在空名字段下）
- 分析资料（同机 `E:\Agent\Pi\tmpwork`，**完整索引见 `tmpwork/INDEX.md`**）：
  - `acs/` — `Assembly-CSharp.dll` 的反编译源码（`acs/full.cs` 是全量合并版，查类/方法最快）；`acs/HutongGames.PlayMaker.Actions/` 是各 PlayMaker Action 源码；
  - `tk2d_src/` — 反编译的 `TeamCherry.TK2D`（`tk2dSprite.EnableKeyword`、`tk2dBaseSprite.color`）；
  - `fsm_Bind.txt` / `fsm_SilkSpecials.txt` / `fsm_Sprint.txt` / `fsm_CrestAttacks.txt` / `_fsm_init_full.txt` / `_fsmtmpl_now.txt` — PlayMaker FSM 状态机 dump；
  - `src/` — 手工整理的关键类；
  - `crestspools/`、`hudtk/`、`sil_all/`、`orb_sprites/`、`spoolparts/` — 提取的游戏美术；
  - 桌面 `Silksong_HUD_Frames/` — 每个纹章一张 HUD idle frame（脚本 `extract_crest_hud_frames.py`）。
- **本轮（v0.2.1）新增的关键脚本（均在 `tmpwork/`）**：
  - `extract_crest_hud_frames.py` — 从 `hud_assets_all.bundle` 的 atlas0 按 UV 裁剪出每个纹章的 HUD idle frame；
  - `_list_hud_clips.py` / `_list_hud_frames.py` — 列出 Bind Orb 动画 clip 与 sprite 名；
  - `_disk_blobs.py` / `_disk_compare.py` / `_dump_cloakless_def.py` — 量 HUD 图里丝轴盘的像素中心/直径，对照原版 sprite 几何；
  - `_dump_red_tools.py` — 全部红工具 + 按 `CountKey` 的分组（免费投掷计数依据）；
  - `_dump_bind.py` — Bind / Spell Control FSM 带参数 dump（快捷制造、Shoot Loop 等）；
  - `_dump_silkbomb2.py` — Silk Specials（符文之怒）状态带参数 dump；
  - `_dump_toolfsm.py` — 工具攻击 FSM 的 `GetToolEquipInfo` / `CustomToolUsage` 参数（确认念珠炮 `Tool` 是直接引用）；
  - `dump_rosarycannon.py` — 念珠炮 ToolItem 字段；
  - `dump_bindorb_mat.py` / `dump_shaders.py` / `dump_shader_recolour.py` — Bind Orb 材质与 `Sprites/Default-ColorFlash` shader 关键字。
- 构建需要 `SilksongPath.props`（已被 git 忽略），指向游戏目录。
- **发版流程**：`dotnet build -c Release` → `dotnet tcli build` → 提交 + `git tag vX.Y.Z` + `git push origin master vX.Y.Z`（CI 自动发 Thunderstore）；GitHub Release 用本机 GCM 凭据调 REST API 建（见“十一”）。

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

## 十二、设计：克隆纹章 vs 真纹章（取舍）

> 纷乱是**运行时克隆猎手**（`CrestService.EnsureCreated`：`Instantiate(Gameplay.HunterCrest)` → 改名 →
> 塞进 `ToolItemManager.crestList`）。它只是一个「新的 `ToolCrest` 对象」，引用的资产（`HeroControllerConfig`、
> 招式对象、动画库、图标/剪影/曝光、HUD 帧、槽位、本地化）全是**猎手的**。所有「随机」行为都是运行时
> Harmony 补丁实现的。

### 1. 本质：资产身份

- 游戏很多系统是**按纹章资产身份**判断：`Gameplay.HunterCrest/WandererCrest/...`、
  `PlayerData.CurrentCrestID == "X"`、`BindOrbHudFrame.DoChangeFrame` 的 if 链、`GetHeroAttackObject` 读
  `CurrentConfigGroup`……克隆纹章不在这些里，真纹章在。
- **即便在 AssetBundle 里做一份「真纹章」**，只要不能改游戏编译/序列化进去的数据（`Gameplay` 字段、
  `DoChangeFrame` if 链、各种 `CurrentCrestID == "X"`），它照样不被这些系统认识，照样要 patch。
  所以对 mod 来说「原生真纹章」并不存在，只是**自带资产多少**的区别。

### 2. 逐系统对照

| 系统 | 克隆（本项目） | 真纹章 |
|---|---|---|
| `HeroControllerConfig` / 招式 | 共用猎手（随机池里「猎手」= 基础猎手，v2/v3 无法区分） | 自己一份 |
| `CurrentCrestID` | `RandomCrest`，`== "Hunter"/"Spell"/...` 全 false | 自己的 ID，检查照旧 |
| `IsEquipped` / `CheckIfCrestEquipped` | 不认识 → 要 spoof | 原生 |
| HUD 外框 | `DoChangeFrame` 落到 `defaultFrameAnims`（猎手）→ overlay + hook | 自带 appear/idle/disappear |
| 图标/剪影/曝光/本地化/槽位 | 运行时覆盖：内嵌 PNG、注入字符串、`ApplySlots` | 自带 |
| 动画库 | 用猎手 clip → `RandomCrestAnimationLibrary` + `AnimationFallbackPatches` 合并兜底 | 自带 |
| 存档 | 手动 `UnlockForCurrentSave`（每会话重建） | 原生 |
| 完成度/成就 | `CrestPatches.CountGameCompletion` 把纷乱那 1 点扣回；`ALL_CRESTS` 靠分子分母平衡 | 原生 |

### 3. 取舍

- **克隆**：无需 Unity 工程/资产包，`Instantiate` + 内嵌 PNG 即可分发；招式/动画/槽位直接复用，风险低。
  代价：**每个「按身份判断」的系统都要 patch/spoof**，且容易被时序/内联等细节咬（本轮的冲刺斩、HUD 过渡
  都是这类）；升级档、原生纹章特效需要额外补。
- **真纹章**：相关系统原生一致，少一堆 spoof；但要游戏资产工程（作者 `ToolCrest`/`HeroControllerConfig`/
  动画/HUD 帧），且**写死的身份检查仍要 patch**，成本高、收益有限。
- 结论：本项目选克隆是务实解；代价就是「桥接层」的复杂度和维护成本。

### 4. 桥接层清单（因选了克隆）

- `ToolCrest.IsEquipped` spoof（`IsSpoofing`/`SpoofCrest`）；`RandomAttackPatches.CheckIfCrestEquipped_IsTrue_Prefix`（防内联）。
- `CurrentConfigGroup` 切换（`RandomAttackService.ApplyGroup`）提供随机招式。
- `CurrentCrestID` 类检查：`IsShamanCrestEquippedForTransition`（Transpiler）、`ReaperPayoutPatch`。
- HUD overlay + `HudFramePatches`（`FrameAppear`/`FrameDisappear`/`AlreadyAppeared`/`DoChangeFrame`）。
- `CrestService.ApplyVisuals/ApplySlots/ApplyLocalisation` 注入美术/槽位/文案；`CrestUpgraderPatches` 扣伊娃进度；
  `SkillGetMsgPatches` 修技能弹窗剪影。
- 随机池/工具/法术/嘲讽各自 spoof。
