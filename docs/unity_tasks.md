# Unity 작업 (PC 루프 담당)

Unity 에디터가 있어야 하는 일만 적는다. PC 루프(tools/agent_loop/run_loop.py)가 위에서부터 1~2개씩 처리한다.
끝내면 `- [x]` 와 한 줄 결과, 막히면 그대로 두고 아래에 `막힘:` 한 줄.

- [ ] B-00 기본 템플릿이 넣은 안 쓰는 패키지 정리: com.unity.ads, com.unity.analytics, com.unity.purchasing, com.unity.xr.legacyinputhelpers, com.unity.multiplayer.center, com.unity.collab-proxy 제거 후 콘솔 오류 0 확인 (WebGL 용량 때문)
- [ ] B-10 Unity 프로젝트 기본 설정: Packages에 2D URP·Input System·com.unity.nuget.newtonsoft-json·Test Framework 확인, Assets/Scenes에 Boot·Run·Inn 빈 씬, Assets/Scripts/Presentation·Sim·Platform 폴더와 asmdef(BloodRain.Core 참조)
- [ ] B-11 데이터 로더: Resources/Data의 JSON(`GameData.FileNames` 에 적힌 것 전부, 지금 7개)을 TextAsset으로 읽어 GameData.FromJson에 넘기는 `Presentation/DataBoot.cs`. Boot 씬 플레이 시 콘솔에 무공 16종 이름이 찍히는지 확인
- [ ] S-04 시너지 표시: 레벨업 카드 연환 띠·문파 인장, HUD 진전 인장, 비전·기연 발견 연출, 장경각 도감 화면(기획서 20장, 웹 프로토타입 동작 참고)
- [ ] J-01U 절기 각성 연출: `docs/design/vfx.md` '절기 각성 연출' 표대로 Fullscreen Pass(흑백 구멍), 카메라 확대·흔들림(언스케일), 이름 붓질(TextMeshPro + Yuji Boku SDF), 낙관, 파동 링. 웹 프로토타입 `#awaken` 과 나란히 놓고 비교. 판정은 Core J-01이 끝난 뒤 연결
- [ ] M-03 강호 지도 화면: 혈운 셰이더·먹 길·깃발(기획서 21장). 배경 이미지(art_queue `map_central_plains`)가 들어온 뒤에 시작
