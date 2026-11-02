import csv, re, shutil
from copy import copy
from pathlib import Path
from openpyxl import load_workbook

root = Path('/Users/hahaha/Documents/GitHub/vessel-gurdian')
xlsx = root / 'Doc/数值设计/balance.xlsx'
csv_path = root / 'Assets/Resources/balance.csv'
backup = root / '_analysis/balance_before_autosync.xlsx'
if not backup.exists():
    shutil.copy2(xlsx, backup)

# Runtime CSV is the current, code-tested truth.
rows = []
with csv_path.open(encoding='utf-8-sig', newline='') as f:
    reader = csv.reader(f)
    next(reader, None)
    for row in reader:
        if len(row) < 3:
            continue
        rows.append((row[0].strip(), row[1].strip(), row[2].strip(), ','.join(row[3:]).strip()))
runtime = {r[1]: r for r in rows}

def parse_value(text):
    try:
        f = float(text)
        return int(f) if f == int(f) else f
    except ValueError:
        return text

def target_sheet(cat, key):
    if key.startswith('player.'):
        return '1.基础'
    if key.startswith('game.'):
        return '5.刷怪与难度'
    if key.startswith('skill.'):
        return '3.技能'
    if key.startswith('enemy.') or key.startswith('boss.'):
        return '4.敌人'
    if key.startswith(('character.', 'doctor.', 'nurse.', 'pharmacist.')):
        return '2.角色'
    return '1.基础'

def chinese_name(key):
    manual = {
        'player.max_hp_cap': '最大生命硬上限',
        'game.evolution_recipe_guarantee': '进化配方保底',
        'skill.rinse.hit_interval': '同一敌人碰撞伤害间隔',
        'skill.needle.lifetime': '弹体持续时间',
        'skill.needle.spread': '双针散射角',
        'skill.needle.pierce': '穿透敌人数',
        'skill.needle.missile_every': '导弹触发间隔(齐射次数)',
        'skill.needle.beam_every': '药流光束触发间隔(齐射次数)',
        'skill.needle.beam_length_min': '药流光束长度下限',
        'skill.needle.beam_length_max': '药流光束长度上限',
        'skill.needle.beam_width': '药流光束宽度',
        'skill.needle.beam_duration': '药流光束持续时间',
        'skill.bloodforceps.chance': '进化吸血触发概率',
        'skill.bloodforceps.heal_percent': '回复最大生命比例',
        'skill.bloodforceps.shield_cap_percent': '溢出护盾上限比例',
    }
    if key in manual:
        return manual[key]
    patterns = [
        (r'skill\.stent\.mult_lv(\d+)', 'Lv{}受伤倍率'),
        (r'skill\.rinse\.damage_lv(\d+)', 'Lv{}碰撞伤害'),
        (r'skill\.rinse\.angular_lv(\d+)', 'Lv{}环绕角速度'),
        (r'skill\.suture\.count_lv(\d+)', 'Lv{}每次攻击目标数'),
        (r'skill\.suture\.damage_lv(\d+)', 'Lv{}缝线伤害'),
        (r'skill\.forceps\.chance_lv(\d+)', 'Lv{}吸血概率'),
        (r'skill\.forceps\.heal_lv(\d+)', 'Lv{}吸血回复最大生命比例'),
        (r'skill\.coil\.slow_lv(\d+)', 'Lv{}移速倍率'),
        (r'skill\.tube\.magnet_lv(\d+)', 'Lv{}磁吸范围倍率'),
        (r'skill\.ablation\.count_lv(\d+)', 'Lv{}每次投掷数量'),
        (r'skill\.ablation\.damage_lv(\d+)', 'Lv{}爆炸伤害'),
        (r'skill\.needle\.interval_lv(\d+)', 'Lv{}发射间隔'),
        (r'skill\.needle\.damage_lv(\d+)', 'Lv{}针头伤害'),
        (r'skill\.needle\.speed_lv(\d+)', 'Lv{}弹体速度'),
        (r'skill\.heart\.mult_lv(\d+)', 'Lv{}最大生命倍率'),
    ]
    for pattern, label in patterns:
        m = re.fullmatch(pattern, key)
        if m:
            return label.format(m.group(1))
    return key

wb = load_workbook(xlsx, data_only=False)
# Locate each data row by key.
located = {}
for ws in wb.worksheets:
    if ws.max_row < 1:
        continue
    headers = [ws.cell(1, c).value for c in range(1, min(8, ws.max_column) + 1)]
    if '键名' not in headers:
        continue
    key_col = headers.index('键名') + 1
    val_col = headers.index('数值') + 1
    cat_col = headers.index('分类') + 1 if '分类' in headers else 1
    desc_col = headers.index('说明') + 1 if '说明' in headers else 5
    for r in range(2, ws.max_row + 1):
        key = ws.cell(r, key_col).value
        if key is None:
            continue
        key = str(key).strip()
        if key:
            located[key] = (ws, r, cat_col, val_col, desc_col)

updated = 0
missing = []
for key, (cat, _, value, desc) in runtime.items():
    if key in located:
        ws, r, cat_col, val_col, desc_col = located[key]
        ws.cell(r, cat_col).value = cat
        ws.cell(r, val_col).value = parse_value(value)
        ws.cell(r, desc_col).value = desc
        updated += 1
    else:
        missing.append(key)

appended = 0
for key in missing:
    cat, _, value, desc = runtime[key]
    ws = wb[target_sheet(cat, key)]
    r = ws.max_row + 1
    # copy style from the last data row, A-E only
    for c in range(1, 6):
        src = ws.cell(max(2, ws.max_row), c)
        ws.cell(r, c)._style = copy(src._style)
    ws.cell(r, 1).value = cat
    ws.cell(r, 2).value = chinese_name(key)
    ws.cell(r, 3).value = key
    ws.cell(r, 4).value = parse_value(value)
    ws.cell(r, 5).value = desc
    appended += 1
    located[key] = (ws, r, 1, 4, 5)

# Update the instructions sheet to describe the Unity auto-export.
for ws in wb.worksheets:
    for row in ws.iter_rows():
        for cell in row:
            if isinstance(cell.value, str) and '一键导出' in cell.value:
                cell.value = ('改法：只改各数据表的「数值」列（D 列）；'
                              '保存后 Unity 编译/刷新会自动生成 Assets/Resources/balance.csv。')
            elif isinstance(cell.value, str) and 'export_balance.py' in cell.value:
                cell.value = ('改法：只改各数据表的「数值」列（D 列）；'
                              '保存后 Unity 编译/刷新会自动生成 Assets/Resources/balance.csv。')

wb.save(xlsx)
print(f'updated={updated} appended={appended} missing={len(missing)}')
print('missing sample:', missing[:10])
