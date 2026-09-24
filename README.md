# RimTalk: Persona Director

**人格导演使用教程 / Persona Director User Guide**  
[简体中文](#简体中文) · [English](#english)

## 简体中文

### 这是什么

Persona Director（RPD）是 [RimTalk](https://github.com/jlibrary/RimTalk) 的人格扩展。它接入 RimTalk 原有的人格编辑、智能生成和随机生成入口，把更丰富的 Pawn 资料用于**生成人格**，并加入预设库、导演台、自动分配、人格演变，以及可选的自动生成和自动更新。生成请求沿用 RimTalk 的 AI 服务；先在 RimTalk 中完成 AI 连接配置。

| 本项目负责 | 仍由 RimTalk 负责；本教程不展开 |
|---|---|
| 组织人格生成资料、导演备注和提示词；生成或套用 Persona | AI 提供商、密钥与模型连接 |
| 预设库、随机应用、规则分配、导入导出、批量生成 | Persona 的基础存储与原有人格编辑窗口 |
| 手动演变、实验性的自动生成与自动更新、更新历史 | 日常对话的触发、间隔、气泡显示及其他对话设置 |
| 为 RimTalk 提供扩展变量，并在 Persona 中渲染 Scriban | RimTalk 自己的日常对话上下文筛选器 |

和 RimTalk 原生的人格功能相比，RPD 接管单人 Smart Gen 的生成请求、把 Random Gen 改为可浏览的预设选择，并让新 Pawn 可以按规则得到初始人格。安装后，随机套用现成预设**不调用 AI**；Smart Gen、导演台快速生成及自动生成会调用 RimTalk 的 AI 服务。RPD 的资料筛选主要影响人格生成与演变，不能代替 RimTalk 的日常对话筛选器。

### 1. 安装与第一次生成人格

1. 使用 RimWorld **1.6**。启用 [Harmony](https://steamcommunity.com/sharedfiles/filedetails/?id=2009463077) → [RimTalk](https://steamcommunity.com/sharedfiles/filedetails/?id=3551203752) → [Persona Director](https://steamcommunity.com/sharedfiles/filedetails/?id=3619548407)。仓库中的 C# 源码位于 `Source`，编译后的 `RPD.dll` 位于 `Assemblies`；源码构建方法见 [BUILDING.md](BUILDING.md)。同步 GitHub 仓库时应保留 `About/`、`Assemblies/`、`Defs/`、`Languages/`、`Source/` 和 `Textures/`，以及 `RPD.csproj`、`RPD.sln`、`RPD.Local.props.example`、`.gitignore`、`BUILDING.md` 和本 `README.md`。
2. 在 RimTalk 的设置里配置并验证 AI 连接，随后载入存档。
3. 打开 **选项 → 模组设置 → RimTalk: Persona Director**。先保留默认数据筛选，选第一个内置提示词槽位。
4. 打开一个 Pawn 的 RimTalk 人格编辑窗口，使用 **Smart Gen／智能生成**。RPD 会按选中的资料和提示词向 AI 请求人格；查看候选结果，再选择想用的版本。
5. 要快速验证 RPD 的另一个入口，点击人格编辑窗口的 **Random Gen／随机生成**。它会打开预设浏览器：选分类与预设后点“应用所选”，或点“随机应用”。这一步直接套用已有文本。

**完整的 Mod 文件结构：**游戏安装目录中的模组文件夹应包含以下运行文件：

```text
Rimtalk-Persona-Director/
├── About/       # About.xml、图标、预览图和 PublishedFileId.txt
├── Assemblies/  # RPD.dll
├── Defs/        # 游戏定义
├── Languages/   # 本地化文本
└── Textures/    # 界面与其他纹理资源
```

`Source/`、工程文件、构建说明和本 `README.md` 属于源码仓库，不需要放进给玩家的 Mod 运行目录。普通玩家运行模组需要 `Assemblies/RPD.dll`，源码不能代替 DLL。Harmony 与 RimTalk 是需要另外安装的前置模组。

### 2. 预设库与自动分配

在 RPD 设置页点 **打开预设库**。首次空库会导入 RimTalk 原有和本项目内置的人格预设。**预设**页可搜索、筛选分类、创建或删除条目，编辑名称、正文和健谈度；“管理原版／管理内置”可把对应预设加入用户库。启用状态决定它是否同步给 RimTalk，以及无规则命中时是否进入全局随机池。

**保存固定人设：**在“预设”页点“创建预设”，用角色名命名，把准备好的完整人格填入正文，再取消该条目的“启用”。预设保存在模组设置中，跨存档可用。以后在另一个存档遇到这个角色，打开其人格编辑窗口 → **Random Gen／随机生成**，找到该预设并点 **应用所选**，无需复制到外部保存，也不会为这次套用调用 AI。未启用的预设仍可在浏览器中手动选用；若只想手动调用，不要把它加入分配规则。浏览器的“随机应用”会从当前显示的预设中抽取，未启用项也可能被抽中。

在 **规则**页新建规则，选择派系、种族、异种型或年龄区间，设置优先级，并勾选允许抽取的预设。新的、尚无人格的人类 Pawn 上图时会套用匹配预设：取最高优先级；同级规则合并候选池；无规则命中则从全局启用池随机抽取。规则分配本身不向 AI 发送请求。

需要备份或分享时，在预设库点 **导入/导出**：左键“导出”复制 XML，右键“导出”保存带时间戳的文件；导入可选“追加”或“覆盖”，也可从文件读取。覆盖前先导出备份；追加不会自动去重，覆盖会替换预设与规则列表。

### 3. 导演备注与导演台

界面底部的 RPD 图标：**左键**打开导演备注，**右键**打开导演台，**Shift+左键**打开模组设置。导演备注是全局文本，切换 Pawn 不会自动清空。适合写游戏尚未记录的共同经历、秘密或剧情边界；结束一组角色的生成后，记得清理或改写备注。

导演台可按殖民者、囚犯、奴隶、访客、敌人、动物等类别筛选当前地图角色，也可搜索、刷新、逐个勾选。每行的 **生成**调用 AI 并应用结果，**编辑**打开 RimTalk 人格编辑窗口，**谈话**打开 RimTalk 对话入口。选中多人后，底部的 **单独发送**会为每人分别请求；**合并发送**会把所选角色放在一次请求中，适合建立互相关联的背景。合并结果需要区分各角色 ID，格式错误的部分可能无法应用。

**小巧思：**一队难民可先在全局备注中写共同逃亡经历，用合并发送建立联系，再逐个生成不同的心理反应。给海盗派系设置较高优先级规则，则可以让海盗偏向一组风格，而其他新 Pawn 仍用全局预设池。

### 4. 资料筛选与提示词

RPD 设置页的“选择发送给 AI 的数据内容”控制人格生成用的资料：基础身份、种族与异种型、基因、童年和成年背景、亲属、特质、意识形态、技能与热情、健康、装备、背包，以及可选的 RimPsyche、记忆和常识。某些项目还有单独的“详细描述”开关。描述越多，请求越长；先保留身份、背景、关系、特质和技能，再按角色需要添加基因或健康描述。RimPsyche、记忆及常识选项会随相应扩展模组是否安装而出现。相关 GitHub 仓库：[RimPsyche](https://github.com/jagerguy36/Rimpsyche)；[记忆拓展（记忆和常识）](https://github.com/sanguodxj-byte/RimTalk-ExpandMemory)。

内置提示词有五个可编辑槽位：**Standard** 提供三种人格解读；**Story-Driven** 写单段故事；**Data-Driven** 严格连接童年与成年背景；另两个分别供演变“追加”与“覆盖”使用。用 Slot 下拉切换，修改名称或正文，**重置默认**仅重置当前槽位。`{LANG}` 表示游戏语言；返回格式协议由模组自动附加。普通单次生成若选到演变专用槽位，会改用 Standard。

设置页还能分别给单次生成和演变选择 **RimTalk 高级预设**；自动生成则可按角色类别单独选择。高级预设走另一条上下文路径，具体区别见本语言末尾的[附录](#附录人格上下文如何构建)。想明确控制 RPD 数据筛选时，先用内置模板验证结果，再切换高级预设。

**高级预设实际怎么写：**在 RimTalk 的高级提示词编辑器中新建预设，启用一个 System 条目写人格任务与返回要求，再启用一个 User 条目放资料。把 `{{ pawn.d_full_profile }}` 写进 User 条目就能取到 RPD 的整合角色资料；把 `{{ director_notes }}` 单独写入可补上全局导演备注。选好预设后回 RPD 设置页将其指定给“单次生成”或对应自动生成类别。手动和自动更新都可引用现有人格、时间和状态差异；有时间记录时还可用 `pawn.d_evolve_memories` 读取此后的记忆。完整可粘贴模板、变量对照和精简格式的方法放在[附录 H](#h-scriban-高级预设与格式精简)。

RPD 也把 `pawn.d_full_profile`、`pawn.d_memories`、`pawn.d_status_diff` 等人物变量和 `director_notes`、`smart_history` 等上下文变量注册给 RimTalk。Persona 文本可写 Scriban 条件，让同一角色在囚禁、殖民地生活等处境下呈现不同表达；这是人格文本的动态渲染，不是给每次对话重新生成一份 Persona。例如：

```scriban
{{ if pawn.IsPrisoner }}Speak cautiously; distinguish compliance from trust.{{ end }}
{{ if pawn.IsColonist }}Let work and family shape the voice.{{ end }}
```

### 5. 手动演变与历史

打开 Pawn 的人格编辑窗口，可先点 **设置时间**记录当前年龄与状态快照。经过一段游戏后点 **演变**：模组把原人格、经过时间、新记忆及启用时的状态差异提供给 AI。结果遵循自动更新界面的同一个模式：“追加”在编辑框末尾添加 `[Development]`，“覆盖”用结果替换整篇人格。内置提示词也按模式选择相应槽位。要得到有用的差异，须开启 RPD 的“数据比较”并保留相关资料类别；这个开关默认关闭。

启用实验功能后，可在自动更新名单里点 **历史**查看更新上下文与前后版本，并在确认后恢复旧版本；默认每名 Pawn 保留最多五条记录。长档建议先设置时间，再选择某个重大事件之后手动演变，避免让 AI 凭空猜测角色为何改变。

### 6. 自动生成与自动更新（可选实验功能）

在 RPD 设置中勾选 **启用附加功能**并确认，然后分别配置：

1. **自动生成：**勾选“启用自动生成”，点“配置”。殖民者、囚犯、奴隶、访客、敌人和其他角色分开控制，默认都关闭。每类可选择内置提示词或 RimTalk 高级预设；内置路径默认同步全局资料筛选，关闭“同步”后可单独选择数据。内置路径的自动生成备注支持 Scriban，并仅在该类别的有效上下文启用“导演备注”时发送；高级预设不会自动附上这份备注，目前也没有对应的 Scriban 变量，需把所需说明写进预设。新 Pawn 首次上图且具有初始人格基线时才会排队；读档重生不会重复触发。
2. **自动更新：**开启总开关后，在名单中为每名 Pawn 开启状态并设置周期。`0` 或负数关闭定时更新，但身份和事件触发仍可运行；可批量启停、批量设置周期。可选身份变化，以及结婚、分手、生育、直系亲属死亡、新增特质等事件。自动更新不依赖自动生成的分类开关；身份变化勾选项由自动更新界面单独控制，仍需该 Pawn 的独立开关。若 Pawn 仍是规则或随机库分配的初始人设，且自动生成总开关及目标分类均已启用，则身份变化优先尝试自动生成；否则尝试自动更新。
3. **更新方式：**手动与自动更新共用此模式：“追加”在原人格后补上发展段（默认）；“覆盖”重写整篇人格。还可选择静默或左上角通知，并设置倍速保护；默认达到 3 倍速时暂停新扫描和请求。

自动生成与自动更新共用请求协调器；实验功能关闭时等待任务失效。若发生超时或运行错误，设置页会显示熔断原因；排查后点 **清除错误**，再手动重新启用。自动更新要求 Pawn 存活、在地图上、有现成人格，且总开关与该 Pawn 的独立开关均已打开。

**建议：**一般只启用殖民者、奴隶、囚犯的自动生成分类和自动更新身份触发。访客、敌人、其他分类仍可自行开启，但一批袭击者或商队可能同时造成大量自动生成请求与 API 开销；殖民者变成中立或敌对后通常也不再与玩家长期互动，自动更新收益有限。若确有相应剧情需求，可以按 Pawn 单独开启。

### 7. 常见问题

| 现象 | 检查顺序 |
|---|---|
| 无法生成人格 | 先验证 RimTalk 的 AI 连接；再检查所选提示词与返回结果。 |
| 新 Pawn 没有自动生成 | 检查“启用附加功能”→“启用自动生成”→对应类别开关，以及是否属于首次上图。 |
| 自动更新未发生 | 检查总开关、Pawn 独立开关、周期或触发项、当前倍速与是否已有 Persona。 |
| 缺少某项资料 | 检查 RPD 的人格资料筛选；高级预设还须确认模板自身引用了该资料。可临时开调试日志看请求。 |
| 更新结果不合预期 | 检查设置时间快照、数据比较、自动更新模式；必要时用“历史”恢复。 |

### 附录：人格上下文如何构建

以下方括号标题和分隔符表示请求中的**实际格式**；尖括号是示意占位符。可选区块只在资料存在且相应设置允许时出现。Context 是请求的指令部分，Prompt 是数据／用户部分。RPD 的过滤器控制人格流程；RimTalk 日常对话的上下文由 RimTalk 自己管理。

**A. 基础角色资料块。**手动单人、导演台和自动生成使用这类资料。开启的栏目按下列顺序组合；快照只保留角色状态资料，不含备注、记忆和常识。

```text
--- Basic Info ---
Name: <角色名>
Gender: <性别>
Age: <生物年龄>
Current Status: <殖民者／囚犯／访客等身份>
Faction: <派系及其与玩家的关系>

--- Race & Xenotype ---
Race: <种族>                 [可选描述]
Xenotype: <异种型>           [可选描述]
--- Genes ---
[Endogenes (Natural)]: <天然基因列表>
[Xenogenes (Artificial)]: <植入基因列表>
--- Backstory ---
Childhood: <标题及可选描述>
Adulthood: <标题及可选描述>
--- Key Relationships ---
- <关系>: <对方姓名> <对方状态>
--- Traits ---
<特质及可选描述>
--- Ideology ---
<意识形态及可选描述>
--- Skills ---
<技能>: <等级／[INCAPABLE]> [热情及可选描述]
--- Health ---
- <身体部位>: <健康状况> [可选描述]
--- Equipment ---
- [Weapon]: <武器>
- [Apparel]: <衣物>
--- Inventory ---
- <物品>
--- RimPsyche ---
<可用的人格倾向数据>
--- Director's Notes (Custom Context) ---
<导演备注或自动生成备注>
--- Memories ---
<近期记忆>
--- Common Knowledge ---
<相关常识>
```

基础身份可能增加奴隶来源派系或外部派系描述；关系只列关键关系。基因区分天然与植入，技能可标记无法从事的工作。备注、记忆和常识在快照模式下不附加。RimPsyche、记忆和常识等联动 Mod 栏目只有取得非空内容时才出现；不会输出空标题或“没有内容”的占位句。

**B. 手动 Smart Gen／导演台单人“生成”（内置模板）。**

```text
Context = <当前生成人格模板，{LANG} 已替换> + <单人 JSON 返回协议>
Prompt  =
[Character Data]
<按 A 构建的角色资料>
```

“导演备注”过滤项控制单人资料中是否出现备注。若选择 RimTalk 高级预设，则改为渲染其启用的 System 条目作为 Context、User/Assistant 条目作为 Prompt，并附加 JSON 协议；此分支使用 RimTalk 的 `PromptContext`／普通 Pawn 上下文，**不会自动附上 A 中的同一份 RPD 资料块**。因此模板应自行放入所需变量或资料。高级预设无法使用时回退内置模板。

**C. 导演台合并发送。**单独发送仍是多次 B；合并发送则是一次请求：

```text
Context = <当前生成人格模板> + <批量 JSON 返回协议>
Prompt  = [Character Data]
          --- Group Context ---
          <全局导演备注，如有>
          --- Character [ID:<Pawn ID>] Name: <姓名> ---
          <角色 1 的 A 资料块>
          --- Character [ID:<Pawn ID>] Name: <姓名> ---
          <角色 2 的 A 资料块>
          ...
```

组上下文会放入全局导演备注；若 A 的“导演备注”也打开，每名角色的资料块中可能再次出现该备注。返回结果按 `[ID:<Pawn ID>]` 和 `---` 分段；解析器优先按 ID、再尝试按姓名对应角色。合并发送不使用单人 RimTalk 高级预设。

**D. 自动生成（内置或高级预设）。**内置路径先按类别选定有效筛选器，渲染该 Pawn 的“自动生成备注”，再构建 A。新 Pawn 首次上图时没有触发附加块；因身份变化进入新类别时，在 A 尾部增加：

```text
[Trigger Event]
Role transition for <姓名> (with faction context):
- Before: role=<旧身份>; faction=<旧派系>; [host/origin/extra home faction=...]
- After: role=<新身份>; faction=<新派系>; [host/origin/extra home faction=...]
```

内置路径的 Prompt 以 `[Character Data]` 开头，随后是 A 和可选的 Trigger Event；Context 为该类别的内置模板加单人返回协议。**自动生成高级预设路径**只渲染已启用的 System/User/Assistant 条目；不会自动追加 A、自动生成备注或内置 JSON 返回协议。若需要身份变化事件，在模板中写 `{{ director_trigger_context }}`。预设不存在、渲染失败或没有有效条目时，该次请求会跳过，而不是回退内置路径。

**E. 手动演变。**编辑窗口中的“设置时间”保存年龄和 A 的状态快照。手动点击“演变”时，内置模板发送：

```text
Context = <按追加／覆盖模式选择的演变模板> + <单人 JSON 返回协议>
Prompt  = [Update Data]
          [Basic Info]
          Name: <姓名> / Gender: <性别> / Age: <年龄> / Status: <身份>
          [Previous Persona (The Starting Point)]
          <编辑框中的现有人格；为空时取已存人格>
          [Time Context]
          <距上次记录的天数及年龄变化；无基线则说明无记录>
          [Status Changes (since last update)]      <可选>
          [Director's Notes]                         <可选>
          [New Memories]                             <有记忆时>
          <上次记录后的新记忆；无时间记录时取近期记忆>
          [Common Knowledge]                         <有常识内容时>
```

状态差异需要“数据比较”和有效快照；普通导演备注需要“导演备注”过滤项。手动高级预设会改为渲染 RimTalk 条目并附返回协议，**不会自动发送上述 `[Update Data]`**；请在模板中引用现有人格、时间、差异和记忆等需要的变量。手动结果按共用模式追加 `[Development]` 或覆盖编辑框内容。

**F. 自动更新：定时、身份和事件。**内置请求同样以 `[Update Data]` 开始，但使用**已存 Persona**及独立的“自动更新备注”；`[New Memories]` 仅在有记忆内容时出现。定时更新通常无事件块；触发更新则在 `[Time Context]` 后加入 `[Trigger Events]`，随后可选状态差异、备注和常识：

```text
[Update Data]
[Basic Info] ...
[Previous Persona (The Starting Point)] ...
[Time Context] ...
[Trigger Events]                       <触发时才有>
[Status Changes (since last update)]   <开启数据比较且有快照时>
[Director's Notes]                     <自动更新备注不为空时>
[New Memories]                         <有记忆内容时>
[Common Knowledge]                     <启用且有内容时>
```

`[Trigger Events]` 的内容按事件改变：

| 触发 | 附加的事实 |
|---|---|
| 身份变化 | 旧／新身份和派系、接待派系、来源派系等；若角色仍是规则／随机池分配的初始人格，可能改走 D 的自动生成流程。 |
| 结婚、离婚或分手 | 另一方姓名；如果有另一方现有人格，还附关系参考，但只更新当前 Pawn。 |
| 生育 | 同一批出生的孩子数与性别、另一方的身份和派系；符合条件时说明遗传父母种族不同。 |
| 直系亲属死亡 | 死者姓名；触发对象限符合条件的配偶、父母或子女。 |
| 新增特质 | 新特质名称及可用描述。 |

自动更新若选 RimTalk 高级预设，只会渲染其 System/User/Assistant 条目；**不会自动附上完整的 `[Update Data]`、触发事件文本或内置 JSON 返回协议**。模板须自行引用现有人格、时间、差异、新记忆、自动更新备注和 `director_trigger_context`。预设不存在、渲染失败或没有有效条目时请求会跳过。结果按“追加／覆盖”模式应用。历史面板保存的**更新上下文摘要**由时间、可选触发事件、状态变化和新记忆组成，它是回看记录，并非另一次 AI 请求。

**G. Persona 在日常对话中的动态渲染。**RPD 可在 RimTalk 构建 Pawn 上下文时把 Persona 文本里的 Scriban 转为当前值，例如 `{{ pawn.d_status_diff }}`。该渲染只替换送入 RimTalk 上下文的 Persona 文本；不会改变 B–F 的请求结构，也不代表每次对话会重新生成人格。随机预设套用和规则分配同样没有生成请求。

#### H. Scriban 高级预设与格式精简

以下内容填在 **RimTalk 的高级提示词预设条目**里，不是填在 RPD 的五个内置提示词槽里。先在 RimTalk 创建或复制一个高级预设，保留两个已启用条目：System 写任务，User 写资料；保存后在 RPD 对应功能的高级预设下拉框选择它。预设中的 `{{ ... }}` 在发送请求前由 RimTalk 的 Scriban 渲染。RPD 人物变量写作 `pawn.d_*`，全局备注写作 `director_notes`；不要把这套写法与其他占位符混用。

System 条目可以从下面开始；**自动生成、自动更新的高级预设尤其需要自行说明返回格式**：

```text
Write one RimWorld persona from the supplied data. Keep facts consistent.
Return only a JSON object with "persona" as a string and "chattiness" as a number from 0.1 to 1.0.
Encode line breaks inside "persona" as \n. Do not use Markdown fences.
```

单次 Smart Gen 的 User 条目可直接粘贴：

```scriban
[Character Data]
{{ pawn.d_full_profile }}
{{ if director_notes != "" }}
[Director's Notes]
{{ director_notes }}
{{ end }}
```

这会拼出与内置单人请求**相近的 `[Character Data]` 结构**，但不是逐字相同：`d_full_profile` 使用 RPD **全局**资料筛选器，由另一组资料函数生成；内置单人请求使用自己的资料构建流程。它也不自动带上导演备注，所以示例单独引入 `director_notes`。若需要严格遵循内置的资料筛选和格式，选“使用内置”提示词。给自动生成类别使用此预设时，可在 User 末尾加入下面的身份变化事件段；高级预设不会自动附加事件或设置页的自动生成备注，类别独立筛选器也不会替代 `d_full_profile` 所用的全局筛选器。若需要备注，把说明直接写入预设正文：

```scriban
{{ if director_trigger_context != "" }}
[Trigger Event]
{{ director_trigger_context }}
{{ end }}
```

想控制每个栏目并去掉整合变量可能带来的重复栏目，就在 User 中用细项变量手工拼装；下面是一个精简版，按需要增删栏目：

```scriban
[Character Data]
--- Basic Info ---
Name: {{ pawn.d_basic_name }}
Gender: {{ pawn.d_basic_gender }}
Age: {{ pawn.d_basic_age }}
Current Status: {{ pawn.d_basic_status }}
Faction: {{ pawn.d_basic_faction_label }}
--- Race & Xenotype ---
Race: {{ pawn.d_race_label }}
Xenotype: {{ pawn.d_race_xenotype }}
--- Backstory ---
Childhood: {{ pawn.d_backstory_childhood_title }}
Adulthood: {{ pawn.d_backstory_adulthood_title }}
{{ pawn.d_relations }}
--- Traits ---
{{ pawn.d_traits_list }}
--- Skills ---
{{ pawn.d_skills_list }}
```

可按 A 的顺序补上 `d_genes_list`、`d_ideology_list`、`d_health_list`、`d_equipment`、`d_inventory`、`d_rimpsyche`、`d_memories`、`d_common_knowledge`；基因、特质、意识形态等还提供 `_with_desc` 版本。想避免整合资料中出现重复装备段时，只在细项模板中放一次 `{{ pawn.d_equipment }}`。细项变量是当前 Pawn 的值，并不自动遵守 RPD 的栏目开关；只放真正需要的项目才会缩短请求。当前 `d_skills_list` / `d_skills_list_with_desc` 和 `d_health_list` / `d_health_list_with_desc` 各自取相同的详细版本，切换这些名字不会进一步精简。

更新用的 User 条目改用更新数据。`d_evolve_memories` 在**手动或自动更新高级预设**渲染期间可取得记忆：有时间记录时只取此后的记忆；未记录时间或没有新增记忆时为空。`d_memories` 始终只取近期记忆，不需要时间记录；联动 Mod 变量无内容时不会自动生成标题：

```scriban
[Update Data]
[Previous Persona (The Starting Point)]
{{ pawn.d_evolve_current_persona }}
[Time Context]
{{ pawn.d_evolve_time_info }}
[Status Changes (since last update)]
{{ pawn.d_evolve_diff }}
{{ if pawn.d_evolve_memories != "" }}
[New Memories]
{{ pawn.d_evolve_memories }}
{{ end }}
{{ if pawn.d_common_knowledge != "" }}
[Common Knowledge]
{{ pawn.d_common_knowledge }}
{{ end }}
{{ if evolve_director_notes != "" }}
[Director's Notes]
{{ evolve_director_notes }}
{{ end }}
{{ if director_trigger_context != "" }}
[Trigger Events]
{{ director_trigger_context }}
{{ end }}
```

这份示例主要适合**自动更新**。手动更新也能使用 `pawn.d_evolve_memories`；若未设置时间且仍想提供近期记忆，可改用 `pawn.d_memories`。手动预设还应把 `evolve_director_notes` 两处改成 `director_notes`，并删除触发事件段。`director_trigger_context` 只在对应自动触发请求中有值。`d_evolve_diff` 取快照差异，模板引用它时不受内置“数据比较”开关控制。模板模拟内置更新数据的主要栏目，但可选数据、时间叙述和状态差异仍以各变量的实际输出为准；完全复用内置请求时选内置提示词。

**精简已有格式：**优先在 RPD 资料筛选中关闭无关栏目和描述；高级预设再对已取得的字符串做替换。普通替换适合已知固定标题，例如把装饰性标题缩短为标签：

```scriban
{{ pawn.d_full_profile | string.replace "--- Basic Info ---" "Identity:" }}
```

若要一次处理所有 `--- 标题 ---` 行并压缩过多空行，用 Scriban 的正则替换；反引号包住正则可免去反斜杠双重转义：

```scriban
{{ pawn.d_full_profile | regex.replace `^--- ([^\r\n]+) ---\r?$` "$1:" "m" | regex.replace `(?:\r?\n){3,}` "\n\n" }}
```

例如 `--- Basic Info ---` 会变成 `Basic Info:`，连续三个以上换行会压成两个。保留栏目名可避免种族、特质和技能混成一段。这里的替换只改变**该条目渲染出的请求文本**，不会改写游戏保存的人格或原始资料。函数语法可查 [Scriban 字符串函数](https://github.com/scriban/scriban/blob/master/site/docs/builtins/string.md)和[正则函数](https://github.com/scriban/scriban/blob/master/site/docs/builtins/regex.md)。

---

## English

### What this add-on does

Persona Director (RPD) is a persona add-on for [RimTalk](https://github.com/jlibrary/RimTalk). It extends RimTalk's persona editor, Smart Gen, and Random Gen with richer Pawn data for **persona generation**, plus a preset library, a director console, assignment rules, persona evolution, and optional Auto-Gen and Auto-Evolve. Persona requests use RimTalk's AI service, so configure that connection in RimTalk first.

| This project handles | RimTalk still handles; outside this guide |
|---|---|
| Persona-generation data, Director Notes, prompts, and creating or applying personas | AI provider, API key, and model connections |
| Preset library, random application, assignment rules, import/export, and batch generation | Base persona storage and the original persona editor |
| Manual evolution, optional Auto-Gen and Auto-Evolve, and update history | Everyday dialogue triggers, intervals, bubbles, and other conversation settings |
| Extra RimTalk variables and Scriban rendering inside persona text | RimTalk's own everyday-dialogue Context Filter |

Compared with RimTalk's native persona tools, RPD intercepts single-pawn Smart Gen, replaces Random Gen with a browsable preset choice, and can assign an initial persona to newly spawned pawns. Applying an existing random preset **does not call AI**. Smart Gen, Director Console Quick Gen, and Auto-Gen do use RimTalk's AI service. RPD's data filters mainly govern persona creation and evolution; they do not replace RimTalk's dialogue filters.

### 1. Install and create a first persona

1. Use RimWorld **1.6**. Enable [Harmony](https://steamcommunity.com/sharedfiles/filedetails/?id=2009463077) → [RimTalk](https://steamcommunity.com/sharedfiles/filedetails/?id=3551203752) → [Persona Director](https://steamcommunity.com/sharedfiles/filedetails/?id=3619548407). The repository stores C# source in `Source` and the compiled `RPD.dll` in `Assemblies`; see [BUILDING.md](BUILDING.md) to build from source. Keep the `About/`, `Assemblies/`, `Defs/`, `Languages/`, `Source/`, and `Textures/` folders, plus `RPD.csproj`, `RPD.sln`, `RPD.Local.props.example`, `.gitignore`, `BUILDING.md`, and this `README.md` in the GitHub repository.
2. Configure and verify an AI connection in RimTalk's settings, then load a save.
3. Open **Options → Mod Settings → RimTalk: Persona Director**. Keep the default data filters for now and select the first built-in prompt slot.
4. Open a Pawn's RimTalk persona editor and use **Smart Gen**. RPD requests a persona from AI using the selected data and prompt. Review the returned options and choose one.
5. To try the other entry point, use **Random Gen** in the persona editor. RPD opens a preset browser. Choose a category and preset, then **Apply Selected** or **Apply Random**. This applies existing text directly.

**Complete mod folder structure:** the mod folder installed in RimWorld should contain these runtime files:

```text
Rimtalk-Persona-Director/
├── About/       # About.xml, icon, preview, and PublishedFileId.txt
├── Assemblies/  # RPD.dll
├── Defs/        # Game definitions
├── Languages/   # Localization files
└── Textures/    # UI and other texture assets
```

`Source/`, the project files, build guide, and this `README.md` belong in the source repository, not the player's runtime mod folder. Players need the compiled `Assemblies/RPD.dll` to run the mod; source files do not replace it. Harmony and RimTalk are separate required dependencies.

### 2. Preset library and assignment rules

Choose **Open Preset Library** in RPD's settings. On an empty first run, RPD imports RimTalk's original presets and the add-on's built-in presets. The **Presets** tab supports search, categories, create/delete, and editing names, text, and chattiness. **Manage Vanilla / Manage Built-in** adds those collections to the user library. Enabled entries sync to RimTalk and join the global random pool used when no assignment rule matches.

**Save a fixed persona for later:** in **Presets**, create an entry, name it after the character, paste the complete persona into its text field, then turn off **Enabled** for that entry. The library is stored in mod settings and is available across saves. When the character appears in another save, open their persona editor → **Random Gen**, find the entry, and click **Apply Selected**. There is no need to keep an external copy, and applying the saved text makes no AI request. Disabled entries remain available for manual selection in the browser. To keep one manual-only, leave it out of assignment rules; the browser's **Apply Random** can still draw from visible disabled entries.

In **Rules**, create a rule for a faction, race, xenotype, or age range, set its priority, and choose its allowed presets. A newly spawned humanlike pawn without a persona receives a matching preset: the highest priority wins, tied rules combine their candidate pools, and a pawn with no matching rule draws from globally enabled entries. Rule assignment itself sends no AI request.

For backups or sharing, open **Import/Export** in the library. Left-click **Export** to copy XML; right-click it to save a timestamped XML file. Import can append or overwrite, including from a file. Export before overwriting: append does not deduplicate entries, while overwrite replaces the preset and rule lists.

### 3. Director Notes and the Director Console

Use the RPD icon at the bottom of the screen: **left-click** opens Director Notes, **right-click** opens the Director Console, and **Shift+left-click** opens Mod Settings. Notes are global and do not clear when switching Pawns. Use them for a shared history, secret, or story constraint missing from game data; review or clear them after finishing a group.

The console can filter current-map pawns by colonist, prisoner, slave, visitor, enemy, animal, and other categories. Search, refresh, and select pawns there. Each row offers **Quick Gen** (AI generation and application), **Edit** (RimTalk persona editor), and **Talk** (RimTalk conversation entry). With multiple pawns selected, **Single Send** makes one request per pawn; **Batch Send** puts them in one request, which is useful for linked backstories. Batch output must identify each pawn, and malformed sections may fail to apply.

**Practical idea:** write a refugee squad's shared escape in Director Notes, use Batch Send to establish connections, then generate individually to give each survivor a different reaction. A high-priority rule can reserve a set of styles for pirates while other new pawns use the global preset pool.

### 4. Data filters and prompts

RPD's **Select Data sent to AI** controls persona-generation material: identity, race/xenotype, genes, childhood and adulthood backstory, key relations, traits, ideology, skills and passions, health, equipment, inventory, and optional RimPsyche, memories, and common knowledge. Some fields have a separate description switch. Longer descriptions make longer requests. Start with identity, background, relations, traits, and skills; add detailed gene or health text only when useful. RimPsyche, memory, and common-knowledge controls appear when their corresponding expansion is installed. GitHub repositories: [RimPsyche](https://github.com/jagerguy36/Rimpsyche) and [RimTalk-ExpandMemory (Memory and Common Sense)](https://github.com/sanguodxj-byte/RimTalk-ExpandMemory).

There are five editable built-in prompt slots: **Standard** offers three interpretations; **Story-Driven** writes one narrative; **Data-Driven** links childhood and adult history more strictly; the last two are for evolution **Append** and **Overwrite**. Use the Slot menu to edit a title or prompt. **Reset Default** resets only the selected slot. `{LANG}` represents the active game language, and RPD appends its response-format rules automatically. Selecting an evolution-only slot for ordinary single generation falls back to Standard.

Separate **RimTalk advanced preset** selectors apply to single generation and evolution; Auto-Gen categories have their own selectors. Advanced presets follow different context paths, detailed in the [appendix](#appendix-how-persona-context-is-built). Start with an internal prompt when you need predictable RPD data filtering, then check what an advanced preset includes before switching.

**Writing an advanced preset:** create one in RimTalk's advanced prompt editor. Enable a System entry for the persona task and response requirements, and a User entry for character data. Put `{{ pawn.d_full_profile }}` in User to import RPD's assembled profile, then add `{{ director_notes }}` separately for global Director Notes. Select the preset in RPD's single-generation setting or an Auto-Gen category. Manual and Auto-Evolve can import the current persona, time, and status difference; `pawn.d_evolve_memories` provides memories since the recorded time in either advanced Evolve workflow. The [Scriban recipes in Appendix H](#h-scriban-advanced-presets-and-format-cleanup) include paste-ready templates, variable mappings, and cleanup examples.

RPD also registers variables such as `pawn.d_full_profile`, `pawn.d_memories`, `pawn.d_status_diff`, `director_notes`, and `smart_history` with RimTalk. Persona text can use Scriban conditions to change expression by situation. This renders the saved persona dynamically; it does not regenerate a new persona for every conversation. For example:

```scriban
{{ if pawn.IsPrisoner }}Speak cautiously; distinguish compliance from trust.{{ end }}
{{ if pawn.IsColonist }}Let work and family shape the voice.{{ end }}
```

### 5. Manual evolution and history

Open a Pawn's persona editor and optionally click **Set Time** to record its current age and state. After the Pawn experiences more of the game, click **Evolve**. RPD gives AI the earlier persona, elapsed time, new memories, and status differences when enabled. Manual Evolve shares Auto-Evolve's mode: **Append** adds `[Development]` to the editor text, while **Overwrite** replaces the persona. The internal prompt slot follows the selected mode. Enable RPD's **Data Comparison** and keep relevant fields in the snapshot to obtain useful differences; that switch is off by default.

With optional features enabled, click **History** for a pawn in the Auto-Evolve list to inspect the update context and earlier/later personas. Confirm **Restore This Version** to revert. The default limit is five records per pawn. For a long-running colony, record a baseline before a significant event and evolve afterward, so the model has evidence of what changed.

### 6. Auto-Gen and Auto-Evolve (optional features)

Check **Enable Optional Features** in RPD settings and confirm. Then configure the two workflows separately:

1. **Auto-Gen:** enable it and open **Configure**. Colonists, prisoners, slaves, visitors, enemies, and others each have an off-by-default category switch. Each category can use an internal prompt or a RimTalk advanced preset. The internal path shares the global RPD data filter by default; disable **Sync** to choose its own fields. Its Auto-Gen Notes support Scriban and are sent only when Director Notes is enabled in that category's effective context. The advanced path does not append these notes, and no Scriban variable currently exposes them; put the needed instruction directly in the preset. A new pawn is queued when it first appears on a map with an initial persona baseline; loading a save does not trigger it again.
2. **Auto-Evolve:** enable the master switch, enable individual pawns in the list, and set intervals. `0` or a negative number disables timed updates but still permits role and event triggers. The list supports batch enable/disable and batch interval changes. Optional triggers include role changes, marriage, breakup, birth, direct-family death, and a new trait. Auto-Evolve does not depend on Auto-Gen category switches; the role-change checkboxes are controlled on the Auto-Evolve page and still require the pawn's individual switch. If the pawn still has an initial rule/random-library persona and both the Auto-Gen master and destination-category switches are on, a role change first attempts Auto-Gen; otherwise it attempts Auto-Evolve.
3. **Update mode:** Manual and Auto-Evolve share this mode. **Append** adds a development section and is the default; **Overwrite** rewrites the persona. Choose silent or top-left notifications and speed protection. By default, new scans and requests pause at speeds of 3× or higher.

Auto-Gen and Auto-Evolve share a request coordinator. Turning optional features off invalidates pending work. If a timeout or runtime error trips the circuit breaker, read the reason shown in settings, resolve it, click **Clear Error**, and enable optional features again. Auto-Evolve requires a living, spawned pawn with an existing persona, with both its master and individual switches enabled.

**Recommendation:** normally enable Auto-Gen categories and Auto-Evolve role triggers only for colonists, slaves, and prisoners. Visitor, enemy, and other categories remain optional, but a group of raiders or traders can cause many Auto-Gen requests and API usage at once. A former colonist who becomes neutral or hostile also usually has little reason to keep evolving for the player's colony. Enable those cases per pawn when they matter to your story.

### 7. Troubleshooting

| Symptom | Check |
|---|---|
| Persona generation fails | Verify RimTalk's AI connection first, then inspect the selected prompt and result. |
| Auto-Gen does not run for a new pawn | Check Enable Optional Features → Enable Auto-Gen → category switch, and whether this is its first map appearance. |
| Auto-Evolve does not run | Check the master switch, pawn switch, interval or trigger, game speed, and presence of an existing persona. |
| Expected data is missing | Check RPD's persona data filters. Advanced presets must also reference the desired data. Temporarily enable debug logging to inspect a request. |
| An update is unsuitable | Check the Set Time snapshot, Data Comparison, and Auto-Evolve mode; use History to restore if needed. |

### Appendix: how persona context is built

Bracketed headings and separators below represent the **actual request format**; angle-bracketed values are illustrative placeholders. Optional sections appear only when data exists and the relevant setting allows them. **Context** is the instruction part of a request; **Prompt** is its data/user part. RPD's filters govern persona workflows; RimTalk manages its own everyday-dialogue context.

**A. Character data block.** Manual single generation, the Director Console, and Auto-Gen use this type of data. Enabled sections appear in this order. A snapshot keeps state fields but omits notes, memories, and common knowledge.

```text
--- Basic Info ---
Name: <name>
Gender: <gender>
Age: <biological age>
Current Status: <colonist / prisoner / visitor / etc.>
Faction: <faction and relation to player>

--- Race & Xenotype ---
Race: <race>                         [optional description]
Xenotype: <xenotype>                 [optional description]
--- Genes ---
[Endogenes (Natural)]: <natural genes>
[Xenogenes (Artificial)]: <implanted genes>
--- Backstory ---
Childhood: <title and optional description>
Adulthood: <title and optional description>
--- Key Relationships ---
- <relation>: <other pawn> <status>
--- Traits ---
<traits and optional descriptions>
--- Ideology ---
<ideology and optional descriptions>
--- Skills ---
<skill>: <level / [INCAPABLE]> [passion and optional description]
--- Health ---
- <body part>: <condition> [optional description]
--- Equipment ---
- [Weapon]: <weapon>
- [Apparel]: <clothing>
--- Inventory ---
- <item>
--- RimPsyche ---
<available psyche data>
--- Director's Notes (Custom Context) ---
<global or Auto-Gen notes>
--- Memories ---
<recent memories>
--- Common Knowledge ---
<relevant knowledge>
```

Basic identity can include an enslaved pawn's origin faction or an external faction description. Relations are limited to key relationships. Genes distinguish endogenes from xenogenes; skills may be marked incapable. Snapshot mode omits notes, memories, and common knowledge. Linked-mod sections such as RimPsyche, memories, and common knowledge appear only when they contain data; empty headings and "no content" placeholders are omitted.

**B. Manual Smart Gen / Director Console Quick Gen (internal prompt).**

```text
Context = <selected generation prompt with {LANG} resolved> + <single-persona JSON response rules>
Prompt  =
[Character Data]
<character data from A>
```

The Director Notes filter controls whether notes appear in the single-pawn data. With a **RimTalk advanced preset**, RPD instead renders enabled System entries into Context and User/Assistant entries into Prompt, then appends response rules. This branch uses RimTalk's `PromptContext` and normal Pawn context; it **does not automatically include the same RPD data block A**. Include needed data or variables in the preset itself. An unusable advanced preset falls back to the internal prompt.

**C. Director Console Batch Send.** Single Send means multiple B requests. Batch Send makes one request:

```text
Context = <selected generation prompt> + <batch JSON response rules>
Prompt  = [Character Data]
          --- Group Context ---
          <global Director Notes, if present>
          --- Character [ID:<Pawn ID>] Name: <name> ---
          <pawn 1's A block>
          --- Character [ID:<Pawn ID>] Name: <name> ---
          <pawn 2's A block>
          ...
```

Global Director Notes go into group context. If A's Director Notes switch is also enabled, they may appear again inside each pawn block. The result is segmented by `[ID:<Pawn ID>]` and `---`; RPD first matches ID, then tries name matching. Batch Send does not use the single-pawn RimTalk advanced preset.

**D. Auto-Gen (internal or advanced preset).** The internal path selects the category's effective filter, renders Auto-Gen Notes for this pawn, then builds A. First map appearance has no trigger addendum. When the pawn changes to an enabled new role category, the following is appended to A:

```text
[Trigger Event]
Role transition for <name> (with faction context):
- Before: role=<old role>; faction=<old faction>; [host/origin/extra home faction=...]
- After: role=<new role>; faction=<new faction>; [host/origin/extra home faction=...]
```

The internal Prompt begins with `[Character Data]`, followed by A and an optional Trigger Event; Context is the category's built-in prompt plus single-persona response rules. The **Auto-Gen advanced-preset branch** only renders enabled System/User/Assistant entries; it does not append A, Auto-Gen Notes, or the internal JSON response rules. Include `{{ director_trigger_context }}` in the template to receive a role-change event. A missing preset, failed rendering, or no usable entries causes that request to be skipped rather than falling back to the internal branch.

**E. Manual Evolve.** **Set Time** stores age and an A-style state snapshot. Clicking Evolve sends this with the internal prompt:

```text
Context = <Append or Overwrite evolution prompt, according to mode> + <single-persona JSON response rules>
Prompt  = [Update Data]
          [Basic Info]
          Name: <name> / Gender: <gender> / Age: <age> / Status: <status>
          [Previous Persona (The Starting Point)]
          <current editor text; stored persona if the editor is empty>
          [Time Context]
          <days and age change since baseline, or no previous record>
          [Status Changes (since last update)]      <optional>
          [Director's Notes]                         <optional>
          [New Memories]                             <when memories exist>
          <new memories since baseline; recent memories without a baseline>
          [Common Knowledge]                         <when knowledge exists>
```

Status differences need Data Comparison and a valid snapshot; ordinary Director Notes need that filter enabled. A manual advanced preset renders RimTalk entries and response rules instead; **the `[Update Data]` block above is not appended automatically**. Reference the current persona, time, differences, memories, and other needed variables in the template. Manual results append `[Development]` or overwrite the editor according to the shared mode.

**F. Auto-Evolve: timed, role, and event triggers.** Its internal request also begins with `[Update Data]`, but uses the **stored persona** and separate **Auto-Evolve Notes**. `[New Memories]` appears only when memories are available. Timed updates usually have no event block. Triggered updates add `[Trigger Events]` after `[Time Context]`; optional differences, notes, and common knowledge follow:

```text
[Update Data]
[Basic Info] ...
[Previous Persona (The Starting Point)] ...
[Time Context] ...
[Trigger Events]                       <only when triggered>
[Status Changes (since last update)]   <Data Comparison and snapshot required>
[Director's Notes]                     <when Auto-Evolve Notes are nonempty>
[New Memories]                         <when memories exist>
[Common Knowledge]                     <when enabled and available>
```

The contents of `[Trigger Events]` depend on the event:

| Trigger | Added facts |
|---|---|
| Role change | Old/new role and faction, host faction, origin faction, and similar details. If the pawn still has an initial persona assigned from the rule/random pool, the workflow may switch to Auto-Gen D. |
| Marriage or breakup | The other pawn's name and, if present, their current persona as relationship context; only the subject pawn is updated. |
| Birth | Number and sexes of newborns, other parent's status and faction, and a cross-race note when applicable. |
| Direct-family death | Deceased pawn's name; eligible relationships are spouse, parent, and child. |
| New trait | Trait name and available description. |

With a RimTalk advanced preset, Auto-Evolve only renders its System/User/Assistant entries; **it does not append the full `[Update Data]` block, trigger text, or internal JSON response rules**. The template must reference the current persona, time, changes, new memories, Auto-Evolve Notes, and `director_trigger_context` itself. A missing preset, failed rendering, or no usable entries skips the request. Results are applied in Append or Overwrite mode. History stores a **summary** of time, optional trigger events, status changes, and new memories for review; that summary is not another AI request.

**G. Dynamic persona rendering during regular dialogue.** When RimTalk builds Pawn context, RPD can render Scriban in the saved persona, such as `{{ pawn.d_status_diff }}`, into current values. This replaces persona text inside RimTalk's context. It does not change request paths B–F and does not regenerate the persona for every conversation. Applying a random preset or assigning one by rule also sends no generation request.

#### H. Scriban advanced presets and format cleanup

Enter these examples in **RimTalk advanced prompt preset entries**, not RPD's five built-in prompt slots. Create or duplicate a preset in RimTalk and enable two entries: System for the task and User for the data. Save it, then select it from the relevant advanced-preset dropdown in RPD. RimTalk renders `{{ ... }}` before sending the request. RPD pawn variables use `pawn.d_*`; global notes use `director_notes`.

Start with this System entry. **Auto-Gen and Auto-Evolve advanced presets especially need their own response-format instruction**:

```text
Write one RimWorld persona from the supplied data. Keep facts consistent.
Return only a JSON object with "persona" as a string and "chattiness" as a number from 0.1 to 1.0.
Encode line breaks inside "persona" as \n. Do not use Markdown fences.
```

Paste this User entry for single-pawn Smart Gen:

```scriban
[Character Data]
{{ pawn.d_full_profile }}
{{ if director_notes != "" }}
[Director's Notes]
{{ director_notes }}
{{ end }}
```

This produces a `[Character Data]` layout **similar to the internal single-pawn request**, not byte-for-byte identical. `d_full_profile` uses RPD's **global** data filter and a separate data builder; the internal single-pawn path has its own builder. Director Notes must be imported separately. Select the internal prompt if exact internal filtering and formatting matter. For an Auto-Gen category, append this role-change event block to User when needed. The advanced path does not append the event or the Auto-Gen Notes from settings, and a category-specific filter does not change the global filter used by `d_full_profile`. Put any needed Auto-Gen instruction directly in the preset:

```scriban
{{ if director_trigger_context != "" }}
[Trigger Event]
{{ director_trigger_context }}
{{ end }}
```

For control over each section, assemble individual variables instead of importing the whole profile. This shorter User entry follows the internal section order and avoids unwanted or repeated sections; add or remove sections as needed:

```scriban
[Character Data]
--- Basic Info ---
Name: {{ pawn.d_basic_name }}
Gender: {{ pawn.d_basic_gender }}
Age: {{ pawn.d_basic_age }}
Current Status: {{ pawn.d_basic_status }}
Faction: {{ pawn.d_basic_faction_label }}
--- Race & Xenotype ---
Race: {{ pawn.d_race_label }}
Xenotype: {{ pawn.d_race_xenotype }}
--- Backstory ---
Childhood: {{ pawn.d_backstory_childhood_title }}
Adulthood: {{ pawn.d_backstory_adulthood_title }}
{{ pawn.d_relations }}
--- Traits ---
{{ pawn.d_traits_list }}
--- Skills ---
{{ pawn.d_skills_list }}
```

Following the order in A, you can add `d_genes_list`, `d_ideology_list`, `d_health_list`, `d_equipment`, `d_inventory`, `d_rimpsyche`, `d_memories`, and `d_common_knowledge`. Genes, traits, ideology, and some other variables offer `_with_desc` variants. To avoid repeated equipment sections in an assembled profile, include `{{ pawn.d_equipment }}` only once in the individual-variable template. Individual variables return the current Pawn's data; RPD's section switches do not automatically gate them. Include only useful fields to shorten a request. Currently `d_skills_list` / `d_skills_list_with_desc` and `d_health_list` / `d_health_list_with_desc` each return the same detailed variant, so renaming those variables will not reduce detail.

Use update data in a manual or Auto-Evolve User entry. `d_evolve_memories` retrieves memories since the recorded time while rendering either advanced Evolve request. It is empty without a time record or new memories. `d_memories` retrieves recent memories without needing a time record; linked-mod variables do not generate headings when empty:

```scriban
[Update Data]
[Previous Persona (The Starting Point)]
{{ pawn.d_evolve_current_persona }}
[Time Context]
{{ pawn.d_evolve_time_info }}
[Status Changes (since last update)]
{{ pawn.d_evolve_diff }}
{{ if pawn.d_evolve_memories != "" }}
[New Memories]
{{ pawn.d_evolve_memories }}
{{ end }}
{{ if pawn.d_common_knowledge != "" }}
[Common Knowledge]
{{ pawn.d_common_knowledge }}
{{ end }}
{{ if evolve_director_notes != "" }}
[Director's Notes]
{{ evolve_director_notes }}
{{ end }}
{{ if director_trigger_context != "" }}
[Trigger Events]
{{ director_trigger_context }}
{{ end }}
```

This example is primarily for **Auto-Evolve**. Manual Evolve can also use `pawn.d_evolve_memories`; use `pawn.d_memories` instead if no time was recorded and you still want recent memories. For a manual preset, replace both instances of `evolve_director_notes` with `director_notes` and remove the trigger block. `director_trigger_context` is populated only during a matching Auto-Gen or Auto-Evolve trigger. `d_evolve_diff` reads the stored snapshot when referenced and is not gated by the internal Data Comparison switch. The template mirrors the main internal update sections, while optional data, time phrasing, and status changes follow the actual variable outputs. Select the internal prompt to use the exact internal request.

**Clean up existing formatting:** first disable irrelevant sections and descriptions in RPD's data filter. In an advanced preset, apply replacements to the remaining text. A literal replacement is useful for a fixed decorative heading:

```scriban
{{ pawn.d_full_profile | string.replace "--- Basic Info ---" "Identity:" }}
```

Use Scriban's regular-expression replacement to shorten all `--- heading ---` lines and collapse excess blank lines. Backticks around the pattern avoid doubled backslashes:

```scriban
{{ pawn.d_full_profile | regex.replace `^--- ([^\r\n]+) ---\r?$` "$1:" "m" | regex.replace `(?:\r?\n){3,}` "\n\n" }}
```

For example, `--- Basic Info ---` becomes `Basic Info:`, and three or more consecutive line breaks become two. Keeping section labels prevents race, traits, and skills from blending together. These transformations affect **the rendered request text in that entry**; they do not rewrite the saved persona or underlying game data. See the [Scriban string functions](https://github.com/scriban/scriban/blob/master/site/docs/builtins/string.md) and [regex functions](https://github.com/scriban/scriban/blob/master/site/docs/builtins/regex.md) for syntax.
