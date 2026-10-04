# RandomCrestMod — 开发交接 / 关键点

> 这份是给后续（重启对话后）的自己和 AI 用的开发笔记，记录**现状、关键决定、坑和待办**。
> 面向玩家的说明见仓库根目录 `README.md` / `README.en.md`。

## 一、当前状态（截至 v0.1.2）

- 编译：`dotnet build -c Debug`，**0 警告 0 错误**。
- 已安装：`<游戏>/BepInEx/plugins/lifelan-RandomCrestMod/RandomCrestMod.dll`（构建会自动复制）。
- 仓库：https://github.com/LIFELAN/RandomCrestMod（默认分支 `master`）。
- 工作区应保持干净；本文件是唯一可能未提交的新增项。

最近提交：
```
43e1add Let the Flea Charm stack with the Chaos silk discount
7af9189 Correct the skid-bind trade-off note
eb44157 Release v0.1.1
90f0329 Drop redundant suffix from the mod's proper name
dff0ccc Fix HUD frame stuck hidden after switching crests
68c0e1e Add developer handover notes
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
  `EnableRandomAttacks / EnableRandomBind / EnableRandomTools / EnableRandomSpells / EnableCustomHudFrame / EnableCustomSaveSpool / EnableRandomIcons / OnlyOnRandomCrest(true) / DebugLogging(false)`。
- **所有随机效果只在装备纷乱时生效**：
  - 攻击/缚丝：`RandomCrestModPlugin.OnlyOnRandomCrest && CrestService.IsRandomCrestEquipped()`；
  - 工具/法术：`RandomToolService.GateOpen`（同上）；
  - 随机图标：`RandomIconService.For` 走 `RandomToolsActive/RandomSpellsActive`；
  - HUD 外框：`HudFrameService.Tick` 检查 `IsRandomCrestEquipped()`。
- 唯一配置项：`[Tools] ToolUsesPerBench = 20`（坐椅子补满次数，补充免费）。
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
- **硬编码排除**：`Extractor`(Needle Phial)、`Silk Snare`(Snare Setter)。
- **特殊处理**：`Lightning Rod`(Voltvessels) 强制投掷(bola)形态（PlayerData bool `LightningToolToggle=true`）；`Rosary Cannon` 保持满充能、且不进入共享次数。
- 共享次数：`_usesLeft` 初始 20，所有 Red 工具同步，坐椅子/读档 `ResetUses`；`BeginFreeRefill/EndFreeRefill` 临时把 Red 工具的 `replenishResource=None` 让补充免费（其它纹章不受影响）。
- 计数补丁：`GetToolStorageAmount`、`HeroController.CanThrowTool`、`HeroController.DidUseAttackTool`、`ToolItemManager.TryReplenishTools`。
- 绑定/法术只在装备纷乱时替换（`RandomToolsActive/RandomSpellsActive`）。
- **法术费用**：`PlayerDataSilkSkillCostPatch` 在 `RandomSpellsActive` 时把 `PlayerData.SilkSkillCost` 整体减 `RandomCrestModPlugin.RandomSpellSilkDiscount`（默认 1，下限 1）：原版 4 → 3，满血带蚤母卵（原版 3）→ 2，所以**蚤母卵在纷乱上仍有效**。判定（`CanThrowTool`）/ HUD 图标（`ToolHudIcon`）/ 所有技能 FSM 的 `TakeSilk`（经 `GetPlayerDataVariable` 读该属性）都读它，所以自动一致；缚丝（`SilkSpool.BindCost`）和工具（`Usage.SilkRequired`）完全不受影响。

## 六、HUD / 存档界面美术

- 资源（`Assets/`，内嵌 DLL）：
  - `crest_icon.png` / `crest_silhouette.png` — 纹章本体图标/剪影；
  - `crest_hud_frame.png` — 左上角缚丝丝轴外框（用户绘制，后期 gamma≈1.35 提亮），**ppu 420**；
  - `crest_save_spool.png` — 存档选择界面 spool（226×173）；
  - `crest_random_tool.png` — HUD 工具固定图标（红色，"兵械库"成就图标再上色 + 圆形柔边遮罩），**ppu 390**；
  - `crest_random_spell.png` — HUD 法术固定图标（白色，"千丝万缕"成就图标），**ppu 420**。
- `HudFrameService`：**叠加方案**——不碰游戏 tk2d 网格/材质，而是在 `Bind Orb` 下挂一个自己的 MeshFilter/MeshRenderer：
  - 装备纷乱时隐藏游戏外框渲染器 + 显示我们的叠加；否则反过来；
  - 叠加对象的 `layer` 必须等于 `Bind Orb.gameObject.layer`（否则 HUD 相机不渲染）；
  - **读档 / 场景加载会重建 `Bind Orb`**，所以 `CrestPatches` 在 `GameManager.SetLoadedGameData` 和 `HeroController.SceneInit` 里调用 `HudFrameService.Reset()` 强制重新获取；
  - **关键坑（已修）**：游戏自己的 `Bind Orb` FSM 会在 HUD 出场动画里开关外框 `MeshRenderer.enabled`（`Init` 先关，等血量 HUD 出来即 `SHOW HP` 后 `Appear` 再开）。因此**绝不能缓存 `enabled` 再恢复**：获取时它通常是 `false`，恢复这个值会让所有其它纹章的外框永久消失。现在只记录「是不是我们把它关掉的」(`_weHidGameFrame`)，恢复时只在我们关过的情况下置 `true`；
  - `Reset()` 会先 `Restore()` 再丢引用（否则活下来的 HUD 会一直保持关闭），并销毁旧的叠加对象/网格，避免残留 ghost；
  - 首次获取时用 `_gameFrameRevealed` 等游戏先把外框显示一次（`Appear`）再接管，这样纷乱外框和其它 HUD 一样「一点点加载」，而不是进档瞬间就蹦出来。
  - 曾用"直接改游戏 `sharedMesh/sharedMaterial`"方案，会破坏其他纹章外框（已弃用，不要回退）。
- 随机图标：`RandomIconPatches.ToolHudIconSpritePatch`（换 sprite）+ `RadialHudIconColourPatch`（强制白 tint）。
- 存档 spool：`SaveProfileCrestPatch` 改 `SaveProfileHealthBar.ShowHealth`（我们的 id 解析不了它的私有枚举）。

## 七、其它

- `CrestService.EnsureCreated`：**不要用一次性 `_created` 门禁**——`crestList` 是可变 ScriptableObject，换存档/场景会被重置，必须每帧检查 `GetByName(CrestName) == null` 再补注册。
- 本地化：`CrestService.EnsureLocalisationCurrent` 按 `Language.CurrentLanguage()` 注入中/英，语言切换会重注入。
- 完成度：`CrestPatches.PlayerData_CountGameCompletion_Postfix` 扣掉纷乱贡献的那 1 点；`ALL_CRESTS` 成就不动（纷乱始终解锁，分子分母平衡）。
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
   - 仍待实测。

## 九、待办 / 待确认

- [x] HUD 外框读档 / 切换纹章后正常显示（`HudFrameService` 不再缓存 `renderer.enabled`，`Reset()` 先恢复再清理，等游戏出场后再接管）。
- [x] 随机萨满空中缚丝落水不再穿出场景（`TickDash` 不再抢跑 + `SurfaceWaterRegion` 的 Shaman 安全网）。
- [x] 野兽/收割者疾风步/滑步缚丝正常获得 Rage/Reaper buff（同一根因：`TickDash` 提前 `Restore`）。
- [x] 疾风步/空中疾风步/滑步里的普通、上、下劈砍也随机（`ApplyIfRequested` 改为静默换配置）。
- [x] 纷乱法术费用降为 3 格（`PlayerDataSilkSkillCostPatch`）；满血带蚤母卵再叠到 2 格。
- [ ] （可选）滑步缚丝时 Sprint FSM 的缓存冲刺劈砍对象不会随 bind 刷新；目前靠下一次冲刺劈砍重掷兜底，未发现可见问题。
- [ ] （可选）SilkCurseMod 兼容：让 SilkCurseMod 在装备纷乱时让路。
- [ ] （可选）随机结果临时日志，验证 7 纹章均匀分布。
- [ ] （可选）把法术费用 / 其它写死参数做成配置项（目前按设计全写死）。
- [x] Release 打包：本机可用 Git Credential Manager 里存的 `github.com` 凭据调 REST API 建 Release 并上传附件（不需额外 token），已发 v0.1.0 / v0.1.1。

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
