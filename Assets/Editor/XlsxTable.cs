#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml.Linq;

// Minimal XLSX reader for the editor-only balance pipelines. It reads cached cell
// values (never evaluates formulas) and returns rows in workbook order.
public class XlsxSheet
{
    public string Name;
    public List<List<string>> Rows = new List<List<string>>();
}

public static class XlsxTable
{
    public static List<XlsxSheet> Read(string path)
    {
        List<XlsxSheet> sheets = new List<XlsxSheet>();
        using (FileStream stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        using (ZipArchive archive = new ZipArchive(stream, ZipArchiveMode.Read, false, Encoding.UTF8))
        {
            List<string> sharedStrings = ReadSharedStrings(archive);
            foreach (SheetRef reference in ReadSheetRefs(archive))
            {
                XDocument document = LoadXml(archive, reference.Path);
                if (document == null) continue;
                sheets.Add(new XlsxSheet
                {
                    Name = reference.Name,
                    Rows = ReadRows(document, sharedStrings)
                });
            }
        }
        return sheets;
    }

    static List<string> ReadSharedStrings(ZipArchive archive)
    {
        List<string> values = new List<string>();
        XDocument document = LoadXml(archive, "xl/sharedStrings.xml");
        if (document == null) return values;
        foreach (XElement item in document.Descendants().Where(element => element.Name.LocalName == "si"))
        {
            StringBuilder text = new StringBuilder();
            foreach (XElement part in item.Descendants().Where(element => element.Name.LocalName == "t"))
                text.Append(part.Value);
            values.Add(text.ToString());
        }
        return values;
    }

    static List<SheetRef> ReadSheetRefs(ZipArchive archive)
    {
        List<SheetRef> sheets = new List<SheetRef>();
        XDocument workbook = LoadXml(archive, "xl/workbook.xml");
        if (workbook == null) return sheets;
        XDocument relationships = LoadXml(archive, "xl/_rels/workbook.xml.rels");
        Dictionary<string, string> targets = new Dictionary<string, string>();
        if (relationships != null)
        {
            foreach (XElement relationship in relationships.Descendants()
                         .Where(element => element.Name.LocalName == "Relationship"))
            {
                string id = relationship.Attribute("Id")?.Value;
                string target = relationship.Attribute("Target")?.Value;
                if (!string.IsNullOrEmpty(id) && !string.IsNullOrEmpty(target))
                    targets[id] = NormalizeZipPath(target);
            }
        }

        XNamespace relationshipNamespace =
            "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        foreach (XElement sheet in workbook.Descendants().Where(element => element.Name.LocalName == "sheet"))
        {
            string name = sheet.Attribute("name")?.Value ?? "";
            string id = sheet.Attribute(relationshipNamespace + "id")?.Value ?? "";
            if (targets.TryGetValue(id, out string path))
                sheets.Add(new SheetRef { Name = name, Path = path });
        }
        return sheets;
    }

    static string NormalizeZipPath(string target)
    {
        string combined = target.StartsWith("/", StringComparison.Ordinal)
            ? target.TrimStart('/')
            : "xl/" + target;
        List<string> parts = new List<string>();
        foreach (string part in combined.Split('/'))
        {
            if (part.Length == 0 || part == ".") continue;
            if (part == "..")
            {
                if (parts.Count > 0) parts.RemoveAt(parts.Count - 1);
                continue;
            }
            parts.Add(part);
        }
        return string.Join("/", parts);
    }

    static XDocument LoadXml(ZipArchive archive, string path)
    {
        ZipArchiveEntry entry = archive.GetEntry(path);
        if (entry == null) return null;
        using (Stream stream = entry.Open()) return XDocument.Load(stream);
    }

    static List<List<string>> ReadRows(XDocument document, List<string> sharedStrings)
    {
        List<List<string>> rows = new List<List<string>>();
        foreach (XElement rowElement in document.Descendants().Where(element => element.Name.LocalName == "row"))
        {
            List<string> row = new List<string>();
            foreach (XElement cell in rowElement.Elements().Where(element => element.Name.LocalName == "c"))
            {
                string reference = cell.Attribute("r")?.Value ?? "";
                int column = ColumnIndex(reference);
                if (column < 0) continue;
                while (row.Count <= column) row.Add("");
                row[column] = CellText(cell, sharedStrings);
            }
            rows.Add(row);
        }
        return rows;
    }

    static string CellText(XElement cell, List<string> sharedStrings)
    {
        string type = cell.Attribute("t")?.Value ?? "";
        if (type == "s")
        {
            string raw = cell.Elements().FirstOrDefault(element => element.Name.LocalName == "v")?.Value;
            if (int.TryParse(raw, out int index) && index >= 0 && index < sharedStrings.Count)
                return sharedStrings[index];
            return "";
        }
        if (type == "inlineStr")
        {
            StringBuilder inline = new StringBuilder();
            foreach (XElement part in cell.Descendants().Where(element => element.Name.LocalName == "t"))
                inline.Append(part.Value);
            return inline.ToString();
        }
        if (type == "b")
            return cell.Elements().FirstOrDefault(element => element.Name.LocalName == "v")?.Value == "1" ? "1" : "0";
        return cell.Elements().FirstOrDefault(element => element.Name.LocalName == "v")?.Value ?? "";
    }

    static int ColumnIndex(string reference)
    {
        if (string.IsNullOrEmpty(reference)) return -1;
        int column = 0;
        bool found = false;
        for (int i = 0; i < reference.Length; i++)
        {
            char character = reference[i];
            if (character >= 'A' && character <= 'Z')
            {
                column = column * 26 + (character - 'A' + 1);
                found = true;
            }
            else if (character >= 'a' && character <= 'z')
            {
                column = column * 26 + (character - 'a' + 1);
                found = true;
            }
            else break;
        }
        return found ? column - 1 : -1;
    }

    class SheetRef
    {
        public string Name;
        public string Path;
    }
}
#endif
