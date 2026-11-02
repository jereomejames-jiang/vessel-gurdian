from openpyxl import load_workbook
p="/Users/hahaha/Desktop/技能数据.xlsx"
wb=load_workbook(p,data_only=False)
print("sheets",wb.sheetnames)
for ws in wb.worksheets:
    print("="*80)
    print("SHEET",ws.title,ws.max_row,ws.max_column)
    for row in ws.iter_rows():
        vals=[]
        for c in row:
            if c.value is not None:
                vals.append(f"{c.coordinate}={c.value!r}")
        if vals:
            print(" | ".join(vals))
