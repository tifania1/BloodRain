using BloodRain.Presentation;
using BloodRain.Presentation.Fx;
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

            var curtain = Object.FindFirstObjectByType<BloodCurtain>();
            if (curtain != null) { curtain.breakthrough = fx; EditorUtility.SetDirty(curtain); }
            var shadows = Object.FindFirstObjectByType<CurtainShadows>();
            if (shadows != null) { shadows.breakthrough = fx; EditorUtility.SetDirty(shadows); }

            EditorUtility.SetDirty(r);
            return r;
        }
    }
}
