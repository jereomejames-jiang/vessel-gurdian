using System.Collections.Generic;
using System.Text;
using UnityEngine;

// 技能文字唯一源：Resources/skill_text.csv（Excel 可直接编辑，F8 热重载）。
// 列：编号,名称,图标字,短描述,描述,科普文案,Lv1效应,Lv2效应,Lv3效应；描述内换行用标准 CSV 引号包裹。
public static class SkillText
{
    public class Row
    {
        public string name, glyph, shortDesc, description, education;
        public readonly string[] lv = new string[3];
    }

    static Dictionary<int, Row> rows;

    public static void EnsureLoaded()
    {
        if (rows != null) return;
        rows = new Dictionary<int, Row>();
        TextAsset csv = Resources.Load<TextAsset>("skill_text");
        if (csv == null)
        {
            Debug.LogWarning("SkillText: Resources/skill_text.csv 未找到，技能文字为空");
            return;
        }
        List<string[]> table = Parse(csv.text);
        for (int i = 1; i < table.Count; i++)
        {
            string[] c = table[i];
            if (c.Length < 6 || !int.TryParse(c[0].Trim(), out int id)) continue;
            Row r = new Row { name = c[1], glyph = c[2], shortDesc = c[3], description = c[4], education = c[5] };
            for (int k = 0; k < 3; k++) r.lv[k] = c.Length > 6 + k ? c[6 + k] : "";
            rows[id] = r;
        }
        Debug.Log($"SkillText: 已加载 {rows.Count} 项技能文字");
    }

    public static void Reload()
    {
        rows = null;
        EnsureLoaded();
    }

    static Row Find(UpgradeType type)
    {
        EnsureLoaded();
        if (!rows.TryGetValue((int)type, out Row r))
            Debug.LogWarning("SkillText: skill_text.csv 缺少编号 " + (int)type);
        return r;
    }

    public static string Name(UpgradeType type) { Row r = Find(type); return r != null ? r.name : type.ToString(); }
    public static string Glyph(UpgradeType type) { Row r = Find(type); return r != null ? r.glyph : ""; }
    public static string ShortDesc(UpgradeType type) { Row r = Find(type); return r != null ? r.shortDesc : ""; }
    public static string Description(UpgradeType type) { Row r = Find(type); return r != null ? r.description : ""; }
    public static string Education(UpgradeType type) { Row r = Find(type); return r != null ? r.education : ""; }

    public static string LevelEffect(UpgradeType type, int level)
    {
        Row r = Find(type);
        if (r == null) return "";
        string s = r.lv[Mathf.Clamp(level, 1, 3) - 1];
        return string.IsNullOrEmpty(s) ? r.shortDesc : s;
    }

    static List<string[]> Parse(string text)
    {
        var table = new List<string[]>();
        var cells = new List<string>();
        var sb = new StringBuilder();
        bool inQuotes = false;
        for (int i = 0; i < text.Length; i++)
        {
            char ch = text[i];
            if (inQuotes)
            {
                if (ch == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"') { sb.Append('"'); i++; }
                    else inQuotes = false;
                }
                else sb.Append(ch);
            }
            else if (ch == '"') inQuotes = true;
            else if (ch == ',') { cells.Add(sb.ToString()); sb.Length = 0; }
            else if (ch == '\n' || ch == '\r')
            {
                if (ch == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                cells.Add(sb.ToString()); sb.Length = 0;
                if (cells.Count > 1 || cells[0].Length > 0) table.Add(cells.ToArray());
                cells = new List<string>();
            }
            else sb.Append(ch);
        }
        if (sb.Length > 0 || cells.Count > 0)
        {
            cells.Add(sb.ToString());
            table.Add(cells.ToArray());
        }
        return table;
    }
}