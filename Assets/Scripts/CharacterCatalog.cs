using UnityEngine;

public enum CharacterRole { Doctor, Nurse, Pharmacist }

// Keep the selection screen and runtime progression on the same definitions.
public static class CharacterCatalog
{
    public const string PreferenceKey = "VesselGuard.SelectedCharacter";
    public const int MaxRank = 5;
    public static readonly string[] Names = { "医生", "护士", "药剂师" };
    public static readonly Color[] Colors = {
        new Color(0.25f, 0.85f, 1f), new Color(1f, 0.5f, 0.72f), new Color(0.38f, 0.9f, 0.55f)
    };
    public static readonly string[][] Routes = {
        new[] { "挥砍电刀，造成扇形范围伤害", "扇形半径、角度小幅增加", "连续挥砍两次", "扇形半径、角度再次增加", "止血：周期性全屏减速", "双电刀剪合攻击，扇形角度大幅扩大" },
        new[] { "注射器发射针头，自动远程攻击", "针头攻速提升", "弹道 +1", "每枚针头可穿透并命中两个敌人", "周期性发射高伤害小范围导弹", "进化为持续药流，伤害路径上的所有敌人" },
        new[] { "投掷安瓿瓶，落地形成范围伤害环", "移速 +10%，最大生命 +1", "投掷频率提升", "落地留下减速、持续伤害药剂，最多两滩", "加大剂量：周期性全屏低伤害攻击", "攻击频率大幅提升，伤害环变为扩散涟漪" }
    };

    public static CharacterRole SelectedRole => (CharacterRole)Mathf.Clamp(PlayerPrefs.GetInt(PreferenceKey, 0), 0, 2);
    public static int RankForLevel(int level) => Mathf.Clamp(level / Mathf.Max(1, GameBalance.GetInt("character.rank_per_levels", 3)), 0, GameBalance.GetInt("character.rank_max", MaxRank));
    public static string Name(CharacterRole role) => Names[Mathf.Clamp((int)role, 0, 2)];
    public static string Route(CharacterRole role, int rank) => Routes[Mathf.Clamp((int)role, 0, 2)][Mathf.Clamp(rank, 0, MaxRank)];
}
