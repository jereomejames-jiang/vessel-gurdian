#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

// Excel -> Resources/balance.csv sync for the main balance workbook.
//
// Source:  Doc/数值设计/balance.xlsx
// Target:  Assets/Resources/balance.csv
// Mirror:  Doc/数值设计/balance.csv
//
// It checks on domain reload (compile), before entering Play Mode, and while the
// editor is idle. The workbook is authoritative: a content-hash change exports;
// an unchanged workbook is skipped.
[InitializeOnLoad]
public static class BalanceExcelSync
{
    const string WorkbookRelativePath = "Doc/数值设计/balance.xlsx";
    const string CsvRelativePath = "Assets/Resources/balance.csv";
    const string MirrorRelativePath = "Doc/数值设计/balance.csv";
    const string StateRelativePath = "Library/BalanceExcelSync.state";
    const string CsvHeader = "分类,键名,数值,说明";

    static double nextTick;

    static BalanceExcelSync()
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
        // Make sure a just-saved workbook is exported before the run starts.
        if (state == PlayModeStateChange.ExitingEditMode) CheckAndExport(false);
    }

    [MenuItem("Tools/Balance/Export Excel to CSV")]
    static void ExportMenu()
    {
        CheckAndExport(true);
    }

    [MenuItem("Tools/Balance/Reveal Balance Workbook")]
    static void RevealWorkbook()
    {
        string path = Path.Combine(ProjectRoot(), WorkbookRelativePath);
        if (!File.Exists(path)) { Debug.LogWarning("[BalanceExcelSync] Workbook not found: " + path); return; }
        EditorUtility.RevealInFinder(path);
    }

    public static void CheckAndExport(bool force)
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogWarning("[BalanceExcelSync] Exit Play Mode before exporting the balance workbook.");
            return;
        }
        string root = ProjectRoot();
        string workbook = Path.Combine(root, WorkbookRelativePath);
        string csv = Path.Combine(root, CsvRelativePath);
        if (!File.Exists(workbook))
        {
            if (force) Debug.LogWarning("[BalanceExcelSync] Workbook not found: " + workbook);
            return;
        }

        string hash;
        try { hash = HashFile(workbook); }
        catch (Exception exception)
        {
            Debug.LogWarning("[BalanceExcelSync] Cannot read workbook: " + exception.Message);
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
                Debug.LogWarning("[BalanceExcelSync] Runtime CSV updated, but the design mirror failed: " +
                    mirrorException.Message);
            }
            AssetDatabase.ImportAsset(CsvRelativePath, ImportAssetOptions.ForceUpdate);
            WriteState(root, hash);
            Debug.Log("[BalanceExcelSync] Exported " + rows + " balance rows from " +
                WorkbookRelativePath + " to " + CsvRelativePath);
        }
        catch (Exception exception)
        {
            // Keep the previous CSV intact: ConvertTo writes a temp file first.
            Debug.LogError("[BalanceExcelSync] Export failed: " + exception);
        }
    }

    // Public for batch-mode/manual verification.
    public static int ConvertTo(string workbookPath, string csvPath)
    {
        List<string> output = new List<string> { CsvHeader };
        List<XlsxSheet> sheets = XlsxTable.Read(workbookPath);
        foreach (XlsxSheet sheet in sheets)
        {
            List<List<string>> rows = sheet.Rows;
            if (!FindHeader(rows, out int headerRow, out int categoryColumn,
                    out int keyColumn, out int valueColumn, out int descriptionColumn))
                continue;

            for (int rowIndex = headerRow + 1; rowIndex < rows.Count; rowIndex++)
            {
                List<string> row = rows[rowIndex];
                string key = Get(row, keyColumn).Trim();
                if (key.Length == 0) continue;
                string category = categoryColumn >= 0 ? Get(row, categoryColumn).Trim() : "";
                string value = NormalizeValue(Get(row, valueColumn));
                string description = descriptionColumn >= 0 ? Get(row, descriptionColumn).Trim() : "";
                description = description.Replace("\r", " ").Replace("\n", " ");
                output.Add(CsvField(category) + "," + CsvField(key) + "," +
                    CsvField(value) + "," + CsvField(description));
            }
        }

        if (output.Count <= 1)
            throw new InvalidDataException("No sheets with 键名/数值/分类/说明 were found.");

        // Write outside Assets, then copy over the old CSV only after success.
        string tempPath = Path.Combine(Path.GetTempPath(),
            "balance_sync_" + Guid.NewGuid().ToString("N") + ".csv");
        try
        {
            File.WriteAllText(tempPath, string.Join("\n", output) + "\n", new UTF8Encoding(true));
            string directory = Path.GetDirectoryName(csvPath);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            File.Copy(tempPath, csvPath, true);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
        return output.Count - 1;
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
            Debug.LogWarning("[BalanceExcelSync] Cannot write state file: " + exception.Message);
        }
    }

    static bool FindHeader(List<List<string>> rows, out int headerRow, out int categoryColumn,
        out int keyColumn, out int valueColumn, out int descriptionColumn)
    {
        for (int rowIndex = 0; rowIndex < Math.Min(rows.Count, 20); rowIndex++)
        {
            List<string> row = rows[rowIndex];
            keyColumn = -1;
            categoryColumn = -1;
            valueColumn = -1;
            descriptionColumn = -1;
            for (int column = 0; column < row.Count; column++)
            {
                string text = row[column].Trim();
                if (text == "键名") keyColumn = column;
                else if (text == "分类") categoryColumn = column;
                else if (text == "数值") valueColumn = column;
                else if (text == "说明") descriptionColumn = column;
            }
            if (keyColumn >= 0 && valueColumn >= 0)
            {
                headerRow = rowIndex;
                return true;
            }
        }
        headerRow = -1;
        categoryColumn = keyColumn = valueColumn = descriptionColumn = -1;
        return false;
    }

    static string Get(List<string> row, int column)
    {
        return row != null && column >= 0 && column < row.Count ? row[column] : "";
    }

    static string NormalizeValue(string raw)
    {
        raw = (raw ?? "").Trim();
        if (raw.Length == 0) return "";
        if (decimal.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out decimal number))
        {
            if (number == decimal.Truncate(number) && number >= long.MinValue && number <= long.MaxValue)
                return ((long)number).ToString(CultureInfo.InvariantCulture);
            return number.ToString("0.############", CultureInfo.InvariantCulture);
        }
        return raw;
    }
}

// Reload the runtime cache as soon as either generated CSV is imported, so the
// next Play session sees the workbook values even when domain reload is disabled.
class BalanceCsvWatcher : AssetPostprocessor
{
    static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets,
        string[] movedAssets, string[] movedFromAssetPaths)
    {
        for (int i = 0; i < importedAssets.Length; i++)
        {
            if (importedAssets[i] == "Assets/Resources/balance.csv" ||
                importedAssets[i] == "Assets/Resources/skill_balance.csv")
            {
                GameBalance.Reload();
                return;
            }
        }
    }
}
#endif
