#nullable enable
// 데이터 정의. Assets/Resources/Data/*.json 을 그대로 담는다.
// 이 폴더(Core)는 UnityEngine을 쓰지 않는다. dotnet 테스트(core/)가 같은 파일을 컴파일한다.
using System.Collections.Generic;

namespace BloodRain.Core.Data
{
    public enum YinYang { Neutral, Yang, Yin }

    public sealed class WeaponDef
    {
        public string Id = "";
        public string NameKo = "";
        public string Category = "";
        public string Yinyang = "neutral";
        public string Behavior = "";
        public float Range;
        public float DamageLv1;
        public float DamageLv8;
        public float Cooldown;
        public string PairPassive = "";
        public string Unlock = "start";

        public YinYang Aspect => YinYangParse.From(Yinyang);
    }

    public sealed class PassiveEffect
    {
        public string Stat = "";
        public float PerLevel;
        public Dictionary<string, float>? AtLevels;
    }

    public sealed class PassiveDef
    {
        public string Id = "";
        public string NameKo = "";
        public string Yinyang = "neutral";
        public int? YinyangPerLevelOverride;
        public List<PassiveEffect> Effects = new List<PassiveEffect>();
        public string PairWeapon = "";

        public YinYang Aspect => YinYangParse.From(Yinyang);
    }

    public sealed class EvolutionDef
    {
        public string Id = "";
        public string NameKo = "";
        /// <summary>각성 연출에 크게 쓰는 한자 이름(글자는 이미지에 굽지 않고 Unity에서 얹는다).</summary>
        public string Hanja = "";
        public string Weapon = "";
        public string Passive = "";
        public string Summary = "";
    }

    public sealed class CharacterDef
    {
        public string Id = "";
        public string NameKo = "";
        public string StartWeapon = "";
        public float MaxHealth = 100;
        public float MoveSpeed = 1;
        public int YinyangStart;
        public int? HarmonyRange;
        public string Ultimate = "";
        public string Trait = "";
        public string Unlock = "start";
    }

    public sealed class UltimateDef
    {
        public string Id = "";
        public string NameKo = "";
        public float MightMultiplier;
        public string Summary = "";
    }

    public sealed class EnemyDef
    {
        public string Id = "";
        public string NameKo = "";
        public string Family = "";
        public float Health;
        public float Speed;
        public float ContactDamage;
        public string Behavior = "";
        public float KnockbackResist;
    }

    public sealed class BossDef
    {
        public string Id = "";
        public string NameKo = "";
        public string Chapter = "";
        public float Health;
        public float TargetKillSeconds;
        public List<string> Patterns = new List<string>();
    }

    public static class YinYangParse
    {
        public static YinYang From(string s)
        {
            switch (s)
            {
                case "yang": return YinYang.Yang;
                case "yin": return YinYang.Yin;
                default: return YinYang.Neutral;
            }
        }
    }
}
