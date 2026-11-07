using BloodRain.Core.Build;
using BloodRain.Presentation;
using BloodRain.Presentation.Combat;
using BloodRain.Presentation.Fx;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BloodRain.Editor
{
    // C-09d 보갑과 각성. ChestLayer 는 LifeRunner 가 플레이 때 붙인다. 각성 연출(AwakeningFx)은 Run 씬에 하나 둔다.
    public static class ChestBootstrap
    {
        const string RunScenePath = "Assets/Scenes/Run.unity";
        const string FontAssetPath = "Assets/Art/Fonts/YujiBoku SDF.asset";

        /// <summary>Run 씬에 각성 연출을 붙인다(주 카메라, 절기 이름 글꼴). 다시 돌려도 하나만.</summary>
        [MenuItem("BloodRain/Bootstrap/Run 씬 각성 연출(AwakeningFx)")]
        public static void AddAwakeningToRun()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != RunScenePath) scene = EditorSceneManager.OpenScene(RunScenePath, OpenSceneMode.Single);
            var fx = Object.FindFirstObjectByType<AwakeningFx>();
            if (fx == null) fx = new GameObject("AwakeningFx").AddComponent<AwakeningFx>();
            var so = new SerializedObject(fx);
            so.FindProperty("cam").objectReferenceValue = Camera.main;
            so.FindProperty("nameFont").objectReferenceValue = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[ChestBootstrap] Run 씬에 AwakeningFx(카메라 {Camera.main?.name}, 글꼴 {(so.FindProperty("nameFont").objectReferenceValue != null ? "있음" : "없음")})");
        }

        [MenuItem("BloodRain/Test/보갑 놓기(플레이 중)")]
        public static void PlaceChest()
        {
            var c = ChestLayer.Instance;
            if (c == null || LifeRunner.Instance == null) { Debug.LogWarning("[ChestBootstrap] Run 씬 플레이 중에만 됩니다"); return; }
            c.PlaceNear();
            Debug.Log("[ChestBootstrap] 보갑 하나를 오른쪽 1.5m 에 놓음");
        }

        [MenuItem("BloodRain/Test/보갑 놓기(플레이 중)", true)]
        static bool PlaceChestValid() => Application.isPlaying;

        // 각성 조건을 맞춘 시험 몸: 아직 안 깨운 첫 절기의 무공을 최고 단계, 짝 심법을 기준 레벨까지, 그릇이 하나 남게 보갑 레벨을 올려 셈.
        [MenuItem("BloodRain/Test/각성 시험 몸(다음 절기 무공 Lv8 + 짝 심법)")]
        public static void AwakenBody()
        {
            var runner = LifeRunner.Instance;
            var chest = ChestLayer.Instance;
            if (runner == null || chest == null) { Debug.LogWarning("[ChestBootstrap] Run 씬 플레이 중에만 됩니다"); return; }
            var data = DataBoot.Data;
            var loadout = runner.Life.Loadout;
            // 아직 안 깨운 절기 가운데 데이터 순서로 첫 번째(다시 누르면 천지인 → 백보신권 → …)
            Core.Data.EvolutionDef evo = null;
            foreach (var e in data.Evolutions)
            {
                bool done = false;
                foreach (var id in loadout.Evolved) if (id == e.Weapon) done = true;
                if (!done) { evo = e; break; }
            }
            if (evo == null) { Debug.LogWarning("[ChestBootstrap] 절기를 다 깨웠다"); return; }
            var weapons = runner.weapons != null ? runner.weapons : Object.FindFirstObjectByType<WeaponRunner>();
            int max = data.Balance.Levels.WeaponMax, pairMin = data.Balance.Evolution.PairPassiveMinLevel;
            for (int guard = 0; guard < 20 && (!loadout.Weapons.TryGetValue(evo.Weapon, out var wl) || wl < max); guard++)
            {
                loadout.Apply(new Offer(loadout.Weapons.ContainsKey(evo.Weapon) ? OfferKind.UpgradeWeapon : OfferKind.NewWeapon, evo.Weapon));
                if (weapons != null) weapons.Learn(evo.Weapon);
            }
            for (int guard = 0; guard < 20 && (!loadout.Passives.TryGetValue(evo.Passive, out var pl) || pl < pairMin); guard++)
                loadout.Apply(new Offer(loadout.Passives.ContainsKey(evo.Passive) ? OfferKind.UpgradePassive : OfferKind.NewPassive, evo.Passive));
            // 그릇이 지금 깨운 수보다 하나 많아지는 가장 낮은 경지 레벨로 센다(절정 1·초절정 2 …)
            int evolved = 0;
            foreach (var _ in loadout.Evolved) evolved++;
            int jeoljeong = 0;
            foreach (var r in data.Balance.Realms)
                if (data.Balance.Evolution.Vessels.TryGetValue(r.Id, out var v) && v > evolved) { jeoljeong = r.Level; break; }
            chest.debugMinLevel = Mathf.Max(chest.debugMinLevel, jeoljeong);
            loadout.Weapons.TryGetValue(evo.Weapon, out var w2);
            loadout.Passives.TryGetValue(evo.Passive, out var p2);
            Debug.Log($"[ChestBootstrap] 시험 몸: {evo.Weapon} Lv{w2} + {evo.Passive} Lv{p2}, 보갑 레벨 {jeoljeong}(그릇 {evolved + 1})로 셈 · 문턱 {loadout.AtThreshold().Count}개");
        }

        [MenuItem("BloodRain/Test/각성 시험 몸(다음 절기 무공 Lv8 + 짝 심법)", true)]
        static bool AwakenBodyValid() => Application.isPlaying;
    }
}
