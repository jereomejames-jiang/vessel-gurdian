#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

// Excel -> Resources/skill_balance.csv sync for the skill design workbook.
//
// Source priority:
//   1. Doc/数值设计/技能数据.xlsx (portable project copy)
//   2. ~/Desktop/技能数据.xlsx  (the workbook referenced in the design docs)
//
// Only rows whose 编号 belongs to the implemented skill set are mapped. The
// generated CSV is loaded by GameBalance AFTER balance.csv, so skill values from
// this workbook override the generic balance table without touching other keys.
[InitializeOnLoad]
public static class SkillExcelSync
{
    const string ProjectWorkbookRelativePath = "Doc/数值设计/技能数据.xlsx";
    const string DesktopWorkbookName = "技能数据.xlsx";
    const string CsvRelativePath = "Assets/Resources/skill_balance.csv";
    const string MirrorRelativePath = "Doc/数值设计/skill_balance.csv";
    const string StateRelativePath = "Library/SkillExcelSync.state";
    const string CsvHeader = "分类,键名,数值,说明";

    static double nextTick;

    static SkillExcelSync()
    {
        EditorApplication.delayCall += () => CheckAndExport(false);
        EditorApplication.update += Tick;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    static void Tick()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode ||
            EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        if (EditorApplication.timeSinceStartup < nextTick) return;
        nextTick = EditorApplication.timeSinceStartup + 3.0;
        CheckAndExport(false);
    }

