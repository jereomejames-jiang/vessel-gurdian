using UnityEngine;

[System.Serializable]
public class EnemyCodexEntry
{
    public EnemyKind kind;
    public string displayName;
    public string medicalName;
    public string description;
    public string educationText;
    public string healthTip;
    public Color cardColor;
    public bool isUnlocked;

    public void Unlock()
    {
        isUnlocked = true;
        PlayerPrefs.SetInt("EnemyCodex_" + (int)kind, 1);
        PlayerPrefs.Save();
    }

    public void LoadUnlockState()
    {
        isUnlocked = PlayerPrefs.GetInt("EnemyCodex_" + (int)kind, 0) == 1;
    }
}

public static class EnemyCodexCatalog
{
    public static readonly EnemyKind[] Types =
    {
        EnemyKind.CellShadow, EnemyKind.SpikeSoldier, EnemyKind.RupturedCell,
        EnemyKind.MicroThrombus, EnemyKind.BleedingGap, EnemyKind.VenousStasis,
        EnemyKind.FibrinNet, EnemyKind.PrimitiveCell, EnemyKind.TargetBlocker
    };

    static readonly string[] Names = { "小细胞影", "镰刀刺兵", "裂红体", "微栓块", "渗血裂口", "静脉淤血", "纤维蛋白网", "原始细胞", "靶点遮蔽者" };
    static readonly string[] MedicalNames = { "小细胞性贫血", "镰刀样红细胞贫血", "溶血", "微血栓形成", "毛细血管出血", "静脉淤滞", "纤维蛋白沉积", "异常克隆扩增", "靶点逃逸" };

    static readonly string[] Descriptions =
    {
        "数量多、刷新快的小型敌人，是毛细血管里最先出现的异常信号。",
        "成群出现且彼此靠近，能为同伴抵挡伤害，并抗击退。需要穿透或范围攻击一并处理。",
        "死亡后短暂延迟原地爆炸，对周围一定范围造成碰撞伤害。远程攻击可在安全距离外处理。",
        "血量高、移动缓慢，接触玩家会生成局部、持续一定时间的障碍。需要高伤害技能快速清除。",
        "固定在地图上的危险区域，持续造成伤害与减速，可被击杀。非指向性攻击更适合处理。",
        "靠近玩家时叠加减速与淤滞层数，叠满 3 层会将玩家定身。持续移动可清除淤滞。",
        "死亡时铺设减速网，黏合附近区域。范围净化技能可有效清除。",
        "存活时间越长，复制速度越快；场上原始细胞总数有上限。需要爆发攻击尽快清除。",
        "为光环内的异常细胞提供 20% 减伤。优先击杀可解除光环效果。"
    };

    static readonly string[] EducationTexts =
    {
        "小细胞性贫血常见于缺铁性贫血或地中海贫血，红细胞体积变小、携氧能力下降，导致组织缺氧。日常应保证铁与蛋白质摄入，如瘦肉、动物肝脏，并搭配富含维生素 C 的蔬果促进铁吸收。",
        "镰刀型细胞贫血症是一种遗传性血液病，异常血红蛋白使红细胞呈镰刀状，容易堵塞微血管、引发疼痛危象。患者需避免脱水、缺氧与剧烈运动，并按医嘱规律随访。",
        "溶血指红细胞破裂、释放血红蛋白的过程，可由免疫反应、感染或遗传因素引起。严重溶血会导致贫血与黄疸，出现不明原因乏力、尿色加深时应及时就医。",
        "微血栓是微小血管中的血凝块，可阻塞血流、造成组织缺血，常见于弥散性血管内凝血（DIC）等情况。抗凝治疗是主要手段，术后早期活动有助于预防。",
        "毛细血管出血表现为皮肤瘀点、瘀斑，可能由血小板减少、血管壁脆弱或凝血功能障碍引起。轻微出血多可自愈，反复或大面积出血需检查凝血功能。",
        "静脉淤滞指血液在静脉中流动缓慢，常见于久坐久站或静脉瓣膜功能不全，可导致下肢水肿、静脉曲张并增加血栓风险。定时活动、穿弹力袜有助于促进回流。",
        "纤维蛋白是凝血过程中的关键蛋白，形成网状结构捕获血细胞；过度沉积会促进血栓形成。日常应遵医嘱使用抗凝药物，并定期监测凝血指标。",
        "异常克隆扩增指细胞不受控制地复制，是肿瘤形成的基础。正常细胞有分裂次数限制（海弗里克极限），而癌细胞可无限增殖。健康生活方式与定期筛查有助于降低风险。",
        "靶点逃逸是肿瘤细胞逃避治疗的机制之一，通过改变表面抗原或激活替代信号通路产生耐药。联合用药与免疫治疗有助于克服耐药，治疗方案需个体化制定。"
    };

    static readonly string[] HealthTips =
    {
        "均衡饮食，适量摄入富含铁的食物；定期体检，关注血常规指标。",
        "有家族史者应进行基因筛查；保持充足水分，避免极端温度与过度劳累。",
        "出现不明原因乏力、黄疸或尿色加深时及时就医；避免自行服用可能诱发溶血的药物。",
        "术后尽早下床活动以预防血栓；长途旅行时定时活动下肢、多饮水。",
        "轻微瘀伤可冷敷处理；反复出血或大面积瘀斑应检查凝血功能。",
        "避免久坐久站，定时活动；穿弹力袜促进静脉回流。",
        "遵医嘱使用抗凝药物，定期监测凝血指标，勿自行增减药量。",
        "保持健康生活方式以降低肿瘤风险；定期筛查，早发现早治疗。",
        "癌症治疗需个体化方案；保持积极心态，配合医生规范治疗。"
    };

    static readonly Color[] Colors =
    {
        new Color(0.22f, 0.78f, 0.95f), new Color(1f, 0.62f, 0.16f),
        new Color(0.95f, 0.18f, 0.28f), new Color(0.56f, 0.22f, 0.78f),
        new Color(1f, 0.12f, 0.52f), new Color(0.4f, 0.2f, 0.6f),
        new Color(0.7f, 0.7f, 0.8f), new Color(0.3f, 0.8f, 0.4f),
        new Color(0.2f, 0.2f, 0.3f)
    };

    public static EnemyCodexEntry CreateEntry(int index)
    {
        index = Mathf.Clamp(index, 0, Types.Length - 1);
        EnemyCodexEntry entry = new EnemyCodexEntry();
        entry.kind = Types[index];
        entry.displayName = Names[index];
        entry.medicalName = MedicalNames[index];
        entry.description = Descriptions[index];
        entry.educationText = EducationTexts[index];
        entry.healthTip = HealthTips[index];
        entry.cardColor = Colors[index];
        entry.LoadUnlockState();
        return entry;
    }

    public static EnemyCodexEntry[] CreateAllEntries()
    {
        EnemyCodexEntry[] entries = new EnemyCodexEntry[Types.Length];
        for (int i = 0; i < Types.Length; i++) entries[i] = CreateEntry(i);
        return entries;
    }

    public static int IndexOf(EnemyKind kind)
    {
        for (int i = 0; i < Types.Length; i++) if (Types[i] == kind) return i;
        return 0;
    }

    public static string NameFor(EnemyKind kind) { return Names[IndexOf(kind)]; }
    public static string EducationFor(EnemyKind kind) { return EducationTexts[IndexOf(kind)]; }
    public static Color ColorFor(EnemyKind kind) { return Colors[IndexOf(kind)]; }
}
