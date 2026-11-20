using BloodRain.Core.Meta;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BloodRain.Presentation.Meta
{
    // LF-03 객잔 씬의 흐름. 저장된 혼으로 객잔 본채(InnScreen, 2026-11-20)를 먼저 띄우고, '새 생 시작'이면 (모드 고르기 →) 침방,
    //   몸을 고르면 혼을 저장한 뒤 Run 씬으로(ChimbangScreen.loadRunScene). 침방의 '객잔으로'(Esc)는 본채로 돌아온다.
    //   서장을 안 마친 혼이면 환혼이 없으니 바로 Run(서장 몸)으로 돌려보낸다.
    [DefaultExecutionOrder(100)] // ChimbangScreen.Start(틀 짓기) 뒤에
    public sealed class InnFlow : MonoBehaviour
    {
        public ChimbangScreen chimbang;
        [Tooltip("객잔 본채 바탕(bg_inn_hub). 비면 침방 바탕")]
        public Sprite hubBackground;
        [Tooltip("연무장 바탕(bg_training_yard). 비면 종이만")]
        public Sprite trainingBackground;

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
            chimbang.Back += ShowHub;
            // 2026-11-20 사람: 저장을 고르거나 죽으면 먼저 객잔 본채(새 생 시작·연무장·시설·설정). 침방은 '새 생 시작'으로 연다
            Inn.Refresh(_soul); // 조건을 갖춘 시설을 혼에 적는다(정산 뒤)
            _hub = new GameObject("객잔 본채").AddComponent<InnScreen>();
            _hub.font = chimbang.font;
            _hub.background = hubBackground != null ? hubBackground : chimbang.background; // 2026-11-21 본채 그림이 따로 있었는데 침방 그림을 쓰고 있었다
            _hub.trainingBackground = trainingBackground;
            _hub.bodyPreview = System.Linq.Enumerable.FirstOrDefault(System.Linq.Enumerable.Select(chimbang.sectArt, a => a.art), a => a != null); // 대장간 몸 색 미리 보기
            _hub.StartLife += OpenChimbang;
            ShowHub();
            Debug.Log($"[객잔] 혼 {_soul.Lives}생, 윤회의 기 {_soul.RebirthQi:N0}, 객잔 본채를 엽니다");
        }

        InnScreen _hub;

        void ShowHub()
        {
            chimbang.SetVisible(false);
            SoulStore.Save(_soul);
            _hub.Open(_soul);
        }

        bool _drawn;

        void OpenChimbang()
        {
            // 한 번 뽑은 후보는 객잔을 오가도 그대로(오가며 공짜로 다시 뽑지 못하게)
            if (_drawn) { chimbang.SetVisible(true); return; }
            _drawn = true;
            // 열린 모드가 둘 이상이면 침방 앞에 모드 고르기(R4-05U). 고르면 침방을 연다
            RebirthHandoff.Mode = null;
            var picker = new GameObject("모드 고르기").AddComponent<ModePicker>();
            picker.font = chimbang.font;
            if (picker.Open(_soul)) picker.Picked += _ => { chimbang.SetVisible(true); chimbang.Open(_soul, new System.Random()); };
            else { Destroy(picker.gameObject); chimbang.SetVisible(true); chimbang.Open(_soul, new System.Random()); }
        }

        void OnDestroy()
        {
            if (chimbang != null) { chimbang.Chosen -= OnChosen; chimbang.Back -= ShowHub; }
        }

        // 침방에서 산 환혼 강화·다시 고르기도 혼에 남는다
        void OnChosen(Core.Meta.RebirthBody body)
        {
            RebirthHandoff.PendingLife = null;
            SoulStore.Save(_soul);
        }
    }
}
