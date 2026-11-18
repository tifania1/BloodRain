using System.IO;
using DamageNumbersPro;
using UnityEditor;
using UnityEngine;

namespace BloodRain.Editor
{
    // DN-01U 피해 숫자를 Damage Numbers Pro 프리팹으로. 데모 2D 'Clear' 를 바탕으로 보통·치명 두 벌을 Resources/Fx 에 짓는다.
    // 글꼴과 종이색 테두리는 실행 때 DamageNumberLayer 가 얹는다(글꼴이 LevelUpScreen 에서 오므로). 다시 돌려도 같은 파일을 고친다.
    public static class DamageNumberProBootstrap
    {
        const string Src = "Assets/DamageNumbersPro/Demo/Prefabs/2D/Clear.prefab";
        const string Dir = "Assets/Resources/Fx";

        [MenuItem("BloodRain/Bootstrap/피해 숫자 프리팹 짓기(DNP)")]
        public static void Build()
        {
            var src = AssetDatabase.LoadAssetAtPath<GameObject>(Src);
            if (src == null) { Debug.LogError("Damage Numbers Pro 데모 프리팹이 없다: " + Src); return; }
            if (!Directory.Exists(Dir)) { Directory.CreateDirectory(Dir); AssetDatabase.Refresh(); }
            Make(src, Dir + "/DamageNumberInk.prefab", crit: false);
            Make(src, Dir + "/DamageNumberCrit.prefab", crit: true);
            AssetDatabase.SaveAssets();
            Debug.Log("피해 숫자 프리팹: " + Dir + "/DamageNumberInk·Crit.prefab");
        }

        static void Make(GameObject src, string path, bool crit)
        {
            var go = (GameObject)Object.Instantiate(src);
            go.name = Path.GetFileNameWithoutExtension(path);
            var dn = go.GetComponent<DamageNumber>();
            foreach (var t in go.GetComponentsInChildren<TMPro.TMP_Text>(true))
            {
                t.fontSize = 10f; // 세계 1m 줄 높이 안팎. 실제 크기는 실행 때 SetScale 로(경지마다 세상 축척이 다르다)
                t.fontStyle = TMPro.FontStyles.Bold;
            }
            dn.numberSettings.bold = true;
            dn.lifetime = 0.55f; // 수명은 웹·balance lifeSeconds 0.7 안팎(fadeIn·fadeOut 포함)
            dn.durationFadeIn = 0.08f;
            dn.durationFadeOut = 0.15f;
            dn.enableScaleFadeIn = crit; // 치명은 한 번 툭 커졌다 줄어듦
            dn.scaleFadeIn = new Vector2(1.6f, 1.6f);
            dn.enableOffsetFadeIn = false;
            dn.enableOffsetFadeOut = false;
            dn.enablePooling = true;
            dn.poolSize = 100;
            dn.limitActiveInstances = true;
            dn.maxActiveInstances = 80; // balance limits.damageNumbers 와 같은 값. 실행 때 다시 넣는다
            dn.enableCombination = true; // 같은 자리에 잇달아 들어간 숫자를 하나로 합침
            var c = new CombinationSettings(0);
            c.method = CombinationMethod.ABSORB_NEW;
            c.maxDistance = 1f; // 실행 때 지금 경지의 1m 로 다시 넣는다
            c.spawnDelay = 0.03f;
            c.absorbDuration = 0.15f;
            c.bonusLifetime = 0.25f;
            c.absorberScaleFactor = 1.25f;
            c.instantGain = true;
            dn.combinationSettings = c;
            dn.spamGroup = crit ? "br_dmg_crit" : "br_dmg";
            PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
        }
    }
}