    static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingEditMode) CheckAndExport(false);
    }

    [MenuItem("Tools/Balance/Export Skill Excel to CSV")]
    static void ExportMenu()
    {
        CheckAndExport(true);
    }

    [MenuItem("Tools/Balance/Reveal Skill Workbook")]
    static void RevealWorkbook()
    {
        string path = FindWorkbook();
        if (path == null) { Debug.LogWarning("[SkillExcelSync] 技能数据.xlsx not found."); return; }
        EditorUtility.RevealInFinder(path);
    }

    public static string FindWorkbook()
    {
        string root = ProjectRoot();
        string local = Path.Combine(root, ProjectWorkbookRelativePath);
        string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        if (string.IsNullOrEmpty(desktop))
            desktop = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Desktop");
        string desktopPath = Path.Combine(desktop, DesktopWorkbookName);

        string chosen = null;
        DateTime chosenTime = DateTime.MinValue;
        if (File.Exists(local))
        {
            chosen = local;
            chosenTime = File.GetLastWriteTimeUtc(local);
        }
        if (File.Exists(desktopPath))
        {
            DateTime desktopTime = File.GetLastWriteTimeUtc(desktopPath);
            if (chosen == null || desktopTime > chosenTime) chosen = desktopPath;
        }
        return chosen;
    }

    public static void CheckAndExport(bool force)
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogWarning("[SkillExcelSync] Exit Play Mode before exporting the skill workbook.");
            return;
        }
        string workbook = FindWorkbook();
        if (workbook == null)
        {
            if (force) Debug.LogWarning("[SkillExcelSync] 技能数据.xlsx not found.");
            return;
        }
        string root = ProjectRoot();
        string csv = Path.Combine(root, CsvRelativePath);

        string hash;
        try { hash = HashFile(workbook); }
        catch (Exception exception)
        {
            Debug.LogWarning("[SkillExcelSync] Cannot read skill workbook: " + exception.Message);
            return;
        }

        string previousHash = ReadState(root);
        if (!force && hash == previousHash) return;

        try
        {
            int rows = ConvertTo(workbook, csv);
            string mirror = Path.Combine(root, MirrorRelativePath);
            try
            {
                if (Directory.Exists(Path.GetDirectoryName(mirror)))
                    File.Copy(csv, mirror, true);
            }
            catch (Exception mirrorException)
            {
                Debug.LogWarning("[SkillExcelSync] Runtime CSV updated, but the design mirror failed: " +
                    mirrorException.Message);
            }
            AssetDatabase.ImportAsset(CsvRelativePath, ImportAssetOptions.ForceUpdate);
            WriteState(root, hash);
            Debug.Log("[SkillExcelSync] Exported " + rows + " skill rows from " +
                Path.GetFileName(workbook) + " to " + CsvRelativePath);
        }
        catch (Exception exception)
        {
            Debug.LogError("[SkillExcelSync] Export failed: " + exception);
        }
    }

    // Public for batch-mode/manual verification.
    public static int ConvertTo(string workbookPath, string csvPath)
    {
        List<XlsxSheet> sheets = XlsxTable.Read(workbookPath);
        Dictionary<int, List<SkillRow>> byId = new Dictionary<int, List<SkillRow>>();
        foreach (XlsxSheet sheet in sheets)
        {
            foreach (List<string> raw in sheet.Rows)
            {
                if (!int.TryParse(Get(raw, 0).Trim(), out int id)) continue;
                if (!int.TryParse(Get(raw, 3).Trim(), out int level)) continue;
                if (!IsImplemented(id) || level <= 0) continue;
                SkillRow row = new SkillRow
                {
                    Id = id,
                    Level = level,
                    Name = Get(raw, 2).Trim(),
                    Damage = Get(raw, 6).Trim(),
                    Duration = Get(raw, 7).Trim(),
                    TimeBetween = Get(raw, 8).Trim(),
                    Cooldown = Get(raw, 9).Trim(),
                    Count = Get(raw, 10).Trim(),
                    Area = Get(raw, 11).Trim(),
                    Speed = Get(raw, 12).Trim(),
                    Angle = Get(raw, 13).Trim(),
                    Effect = Get(raw, 14).Trim(),
                    Special = Get(raw, 15).Trim(),
                    Description = Get(raw, 16).Trim()
                };
                if (!byId.TryGetValue(id, out List<SkillRow> rows))
                {
                    rows = new List<SkillRow>();
                    byId[id] = rows;
                }
                rows.Add(row);
            }
        }
        if (byId.Count == 0)
            throw new InvalidDataException("No implemented skill rows were found in 技能数据.xlsx.");

        SortedDictionary<string, Entry> output = new SortedDictionary<string, Entry>(StringComparer.Ordinal);
        foreach (KeyValuePair<int, List<SkillRow>> pair in byId)
        {
            pair.Value.Sort((a, b) => a.Level.CompareTo(b.Level));
            Map(pair.Key, pair.Value, output);
        }

        List<string> lines = new List<string> { CsvHeader };
        foreach (KeyValuePair<string, Entry> pair in output)
        {
            Entry entry = pair.Value;
            lines.Add(CsvField(entry.Category) + "," + CsvField(pair.Key) + "," +
                CsvField(entry.Value) + "," + CsvField(entry.Description));
        }

        string tempPath = Path.Combine(Path.GetTempPath(),
            "skill_sync_" + Guid.NewGuid().ToString("N") + ".csv");
        try
        {
            File.WriteAllText(tempPath, string.Join("\n", lines) + "\n", new UTF8Encoding(true));
            string directory = Path.GetDirectoryName(csvPath);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            File.Copy(tempPath, csvPath, true);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
        return lines.Count - 1;
    }

    static bool IsImplemented(int id)
    {
        switch (id)
        {
            case 101: case 107: case 109: case 203:
            case 1001: case 1002: case 1003: case 1004:
            case 1005: case 1006: case 1007: case 1008:
            case 1010:
                return true;
        }
        return false;
    }

    static void Map(int id, List<SkillRow> rows, SortedDictionary<string, Entry> output)
    {
        SkillRow first = rows[0];
        switch (id)
        {
            case 101:
                foreach (SkillRow row in rows)
                    Set(output, "skill.stent.mult_lv" + row.Level, row.Effect, row);
                Match shield = Regex.Match(first.Special, @"护盾(\d+)");
                Set(output, "skill.stent.shield", shield.Success ? shield.Groups[1].Value : "60", first);
                Match regen = Regex.Match(first.Description, @"(\d+)秒不受击后每(\d+)秒回([\d.]+)%");
                if (regen.Success)
                {
                    Set(output, "skill.stent.regen_delay", regen.Groups[1].Value, first);
                    Set(output, "skill.stent.regen_tick", regen.Groups[2].Value, first);
                    Set(output, "skill.stent.regen_percent",
                        Format(Parse(regen.Groups[3].Value) / 100.0), first);
                }
                else
                {
                    Set(output, "skill.stent.regen_delay", "5", first);
                    Set(output, "skill.stent.regen_tick", "1", first);
                    Set(output, "skill.stent.regen_percent", "0.035", first);
                }
                break;
            case 1001:
                foreach (SkillRow row in rows)
                {
                    Set(output, "skill.rinse.damage_lv" + row.Level, row.Damage, row);
                    Set(output, "skill.rinse.count_lv" + row.Level, row.Count, row);
                    Set(output, "skill.rinse.angular_lv" + row.Level, Abs(row.Angle), row);
                }
                Set(output, "skill.rinse.radius_base", first.Area, first);
                Set(output, "skill.rinse.radius_per_level", "0", first);
                Match knockback = Regex.Match(first.Special, @"\(([\d.]+)\)");
                Set(output, "skill.rinse.knockback",
                    knockback.Success ? knockback.Groups[1].Value : "0.18", first);
                break;
            case 1002:
                SkillRow second = rows.Count > 1 ? rows[1] : first;
                foreach (SkillRow row in rows)
                    Set(output, "skill.scissors.period_lv" + row.Level, row.Cooldown, row);
                Set(output, "skill.scissors.period_base", first.Cooldown, first);
                Set(output, "skill.scissors.period_per_level",
                    Format(Parse(first.Cooldown) - Parse(second.Cooldown)), first);
                Match periodMin = Regex.Match(first.Description, @"下限([\d.]+)秒");
                Set(output, "skill.scissors.period_min",
                    periodMin.Success ? periodMin.Groups[1].Value : "1.2", first);
                Set(output, "skill.scissors.radius_lv1", first.Area, first);
                Set(output, "skill.scissors.radius_lv2", second.Area, second);
                Set(output, "skill.scissors.angle_lv1", first.Angle, first);
                Set(output, "skill.scissors.angle_lv2", second.Angle, second);
                SkillRow third = rows.Count > 2 ? rows[2] : first;
                Match cross = Regex.Match(third.Special, @"第二剪(\d+)");
                Set(output, "skill.scissors.cross_angle",
                    cross.Success ? cross.Groups[1].Value : "42", first);
                break;
            case 1003:
                foreach (SkillRow row in rows)
                {
                    Match match = Regex.Match(row.Description, @"概率([\d.]+)回([\d.]+)%");
                    if (!match.Success) continue;
                    Set(output, "skill.forceps.chance_lv" + row.Level, match.Groups[1].Value, row);
                    Set(output, "skill.forceps.heal_lv" + row.Level,
                        Format(Parse(match.Groups[2].Value) / 100.0), row);
                }
                break;
            case 1004:
                foreach (SkillRow row in rows)
                {
                    Set(output, "skill.suture.damage_lv" + row.Level, row.Damage, row);
                    Set(output, "skill.suture.count_lv" + row.Level, row.Count, row);
                }
                break;
            case 1005:
                foreach (SkillRow row in rows)
                    Set(output, "skill.clamp.radius_lv" + row.Level, row.Area, row);
                Set(output, "skill.clamp.radius_base", first.Area, first);
                Set(output, "skill.clamp.radius_per_level",
                    Format(Parse(rows.Count > 1 ? rows[1].Area : first.Area) - Parse(first.Area)), first);
                Set(output, "skill.clamp.duration", first.Duration, first);
                Set(output, "skill.clamp.tick", first.TimeBetween, first);
                Match clampDiv = Regex.Match(first.Damage, @"D÷(\d+)");
                Set(output, "skill.clamp.damage_div",
                    clampDiv.Success ? clampDiv.Groups[1].Value : "2", first);
                break;
            case 1006:
                foreach (SkillRow row in rows) Set(output, "skill.coil.slow_lv" + row.Level, row.Effect, row);
                Set(output, "skill.coil.duration", first.Duration, first);
                break;
            case 1007:
                foreach (SkillRow row in rows)
                    Set(output, "skill.suction.count_lv" + row.Level, row.Count, row);
                Set(output, "skill.suction.laser_interval", first.Cooldown, first);
                Set(output, "skill.suction.orbit_radius", first.Area, first);
                Set(output, "skill.suction.angular_speed", first.Angle, first);
                Match suctionDiv = Regex.Match(first.Damage, @"D÷(\d+)");
                Set(output, "skill.suction.damage_div",
                    suctionDiv.Success ? suctionDiv.Groups[1].Value : "2", first);
                Match laser = Regex.Match(first.Special, @"长([\d.]+)宽([\d.]+)持续([\d.]+)");
                if (laser.Success)
                {
                    Set(output, "skill.suction.laser_length", laser.Groups[1].Value, first);
                    Set(output, "skill.suction.laser_width", laser.Groups[2].Value, first);
                    Set(output, "skill.suction.laser_duration", laser.Groups[3].Value, first);
                }
                break;
            case 107:
                foreach (SkillRow row in rows) Set(output, "skill.tube.magnet_lv" + row.Level, row.Effect, row);
                break;
            case 1008:
                foreach (SkillRow row in rows)
                {
                    Set(output, "skill.ablation.count_lv" + row.Level, row.Count, row);
                    Set(output, "skill.ablation.damage_lv" + row.Level, row.Damage, row);
                }
                Set(output, "skill.ablation.interval", first.Cooldown, first);
                Set(output, "skill.ablation.blast_radius", first.Area, first);
                break;
            case 1010:
                foreach (SkillRow row in rows)
                {
                    Set(output, "skill.needle.interval_lv" + row.Level, row.TimeBetween, row);
                    Set(output, "skill.needle.damage_lv" + row.Level, row.Damage, row);
                    Set(output, "skill.needle.count_lv" + row.Level, row.Count, row);
                    Set(output, "skill.needle.speed_lv" + row.Level, row.Speed, row);
                    if (row.Level == 3)
                    {
                        Match pierce = Regex.Match(row.Special, @"穿透(\d+)");
                        if (pierce.Success) Set(output, "skill.needle.pierce", pierce.Groups[1].Value, row);
                    }
                    if (row.Level == 5)
                    {
                        Match beam = Regex.Match(row.Special, @"长([\d.]+)[-~至]([\d.]+)宽([\d.]+)");
                        if (beam.Success)
                        {
                            Set(output, "skill.needle.beam_length_min", beam.Groups[1].Value, row);
                            Set(output, "skill.needle.beam_length_max", beam.Groups[2].Value, row);
                            Set(output, "skill.needle.beam_width", beam.Groups[3].Value, row);
                        }
                    }
                }
                Set(output, "skill.needle.lifetime", first.Duration, first);
                Set(output, "skill.needle.spread", first.Angle, first);
                break;
            case 109:
                foreach (SkillRow row in rows) Set(output, "skill.heart.mult_lv" + row.Level, row.Effect, row);
                break;
            case 203:
                Match blood = Regex.Match(first.Description, @"概率([\d.]+)回([\d.]+)%");
                if (blood.Success)
                {
                    Set(output, "skill.bloodforceps.chance", blood.Groups[1].Value, first);
                    Set(output, "skill.bloodforceps.heal_percent",
                        Format(Parse(blood.Groups[2].Value) / 100.0), first);
                }
                break;
        }
    }

    static void Set(SortedDictionary<string, Entry> output, string key, string value, SkillRow row)
    {
        if (string.IsNullOrEmpty(value)) return;
        string category = string.IsNullOrEmpty(row.Name) ? "技能" : "技能-" + row.Name;
        string description = string.IsNullOrEmpty(row.Description) ? row.Special : row.Description;
        output[key] = new Entry
        {
            Category = category,
            Value = NormalizeNumber(value),
            Description = description
        };
    }

    static string Get(List<string> row, int index)
    {
        return row != null && index >= 0 && index < row.Count ? (row[index] ?? "") : "";
    }

    static double Parse(string raw)
    {
        return double.TryParse((raw ?? "").Trim(), NumberStyles.Float,
            CultureInfo.InvariantCulture, out double value) ? value : 0.0;
    }

    static string Abs(string raw)
    {
        return Format(Math.Abs(Parse(raw)));
    }

    static string Format(double value)
    {
        if (value == Math.Truncate(value) && value >= long.MinValue && value <= long.MaxValue)
            return ((long)value).ToString(CultureInfo.InvariantCulture);
        return value.ToString("0.############", CultureInfo.InvariantCulture);
    }

    static string NormalizeNumber(string raw)
    {
        raw = (raw ?? "").Trim();
        if (double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
            return Format(value);
        return raw;
    }

    static string CsvField(string value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        bool needsQuotes = value.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0;
        if (!needsQuotes) return value;
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }

    static string ProjectRoot()
    {
        return Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
    }

    static string HashFile(string path)
    {
        using (FileStream stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        using (SHA256 sha = SHA256.Create())
        {
            byte[] hash = sha.ComputeHash(stream);
            StringBuilder builder = new StringBuilder(hash.Length * 2);
            for (int i = 0; i < hash.Length; i++) builder.Append(hash[i].ToString("x2"));
            return builder.ToString();
        }
    }

    static string ReadState(string root)
    {
        string path = Path.Combine(root, StateRelativePath);
        try { return File.Exists(path) ? File.ReadAllText(path).Trim() : ""; }
        catch { return ""; }
    }

    static void WriteState(string root, string hash)
    {
        string path = Path.Combine(root, StateRelativePath);
        try
        {
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            File.WriteAllText(path, hash);
        }
        catch (Exception exception)
        {
            Debug.LogWarning("[SkillExcelSync] Cannot write state file: " + exception.Message);
        }
    }

    class SkillRow
    {
        public int Id;
        public int Level;
        public string Name;
        public string Damage;
        public string Duration;
        public string TimeBetween;
        public string Cooldown;
        public string Count;
        public string Area;
        public string Speed;
        public string Angle;
        public string Effect;
        public string Special;
        public string Description;
    }

    class Entry
    {
        public string Category;
        public string Value;
        public string Description;
    }
}
#endif
