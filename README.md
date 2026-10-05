# RandomCrestMod 使用说明（纷乱）

**简体中文** | [English](https://github.com/LIFELAN/RandomCrestMod/blob/master/README.en.md)

> 已发布到 Thunderstore（社区 **Hollow Knight: Silksong**），推荐用 mod 管理器（r2modman / Gale /
> Thunderstore Mod Manager）搜索 **RandomCrestMod** 安装，会自动处理好 BepInEx 依赖。

一个《空洞骑士：丝之歌》BepInEx 模组：新增一个纹章 **「纷乱」**（英文 **Chaos**）。
装备它之后，攻击、缚丝、工具、法术全部随机 —— **你永远不知道下一秒会发生什么**。

纹章在每个存档**开局即解锁**，可在**任意长椅切换**，**六个槽位默认全部解锁**。
所有随机效果**只在装备纷乱时生效**，完全不影响其他纹章。

---

## 安装

1. 先安装 **BepInExPack Silksong**（用 mod 管理器会自动装好）。
2. 将 `RandomCrestMod.dll` 放入：

   ```
   <游戏目录>/BepInEx/plugins/lifelan-RandomCrestMod/RandomCrestMod.dll
   ```

   （用模组管理器安装，或自行 `dotnet build -c Release` 构建，构建脚本会自动复制。）
3. 启动游戏，在任意长椅选择 **纷乱** 即可。

---

## 纹章

| | |
| --- | --- |
| 名称 | 中文 **纷乱**，英文 **Chaos** |
| 说明 | 你永远不知道下一秒会发生什么。 |
| 解锁 | 每个存档**开局即可解锁** |
| 切换 | **任意长椅**，与普通纹章一样 |
| 槽位 | **六个全部解锁**（1 红 / 1 技能 / 2 蓝 / 2 黄） |

---

## 功能

以下全部**只在装备纷乱时生效**。卸下后，其他纹章的行为与游戏原版完全一致。

| 功能 | 说明 |
| --- | --- |
| 随机攻击 | 每次攻击随机使用某个纹章的攻击模组，你按下的方向（普通 / 上 / 下 / 蹬墙 / 冲刺 / 蓄力斩）保持不变。 |
| 随机缚丝 | 每次缚丝随机使用某个纹章的缚丝效果。 |
| 随机工具 | 每次投掷随机变成游戏内任意一个红色工具，无论是否已获得。 |
| 随机法术 | 每次释放灵丝技能随机变成 6 种法术之一。 |
| 随机嘲讽 | 按下嘲讽键（R3 / V）随机使用三种形态之一：普通、野兽纹章吼叫、投掷环特殊动作；野兽形态会连专属动作一起切换。 |
| 十字绣强化 | 装备纷乱时，十字绣摆好架势后即使未被攻击也会自动反击；命中火花被去掉，后退的瞬间会加一层粉白高光。其它纹章的十字绣完全原版。 |
| 工具次数 | 每次坐椅子前所有工具共享 **20 次**使用次数（可配置），补充**不消耗碎片**。 |
| 自定义 HUD | 自定义缚丝外框、固定的"随机"工具 / 法术 HUD 图标、存档选择界面的自定义纹章 spool。 |

### 特殊工具处理

- `Extractor`（**Needle Phial** / 针剂）、`Silk Snare`（**Snare Setter** / 陷阱设置器）、
  `Rosary Cannon`（**念珠炮**）与 `Screw Attack`（**Delver's Drill** / 掘洞钻）被排除在随机工具池之外 ——
  前两者随机投掷时无法正常工作，念珠炮使用方式特殊且快速连投容易哑弹，掘洞钻是向下突进的钻头、随机投掷时无法正常工作。
- `Lightning Rod`（**Voltvessels** / 电枢球）每次抽取时在**流星锤**与**标枪**两种形态间随机，之后会还原玩家自己的形态设置。
- `Rosary Cannon`（念珠炮）始终保持**已充能**形态。

---

## 配置项

配置文件：`BepInEx/config/io.github.lifelan.randomcrestmod.cfg`

| 配置项 | 默认值 | 说明 |
| --- | --- | --- |
| `Tools/ToolUsesPerBench` | `20` | 坐椅子时所有工具补充到的使用次数。 |

> 其余数值均已**写死**（经调试确定），不提供配置：随机攻击 / 缚丝 / 工具 / 法术 / 嘲讽 / 十字绣强化
> 始终开启且仅限纷乱生效；工具排除名单固定为 Needle Phial、Snare Setter、Rosary Cannon 与 Delver's Drill；Voltvessels 两种形态随机；
> Rosary Cannon 固定已充能；自定义 HUD 外框 / 存档 spool / 随机图标固定开启，HUD 外框的偏移与
> 缩放也已固定。

---

## 注意事项

- 只有装备纷乱时随机效果才会触发；其他纹章完全不受影响（工具次数、补充花费、工具形态等都不会被改动）。
- 随机工具池包含你**尚未获得**的工具；随机法术为全部 **6 种**灵丝技能。
- 随机缚丝需要**满丝（9 格）**才会触发；灵丝不足时表现为原版"丝不够"。
- 纷乱**不计入游戏完成度**（其他纹章、工具、技能的完成度不受影响）。

---

## 构建

```sh
dotnet build -c Release
```

构建完成后会将 `RandomCrestMod.dll` 复制到 `BepInEx/plugins/lifelan-RandomCrestMod/`。
自定义美术资源位于 `Assets/`（纹章图标、HUD 外框、存档 spool、随机图标等），会嵌入 DLL。

游戏路径在 `SilksongPath.props` 中配置。
