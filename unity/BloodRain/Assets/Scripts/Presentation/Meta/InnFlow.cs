using BloodRain.Core.Meta;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BloodRain.Presentation.Meta
{
    // LF-03 객잔 씬의 흐름. 저장된 혼으로 침방을 열고, 몸을 고르면 혼을 저장한 뒤 Run 씬으로(ChimbangScreen.loadRunScene).
    //   객잔 본채·시설 문(RM-03b)이 생기면 침방은 그 문 하나가 되고, 이 컴포넌트는 그쪽으로 옮긴다.
    //   서장을 안 마친 혼이면 환혼이 없으니 바로 Run(서장 몸)으로 돌려보낸다.
    [DefaultExecutionOrder(100)] // ChimbangScreen.Start(틀 짓기) 뒤에
    public sealed class InnFlow : MonoBehaviour
    {
        public ChimbangScreen chimbang;

        Soul _soul;

        void Start()
        {
            var data = DataBoot.Data;
            _soul = SoulStore.Load(data);
            RebirthHandoff.Soul = _soul;
            if (!_soul.PrologueDone)
            {
                Debug.Log("[객잔] 서장을 안 마친 혼이라 서장으로 돌아갑니다");
                RebirthHandoff.ClearBody();
                SceneManager.LoadScene("Run");
                return;
            }
            if (chimbang == null) chimbang = FindFirstObjectByType<ChimbangScreen>();
            chimbang.Chosen += OnChosen;
            // 열린 모드가 둘 이상이면 침방 앞에 모드 고르기(R4-05U). 고르면 침방을 연다
            RebirthHandoff.Mode = null;
            var picker = new GameObject("모드 고르기").AddComponent<ModePicker>();
            picker.font = chimbang.font;
            if (picker.Open(_soul)) picker.Picked += _ => chimbang.Open(_soul, new System.Random());
            else { Destroy(picker.gameObject); chimbang.Open(_soul, new System.Random()); }
            Debug.Log($"[객잔] 혼 {_soul.Lives}생, 윤회의 기 {_soul.RebirthQi:N0}, 침방을 엽니다");
        }

        void OnDestroy()
        {
            if (chimbang != null) chimbang.Chosen -= OnChosen;
        }

        // 침방에서 산 환혼 강화·다시 고르기도 혼에 남는다
        void OnChosen(Core.Meta.RebirthBody body)
        {
            RebirthHandoff.PendingLife = null;
            SoulStore.Save(_soul);
        }
    }
}
