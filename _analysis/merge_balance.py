# Merge the workbook skill values into Assets/Resources/balance.csv without
# rewriting unrelated rows (the working tree already has text-only edits).
from pathlib import Path

p = Path("Assets/Resources/balance.csv")
raw = p.read_bytes()
text = raw.decode("utf-8-sig")
lines = text.splitlines()

def key_of(line):
    parts = line.split(",")
    return parts[1].strip() if len(parts) >= 3 else None

def find(key):
    for i, line in enumerate(lines):
        if key_of(line) == key:
            return i
    return -1

def set_row(key, value, desc=None):
    i = find(key)
    if i < 0:
        raise KeyError(key)
    parts = lines[i].split(",", 3)
    parts[2] = str(value)
    if desc is not None:
        parts[3] = desc
    lines[i] = ",".join(parts)

def remove_row(key):
    i = find(key)
    if i >= 0:
        del lines[i]

def insert_after(anchor, rows):
    i = find(anchor)
    if i < 0:
        raise KeyError(anchor)
    for offset, row in enumerate(rows, 1):
        lines.insert(i + offset, row)

def add_rows(anchor, rows):
    for row in rows:
        key = row.split(",")[1]
        if find(key) < 0:
            insert_after(anchor, [row])
            anchor = key

# Player cap (optional; 0 keeps the existing uncapped behavior).
add_rows("player.max_hp", ["玩家,player.max_hp_cap,0,最大生命硬上限(0=不限制)"])

# 101 Membrane Stent
set_row("skill.stent.mult_lv3", "0.65", "Lv3受伤倍率(减伤35%)")
add_rows("skill.stent.mult_lv3", [
    "技能-覆膜支架,skill.stent.mult_lv4,0.55,Lv4受伤倍率(减伤45%)",
    "技能-覆膜支架,skill.stent.mult_lv5,0.45,Lv5受伤倍率(减伤55%)",
])
set_row("skill.stent.shield", "60", "Lv3起临时护盾点数")

# 1001 Flush Needle
set_row("skill.rinse.radius_base", "1.5", "环绕半径(全等级1.5)")
set_row("skill.rinse.radius_per_level", "0", "环绕半径每级增加(表格为固定1.5)")
remove_row("skill.rinse.angular_base")
remove_row("skill.rinse.angular_per_level")
set_row("skill.rinse.knockback", "0.18", "击退力度(60Hz参考值;按物理步长换算,与帧率无关)")
add_rows("skill.rinse.knockback", [
    "技能-冲洗针,skill.rinse.hit_interval,0.5,同一敌人碰撞伤害间隔(秒)",
    "技能-冲洗针,skill.rinse.damage_lv1,5,Lv1碰撞伤害",
    "技能-冲洗针,skill.rinse.damage_lv2,10,Lv2碰撞伤害",
    "技能-冲洗针,skill.rinse.damage_lv3,10,Lv3碰撞伤害",
    "技能-冲洗针,skill.rinse.damage_lv4,15,Lv4碰撞伤害",
    "技能-冲洗针,skill.rinse.damage_lv5,20,Lv5碰撞伤害",
    "技能-冲洗针,skill.rinse.angular_lv1,200,Lv1环绕角速度(度/秒)",
    "技能-冲洗针,skill.rinse.angular_lv2,200,Lv2环绕角速度(度/秒)",
    "技能-冲洗针,skill.rinse.angular_lv3,200,Lv3环绕角速度(度/秒)",
    "技能-冲洗针,skill.rinse.angular_lv4,250,Lv4环绕角速度(度/秒)",
    "技能-冲洗针,skill.rinse.angular_lv5,300,Lv5环绕角速度(度/秒)",
])

# 1002 Dissection Scissors comments now cover Lv5.
set_row("skill.scissors.radius_lv2", "4.1", "Lv2-Lv5挥剪半径")
set_row("skill.scissors.angle_lv2", "100", "Lv2-Lv5挥剪角度(度)")
set_row("skill.scissors.cross_angle", "42", "Lv3起十字第二剪角度(度)")

# 1003 Sidewall Forceps
set_row("skill.forceps.chance_lv3", "0.3", "Lv3吸血概率")
remove_row("skill.forceps.heal_percent")
add_rows("skill.forceps.chance_lv3", [
    "技能-侧壁钳,skill.forceps.chance_lv4,0.4,Lv4吸血概率",
    "技能-侧壁钳,skill.forceps.chance_lv5,0.5,Lv5吸血概率",
    "技能-侧壁钳,skill.forceps.heal_lv1,0.05,Lv1吸血回复最大生命比例",
    "技能-侧壁钳,skill.forceps.heal_lv2,0.10,Lv2吸血回复最大生命比例",
    "技能-侧壁钳,skill.forceps.heal_lv3,0.15,Lv3吸血回复最大生命比例",
    "技能-侧壁钳,skill.forceps.heal_lv4,0.20,Lv4吸血回复最大生命比例",
    "技能-侧壁钳,skill.forceps.heal_lv5,0.30,Lv5吸血回复最大生命比例",
])

# 1004 Suture
remove_row("skill.suture.count_base")
add_rows("skill.scissors.cross_angle", [
    "技能-缝线,skill.suture.count_lv1,1,Lv1每次攻击目标数",
    "技能-缝线,skill.suture.count_lv2,2,Lv2每次攻击目标数",
    "技能-缝线,skill.suture.count_lv3,2,Lv3每次攻击目标数",
    "技能-缝线,skill.suture.count_lv4,3,Lv4每次攻击目标数",
    "技能-缝线,skill.suture.count_lv5,3,Lv5每次攻击目标数",
    "技能-缝线,skill.suture.damage_lv1,20,Lv1缝线伤害",
    "技能-缝线,skill.suture.damage_lv2,20,Lv2缝线伤害",
    "技能-缝线,skill.suture.damage_lv3,25,Lv3缝线伤害",
    "技能-缝线,skill.suture.damage_lv4,25,Lv4缝线伤害",
    "技能-缝线,skill.suture.damage_lv5,25,Lv5缝线伤害",
])

