using System.Collections.Generic;
using UnityEngine;

public static class VesselSkillCatalog
{
    public static readonly UpgradeType[] Types =
    {
        UpgradeType.MembraneStent, UpgradeType.FlushNeedle,
        UpgradeType.DissectionScissors, UpgradeType.SidewallForceps,
        UpgradeType.Suture, UpgradeType.MicroscopicHemostat,
        UpgradeType.SpringCoil, UpgradeType.ThrombusCatheter,
        UpgradeType.SuctionTube, UpgradeType.AblationBomb
    };

    static readonly string[] Names = { "覆膜支架", "冲洗针", "解剖剪", "侧壁钳", "缝线", "显微止血夹", "弹簧圈", "血栓抽吸导管", "吸液管", "消融弹" };
    static readonly string[] Glyphs = { "盾", "针", "剪", "钳", "缝", "夹", "圈", "管", "吸", "弹" };
    static readonly string[] Descriptions =
    {
        "在周身形成护盾，降低自身受到的伤害。\nLv1：减伤 15%，护盾亮度提升\nLv2：减伤 25%，护盾亮度进一步提升\nLv3：减伤 35%，增加临时生命上限，5 秒不受击后回复最大生命 3.5%",
        "在周身形成环绕自身运动的子弹，撞击敌人造成轻微击退。\nLv1：2 枚\nLv2：3 枚\nLv3：4 枚",
        "周期性释放子弹穿出，取向逻辑同电刀，对路径上敌人造成碰撞伤害。\nLv1：基础体积\nLv2：体积增大\nLv3：变为十字形释放",
        "攻击特效附带红色尾调，攻击附带吸血。\nLv1：15% 概率回复 5% 血量\nLv2：20% 概率\nLv3：30% 概率",
        "怪物上方伸出形如闪电的红线，每次随机攻击视野内敌人。\nLv1：攻击 2 个敌人\nLv2：攻击 3 个敌人\nLv3：攻击 4 个敌人，特效增强",
        "小范围攻击，击杀敌人时留下区域，1 秒内该位置附近敌人受到持续小伤害。\nLv1：1 个区域\nLv2：2 个区域\nLv3：3 个区域",
        "对攻击的敌人造成持续 1 秒减速，敌人受击后显示掉落碎冰块特效。\nLv1：减速 10%\nLv2：减速 20%\nLv3：减速 30%",
        "角色周边出现跟随物发射低伤害激光，参考浮游炮风格。\nLv1：1 个跟随物\nLv2：2 个跟随物\nLv3：3 个跟随物",
        "周身周期性形成龙卷吸入特效，增加拾取范围半径。\nLv1：+10%\nLv2：+20%\nLv3：+30%",
        "投掷炸弹，延迟后对范围内敌人造成伤害。\nLv1：投掷 2 枚，延迟 1 秒\nLv2：投掷 3 枚\nLv3：延迟缩短至 0.5 秒"
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
        "消融弹对应射频消融术（RFA），通过热能闭合病变血管，是治疗静脉曲张的常用微创方法。术后需穿戴弹力袜并适度活动。"
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
        "投掷延迟爆炸的消融弹"
    };

    public static bool IsVesselSkill(UpgradeType type)
    {
        return (int)type >= (int)UpgradeType.MembraneStent && (int)type <= (int)UpgradeType.AblationBomb;
    }

    public static UpgradeOption[] CreateOptions()
    {
        List<UpgradeOption> result = new List<UpgradeOption>();
        for (int i = 0; i < Types.Length; i++)
        {
            UpgradeOption option = ScriptableObject.CreateInstance<UpgradeOption>();
            option.name = Names[i];
            option.displayName = Names[i];
            option.description = Descriptions[i];
            option.type = Types[i];
            option.value = 1f;
            option.skillCard = CreateCard(i);
            result.Add(option);
        }
        return result.ToArray();
    }

    public static SkillCardDefinition CreateCard(int index)
    {
        index = Mathf.Clamp(index, 0, Types.Length - 1);
        SkillCardDefinition card = ScriptableObject.CreateInstance<SkillCardDefinition>();
        card.name = Names[index] + "卡";
        card.cardId = "VesselSkill_" + ((int)Types[index]);
        card.displayName = Names[index];
        card.description = Descriptions[index];
        card.educationText = Education[index];
        card.linkedUpgradeType = Types[index];
        card.iconGlyph = Glyphs[index];
        card.cardColor = ColorFor(Types[index]);
        return card;
    }

    public static int IndexOf(UpgradeType type)
    {
        for (int i = 0; i < Types.Length; i++) if (Types[i] == type) return i;
        return 0;
    }

    public static string EducationFor(UpgradeType type) { return Education[IndexOf(type)]; }
    public static string GlyphFor(UpgradeType type) { return Glyphs[IndexOf(type)]; }
    public static string ShortDescriptionFor(UpgradeType type) { return ShortDescriptions[IndexOf(type)]; }

    public static Color ColorFor(UpgradeType type)
    {
        Color[] colors =
        {
            new Color(0.24f, 0.68f, 0.92f), new Color(0.35f, 0.78f, 0.86f),
            new Color(0.72f, 0.42f, 0.95f), new Color(0.88f, 0.24f, 0.30f),
            new Color(0.92f, 0.38f, 0.50f), new Color(0.96f, 0.62f, 0.23f),
            new Color(0.46f, 0.76f, 0.36f), new Color(0.27f, 0.82f, 0.70f),
            new Color(0.55f, 0.85f, 0.95f), new Color(1.00f, 0.55f, 0.20f)
        };
        return colors[IndexOf(type)];
    }
}
