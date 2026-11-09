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
        UpgradeType.SuctionTube, UpgradeType.AblationBomb,
        UpgradeType.ContinuousFlush, UpgradeType.UltrasonicScissors,
        UpgradeType.BloodRecoveryForceps, UpgradeType.StapleGun,
        UpgradeType.EmbolismHemostatNet, UpgradeType.VascularSpasmRing,
        UpgradeType.ExtracorporealPump, UpgradeType.DirectedAblationRain,
        UpgradeType.HighFrequencyElectroknife, UpgradeType.MicroInfusionPump,
        UpgradeType.AtomizationDrugMachine
    };

    static readonly string[] Names = { "覆膜支架", "冲洗针", "解剖剪", "侧壁钳", "缝线", "显微止血夹", "弹簧圈", "血栓抽吸导管", "吸液管", "消融弹",
        "持续冲洗", "超声解剖剪", "血液回收钳", "吻合钉枪", "栓塞止血网", "血管痉挛环", "体外循环泵", "定向消融弹雨", "高频电刀", "微量输液泵", "雾化给药机" };
    static readonly string[] Glyphs = { "盾", "针", "剪", "钳", "缝", "夹", "圈", "管", "吸", "弹",
        "续", "超", "血", "钉", "网", "痉", "泵", "雨", "电", "泵", "雾" };
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
        "投掷炸弹，延迟后对范围内敌人造成伤害。\nLv1：投掷 2 枚，延迟 1 秒\nLv2：投掷 3 枚\nLv3：延迟缩短至 0.5 秒",
        "持续冲洗系统：针体持续环绕，不再周期消失。伤害间隔缩短，击退力度增加。",
        "超声解剖剪：剪刃飞射旋转滞留，十字二剪42°。范围更大，滞留时间更长。",
        "血液回收钳：吸血8%（溢出转护盾）。吸血概率提升，溢出血量转化为临时护盾。",
        "吻合钉枪：高速穿透缝合钉。弹速提升，可穿透多个敌人。",
        "栓塞止血网：区域吸附周围敌人（磁吸）。生成磁吸区域，持续拉拽敌人。",
        "血管痉挛环：范围减速50%+持续伤害。减速效果增强，附加持续伤害。",
        "体外循环泵：激光命中回血（吸血）。激光攻击附带吸血效果。",
        "定向消融弹雨：从上方投下多枚消融弹。覆盖范围更广，伤害更密集。",
        "高频电刀：电弧刀波（伤害5）。释放电弧波，范围伤害。",
        "微量输液泵：极短冷却持续发射。攻击间隔大幅缩短，持续输出。",
        "雾化给药机：药剂池（最多3滩）。同时存在的药剂池数量增加。"
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
        "持续冲洗系统模拟介入手术中的持续灌注，用于保持导管通畅、防止血栓堵塞。现实中导管需定期维护与冲管，患者切勿自行调整输液装置。",
        "超声解剖剪对应超声刀技术，利用高频机械振动切割组织并同时凝血，可减少术中出血。术后应注意休息，按医嘱观察有无出血或感染迹象。",
        "血液回收钳对应自体血液回收技术，将术中流失的血细胞回输给患者，减少异体输血需求。是否适合回收需由医生评估，感染或肿瘤手术等情况通常禁用。",
        "吻合钉枪用于血管与组织吻合，以缝合钉快速连接断端，缩短手术时间。术后需保持伤口清洁干燥，出现红肿渗液应及时复诊。",
        "栓塞止血网模拟介入栓塞材料，通过阻断异常血流达到止血或治疗目的。现实栓塞治疗需影像引导与专业团队评估，术后遵医嘱复查。",
        "血管痉挛环提醒我们：血管痉挛可由寒冷、情绪压力或某些药物诱发，表现为局部缺血疼痛。日常注意保暖、戒烟限酒，反复发作应就医排查。",
        "体外循环泵对应心肺旁路技术，在心脏手术期间暂时代替心肺维持血液循环。这是高难度的生命支持手段，需由专业团队全程监护。",
        "定向消融弹雨对应精准靶向治疗，集中作用于病灶、尽量减少对健康组织的损伤。现实中的消融与靶向治疗需严格定位，并由医生制定方案。",
        "高频电刀利用高频电流实现切割与凝血，是现代手术的基础工具。使用不当可能造成灼伤，必须由受训医务人员操作。",
        "微量输液泵用于精准、匀速给药，维持稳定的血药浓度，常用于重症与儿科。输液速度需遵医嘱设定，不可自行调快。",
        "雾化给药机模拟吸入式给药，使药物直达呼吸道、起效快且全身副作用较小。使用后应漱口并清洁面罩，按医嘱控制剂量。"
    };

    static readonly string[] ShortDescriptions =
    {
        "形成护盾，降低受到的伤害",
        "环绕针体，撞击敌人并击退",
        "释放剪刃，切开路径敌人",
        "攻击附带吸血效果",
        "闪电红线随机打击视野内敌人",
        "击杀后留下持续伤害区域",
        "命中后减速敌人",
        "跟随导管发射激光",
        "增加拾取范围半径",
        "投掷延迟爆炸的消融弹",
        "针体持续环绕，伤害间隔缩短",
        "剪刃飞射旋转滞留，十字释放",
        "吸血8%，溢出转护盾",
        "高速穿透缝合钉",
        "区域磁吸拉拽敌人",
        "范围减速50%+持续伤害",
        "激光命中回血",
        "多枚消融弹覆盖打击",
        "释放电弧波范围伤害",
        "极短冷却持续发射",
        "药剂池数量增至3滩"
    };

    public static bool IsVesselSkill(UpgradeType type)
    {
        int t = (int)type;
        return (t >= 10 && t <= 19) || (t >= 20 && t <= 30);
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
            new Color(0.55f, 0.85f, 0.95f), new Color(1.00f, 0.55f, 0.20f),
            new Color(0.30f, 0.85f, 0.95f), new Color(0.80f, 0.50f, 1.00f),
            new Color(0.95f, 0.30f, 0.35f), new Color(0.98f, 0.70f, 0.30f),
            new Color(0.50f, 0.80f, 0.40f), new Color(0.30f, 0.85f, 0.75f),
            new Color(0.60f, 0.90f, 1.00f), new Color(1.00f, 0.60f, 0.25f),
            new Color(0.75f, 0.45f, 0.98f), new Color(0.40f, 0.88f, 0.98f),
            new Color(0.85f, 0.60f, 0.95f)
        };
        // 色相表同时决定卡框发光色，统一降饱和：保留稀有度辨识度，但去掉霓虹感。
        Color raw = colors[IndexOf(type)];
        Color.RGBToHSV(raw, out float hue, out float sat, out float val);
        return Color.HSVToRGB(hue, Mathf.Clamp(sat * 0.62f, 0.18f, 0.58f), Mathf.Clamp(val, 0.72f, 0.94f));
    }
}
