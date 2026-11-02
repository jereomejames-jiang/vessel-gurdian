using System.Collections.Generic;
using UnityEngine;

public enum VesselSkillCategory { Active, Passive, Evolution }

// Stable data for every vessel skill that this build understands. Skill IDs come
// from the design workbook and are intentionally independent from the serialized
// UpgradeType values, so appending a new enum member never changes existing IDs.
public static class VesselSkillCatalog
{
    public const int StandardMaxLevel = 5;
    public const int EvolutionMaxLevel = 1;

    // Skills that can appear in the regular level-up pool.
    public static readonly UpgradeType[] Types =
    {
        UpgradeType.MembraneStent, UpgradeType.FlushNeedle,
        UpgradeType.DissectionScissors, UpgradeType.SidewallForceps,
        UpgradeType.Suture, UpgradeType.MicroscopicHemostat,
        UpgradeType.SpringCoil, UpgradeType.ThrombusCatheter,
        UpgradeType.SuctionTube, UpgradeType.AblationBomb,
        UpgradeType.NeedleBullet, UpgradeType.HeartAmulet
    };

    // Evolution skills are offered only through their recipe, never through the
    // regular pool.
    public static readonly UpgradeType[] EvolutionTypes =
    {
        UpgradeType.BloodRecycleForceps
    };

    public static readonly UpgradeType[] AllTypes =
    {
        UpgradeType.MembraneStent, UpgradeType.FlushNeedle,
        UpgradeType.DissectionScissors, UpgradeType.SidewallForceps,
        UpgradeType.Suture, UpgradeType.MicroscopicHemostat,
        UpgradeType.SpringCoil, UpgradeType.ThrombusCatheter,
        UpgradeType.SuctionTube, UpgradeType.AblationBomb,
        UpgradeType.NeedleBullet, UpgradeType.HeartAmulet,
        UpgradeType.BloodRecycleForceps
    };

    static readonly int[] SkillIds =
    {
        101, 1001, 1002, 1003, 1004, 1005, 1006, 1007, 107, 1008,
        1010, 109, 203
    };

    static readonly VesselSkillCategory[] Categories =
    {
        VesselSkillCategory.Passive, VesselSkillCategory.Active,
        VesselSkillCategory.Active, VesselSkillCategory.Active,
        VesselSkillCategory.Active, VesselSkillCategory.Active,
        VesselSkillCategory.Active, VesselSkillCategory.Active,
        VesselSkillCategory.Passive, VesselSkillCategory.Active,
        VesselSkillCategory.Active, VesselSkillCategory.Passive,
        VesselSkillCategory.Evolution
    };

    static readonly string[] Names =
    {
        "覆膜支架", "冲洗针", "解剖剪", "侧壁钳", "缝线", "显微止血夹",
        "弹簧圈", "血栓抽吸导管", "吸液管", "消融弹",
        "针头子弹", "心形护符", "血液回收钳"
    };

    static readonly string[] Glyphs =
    {
        "盾", "针", "剪", "钳", "缝", "夹", "圈", "管", "吸", "弹",
        "针", "心", "钳"
    };

