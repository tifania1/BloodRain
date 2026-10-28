# Unity 작업 (PC 루프 담당)

Unity 에디터가 있어야 하는 일만 적는다. PC 루프(tools/agent_loop/run_loop.py)가 위에서부터 1~2개씩 처리한다.
끝내면 `- [x]` 와 한 줄 결과, 막히면 그대로 두고 아래에 `막힘:` 한 줄.

- [ ] B-10 Unity 프로젝트 기본 설정: Packages에 2D URP·Input System·com.unity.nuget.newtonsoft-json·Test Framework 확인, Assets/Scenes에 Boot·Run·Inn 빈 씬, Assets/Scripts/Presentation·Sim·Platform 폴더와 asmdef(BloodRain.Core 참조)
- [ ] B-11 데이터 로더: Resources/Data의 JSON 6개를 TextAsset으로 읽어 GameData.FromJson에 넘기는 `Presentation/DataBoot.cs`. Boot 씬 플레이 시 콘솔에 무공 16종 이름이 찍히는지 확인
