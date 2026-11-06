#nullable enable
// 기연 사건을 만났을 때 이번 생이 받는 것(기획서 20장 '기연 사건 8', synergies.json encounters).
// 무엇이 나올지 굴리는 것은 Spawning/EncounterRoller, 여기는 만난 뒤의 셈만 한다. 웹 한 생 판(WP-27~32)과 같은 규칙.
//   은거기인: 곁에 staySeconds(6초) 머물면 깨달음 하나를 더 고른다(고르는 화면은 Presentation, Insights.Roll 그대로).
//   고수의 유해: bowSeconds(1초) 멈춰 서면 그 땅 문파 진전 +schoolBonus.
//   영약: 오의 게이지 가득, 음양 0.
//   영물: 영약 효과 + 혈정(LifeLog.CatchSpiritBeast, 정예 하나 몫).
//   혈마의 속삭임: 받으면 마도(demonic) 진전 +schoolBonus, 몸이 받는 회복 × healingMultiplier. 혈공·계승 표식은 WP-29a 결정 대기라 세기만.
//   검총: 공격 무공 하나를 이번 생에서 버리고, 그 레벨 × mightPerLevel 을 남은 무공 공력에 더한다(여러 번 꽂으면 더함).
//   하오문 밀서: 고르는 셈은 LifeLog.ReadLetter(혼이 계정에 남김), 여기는 만났다는 표시만(Life.ReadLetter). 벼랑은 WP-32a 결정 대기.
using System;
using System.Collections.Generic;
using BloodRain.Core.Build;
using BloodRain.Core.Combat;
using BloodRain.Core.Data;
using BloodRain.Core.Meta;
using BloodRain.Core.Progression;

namespace BloodRain.Core.Run
{
    public sealed class EncounterBoons
    {
        public const string Hermit = "hermit";
        public const string Relic = "relic";
        public const string Elixir = "elixir";
        public const string SpiritBeast = "spiritbeast";
        public const string Whisper = "whisper";
        public const string SwordTomb = "sword_tomb";
        public const string Letter = "letter";
        /// <summary>혈마의 속삭임이 올려 주는 진전(synergies.json schools 의 마교).</summary>
        public const string WhisperSchool = "demonic";

        readonly SynergyDefs _syn;
        readonly Dictionary<string, int> _schools = new Dictionary<string, int>();
        readonly List<string> _met = new List<string>();

        public EncounterBoons(GameData data) { _syn = data.Synergies; }

        /// <summary>기연으로 더 가진 것으로 치는 진전(문파 id → 수). Rebirth.ComputeSynergy 의 more 로 넘긴다.</summary>
        public IReadOnlyDictionary<string, int> SchoolExtra => _schools;
        /// <summary>검총으로 남은 무공에 더하는 공력(0.15 = +15%).</summary>
        public float WeaponMightBonus { get; private set; }
        /// <summary>혈마의 속삭임을 받았나(계승 엔딩 표식은 Soul 이 셀 때 이 값을 본다).</summary>
        public bool WhisperTaken { get; private set; }
        /// <summary>몸이 받는 회복에 곱하는 수. 문파 고유 능력(마교 0.5)과는 따로 곱한다.</summary>
        public float HealingMultiplier => WhisperTaken ? _syn.Encounter(Whisper).Param("healingMultiplier") : 1f;
        /// <summary>이번 생에 받은 기연(받은 순서). 객잔 귀환 대사·도감이 본다.</summary>
        public IReadOnlyList<string> Met => _met;
        public bool HasMet(string id) => _met.Contains(id);

        void Note(string id) { if (!_met.Contains(id)) _met.Add(id); }

        /// <summary>하오문 밀서를 주웠다는 표시(객잔 귀환 대사 '밀서를 주운 뒤' 줄이 본다).</summary>
        public void NoteLetter() => Note(Letter);

        /// <summary>은거기인 곁에 머문 시간이 다 찼나.</summary>
        public bool HermitReady(float stayedSeconds) => stayedSeconds >= _syn.Encounter(Hermit).Param("staySeconds");

        /// <summary>은거기인을 다 만났다. 깨달음 하나를 더 고르게 하는 것은 Presentation(Insights.Roll → Life.TakeInsight).</summary>
        public void MeetHermit() => Note(Hermit);

        /// <summary>고수의 유해 앞에 멈춰 선 시간이 다 찼나.</summary>
        public bool RelicReady(float bowedSeconds) => bowedSeconds >= _syn.Encounter(Relic).Param("bowSeconds");

        /// <summary>고수의 유해에 예를 갖췄다. 그 땅 문파 진전을 하나 더 가진 것으로 친다.</summary>
        public void HonorRelic(string school)
        {
            if (string.IsNullOrEmpty(school)) throw new ArgumentException("고수의 유해는 그 땅 문파가 있어야 합니다", nameof(school));
            AddSchool(school, (int)_syn.Encounter(Relic).Param("schoolBonus"));
            Note(Relic);
        }

        /// <summary>영약: 오의 게이지 가득, 음양 0.</summary>
        public void TakeElixir(UltimateCharge charge, YinYangGauge gauge)
        {
            FillUp(charge, gauge);
            Note(Elixir);
        }

        /// <summary>영물을 잡았다: 영약 효과와 혈정. 늘어난 혈정을 돌려준다.</summary>
        public int CatchSpiritBeast(UltimateCharge charge, YinYangGauge gauge, LifeLog log)
        {
            FillUp(charge, gauge);
            Note(SpiritBeast);
            return log.CatchSpiritBeast();
        }

        /// <summary>혈마의 속삭임을 받았다(석상 앞에서 오의). 한 생에 한 번만 받는다 — 두 번째는 false.</summary>
        public bool AcceptWhisper()
        {
            if (WhisperTaken) return false;
            WhisperTaken = true;
            AddSchool(WhisperSchool, (int)_syn.Encounter(Whisper).Param("schoolBonus"));
            Note(Whisper);
            return true;
        }

        /// <summary>검총에 검을 꽂을 수 있나(무공이 둘 이상이고 그 무공을 가졌을 때).</summary>
        public static bool CanPlantSword(Loadout loadout, string weaponId) =>
            loadout.Weapons.Count > 1 && loadout.Weapons.ContainsKey(weaponId);

        /// <summary>검총에 무공 하나를 꽂는다. 이번 생에서 버리고 그 레벨 × mightPerLevel 을 남은 무공 공력에 더한다. 더한 몫을 돌려준다.</summary>
        public float PlantSword(Loadout loadout, string weaponId)
        {
            if (!CanPlantSword(loadout, weaponId)) throw new InvalidOperationException($"검총에 꽂을 수 없습니다: {weaponId}");
            int level = loadout.RemoveWeapon(weaponId);
            float bonus = level * _syn.Encounter(SwordTomb).Param("mightPerLevel");
            WeaponMightBonus += bonus;
            Note(SwordTomb);
            return bonus;
        }

        /// <summary>검총 공력을 능력치 보너스로(Life.CurrentStats 의 extra 에 더한다).</summary>
        public StatBonus StatBonus()
        {
            var b = new StatBonus();
            if (WeaponMightBonus > 0) b.Add(StatKind.Might, WeaponMightBonus);
            return b;
        }

        static void FillUp(UltimateCharge charge, YinYangGauge gauge)
        {
            charge.AddFlat(charge.Max);
            gauge.Reset();
        }

        void AddSchool(string school, int n) => _schools[school] = (_schools.TryGetValue(school, out var v) ? v : 0) + n;
    }
}
