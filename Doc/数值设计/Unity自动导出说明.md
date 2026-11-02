# Excel 数值自动导出

## 数据流

```text
Doc/数值设计/balance.xlsx
        │  （Unity 编译 / 刷新 / 进入 Play 前自动检测）
        ▼
Assets/Resources/balance.csv
        │
        ▼
GameBalance.Get(...) / GetInt(...)
```

同时会同步 `Doc/数值设计/balance.csv` 这份设计镜像。

## 怎么用

1. 用 Excel 打开 `Doc/数值设计/balance.xlsx`。
2. 只改各数据表的「数值」列（D 列）。
   - 有效数据表：`1.基础`、`2.角色`、`3.技能`、`4.敌人`、`5.刷怪与难度`
   - `0.说明`、`总览`、`角色宽表`、`敌人宽表` 不参与导出
3. 保存 xlsx。
4. Unity 会在以下时机自动检测并导出：
   - 编译完成 / 域重载后
   - 进入 Play Mode 前
   - 编辑器空闲时每 3 秒检查一次
   - 也可手动执行 `Tools/Balance/Export Excel to CSV`
5. 导出后会自动 `AssetDatabase.ImportAsset`，并调用 `GameBalance.Reload()`，下一次 Play 使用新值。
6. `Assets/Resources/balance.csv` 与 `Doc/数值设计/balance.csv` 会同时更新。

## 安全规则

- 导出文件只读取含「键名 / 数值 / 分类 / 说明」表头的数据表，宽表与公式页不会被当成数据。
- Excel 是唯一数值源：内容哈希变化就导出；如果没有改动则跳过。
- 手动修改 CSV 不会反向写回 Excel，下一次 Excel 变化后会被覆盖。
- 需要立即强制导出时，使用 `Tools/Balance/Export Excel to CSV`。
- 写入采用系统临时文件 + 覆盖复制，导出失败不会破坏旧 `balance.csv`。
- 描述里含英文逗号时会自动加 CSV 引号，保证每行仍是 4 列。
- Play Mode 中修改 Excel 不会立即导出；退出 Play 后会自动检查，避免运行时刷新资源。

## 菜单

- `Tools/Balance/Export Excel to CSV`：强制立即导出。
- `Tools/Balance/Reveal Balance Workbook`：在 Finder 中定位 xlsx。

## 说明

- 当前工作簿已与运行时 `balance.csv` 同步，包含针头子弹、心形护符、血液回收钳等新增键。
- 工作簿里旧的废弃键已清理，当前与运行时 CSV 均为 330 个键。
- 旧版 `export_balance.py` / `一键导出.command` 流程已被 Unity 自动导出取代。

## 技能宽表：技能数据.xlsx

除了 `balance.xlsx`，技能宽表也会单独生成：

```text
Doc/数值设计/技能数据.xlsx  或  ~/Desktop/技能数据.xlsx
        ▼
Assets/Resources/skill_balance.csv
        ▼
GameBalance 在 balance.csv 之后加载，技能键覆盖同名键
```

- 查找顺序：项目内 `Doc/数值设计/技能数据.xlsx` 和桌面 `技能数据.xlsx` 都存在时，使用修改时间较新的那份；只有桌面文件时读桌面，只有项目内副本时读副本。
- 只映射当前已实现的技能编号：101、107、109、203、1001–1008、1010。
- 只映射代码实际读取的字段；例如冲洗针的持续/冷却、缝线的持续/冷却/弹速、消融弹的时长/连续打击间隔目前不参与技能逻辑，因此不会生成对应键。
- 已映射字段包括：伤害、数量、半径、角度、冷却/间隔、减速倍率、磁吸倍率、生命倍率、吸血概率/比例、护盾/再生、针头子弹的导弹/光束特殊值等。
- 菜单：`Tools/Balance/Export Skill Excel to CSV`、`Tools/Balance/Reveal Skill Workbook`。
- 生成的 `skill_balance.csv` 也会同步到 `Doc/数值设计/skill_balance.csv`。

### 优先级

- `技能数据.xlsx` 是**技能键的唯一源**。
- `balance.xlsx` 仍然负责玩家/敌人/刷怪等通用数值。
- 两者出现同名技能键时，`skill_balance.csv` 在 `balance.csv` 之后加载，因此以 `技能数据.xlsx` 为准。
- 如果只想改通用数值，不要改 `技能数据.xlsx` 里的同名技能行，否则会被它覆盖。

### 没有桌面 Excel 的机器

- 项目内已经放入一份 `Doc/数值设计/技能数据.xlsx`，并在 `Assets/Resources/skill_balance.csv` 生成好技能数值。
- 运行时只依赖 `skill_balance.csv`，不依赖桌面 Excel，也不依赖 `SkillExcelSync`（它只在 Editor 中工作）。
- 因此拷贝工程或出包时，不需要把桌面 `技能数据.xlsx` 一起带过去；如果要在那台机器上继续改表，直接改项目内副本即可。
- 建议把 `Assets/Resources/skill_balance.csv`、`Doc/数值设计/技能数据.xlsx` 和 `Doc/数值设计/skill_balance.csv` 一起纳入版本控制。

如果以后给更多技能编号做实现，只需要在 `Assets/Editor/SkillExcelSync.cs` 的 `IsImplemented` 和 `Map` 中加对应映射。