    static readonly string[] Descriptions =
    {
        "在周身形成护盾，降低自身受到的伤害。\nLv1：减伤 15%\nLv2：减伤 25%\nLv3：减伤 35%，获得 60 点护盾；5 秒不受击后每秒回复最大生命 3.5%\nLv4：减伤 45%\nLv5：减伤 55%",
        "在周身形成环绕自身运动的针体，撞击敌人造成伤害和轻微击退。\nLv1：2 枚，伤害 5\nLv2：3 枚，伤害 10\nLv3：4 枚，伤害 10\nLv4：5 枚，伤害 15\nLv5：6 枚，伤害 20",
        "周期性释放剪刃，对路径上敌人造成接触伤害。\nLv1：冷却 2.8 秒，半径 3.3\nLv2：冷却 2.45 秒，半径 4.1\nLv3：冷却 2.1 秒，追加十字第二剪\nLv4：冷却 1.75 秒\nLv5：冷却 1.4 秒",
        "攻击附带吸血，角色周围出现红色血环；每次成功吸血会扩散一圈红光。\nLv1：15% 概率回复 5% 最大生命\nLv2：20% 概率回复 10%\nLv3：30% 概率回复 15%\nLv4：40% 概率回复 20%\nLv5：50% 概率回复 30%",
        "怪物上方伸出形如闪电的红线，每次随机攻击视野内敌人。\nLv1：攻击 1 个敌人，伤害 20\nLv2：攻击 2 个，伤害 20\nLv3：攻击 2 个，伤害 25，特效增强\nLv4：攻击 3 个，伤害 25\nLv5：攻击 3 个，伤害 25",
        "小范围攻击，击杀敌人时留下区域，1 秒内该位置附近敌人受到持续小伤害。\nLv1：区域半径 1.5\nLv2：区域半径 1.75\nLv3：区域半径 2.0\nLv4：区域半径 2.25\nLv5：区域半径 2.5",
        "对攻击的敌人造成持续 1.2 秒减速，敌人受击后显示掉落碎冰块特效。\nLv1：减速 10%\nLv2：减速 20%\nLv3：减速 30%\nLv4：减速 40%\nLv5：减速 50%",
        "角色周边出现跟随物发射低伤害激光，参考浮游炮风格。\nLv1：1 个跟随物\nLv2：2 个跟随物\nLv3：3 个跟随物\nLv4：4 个跟随物\nLv5：5 个跟随物",
        "周身周期性形成龙卷吸入特效，增加拾取范围半径。\nLv1：+10%\nLv2：+20%\nLv3：+30%\nLv4：+40%\nLv5：+50%",
        "投掷炸弹，延迟后对范围内敌人造成伤害。\nLv1：投掷 2 枚，伤害 15\nLv2：投掷 3 枚，伤害 15\nLv3：投掷 3 枚，伤害 15\nLv4：投掷 3 枚，伤害 20\nLv5：投掷 3 枚，伤害 20",
        "自动索敌发射针头子弹。\nLv1：1 发，间隔 1 秒，伤害 10\nLv2：2 发，±5° 散射，间隔 0.5 秒\nLv3：每发穿透 2 个敌人，伤害 15\nLv4：追加导弹（半径 1.7，伤害 D×4+2）\nLv5：追加药流光束（长 7–16，宽 0.85）",
        "提高最大生命值，倍率以未升级时的基础最大生命计算。\nLv1：最大生命 ×1.2\nLv2：最大生命 ×1.4\nLv3：最大生命 ×1.6\nLv4：最大生命 ×1.8\nLv5：最大生命 ×2.0",
        "侧壁钳的进化形态，以血液回收钳替换侧壁钳的吸血效果；血环变为深红色并明显增强。\n50% 概率回复最大生命 8%，溢出治疗转化为临时护盾（上限为最大生命 50%，且不低于 60）。\n进化条件：侧壁钳 Lv5 + 心形护符 Lv1"
    };

    static readonly string[] ShortDescriptions =
    {
        "形成护盾，降低受到的伤害",
        "环绕针体，撞击敌人并击退",
        "释放剪刃，切开路径敌人",
        "攻击附带吸血效果",
        "随机牵引视野内敌人",
        "击杀后留下持续伤害区域",
        "命中后减速敌人",
        "跟随导管发射激光",
        "增加拾取范围半径",
        "投掷延迟爆炸的消融弹",
        "自动索敌的针头子弹",
        "按基础生命倍率提高最大生命",
        "进化吸血，溢出治疗转护盾"
    };

