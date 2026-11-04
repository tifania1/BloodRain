using System.Collections.Generic;
using BloodRain.Core.Build;
using BloodRain.Core.Data;
using BloodRain.Core.Meta;
using BloodRain.Presentation.Fx;
using UnityEngine;

namespace BloodRain.Presentation.Hud
{
    // 판 하나의 비전 판정(S-04, 기획서 20장). Core RunRecord·SecretChecker 를 들고,
    //   매 프레임 게임 시간과 음양 게이지를 기록하고 0.5초마다(그리고 레벨업 화면이 닫힌 직후) 판정해
    //   새로 열린 비전을 DiscoveryFx 로 띄운다. 정신부·음식·보갑·역류 끊기 같은 사건은
    //   그 일을 하는 쪽이 Record 에 알리고 Poke() 를 부른다(사건 조건은 사건 바로 뒤 판정만 본다).
    //   찾은 비전은 Found 에 남는다. 판이 끝나면 정산·장경각 '새 쪽' 표시가 이것을 읽는다(아직 안 이어짐).
    //   기연 사건은 맵에 붙을 때 ShowEncounter 로 같은 연출을 쓴다.
    public sealed class SecretWatch : MonoBehaviour
    {
        public LevelUpScreen levelUp;
        public DiscoveryFx fx;

        GameData _data;
        RunRecord _record;
        SecretChecker _checker;
        readonly List<SecretDef> _found = new List<SecretDef>();
        float _next;
        bool _wasOpen;
        bool _poke;

        public RunRecord Record => _record;
        public IReadOnlyList<SecretDef> Found => _found;

        void Start()
        {
            _data = DataBoot.Data;
            if (levelUp == null) levelUp = FindFirstObjectByType<LevelUpScreen>();
            if (fx == null) fx = FindFirstObjectByType<DiscoveryFx>();
            string characterId = (Meta.RebirthHandoff.Body ?? Rebirth.Prologue(_data)).Character.Id;
            // 판 밖 기록(깬 장·의서)은 Life·혼이 붙을 때 넘긴다.
            _record = new RunRecord(characterId);
            _checker = new SecretChecker(_data, _record);
        }

        /// <summary>사건을 Record 에 알린 바로 뒤에 부른다.</summary>
        public void Poke() => _poke = true;

        public void ShowEncounter(EncounterDef e)
        {
            if (fx != null) fx.Show(DiscoveryFx.Kind.Encounter, Loc.T($"synergies.encounters.{e.Id}.name", e.NameKo), "");
        }

        void Update()
        {
            if (_record == null || levelUp == null || levelUp.Loadout == null) return;
            // 레벨업 화면이 시간을 멈추면 deltaTime 이 0 이다. Tick 은 0 을 받지 않는다.
            if (Time.deltaTime > 0f) _record.Tick(Time.deltaTime, levelUp.Loadout.Gauge.Value);

            bool open = levelUp.IsOpen;
            bool justClosed = _wasOpen && !open;
            _wasOpen = open;
            if (open) return; // 고르는 중엔 판정하지 않는다. 닫히면 바로 본다.

            _next -= Time.unscaledDeltaTime;
            if (!_poke && !justClosed && _next > 0f) return;
            _poke = false;
            _next = 0.5f;
            foreach (var s in _checker.Check(levelUp.Loadout))
            {
                _found.Add(s);
                Debug.Log($"[SecretWatch] 비전 발견: {s.NameKo}({s.Id})");
                if (fx != null) fx.Show(DiscoveryFx.Kind.Secret, Loc.T($"synergies.secrets.{s.Id}.name", s.NameKo), s.Hanja);
            }
        }
    }
}
