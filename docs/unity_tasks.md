# Unity 작업 (PC Unity 세션 담당)

Unity 에디터가 있어야 하는 일만 적는다. 밤마다(20:37·22:37·00:37·02:37·04:37) PC Unity 세션이 PC의 Unity(unityMCP)로 위에서부터 처리한다. PC가 꺼져 있거나 다른 프로젝트 Unity가 떠 있으면 그 차례는 건너뛴다.
끝내면 `- [x]` 와 한 줄 결과, 막히면 그대로 두고 아래에 `막힘:` 한 줄.

- [x] B-00 기본 템플릿이 넣은 안 쓰는 패키지 정리: com.unity.ads, com.unity.analytics, com.unity.purchasing, com.unity.xr.legacyinputhelpers, com.unity.multiplayer.center, com.unity.collab-proxy 제거 후 콘솔 오류 0 확인 (WebGL 용량 때문) → 2026-10-30 여섯 개 지움. 패키지 46개, 콘솔 오류 0. ads가 띄우던 Mobile Dependency Resolver 창도 이제 안 뜬다
- [x] B-10 Unity 프로젝트 기본 설정: Packages에 2D URP·Input System·com.unity.nuget.newtonsoft-json·Test Framework 확인, Assets/Scenes에 Boot·Run·Inn 빈 씬, Assets/Scripts/Presentation·Sim·Platform 폴더와 asmdef(BloodRain.Core 참조) → 2026-10-30 패키지 넷 확인. `BloodRain/Bootstrap/기본 씬 만들기` 메뉴(Assets/Editor/SceneBootstrap.cs)로 Boot·Run·Inn 씬과 빌드 목록(Boot 0번). Sim·Platform·Presentation·Editor asmdef. 입력 처리는 Both(Input System 백엔드 켬)
- [x] B-11 데이터 로더: Resources/Data의 JSON(`GameData.FileNames` 에 적힌 것 전부, 지금 12개)을 TextAsset으로 읽어 GameData.FromJson에 넘기는 `Presentation/DataBoot.cs`. Boot 씬 플레이 시 콘솔에 무공 16종 이름이 찍히는지 확인 → 2026-10-30 `DataBoot.Data` 로 한 번 읽어 둔다. Boot 씬 플레이 때 "파일 12개, 무공 16종" 확인, 오류 0. Boot 씬 배치는 `BloodRain/Bootstrap/Boot 씬에 DataBoot 붙이기` 메뉴
- [ ] S-04 시너지 표시: 레벨업 카드 연환 띠·문파 인장, HUD 진전 인장, 비전·기연 발견 연출, 장경각 도감 화면(기획서 20장, 웹 프로토타입 동작 참고)
- [ ] J-01U 절기 각성 연출: `docs/design/vfx.md` '절기 각성 연출' 표대로 Fullscreen Pass(흑백 구멍), 카메라 확대·흔들림(언스케일), 이름 붓질(TextMeshPro + Yuji Boku SDF), 낙관, 파동 링. 웹 프로토타입 `#awaken` 과 나란히 놓고 비교. 판정은 Core J-01이 끝난 뒤 연결
- [x] M-03 강호 지도 화면: 혈운 셰이더·먹 길·깃발(기획서 21장). 배경 이미지(art_queue `map_central_plains`)가 들어온 뒤에 시작 — 천하 구조로 바뀌어 안 함(2026-10-29). 지도는 침방 벽 지도(TU-12)로 옮김

## 천하 (2026-10-29 결정. 기획서 7·12·15·21장, `cheonha.json`)

줄마다 적은 Core 입구 파일은 클라우드 세션이 만드는 중이다. 그 파일이 아직 저장소에 없으면 그 줄은 건너뛴다(막힘 아님). 숫자는 직접 적지 말고 `cheonha.json` 과 Core 에서 읽는다. 움직임은 웹 시안 `web/lookdev/cheonha.html` 과 나란히 놓고 비교한다.

