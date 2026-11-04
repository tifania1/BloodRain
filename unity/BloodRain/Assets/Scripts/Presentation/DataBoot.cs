using System.Collections.Generic;
using System.Linq;
using BloodRain.Core.Data;
using UnityEngine;

namespace BloodRain.Presentation
{
    // Resources/Data 의 JSON 을 읽어 GameData 하나를 만든다. 테스트(core/)와 같은 파일, 같은 FromJson 을 쓴다.
    public sealed class DataBoot : MonoBehaviour
    {
        public const string ResourceDir = "Data";

        static GameData _data;

        /// <summary>처음 부를 때 한 번 읽고 그 뒤로는 같은 것을 돌려준다.</summary>
        public static GameData Data => _data ??= Load();

        [SerializeField] bool logWeapons = true;

        public static GameData Load()
        {
            var files = new Dictionary<string, string>();
            foreach (var name in GameData.FileNames)
            {
                var asset = Resources.Load<TextAsset>($"{ResourceDir}/{name}");
                if (asset == null)
                {
                    Debug.LogError($"[DataBoot] Resources/{ResourceDir}/{name}.json 이 없다");
                    continue;
                }
                files[name] = asset.text;
            }
            var data = GameData.FromJson(files);
            // 0 나누기·무한 반복이 날 값은 읽을 때 바로 알린다(QA-03).
            foreach (var problem in DataCheck.Problems(data))
                Debug.LogError($"[DataBoot] 데이터 값 문제: {problem}");
            return data;
        }

        void Awake()
        {
            var data = Data;
            if (!logWeapons) return;
            var names = string.Join(", ", data.Weapons.Select(w => w.NameKo));
            Debug.Log($"[DataBoot] 파일 {GameData.FileNames.Length}개, 무공 {data.Weapons.Count}종: {names}");
        }

#if UNITY_EDITOR
        // 도메인 리로드를 꺼 둔 경우에도 플레이마다 새로 읽게.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatic() => _data = null;
#endif
    }
}