# 1005 Microscopic Hemostat
set_row("skill.clamp.radius_base", "1.5", "Lv1击杀区域半径")
set_row("skill.clamp.radius_per_level", "0.25", "每级区域半径增加")

# 1006 Spring Coil
add_rows("skill.coil.slow_lv3", [
    "技能-弹簧圈,skill.coil.slow_lv4,0.6,Lv4移速倍率(减速40%)",
    "技能-弹簧圈,skill.coil.slow_lv5,0.5,Lv5移速倍率(减速50%)",
])

# 107 Suction Tube
add_rows("skill.tube.magnet_lv3", [
    "技能-吸液管,skill.tube.magnet_lv4,1.4,Lv4磁吸范围倍率",
    "技能-吸液管,skill.tube.magnet_lv5,1.5,Lv5磁吸范围倍率",
])

# 1008 Ablation Bomb
set_row("skill.ablation.interval", "5", "投掷间隔(秒)")
add_rows("skill.ablation.count_lv3", [
    "技能-消融弹,skill.ablation.count_lv4,3,Lv4每次投掷数量",
    "技能-消融弹,skill.ablation.count_lv5,3,Lv5每次投掷数量",
])
remove_row("skill.ablation.damage_add")
remove_row("skill.ablation.damage_min")
add_rows("skill.ablation.blast_radius", [
    "技能-消融弹,skill.ablation.damage_lv1,15,Lv1爆炸伤害",
    "技能-消融弹,skill.ablation.damage_lv2,15,Lv2爆炸伤害",
    "技能-消融弹,skill.ablation.damage_lv3,15,Lv3爆炸伤害",
    "技能-消融弹,skill.ablation.damage_lv4,20,Lv4爆炸伤害",
    "技能-消融弹,skill.ablation.damage_lv5,20,Lv5爆炸伤害",
])

# 1010 Needle Bullet
add_rows("skill.ablation.damage_lv5", [
    "技能-针头子弹,skill.needle.interval_lv1,1,Lv1发射间隔(秒)",
    "技能-针头子弹,skill.needle.interval_lv2,0.5,Lv2发射间隔(秒)",
    "技能-针头子弹,skill.needle.interval_lv3,0.4,Lv3发射间隔(秒)",
    "技能-针头子弹,skill.needle.interval_lv4,0.35,Lv4发射间隔(秒)",
    "技能-针头子弹,skill.needle.interval_lv5,0.25,Lv5发射间隔(秒)",
    "技能-针头子弹,skill.needle.damage_lv1,10,Lv1针头伤害",
    "技能-针头子弹,skill.needle.damage_lv2,10,Lv2针头伤害",
    "技能-针头子弹,skill.needle.damage_lv3,15,Lv3针头伤害",
    "技能-针头子弹,skill.needle.damage_lv4,20,Lv4针头伤害",
    "技能-针头子弹,skill.needle.damage_lv5,20,Lv5针头伤害",
    "技能-针头子弹,skill.needle.speed_lv1,5,Lv1弹体速度",
    "技能-针头子弹,skill.needle.speed_lv2,6,Lv2弹体速度",
    "技能-针头子弹,skill.needle.speed_lv3,7,Lv3弹体速度",
    "技能-针头子弹,skill.needle.speed_lv4,8,Lv4弹体速度",
    "技能-针头子弹,skill.needle.speed_lv5,8,Lv5弹体速度",
    "技能-针头子弹,skill.needle.lifetime,5,弹体持续时间(秒)",
    "技能-针头子弹,skill.needle.spread,5,Lv2起双针散射角(±度)",
    "技能-针头子弹,skill.needle.pierce,2,Lv3起每发穿透敌人数",
    "技能-针头子弹,skill.needle.missile_every,4,Lv4导弹每N次齐射",
    "技能-针头子弹,skill.needle.beam_every,3,Lv5药流光束每N次齐射",
    "技能-针头子弹,skill.needle.beam_length_min,7,药流光束长度下限",
    "技能-针头子弹,skill.needle.beam_length_max,16,药流光束长度上限",
    "技能-针头子弹,skill.needle.beam_width,0.85,药流光束宽度",
    "技能-针头子弹,skill.needle.beam_duration,0.22,药流光束持续(秒)",
])

# 109 Heart Amulet
add_rows("skill.needle.beam_duration", [
    "技能-心形护符,skill.heart.mult_lv1,1.2,Lv1最大生命倍率",
    "技能-心形护符,skill.heart.mult_lv2,1.4,Lv2最大生命倍率",
    "技能-心形护符,skill.heart.mult_lv3,1.6,Lv3最大生命倍率",
    "技能-心形护符,skill.heart.mult_lv4,1.8,Lv4最大生命倍率",
    "技能-心形护符,skill.heart.mult_lv5,2.0,Lv5最大生命倍率",
])

# 203 Blood Recycle Forceps
add_rows("skill.heart.mult_lv5", [
    "技能-血液回收钳,skill.bloodforceps.chance,0.5,进化吸血触发概率",
    "技能-血液回收钳,skill.bloodforceps.heal_percent,0.08,回复最大生命比例(溢出转护盾)",
])

p.write_bytes(("\ufeff" + "\n".join(lines) + "\n").encode("utf-8"))
print("rows:", len(lines))