- [ ] TU-01 축척 카메라: 카메라 직교 크기를 7장 화면 대각선 식(36m × 2.25^(경지 순번 + 0.35 × 경지 안 진행))에 맞춘다. 직교 크기 = 대각선 × 세로 ÷ √(가로² + 세로²) ÷ 2, 16:9면 대각선 × 0.245. 레벨이 오를 때 튀지 않게 보간. 플레이어·지금 경지 적·무공 이펙트의 크기·속도·사거리에 배율을 곱하는 곳을 Sim 한 군데로 모은다(15장 '배율 한 곳에서'). 시험 씬에서 키 하나로 경지를 올려 삼류~현경 일곱 화면을 스크린샷으로 남긴다. Core 입구: `Core/World/Scale.cs`. 먼저: B-10, B-11
- [ ] TU-02 돌파 줌: 7장 '돌파 연출' 표대로 1.4초(연출 시계는 언스케일). 0~0.2초 세상 시간 0.05배와 흑백 심상(J-01U의 Fullscreen Pass 재사용), 0.2~1.4초 카메라 직교 크기와 세계 배율을 천천히 출발해 천천히 멈추게 함께 보간, 0.8초에 쓸림 신호, 1.4초 동안 적 생성 멈춤, 끝나면 깨달음 3택1 호출 자리. 돌파 순간 공간 해시 격자를 2.25배로 다시 짓는 호출 자리도 둔다. 값은 `cheonha.json` `breakthrough`·`crush`. Core 입구: `Core/World/Scale.cs`. 먼저: TU-01
- [ ] TU-03 밟힘과 쓸림: 아래 경지에서 생긴 적은 이동을 멈춘 가벼운 목록으로 옮겨 작게 그리고, 닿으면 먹물로 터지며 쓰러진다(피해 없음, 진기 0.3). 아래 경지의 진기 구슬·음식·은자는 지나가면 주워지고 작은 등롱은 밟으면 깨진다. 돌파 0.8초에 화면 대각선 0.6배 안의 아래 경지 적이 한꺼번에 쓸려 나가는 연출. 적 시뮬레이션(backlog C-02)이 아직이면 시험 씬에 적을 직접 놓고 확인. Core 입구: `Core/World/Scale.cs`(밟힘·쓸림 대상). 먼저: TU-02
- [ ] TU-04 덩이 렌더러: 적 그림 한 장을 덩이당 최대 64번 겹쳐 그리는 인스턴싱(Graphics.RenderMeshInstanced, 적 아틀라스 하나). 덩이 크기는 경지별 1·4·9·16·28·44·64, 그리는 사람 수 = ceil(덩이 크기 × 남은 기혈 비율). 덩이 안 자리는 시드로 고정해 사람 수가 줄어도 남은 사람이 튀지 않게 하고, 빠지는 사람은 먹물 디졸브. 강시 콩콩 트윈은 사람마다 박자를 조금씩 어긋나게. 현경 덩이 600개(64명씩)에서 PC 60fps 확인. Core 입구: `Core/World/Scale.cs`(덩이 그릴 사람 수). 먼저: TU-01
- [ ] TU-05 혈우 장막 셰이더: 태어난 자리를 가운데 두고 반경(그 경지 첫 화면 대각선 × 2.4)에 붉은 빗줄기 벽을 그리는 셰이더(흐르는 빗줄기 노이즈, 안쪽으로 갈수록 옅게 번짐, 주홍 #B3261E 하나). 돌파 때 반경이 새 값으로 밀려나며 빗줄기가 갈라지는 연출을 TU-02 시계에 맞춘다. 현경에서 걷힌다. Core 입구: `Core/World/Scale.cs`(장막 반경). 먼저: TU-02
- [ ] TU-06 장막 밀어내기와 너머 그림자: 장막에 다가가면 플레이어를 안쪽으로 밀어낸다(세기는 Core 값). 장막 너머로 윗 경지의 거대한 혈운 군세가 검은 실루엣으로만 어른거리게(덩이 렌더러를 한 단계 큰 배율로 재사용). 플레이어 이동(backlog C-01)이 아직이면 시험용 이동 스크립트로. Core 입구: `Core/World/Scale.cs`(밀어내기). 먼저: TU-04, TU-05
- [ ] TU-07 지형 단계 1, 소품 층: 경지마다 소품 층(나무·집·탑·산)을 두고 시드로 지역 둘레에 뿌려 격자로 찾는다. 화면에서 2픽셀보다 작아진 소품은 그리지 않는다(웹 시안과 같게). 지역 위치는 `regions[].pos` × 세계 크기(4,000 x 2,600m). 소품 그림이 아직 없으니 웹 시안의 도형 소품을 옮겨 임시로. Core 입구: `Core/World/Regions.cs`(지역 위치·둘레), `Scale.cs`(화면 대각선). 먼저: TU-01
- [ ] TU-08 지형 단계 2, 수묵 지도 섞기: 화면 대각선에 비해 너무 작아진 소품 층은 수묵 지도 텍스처(art_queue `map_central_plains`)로 크로스페이드해 넘기고, 바닥 타일도 경지가 오를수록 지도 쪽으로 섞는다. 지도 텍스처는 세계 좌표(삼류 m)에 맞춰 깐다. 지도 그림이 들어오기 전엔 임시 텍스처로. Core 입구: `Core/World/Scale.cs`, `Regions.cs`. 먼저: TU-07
- [ ] TU-09 지역 HUD: 위 가운데 경과 시간 아래에 지금 지역 이름(한글과 한자)과 혈운 농도(12장 '전투 HUD 배치'). 화면 빗줄기 굵기도 혈운 농도를 따른다(지역 사이에선 섞인 값). Core 입구: `Core/World/Regions.cs`(서 있는 지역, 혈운 농도). 먼저: B-11
- [ ] TU-10 화면 가장자리 표시: 화면 밖 땅의 주인·정예·금두꺼비 방향 화살표, 땅의 주인이 깨어날 때 그 지역 위로 솟는 혈운 기둥, 혈우 장막이 가까우면 그쪽 가장자리가 붉게 번지는 화면 효과(가까울수록 진하게). Core 입구: `Core/World/BossAwakening.cs`(깨어난 보스 지역), `Scale.cs`(장막까지 거리). 먼저: TU-05
- [ ] TU-11 침방 화면 1, 후보 카드: 배경 `bg_chimbang` 위 목 노인 앞에 환생 후보 카드 셋(넷째 몸을 샀으면 넷). 카드마다 문파 산문 그림(art_queue `card_sect_*`), 문파 이름, 대표 인물, 타고난 성질, 태어나는 땅의 혈운 농도. 다시 고르기 버튼(남은 횟수). 고르면 그 몸으로 한 생 시작. 그림이 없으면 단색 카드로. 웹 시안 환생 화면과 비교. Core 입구: `Core/Meta/Rebirth.cs`(후보 추첨·몸 만들기), `Core/Meta/Soul.cs`(해금 문파·환혼 강화 레벨). 먼저: B-11
- [ ] TU-12 침방 화면 2, 벽의 중원 지도: 배경 벽 빈자리에 `map_central_plains` 를 건다. 밟은 땅은 먹으로, 못 가 본 땅은 붉은 구름 아래 지명만 흐릿하게, 봉인석을 가져온 땅은 혈운이 걷히고 문파 깃발. 후보 카드에 커서가 가면 그 문파 자리에 붉은 점과 절정·초절정 장막 원을 먹선으로 그리고, 원 안에 드는 땅의 주인을 표시. 지명·깃발·원은 모두 Unity에서 얹는다(21장 '침방의 중원 지도'). Core 입구: `Core/Meta/Soul.cs`(밟은 땅·봉인석), `Core/World/Scale.cs`(장막 반경), `Regions.cs`(지역 위치). 먼저: TU-11
- [ ] TU-13 정산 화면: 생존 시간, 오른 경지, 밟은 땅, 처치 수(덩이 속 사람 수로 셈), 무공별 피해 비중 막대, 혈정, 윤회의 기 내역(진기 × 혈운 농도, 땅의 주인, 처음 밟은 땅, 처음 오른 경지, 남은 은자 절반, 성질 배율)을 한 줄씩 차오르게. 쓰러짐과 좌화는 같은 화면이고 끝나면 객잔으로. Core 입구: `Core/Meta/RebirthQi.cs`(항목별 내역), `Core/Meta/Soul.cs`(잔고에 더하기). 먼저: B-11
- [ ] TU-14 연무장 화면: 윤회의 기 잔고와 10장 연무장 20항목(레벨, 다음 비용, 효과), 사기와 전부 환불. 같은 구매 칸으로 침방 아래에 환혼 강화 두 줄(넷째 몸, 다시 고르기)도 붙인다. Core 입구: `Core/Meta/Soul.cs`(잔고·연무장 레벨·환혼 강화), `Core/Build/Stats.cs`(연무장 비용 공식, balance.json `training`). 먼저: B-11

## M1 코어 프로토타입 (backlog C-01~C-10 에서 옮김, 2026-10-30)

천하 놀이판(위 TU)보다 뒤에 한다. 숫자는 balance.json·weapons.json 과 Core 에서 읽고, 움직임은 웹 프로토타입(`web/prototype/template.html`)과 나란히 놓고 비교한다.

- [ ] C-01 플레이어 이동(WASD·패드), 카메라 추적, 경공 대시(0.25초 4m, 무적 0.2초, 2회 충전. balance.json `dash`). 먼저: B-10, B-11
- [ ] C-02 적 시뮬레이션: 구조체 배열 + Burst 잡(추적 + 밀어내기), 강시 콩콩 박자 이동, 오브젝트 풀, 적 300마리 60fps 확인. 먼저: C-01
- [ ] C-03 격자 공간 해시 충돌(원 대 원), 접촉 피해 0.35초 간격. 먼저: C-02
- [ ] C-04 무공 실행기: behavior별 컴포넌트(front_arc, homing_dagger, orbit_slash, aura …) 처음 8종. Core 입구: `WeaponMath`(Core/Combat/EnemyScaling.cs 안)·`Loadout`. 먼저: C-03
- [ ] C-05 진기 구슬 드롭·흡수, 400개 넘으면 합치기. Core 입구: `Core/Progression/RunXp.cs`. 먼저: C-03
- [ ] C-06 레벨업 화면(비급 책장 카드 3~4장, 다시 고르기·파기·정진). Core 입구: `Loadout`, `Synergy.cs`(카드 미리보기). 먼저: C-04, C-05
- [ ] C-07 경지 돌파 연출(흑백 심상 2초) + 깨달음 3택1. Core 입구: `Insights.cs`, `World/Breakthrough.cs`. 먼저: C-06
- [ ] C-08 음양 태극 문양 HUD, 기혈역류 연출. Core 입구: `YinYangGauge`, `Backlash.cs`. 먼저: C-06
- [ ] C-09 오의 버튼(진소운 일검), 보갑, 절기 그릇과 각성 연출(J-01U·vfx.md), 절기 4종. Core 입구: `UltimateCharge.cs`, `Awakening.cs`. 먼저: C-07
- [ ] C-10 서장 10분 통과: 스폰 디렉터 + 강시 포쾌 장삼 보스. Core 입구: `Run/Prologue.cs`, `Spawning/LifeDirector.cs`. 먼저: C-01~C-09