    static readonly string[] Education =
    {
        "覆膜支架用于支撑狭窄血管。现实中的支架需要由医生评估并配合抗血小板治疗。",
        "冲洗针对应导管介入中的冲洗与清除。日常应避免自行使用针具处理血管问题。",
        "解剖剪提醒我们：急救或手术必须由受训人员完成，切勿自行尝试。",
        "血管壁健康与血脂、血压密切相关。控制烟草暴露和饮食中的饱和脂肪有助于保护内皮。",
        "缝线代表修复血管损伤。规律运动、控制血压和血糖，能减少血管长期受损风险。",
        "止血夹用于控制出血。突发大量出血时先呼叫急救并持续直接压迫止血。",
        "弹簧圈可用于介入栓塞治疗。现实治疗需要影像引导和专业团队评估。",
        "导管抽吸用于清除血栓。若出现单侧无力、口角歪斜或言语含糊，应立即呼叫急救。",
        "吸液管对应介入手术中的吸引与清除操作。术后需遵医嘱保持穿刺部位清洁干燥。",
        "消融弹对应射频消融术（RFA），通过热能闭合病变血管，是治疗静脉曲张的常用微创方法。术后需穿戴弹力袜并适度活动。",
        "针头子弹对应注射与输液治疗。任何注射都应由受训医护人员在无菌条件下完成。",
        "心脏健康与血压、血脂、血糖和生活方式密切相关。规律体检有助于早期发现心血管风险。",
        "血液回收技术可在手术中收集并回输患者自身血液，减少异体输血需求，需由专业团队操作。"
    };

    static readonly Color[] Colors =
    {
        new Color(0.24f, 0.68f, 0.92f), new Color(0.35f, 0.78f, 0.86f),
        new Color(0.72f, 0.42f, 0.95f), new Color(0.88f, 0.24f, 0.30f),
        new Color(0.92f, 0.38f, 0.50f), new Color(0.96f, 0.62f, 0.23f),
        new Color(0.46f, 0.76f, 0.36f), new Color(0.27f, 0.82f, 0.70f),
        new Color(0.55f, 0.85f, 0.95f), new Color(1.00f, 0.55f, 0.20f),
        new Color(1.00f, 0.82f, 0.28f), new Color(0.95f, 0.32f, 0.56f),
        new Color(0.78f, 0.10f, 0.16f)
    };

    static VesselSkillCatalog()
    {
        // Keep the three views in sync: a duplicate or missing entry would make a
        // stable ID point at the wrong upgrade at runtime.
        HashSet<UpgradeType> seenTypes = new HashSet<UpgradeType>();
        HashSet<int> seenIds = new HashSet<int>();
        for (int i = 0; i < AllTypes.Length; i++)
        {
            if (!seenTypes.Add(AllTypes[i]) || !seenIds.Add(SkillIds[i]))
                Debug.LogError("VesselSkillCatalog: duplicate skill type or skillId at index " + i);
        }
    }

    // Explicit membership instead of an enum range check: new evolution/sample
    // members are appended outside the original 10..19 block.
    public static bool IsVesselSkill(UpgradeType type)
    {
        switch (type)
        {
            case UpgradeType.MembraneStent:
            case UpgradeType.FlushNeedle:
            case UpgradeType.DissectionScissors:
            case UpgradeType.SidewallForceps:
            case UpgradeType.Suture:
            case UpgradeType.MicroscopicHemostat:
            case UpgradeType.SpringCoil:
            case UpgradeType.ThrombusCatheter:
            case UpgradeType.SuctionTube:
            case UpgradeType.AblationBomb:
            case UpgradeType.NeedleBullet:
            case UpgradeType.HeartAmulet:
            case UpgradeType.BloodRecycleForceps:
                return true;
        }
        return false;
    }

    public static bool IsEvolution(UpgradeType type)
    {
        return type == UpgradeType.BloodRecycleForceps;
    }

    public static VesselSkillCategory CategoryFor(UpgradeType type)
    {
        int index = IndexOf(type);
        return index < 0 ? VesselSkillCategory.Active : Categories[index];
    }

    public static int MaxLevelFor(UpgradeType type)
    {
        if (IsEvolution(type)) return EvolutionMaxLevel;
        return StandardMaxLevel;
    }

