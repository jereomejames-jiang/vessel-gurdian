using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

public static class GameBalance
{
    static Dictionary<string, float> values;

    public static void EnsureLoaded()
    {
        if (values != null) return;
        values = new Dictionary<string, float>();
        // skill_balance.csv is generated from 技能数据.xlsx and intentionally
        // loads last, so skill-specific values override the generic balance table.
        bool loadedBase = LoadResource("balance", true);
        LoadResource("skill_balance", false);
        if (!loadedBase && values.Count == 0)
            Debug.LogWarning("GameBalance: Resources/balance.csv 未找到，全部使用脚本默认值");
        else
            Debug.Log($"GameBalance: 已加载 {values.Count} 项数值配置");
    }

    static bool LoadResource(string resourceName, bool required)
    {
        TextAsset csv = Resources.Load<TextAsset>(resourceName);
        if (csv == null)
        {
            if (required)
                Debug.LogWarning("GameBalance: Resources/" + resourceName + ".csv 未找到，使用脚本默认值");
            return false;
        }

        string[] lines = csv.text.Split('\n');
        for (int i = 1; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (line.Length == 0 || line.StartsWith("#")) continue;
            string[] parts = line.Split(',');
            if (parts.Length < 3) continue;
            string key = parts[1].Trim();
            if (key.Length == 0) continue;
            if (float.TryParse(parts[2].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float value))
                values[key] = value;
        }
        return true;
    }

    public static float Get(string key, float fallback)
    {
        EnsureLoaded();
        return values.TryGetValue(key, out float value) ? value : fallback;
    }

    public static int GetInt(string key, int fallback)
    {
        return Mathf.RoundToInt(Get(key, fallback));
    }

    // 强度测试用：F8 热重载 CSV，只对之后重新读取的数值生效
    public static void Reload()
    {
        values = null;
        EnsureLoaded();
    }
}
