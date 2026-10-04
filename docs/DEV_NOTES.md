# RandomCrestMod — 开发交接 / 关键点

> 这份是给后续（重启对话后）的自己和 AI 用的开发笔记，记录**现状、关键决定、坑和待办**。
> 面向玩家的说明见仓库根目录 `README.md` / `README.en.md`。

## 一、当前状态（截至最后一次提交 `854219a`）

- 编译：`dotnet build -c Debug`，**0 警告 0 错误**。
- 已安装：`<游戏>/BepInEx/plugins/lifelan-RandomCrestMod/RandomCrestMod.dll`（构建会自动复制）。
- 仓库：https://github.com/LIFELAN/RandomCrestMod（默认分支 `master`）。
- 工作区应保持干净；本文件是唯一可能未提交的新增项。

最近提交：
```
854219a Cancel a random bind that enters surface water
c48f0c8 Re-acquire the HUD frame overlay on save load / scene init
a8ee1ee Tune random-tool icon size to ppu 390
475ce33 Enlarge the random-tool HUD icon to match the spell icon
641b9f4 Widen the random-tool icon circle slightly
3432d2c Brighten the Chaos HUD frame and round the random-tool icon
c4b032a Wait out the post-release sprint skid before swapping the crest config
6fb3e56 Remove the cursed-bind feature and all of its workarounds
81c93ab Exclude the mod crest from game completion percentage
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
    - 代价（已知取舍）：滑步那一次缚丝可能拿不到纹章专属状态（野兽狂暴/收割模式），站立缚丝完全正常。
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
  - **读档 / 场景加载会重建 `Bind Orb`**，所以 `CrestPatches` 在 `GameManager.SetLoadedGameData` 和 `HeroController.SceneInit` 里调用 `HudFrameService.Reset()` 强制重新获取（否则只有重启游戏才显示）。
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
4. **随机萨满空中缚丝落水卡死**：`SurfaceWaterRegion` 用 `SpellCrest.IsEquipped` 放行进水，但原版会在进水时走 Bind FSM 的 `Shaman Fall --CANCEL--> Shaman Air Cancel`；我们换配置后没触发。已在 `RandomAttackService.Tick` 里检测 `cState.swimming` 时给 Bind FSM 发 `CANCEL`（`854219a`，**待用户实测确认**）。

## 九、待办 / 待确认

- [ ] 实测确认「随机萨满落水取消」是否生效（`854219a`）。
- [ ] 实测确认「纷乱 HUD 读档后立即显示」（`c48f0c8`）。
- [ ] 滑步缚丝缺纹章专属状态：当前是已知取舍；若要修需同步两个 FSM，风险高。
- [ ] （可选）SilkCurseMod 兼容：让 SilkCurseMod 在装备纷乱时让路。
- [ ] （可选）随机结果临时日志，验证 7 纹章均匀分布。
- [ ] Release 打包（GitHub Release 需要 token，本机无法用 API 建；可手动发）。

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
