using UnityEngine;

public static class RuntimeFont
{
    const string RegularAsset = "notosanssc";
    const string MediumAsset = "notosanssc_medium";
    const string LegacyAsset = "simhei";

    static Font regular;
    static Font medium;
    static bool triedLegacy;

    public static Font Get()
    {
        if (regular == null) regular = Load(RegularAsset) ?? LoadLegacy();
        if (regular == null) Debug.LogError("RuntimeFont: Resources/notosanssc.ttf 未找到");
        return regular;
    }

    // 烘焙字体用不了 fontStyle=Bold，标题的重量层级只能靠独立的 Medium 字面撑。
    public static Font GetTitle()
    {
        if (medium == null) medium = Load(MediumAsset);
        return medium == null ? Get() : medium;
    }

    static Font Load(string path) => Resources.Load<Font>(path);

    static Font LoadLegacy()
    {
        if (triedLegacy) return null;
        triedLegacy = true;
        Font fallback = Load(LegacyAsset);
        if (fallback != null) Debug.LogWarning("RuntimeFont: notosanssc 缺失，回退 simhei（WebGL 下会糊）。");
        return fallback;
    }
}