    public static int SkillIdFor(UpgradeType type)
    {
        int index = IndexOf(type);
        return index < 0 ? 0 : SkillIds[index];
    }

    public static UpgradeOption[] CreateOptions()
    {
        List<UpgradeOption> result = new List<UpgradeOption>();
        for (int i = 0; i < Types.Length; i++) result.Add(CreateOption(Types[i]));
        return result.ToArray();
    }

    public static UpgradeOption[] CreateEvolutionOptions()
    {
        List<UpgradeOption> result = new List<UpgradeOption>();
        for (int i = 0; i < EvolutionTypes.Length; i++) result.Add(CreateOption(EvolutionTypes[i]));
        return result.ToArray();
    }

    public static UpgradeOption CreateOption(UpgradeType type)
    {
        int index = IndexOf(type);
        if (index < 0) return null;
        UpgradeOption option = ScriptableObject.CreateInstance<UpgradeOption>();
        option.name = Names[index];
        option.displayName = Names[index];
        option.description = Descriptions[index];
        option.type = type;
        option.skillId = SkillIds[index];
        option.value = 1f;
        option.skillCard = CreateCard(index);
        return option;
    }

    public static SkillCardDefinition CreateCard(int index)
    {
        if (AllTypes.Length == 0) return null;
        index = Mathf.Clamp(index, 0, AllTypes.Length - 1);
        UpgradeType type = AllTypes[index];
        SkillCardDefinition card = ScriptableObject.CreateInstance<SkillCardDefinition>();
        card.name = Names[index] + "卡";
        card.cardId = "VesselSkill_" + SkillIds[index];
        card.skillId = SkillIds[index];
        card.legacyCardId = LegacyCardIdFor(type);
        card.displayName = Names[index];
        card.description = Descriptions[index];
        card.educationText = Education[index];
        card.linkedUpgradeType = type;
        card.iconGlyph = Glyphs[index];
        card.cardColor = Colors[index];
        return card;
    }

    public static SkillCardDefinition[] CreateCodexCards()
    {
        List<SkillCardDefinition> result = new List<SkillCardDefinition>();
        for (int i = 0; i < AllTypes.Length; i++) result.Add(CreateCard(i));
        result.Sort((a, b) => a.skillId.CompareTo(b.skillId));
        return result.ToArray();
    }

    // The old catalog generated keys from the enum value (10..19). Those saves must
    // keep unlocking after the card key moved to the workbook ID.
    public static string LegacyCardIdFor(UpgradeType type)
    {
        switch (type)
        {
            case UpgradeType.MembraneStent:
            case UpgradeType.FlushNeedle:
            case UpgradeType.DissectionScissors:
            case UpgradeType.SidewallForceps:
            case UpgradeType.Suture:
            case UpgradeType.MicroscopicHemostat:
            case UpgradeType.SpringCoil:
            case UpgradeType.ThrombusCatheter:
            case UpgradeType.SuctionTube:
            case UpgradeType.AblationBomb:
                return "VesselSkill_" + ((int)type);
        }
        return "";
    }

    public static int IndexOf(UpgradeType type)
    {
        for (int i = 0; i < AllTypes.Length; i++) if (AllTypes[i] == type) return i;
        return -1;
    }

    public static string NameFor(UpgradeType type)
    {
        int index = IndexOf(type);
        return index < 0 ? type.ToString() : Names[index];
    }

    public static string EducationFor(UpgradeType type)
    {
        int index = IndexOf(type);
        return index < 0 ? "保持均衡饮食、规律运动和定期检查。" : Education[index];
    }

    public static string GlyphFor(UpgradeType type)
    {
        int index = IndexOf(type);
        return index < 0 ? "" : Glyphs[index];
    }

    public static string ShortDescriptionFor(UpgradeType type)
    {
        int index = IndexOf(type);
        return index < 0 ? "战斗强化" : ShortDescriptions[index];
    }

    public static Color ColorFor(UpgradeType type)
    {
        int index = IndexOf(type);
        return index < 0 ? Color.white : Colors[index];
    }
}
