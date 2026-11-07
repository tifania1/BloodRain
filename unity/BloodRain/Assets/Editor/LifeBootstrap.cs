using BloodRain.Presentation;
using BloodRain.Presentation.Fx;
using BloodRain.Presentation.Meta;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BloodRain.Editor
{
    // LF-01 Run 씬에 한 생(LifeRunner)을 붙인다. 돌파 줌(BreakthroughFx)도 같은 오브젝트에 두고 혈우 장막·그림자에 잇는다.
    // 진기 구슬의 시험 RunXp 는 끈다(레벨은 Life 가 센다). RunBootstrap 이 씬을 새로 지을 때도 부른다.
    public static class LifeBootstrap
    {
        const string RunScenePath = "Assets/Scenes/Run.unity";

        [MenuItem("BloodRain/Bootstrap/Run 씬에 한 생 붙이기")]
        public static void BuildRunSceneWithLife()
        {
            // Run 씬을 새로 짓는다(RunBootstrap 이 끝에서 Attach 를 부른다)
            RunBootstrap.BuildRunScene();
            Debug.Log("[LifeBootstrap] Run 씬에 한 생 붙임");
        }

        public static LifeRunner Attach()
        {
            var r = Object.FindFirstObjectByType<LifeRunner>();
            if (r == null) r = new GameObject("한 생").AddComponent<LifeRunner>();
            var fx = r.GetComponent<BreakthroughFx>();
            if (fx == null) fx = r.gameObject.AddComponent<BreakthroughFx>();
            r.breakthrough = fx;

            var player = Object.FindFirstObjectByType<PlayerController>();
            r.player = player != null ? player.transform : null;
            r.enemies = Object.FindFirstObjectByType<EnemyField>();
            if (r.enemies != null)
            {
                // LF-02 Run 씬 적은 생성 디렉터가 만든다(시험 씬 메뉴들은 testSpawn 켠 채)
                r.enemies.testSpawn = false;
                EditorUtility.SetDirty(r.enemies);
            }
            r.orbs = Object.FindFirstObjectByType<OrbLayer>();
            if (r.orbs != null)
            {
                r.orbs.ownTestXp = false;
                EditorUtility.SetDirty(r.orbs);
            }

            // LF-03 쓰러지면 LifeRunner 가 받는다(시험 씬처럼 다시 채우지 않는다). 정산 화면은 끝나면 객잔으로
            var vitals = Object.FindFirstObjectByType<PlayerVitals>();
            if (vitals != null) { vitals.refillOnDeath = false; EditorUtility.SetDirty(vitals); }
            r.vitals = vitals;
            var settle = Object.FindFirstObjectByType<SettlementScreen>();
            if (settle == null) settle = new GameObject("정산 화면").AddComponent<SettlementScreen>();
            settle.font = HudBootstrap.EnsureBodyFont();
            settle.demo = false;
            settle.loadInnScene = true;
            EditorUtility.SetDirty(settle);
            r.settlement = settle;

            var curtain = Object.FindFirstObjectByType<BloodCurtain>();
            if (curtain != null) { curtain.breakthrough = fx; EditorUtility.SetDirty(curtain); }
            var shadows = Object.FindFirstObjectByType<CurtainShadows>();
            if (shadows != null) { shadows.breakthrough = fx; EditorUtility.SetDirty(shadows); }

            // QA-05U 화면 가장자리 표시(땅의 주인 화살표·혈운 기둥·장막 번짐). LifeRunner 가 Start 에서 이 생의 Bosses 로 묶는다
            var edge = Object.FindFirstObjectByType<EdgeIndicators>();
            if (edge == null) edge = new GameObject("화면 가장자리 표시").AddComponent<EdgeIndicators>();
            edge.material = ClusterBootstrap.EnsureMaterial();
            EditorUtility.SetDirty(edge);

            BgmBootstrap.Attach(); // BGM-06U
            SfxBootstrap.Attach(); // SFX-14U
            HudBootstrap.AttachRunHud(); // C-08a 음양 HUD

            EditorUtility.SetDirty(r);
            return r;
        }

        /// <summary>Run 씬을 새로 짓지 않고 지금 있는 씬에 Attach 만 다시 한다(붓 획 연결 같은 다른 설정을 지키려고).</summary>
        [MenuItem("BloodRain/Bootstrap/Run 씬 한 생 연결만 다시")]
        public static void ReattachOnly()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.OpenScene(RunScenePath);
            Attach();
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[LifeBootstrap] Run 씬 한 생 연결 다시 함");
        }
    }
}
