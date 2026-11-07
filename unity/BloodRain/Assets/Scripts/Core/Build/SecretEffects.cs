#nullable enable
// 비전을 찾은 뒤 얻는 것(기획서 20장 비전 8 표 '얻는 것', synergies.json secrets[].effect·target·params).
//   판정(언제 열리나)은 Build/Secrets.cs, 열린 뒤 무엇이 바뀌나는 여기. SecretChecker.Unlocked 가 바뀔 때마다
//   SecretEffects.Of(data, checker.Unlocked) 로 새로 만든다. 비전은 판마다 새로 채워야 켜진다.
//
//   plum_branch_sword  낙매검선의 피  target 낙화검우가 낙매일검으로, 오의 일검이 ultimateBends 번 꺾여 나감(길은 Combat/UltimateEffects.PlumBranchPath)
//   jiangshi_thrall    어강술        멈춘 강시를 사슬로 끌어오면 seconds 초 동안 내 편, 최대 maxAllies
//   plum_storm         매화삼롱      target 음공 음파 everyWaves 번째마다 화면 가득 매화 폭풍(크기·피해는 기획서에 없음)
//   boundless          무극          음양 게이지가 사라지고 모든 무공이 음·양 보너스 aspectDamageBonus 를 함께 받음
//   drunken_step       취선          음식마다 공력 +mightPerFood 누적, 공격을 evadeChance 확률로 흘림
//   no_backlash        역천          그 판 동안 기혈역류가 다시 오지 않음(Combat.Backlash.Immune), 극성 보너스는 그대로
//   blood_whisper      혈해마공 잔편 그 판에 '혈마의 속삭임'이 반드시 나타남(받은 뒤 무엇이 바뀌나는 WP-29a 결정 대기)
//   nameless_sword     무명검        target 삼재검법 피해 +damageBonus, 오의 충전 × ultimateChargeMultiplier. 뒤에 무공을 배워도 유지
using System.Collections.Generic;
using System.Linq;
using BloodRain.Core.Data;

namespace BloodRain.Core.Build
{
    public sealed class SecretEffects
    {
        /// <summary>코드가 읽는 effect 이름. 데이터에 이 밖의 이름이 있으면 테스트가 잡는다.</summary>
        public static readonly IReadOnlyList<string> Known = new[]
        {
            "plum_branch_sword", "jiangshi_thrall", "plum_storm", "boundless",
            "drunken_step", "no_backlash", "blood_whisper", "nameless_sword",
        };

        readonly Dictionary<string, SecretDef> _active = new Dictionary<string, SecretDef>();

        SecretEffects() { }

        public static readonly SecretEffects None = new SecretEffects();

        /// <summary>열린 비전 id 들로 만든다(SecretChecker.Unlocked). 모르는 id 는 건너뛴다.</summary>
        public static SecretEffects Of(GameData data, IEnumerable<string> unlockedIds)
        {
            var e = new SecretEffects();
            var ids = new HashSet<string>(unlockedIds);
            foreach (var s in data.Synergies.Secrets)
                if (ids.Contains(s.Id) && s.Effect.Length > 0) e._active[s.Effect] = s;
            return e;
        }

        public bool Has(string effect) => _active.ContainsKey(effect);
        SecretDef? Find(string effect) => _active.TryGetValue(effect, out var s) ? s : null;

        /// <summary>그 효과가 켜져 있으면 비전 정의(Param·Target), 아니면 null. 모양이 있는 효과는 Unity 가 이걸 읽어 그린다.</summary>
        public SecretDef? Secret(string effect) => Find(effect);

        /// <summary>무공 한 방 피해에 곱하는 값(무명검: 삼재검법 +50%).</summary>
        public float WeaponDamageMultiplier(string weaponId)
        {
            var n = Find("nameless_sword");
            return n != null && n.Target == weaponId ? 1f + n.Param("damageBonus") : 1f;
        }

        /// <summary>오의 충전에 곱하는 값(무명검 2배).</summary>
        public float UltimateChargeMultiplier => Find("nameless_sword") is SecretDef n ? n.Param("ultimateChargeMultiplier") : 1f;

        /// <summary>기혈역류가 다시 오지 않는가(역천). 켜지면 Backlash.Immune = true.</summary>
        public bool NoBacklash => Has("no_backlash");

        /// <summary>음양 게이지를 숨기고 모든 무공이 음·양 보너스를 함께 받는가(무극).</summary>
        public bool GaugeRemoved => Has("boundless");

        /// <summary>
        /// 그 무공 속성 보너스(편향·극성 셈 대신 쓸 값). 무극이면 음·양 무공 모두 aspectDamageBonus, 아니면 null(평소 셈 그대로).
        /// 속성 없는 무공(Neutral)은 무극이어도 받지 않는다 — 표의 '음·양 보너스'가 원래 속성 무공에만 붙는 값이라.
        /// </summary>
        public float? AspectDamageBonus(YinYang weaponAspect)
        {
            var b = Find("boundless");
            if (b == null) return null;
            return weaponAspect == YinYang.Neutral ? 0f : b.Param("aspectDamageBonus");
        }

        /// <summary>취선을 얻은 뒤 먹은 음식 수로 더할 공력 비율(+3%씩 쌓임). 취선이 없으면 0.</summary>
        public float FoodMightBonus(int foodEatenSinceFound) =>
            Find("drunken_step") is SecretDef d && foodEatenSinceFound > 0 ? d.Param("mightPerFood") * foodEatenSinceFound : 0f;

        /// <summary>받는 공격을 흘릴 확률(취선 20%). 없으면 0.</summary>
        public float EvadeChance => Find("drunken_step") is SecretDef d ? d.Param("evadeChance") : 0f;

        /// <summary>흘릴지. roll 은 [0,1) 난수.</summary>
        public bool Evades(double roll) => roll < EvadeChance;

        /// <summary>어강술: 강시를 내 편으로 부리는 시간(초)과 최대 수. 없으면 null.</summary>
        public (float seconds, int max)? Thrall =>
            Find("jiangshi_thrall") is SecretDef t ? (t.Param("seconds"), (int)t.Param("maxAllies")) : ((float, int)?)null;

        /// <summary>어강술: 지금 부리는 수가 count 일 때 하나 더 부릴 수 있나.</summary>
        public bool CanTakeThrall(int count) => Thrall is (float, int) t && count < t.max;

        /// <summary>매화삼롱: 그 무공의 몇 번째 음파(1부터)가 매화 폭풍인가.</summary>
        public bool IsPlumStormWave(string weaponId, int waveIndex) =>
            Find("plum_storm") is SecretDef p && p.Target == weaponId && waveIndex > 0 && waveIndex % (int)p.Param("everyWaves") == 0;

        /// <summary>낙매검선의 피: 이 무공이 낙매일검으로 바뀌었나.</summary>
        public bool IsPlumBranchSword(string weaponId) => Find("plum_branch_sword") is SecretDef p && p.Target == weaponId;

        /// <summary>낙매검선의 피: 오의 일검이 꺾여 나가는 번 수. 없으면 0(곧게 한 번).</summary>
        public int UltimateBends => Find("plum_branch_sword") is SecretDef p ? (int)p.Param("ultimateBends") : 0;

        /// <summary>혈해마공 잔편: 그 판에 혈마의 속삭임이 반드시 나타나는가.</summary>
        public bool WhisperGuaranteed => Has("blood_whisper");
    }
}
