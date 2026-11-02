from pathlib import Path
p = Path("Assets/Resources/balance.csv")
text = p.read_text(encoding="utf-8-sig")
lines = text.splitlines()

def key_of(line):
    parts = line.split(",")
    return parts[1].strip() if len(parts) >= 3 else None

def find(key):
    for i, line in enumerate(lines):
        if key_of(line) == key:
            return i
    raise KeyError(key)

def set_row(key, value, desc=None):
    i = find(key)
    parts = lines[i].split(",", 3)
    parts[2] = str(value)
    if desc is not None:
        parts[3] = desc
    lines[i] = ",".join(parts)

# D and enemy HP share the same x10 scale so basic-attack pacing is preserved.
set_row("attack.bullet_damage", 10, "基础子弹伤害D(统一×10口径)")
set_row("doctor.bonus_damage", 10, "每次攻击伤害=D+此值(×10口径)")
set_row("nurse.missile_damage_add", 20, "R4特殊导弹伤害附加值(×10口径)")
set_row("pharmacist.blast_damage_add", 10, "安瓿爆炸伤害=D+此值(×10口径)")

for key, value in {
    "enemy.cell.hp_base": 10,
    "enemy.cell.hp_per_stage": 0,
    "enemy.spike.hp_base": 30,
    "enemy.spike.hp_per_stage": 10,
    "enemy.ruptured.hp_base": 40,
    "enemy.ruptured.hp_per_stage": 20,
    "enemy.thrombus.hp_base": 80,
    "enemy.thrombus.hp_per_stage": 30,
    "enemy.bleeding.hp_base": 120,
    "enemy.bleeding.hp_per_stage": 30,
    "enemy.boss.hp_base": 340,
    "enemy.boss.hp_per_stage": 120,
}.items():
    set_row(key, value)

p.write_text("\ufeff" + "\n".join(lines) + "\n", encoding="utf-8")
print("updated x10 balance rows")
