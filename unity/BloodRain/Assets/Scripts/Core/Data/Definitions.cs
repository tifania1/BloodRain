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
        /// <summary>지역 고유 적이면 그 지역의 장 이름(cheonha.json regions[].chapter: chapter1 …). 공통 적은 null.
        /// 공통 적이 처음 나오는 시각은 chapters.json 흐름의 가중치표가 정한다.</summary>
        public string? Chapter;
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

    /// <summary>돌파 깨달음 한 장(insights.json). 효과 수치는 Params에 이름을 붙여 둔다.</summary>
    public sealed class InsightDef
    {
        public string Id = "";
        public string NameKo = "";
        /// <summary>한자 이름. 한글 이름과 글자 수가 같다.</summary>
        public string Hanja = "";
        /// <summary>카드 아이콘에 크게 쓰는 한 글자. Hanja 안의 글자다.</summary>
        public string Glyph = "";
        public string TextKo = "";
        /// <summary>효과 종류. 실행하는 쪽(Unity)이 이 값으로 갈라 처리한다.</summary>
        public string Effect = "";
        public Dictionary<string, float> Params = new Dictionary<string, float>();
        /// <summary>효과가 걸리는 무공 behavior(이기어검처럼 일부 무공에만 걸릴 때). 비어 있으면 해당 없음.</summary>
        public List<string> WeaponBehaviors = new List<string>();

        public float Param(string key)
        {
            if (!Params.TryGetValue(key, out var v))
                throw new KeyNotFoundException($"깨달음 {Id} 에 {key} 값이 없습니다");
            return v;
        }
    }

    /// <summary>돌파 때 보여 주는 깨달음 수. 특성(trait) 이름으로 따로 정한 캐릭터만 다르다.</summary>
    public sealed class InsightChoicesDef
    {
        public int Base = 3;
        public Dictionary<string, int> ByTrait = new Dictionary<string, int>();
        /// <summary>balance.json realms reward 안에서 깨달음 선택을 뜻하는 말.</summary>
        public string RealmReward = "insight_choice";
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
