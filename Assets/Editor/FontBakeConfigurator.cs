using UnityEditor;
using UnityEngine;

// 子集化后的 Noto Sans SC 只有 967 个字，必须让 Unity 走"烘焙"而不是"动态"：
// 动态字体在 WebGL 上渲染不出中文，而烘焙时若不指定字符集，Unity 会试图把整张
// cmap 全部烤进图集。customCharacters 就是把烘焙范围钉死在这 967 个字上。
public static class FontBakeConfigurator
{
    const int BakeSize = 48;
    const string CharsPath = "Assets/Resources/font_chars.txt";
    const string Marker = "VesselGuard.FontBakeConfigured";
    static readonly string[] Fonts =
    {
        "Assets/Resources/notosanssc.ttf",
        "Assets/Resources/notosanssc_medium.ttf",
    };

    [InitializeOnLoadMethod]
    static void AutoConfigure()
    {
        if (SessionState.GetBool(Marker, false)) return;
        if (EditorApplication.isCompiling) return;
        EditorApplication.delayCall += () => Configure(false);
    }

    [MenuItem("VesselGuard/配置中文字体烘焙")]
    public static void ConfigureManual()
    {
        Configure(true);
    }

    static void Configure(bool manual)
    {
        SessionState.SetBool(Marker, true);
        string characters = LoadCharacters();
        if (string.IsNullOrEmpty(characters))
        {
            Debug.LogError("FontBakeConfigurator: 读不到 " + CharsPath + "，请先运行 python tools/subset_font.py");
            if (manual) EditorUtility.DisplayDialog("缺少字符表", CharsPath + " 不存在或为空。", "确定");
            return;
        }

        int configured = 0;
        for (int i = 0; i < Fonts.Length; i++)
            if (Apply(Fonts[i], characters)) configured++;

        if (manual)
            EditorUtility.DisplayDialog("中文字体烘焙",
                configured == 0 ? "两个字体已经配置好了，无需重导入。" : "已重新导入 " + configured + " 个字体（烘焙 " + BakeSize + "px / " + characters.Length + " 字）。",
                "确定");
    }

    static string LoadCharacters()
    {
        TextAsset asset = AssetDatabase.LoadAssetAtPath<TextAsset>(CharsPath);
        if (asset == null) return null;
        // 去掉 BOM 与换行：这些不是要烤的字形。
        return asset.text.Replace("\uFEFF", string.Empty).Replace("\r", string.Empty).Replace("\n", string.Empty);
    }

    // 返回值表示是否真的重导入了。已经是目标配置就跳过，否则每次域重载都会重烤一遍图集。
    static bool Apply(string path, string characters)
    {
        TrueTypeFontImporter importer = AssetImporter.GetAtPath(path) as TrueTypeFontImporter;
        if (importer == null)
        {
            Debug.LogWarning("FontBakeConfigurator: 未找到 " + path);
            return false;
        }
        if (importer.fontSize == BakeSize
            && importer.includeFontData == false
            && importer.customCharacters == characters)
            return false;

        importer.fontSize = BakeSize;
        importer.customCharacters = characters;
        importer.includeFontData = false;
        importer.SaveAndReimport();
        return true;
    }
}
