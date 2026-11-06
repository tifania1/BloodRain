using BloodRain.Core.Run;
using BloodRain.Presentation.Fx;
using UnityEditor;
using UnityEngine;

namespace BloodRain.Editor
{
    // WP-39U 시험: 기연을 굴리지 않고 플레이어 앞 2m 에 바로 놓는다. EncounterLayer 는 LifeRunner 가 플레이 때 붙인다(서장 몸에선 안 나옴).
    public static class EncounterBootstrap
    {
        const string Root = "BloodRain/Test/기연 놓기(플레이 중)/";

        [MenuItem(Root + "은거기인")] static void Hermit() => Place(EncounterBoons.Hermit);
        [MenuItem(Root + "고수의 유해")] static void Relic() => Place(EncounterBoons.Relic);
        [MenuItem(Root + "영약")] static void Elixir() => Place(EncounterBoons.Elixir);
        [MenuItem(Root + "혈마의 속삭임")] static void Whisper() => Place(EncounterBoons.Whisper);
        [MenuItem(Root + "영물")] static void Beast() => Place(EncounterBoons.SpiritBeast);
        [MenuItem(Root + "검총")] static void Tomb() => Place(EncounterBoons.SwordTomb);
        [MenuItem(Root + "하오문 밀서(다음 보통 등롱)")] static void Letter() => Place(EncounterBoons.Letter);

        [MenuItem(Root + "은거기인", true)] [MenuItem(Root + "고수의 유해", true)] [MenuItem(Root + "영약", true)]
        [MenuItem(Root + "혈마의 속삭임", true)] [MenuItem(Root + "영물", true)] [MenuItem(Root + "검총", true)]
        [MenuItem(Root + "하오문 밀서(다음 보통 등롱)", true)]
        static bool Valid() => Application.isPlaying;

        static void Place(string id)
        {
            var l = EncounterLayer.Instance;
            if (l == null) { Debug.LogWarning("[EncounterBootstrap] Run 씬 플레이 중에만 됩니다"); return; }
            Debug.Log(l.PlaceNow(id) ? $"[EncounterBootstrap] {id} 를 앞 2m 에 놓음" : $"[EncounterBootstrap] {id} 는 놓지 못함(서장 몸이거나 아직 붙이지 않은 사건)");
        }
    }
}
