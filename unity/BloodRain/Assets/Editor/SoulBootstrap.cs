using BloodRain.Core.Meta;
using BloodRain.Core.Run;
using BloodRain.Presentation;
using BloodRain.Presentation.Meta;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace BloodRain.Editor
{
    // LF-03 한 생이 돌아가는 고리: Run 쓰러짐 → 정산 → Inn(침방) → Run. 혼은 SoulStore 가 저장한다.
    public static class SoulBootstrap
    {
        const string InnScenePath = "Assets/Scenes/Inn.unity";

        /// <summary>Inn 씬을 새로 짓는다. 지금은 침방 하나(객잔 본채·시설 문은 RM-03b).</summary>
        [MenuItem("BloodRain/Bootstrap/객잔 씬(침방) 만들기")]
        public static void BuildInnScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.OpenScene(InnScenePath, OpenSceneMode.Single);
            foreach (var go in scene.GetRootGameObjects()) Object.DestroyImmediate(go);

            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.91f, 0.87f, 0.78f);
            camGo.transform.position = new Vector3(0, 0, -10);
            camGo.AddComponent<UniversalAdditionalCameraData>();

            new GameObject("DataBoot").AddComponent<DataBoot>();
            var screen = ChimbangBootstrap.CreateScreen();
            screen.openOnStart = false; // InnFlow 가 저장된 혼으로 연다
            screen.loadRunScene = true;
            var flow = new GameObject("객잔 흐름").AddComponent<InnFlow>();
            flow.chimbang = screen;
            BgmBootstrap.Attach(); // BGM-06U

            EditorSceneManager.SaveScene(scene, InnScenePath);
            Debug.Log($"[SoulBootstrap] {InnScenePath} 다시 지음(침방 + InnFlow)");
        }

        [MenuItem("BloodRain/Test/혼 저장 지우기")]
        public static void ClearSave()
        {
            SoulStore.Clear();
            RebirthHandoff.Soul = null;
            RebirthHandoff.ClearBody();
            Debug.Log($"[SoulBootstrap] 혼 저장 지움 ({SoulStore.SoulPath})");
        }

        /// <summary>시험: 서장을 마친 혼으로 저장한다(첫 환혼 셋이 열림). 서장 끝(C-10b)이 붙기 전까지 천하 몸 시험에 쓴다.</summary>
        [MenuItem("BloodRain/Test/서장 마친 혼으로 저장")]
        public static void SavePrologueDoneSoul()
        {
            var data = DataBoot.Load();
            var soul = SoulStore.Load(data);
            if (!soul.PrologueDone)
                soul.EndLife(soul.BeginLife(Rebirth.Prologue(data)).Finish(LifeEndWay.Fallen, 0));
            SoulStore.Save(soul);
            RebirthHandoff.Soul = null;
            RebirthHandoff.ClearBody();
            Debug.Log($"[SoulBootstrap] 서장 마친 혼 저장 · {soul.Lives}생 · 윤회의 기 {soul.RebirthQi:N0}");
        }

        // QA-05U 시험: 저장된 혼의 봉인석을 n 개로(봉인석 있는 지역 앞에서부터). 봉마곡은 7개(bosses.finalRequires.sealStones)부터 깨어난다.
        [MenuItem("BloodRain/Test/혼에 봉인석 넣기/0")] static void Seal0() => SetSealStones(0);
        [MenuItem("BloodRain/Test/혼에 봉인석 넣기/6")] static void Seal6() => SetSealStones(6);
        [MenuItem("BloodRain/Test/혼에 봉인석 넣기/7")] static void Seal7() => SetSealStones(7);

        static void SetSealStones(int n)
        {
            var data = DataBoot.Load();
            var soul = SoulStore.Load(data);
            var stones = new System.Collections.Generic.List<string>();
            foreach (var r in data.Cheonha.Regions)
                if (r.SealStone != null && stones.Count < n) stones.Add(r.Id);
            var json = Newtonsoft.Json.Linq.JObject.Parse(soul.ToJson());
            string key = "sealStones";
            foreach (var p in json.Properties())
                if (string.Equals(p.Name, "sealStones", System.StringComparison.OrdinalIgnoreCase)) { key = p.Name; break; }
            json[key] = new Newtonsoft.Json.Linq.JArray(stones);
            soul = Soul.FromJson(data, json.ToString());
            SoulStore.Save(soul);
            RebirthHandoff.Soul = null;
            Debug.Log($"[SoulBootstrap] 봉인석 {soul.SealStoneCount}개로 저장: {string.Join(", ", stones)}");
        }

        [MenuItem("BloodRain/Test/지금 쓰러지기(플레이 중)")]
        public static void FallNow()
        {
            if (LifeRunner.Instance == null) { Debug.LogWarning("[SoulBootstrap] Run 씬 플레이 중에만 됩니다"); return; }
            LifeRunner.Instance.FallNow();
        }

        [MenuItem("BloodRain/Test/지금 쓰러지기(플레이 중)", true)]
        static bool FallNowValid() => Application.isPlaying;

        // R-08U 시험: 이번 생 처치 수에 4,990 을 더한다(도전 과제 '한 생에서 적 5,000 처치'를 열 직전까지)
        [MenuItem("BloodRain/Test/처치 수 채우기(플레이 중)")]
        public static void AddKills()
        {
            if (LifeRunner.Instance == null || LifeRunner.Instance.Life.Record != null) { Debug.LogWarning("[SoulBootstrap] Run 씬 플레이 중에만 됩니다"); return; }
            LifeRunner.Instance.Life.Log.Kill(4990);
            Debug.Log($"[SoulBootstrap] 처치 수 → {LifeRunner.Instance.Life.Log.Kills:N0}");
        }

        [MenuItem("BloodRain/Test/처치 수 채우기(플레이 중)", true)]
        static bool AddKillsValid() => Application.isPlaying;

        // C-07a 시험: 다음 경지가 될 때까지 진기를 넣는다(돌파 줌·보상이 Life 쪽에서 나옴)
        [MenuItem("BloodRain/Test/다음 경지까지 진기(플레이 중)")]
        public static void NextRealm()
        {
            if (LifeRunner.Instance == null) { Debug.LogWarning("[SoulBootstrap] Run 씬 플레이 중에만 됩니다"); return; }
            LifeRunner.Instance.GiveXpToNextRealm();
            var life = LifeRunner.Instance.Life;
            Debug.Log($"[SoulBootstrap] 레벨 {life.Level} · 경지 {life.Scale.Realms[life.Realm].NameKo} · 받는 피해 x{life.DamageTakenMultiplier:0.00}");
        }

        [MenuItem("BloodRain/Test/다음 경지까지 진기(플레이 중)", true)]
        static bool NextRealmValid() => Application.isPlaying;
    }
}
