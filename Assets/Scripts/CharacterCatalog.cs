using UnityEngine;

public enum CharacterRole { Doctor, Nurse, Pharmacist }

// Keep the selection screen and runtime progression on the same definitions.
public static class CharacterCatalog
{
    public const string PreferenceKey = "VesselGuard.SelectedCharacter";
    // 设定集(3) §5 基础攻击升级路线 lv0–lv3，封顶 3 级。
    public const int MaxRank = 3;
    public static readonly string[] Names = { "医生", "护士", "药剂师" };
    public static readonly Color[] Colors = {
        new Color(0.25f, 0.85f, 1f), new Color(1f, 0.5f, 0.72f), new Color(0.38f, 0.9f, 0.55f)
    };
    public static readonly string[][] Routes = {
        new[] { "挥砍手术刀，造成扇形范围攻击", "扇形半径、角度小幅变大", "一次连续挥砍2次", "更改为电刀，扇形半径、角度小幅变大" },
        new[] { "手持注射器进行远程攻击，发射针头子弹", "攻速加快", "弹道＋1", "子弹穿透等级增加，可以一次穿透2个敌人" },
        new[] { "手持安瓿瓶往地上摔，形成一圈范围伤害环", "小幅强化自身属性，增加移速血量", "攻击频率提升", "攻击可以留下一滩可减速并对敌人造成持续伤害的药剂（同时只能存在2个）" }
    };

    public static CharacterRole SelectedRole => (CharacterRole)Mathf.Clamp(PlayerPrefs.GetInt(PreferenceKey, 0), 0, 2);
    public static int RankForLevel(int level) => Mathf.Clamp(level / Mathf.Max(1, GameBalance.GetInt("character.rank_per_levels", 3)), 0, GameBalance.GetInt("character.rank_max", MaxRank));
    public static string Name(CharacterRole role) => Names[Mathf.Clamp((int)role, 0, 2)];
    public static string Route(CharacterRole role, int rank) => Routes[Mathf.Clamp((int)role, 0, 2)][Mathf.Clamp(rank, 0, MaxRank)];
}
