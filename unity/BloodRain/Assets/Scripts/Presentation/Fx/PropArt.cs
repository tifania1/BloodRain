using UnityEngine;

namespace BloodRain.Presentation.Fx
{
    // 줍는 것·부수는 것·보갑 낱장 그림 목록(PR-01U). Assets/Art/Props/Parts/*.png 를 이름 → Sprite 로 들고 있다.
    //   에디터 메뉴 BloodRain/Bootstrap/소품 그림 목록 채우기 가 Run 씬 LifeRunner 오브젝트에 붙이고 채운다(Resources 로 옮기지 않음).
    //   LanternLayer·ChestLayer 가 Get 으로 꺼내 쓰고, 없으면 지금처럼 색 동그라미·네모로 그린다.
    public sealed class PropArt : MonoBehaviour
    {
        public string[] names = new string[0];
        public Sprite[] sprites = new Sprite[0];

        static PropArt _instance;
        public static PropArt Instance => _instance != null ? _instance : (_instance = FindFirstObjectByType<PropArt>());

        void OnDestroy() { if (_instance == this) _instance = null; }

        public Sprite Get(string id)
        {
            for (int i = 0; i < names.Length && i < sprites.Length; i++)
                if (names[i] == id) return sprites[i];
            return null;
        }

        /// <summary>그림이 있으면 SpriteRenderer 오브젝트를 만들어 긴 변이 worldSize 가 되게 맞춘다. 없으면 null.</summary>
        public static GameObject Make(Transform parent, string name, string id, Vector2 at, float worldSize, int order)
        {
            var art = Instance;
            var sp = art != null ? art.Get(id) : null;
            if (sp == null) return null;
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(at.x, at.y, 0f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sp;
            sr.sortingOrder = order;
            Fit(go.transform, sp, worldSize);
            return go;
        }

        public static void Fit(Transform t, Sprite sp, float worldSize)
        {
            float longest = Mathf.Max(sp.rect.width, sp.rect.height) / sp.pixelsPerUnit;
            t.localScale = Vector3.one * (worldSize / Mathf.Max(1e-4f, longest));
        }
    }
}
