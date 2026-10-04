# RandomCrestMod

> **纷乱 / Chaos** — 你永远不知道下一秒会发生什么。
> **纷乱 / Chaos** — You never know what will happen next.

<details open>
<summary><b>🇨🇳 中文（默认）</b></summary>

<br>

RandomCrestMod 为《空洞骑士：丝之歌》加入了一个全新纹章 —— **纷乱**。装备纷乱时，攻击、缚丝、
工具与法术都会从游戏内所有纹章 / 工具 / 灵丝技能中随机抽取（包括你还没获得的）。其他纹章完全
不受影响。

## 纹章

- 名称：中文 **纷乱**，英文 **Chaos**。
- 说明：*"你永远不知道下一秒会发生什么。"*
- 每个存档**开局即可解锁**。
- **可在任意长椅切换**，与普通纹章一样。
- **六个槽位默认全部解锁**（1 红、1 技能、2 蓝、2 黄）。

## 功能

以下全部**只在装备纷乱时生效**。卸下后，其他纹章的行为与游戏原版完全一致。

| 功能 | 说明 |
| --- | --- |
| 随机攻击 | 每次攻击随机使用某个纹章的攻击模组，你按下的方向（普通 / 上 / 下 / 蹬墙 / 冲刺 / 蓄力斩）保持不变。 |
| 随机缚丝 | 每次缚丝随机使用某个纹章的缚丝效果。 |
| 诅咒缚丝 | 缚丝有 **5%** 概率被拒绝（播放诅咒缚丝序列）。可关闭。 |
| 随机工具 | 每次投掷都会随机变成游戏内任意一个红色工具，无论是否已获得。 |
| 随机法术 | 每次释放灵丝技能都会随机变成 6 种法术之一。 |
| 工具次数 | 每次坐椅子前所有工具共享 **20 次**使用次数（可配置），补充**不消耗碎片**。 |
| 自定义 HUD | 自定义缚丝外框、固定的"随机"工具/法术 HUD 图标、存档选择界面的自定义纹章 spool。 |

## 特殊工具处理

- `Extractor`（**Needle Phial** / 针剂）与 `Silk Snare`（**Snare Setter** / 陷阱设置器）被排除在随机
  工具池之外——它们随机投掷时无法正常工作。
- `Lightning Rod`（**Voltvessels**）始终强制为投掷的**流星锤（bola）**形态。
- `Rosary Cannon`（念珠炮）始终保持**已充能**形态。

## 配置

只保留两个配置项（其余数值已写死）：

```ini
[Bind]
## If true, a bind has a 5% chance to be refused like the Cursed crest. Turn off to always bind normally.
EnableCursedBind = true

[Tools]
## Number of uses every tool is refilled to when resting at a bench (refills are free).
ToolUsesPerBench = 20
```

## 安装

1. 为丝之歌安装 [BepInEx 5.4.x (x64)](https://github.com/BepInEx/BepInEx/releases)。
2. 将 `RandomCrestMod.dll` 放入 `BepInEx/plugins/`。
3. 启动游戏，在任意长椅选择 **纷乱**。

## 从源码构建

需要 .NET SDK 与游戏本体。

1. 新建 `SilksongPath.props` 指向你的游戏目录（该文件已被 git 忽略）：

   ```xml
   <Project>
     <PropertyGroup>
       <SilksongFolder>.../Hollow Knight Silksong</SilksongFolder>
       <SilksongPluginsFolder>$(SilksongFolder)/BepInEx/plugins</SilksongPluginsFolder>
     </PropertyGroup>
   </Project>
   ```

2. `dotnet build -c Release`

构建后会自动把插件复制到 `$(SilksongPluginsFolder)/lifelan-RandomCrestMod/`。

</details>

<details>
<summary><b>🇬🇧 English</b></summary>

<br>

RandomCrestMod adds a brand-new crest — **Chaos** (纷乱) — to *Hollow Knight: Silksong*. While Chaos
is equipped, attacks, binds, tools and spells are randomly chosen from every crest, tool and silk
skill in the game, including ones you have not obtained yet. Other crests are completely untouched.

## The crest

- Named **Chaos** in English, **纷乱** in Chinese.
- Description: *"You never know what will happen next."*
- **Unlocked from the start** for every save.
- **Switchable at any bench**, like a normal crest.
- **All six slots are unlocked** (1 Red, 1 Skill, 2 Blue, 2 Yellow).

## Features

Everything below only happens **while Chaos is equipped**. Unequip it and the game behaves exactly
like vanilla for every other crest.

| Feature | Behaviour |
| --- | --- |
| Random attacks | Every attack uses a random crest's attack moveset. The direction you pressed (normal / up / down / wall / dash / charge slash) is preserved. |
| Random bind | Every bind uses a random crest's bind effect. |
| Cursed bind | A bind has a **5%** chance to be refused like the Cursed crest (plays the cursed sequence). Can be turned off. |
| Random tools | Every thrown tool becomes a random Red tool from the whole game — obtained or not. |
| Random spells | Every cast silk skill becomes one of the six silk skills at random. |
| Tool budget | Tools share **20 uses per bench** (configurable) and refill for **free**. |
| Custom HUD | A custom bind-orb frame, fixed "random" tool / spell HUD icons, and a custom crest spool on the save-selection screen. |

## Special tool handling

- `Extractor` (**Needle Phial**) and `Silk Snare` (**Snare Setter**) are excluded from the random
  tool pool — they do not work when thrown at random.
- `Lightning Rod` (**Voltvessels**) is always forced into its thrown **bola** form.
- `Rosary Cannon` is kept **fully charged**, so it always uses its charged form.

## Configuration

Only two settings are exposed (all other values are baked in):

```ini
[Bind]
## If true, a bind has a 5% chance to be refused like the Cursed crest. Turn off to always bind normally.
EnableCursedBind = true

[Tools]
## Number of uses every tool is refilled to when resting at a bench (refills are free).
ToolUsesPerBench = 20
```

## Installation

1. Install [BepInEx 5.4.x (x64)](https://github.com/BepInEx/BepInEx/releases) for Silksong.
2. Drop `RandomCrestMod.dll` into `BepInEx/plugins/`.
3. Launch the game and pick up the **Chaos** crest at any bench.

## Building from source

Requirements: .NET SDK and the game installed.

1. Create `SilksongPath.props` pointing at your game install (it is git-ignored):

   ```xml
   <Project>
     <PropertyGroup>
       <SilksongFolder>.../Hollow Knight Silksong</SilksongFolder>
       <SilksongPluginsFolder>$(SilksongFolder)/BepInEx/plugins</SilksongPluginsFolder>
     </PropertyGroup>
   </Project>
   ```

2. `dotnet build -c Release`

The build copies the plugin into `$(SilksongPluginsFolder)/lifelan-RandomCrestMod/`.

</details>
