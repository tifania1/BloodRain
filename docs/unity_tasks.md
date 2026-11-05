# Unity 작업 (PC Unity 세션·PC API 루프 담당)

Unity 에디터가 있어야 하는 일만 적는다. PC Unity 예약 세션(20:37·22:37·00:37·02:37·04:37)과 PC API 루프(`tools/agent_loop`, 21:32·23:32·01:32·03:32)가 PC의 Unity(unityMCP)로 **'할 차례' 절을 위에서부터** 처리한다. PC가 꺼져 있거나 다른 프로젝트 Unity가 떠 있으면 그 차례는 건너뛴다.
끝내면 `- [x] 날짜 — 한 줄 결과`, 막히면 그대로 두고 그 항목 아래에 `막힘:` 한 줄. 남은 것이 있으면 그 항목 끝에 `(진행: 어디까지)`.

## 항목 읽는 법 (2026-11-04 UT-01 다듬음)
- 한 항목은 40분 안쪽 크기다. 첫 줄 끝 `먼저:` 에 적힌 항목이 `[x]` 가 아니면 건너뛰고 다음 것을 한다(막힘 아님).
- 밑줄 다섯 칸: **할 것**(무엇을 만드나) · **쓸 것**(Core API·데이터 파일. 숫자는 여기서 읽고 직접 적지 않는다) · **메뉴**(만들거나 다시 돌릴 `[MenuItem]`, Assets/Editor 의 파일) · **끝**(무엇이 보이면 끝인지) · **찍기**(플레이 모드 스크린샷 경로).
- 지난 진행 기록(무엇이 이미 붙어 있는지)은 맨 아래 '옛 줄 원문'의 같은 ID 줄에 그대로 있다. 손대기 전에 그 줄을 읽는다.
- 다른 세션이 새 항목을 넣을 때는 '할 차례'의 알맞은 절(의존 순서 자리)에 같은 꼴(첫 줄 `먼저:`, 밑줄 다섯 칸)로 넣는다. 맨 아래나 '끝난 것'에 붙이지 않는다. 앞으로 생길 스크린샷·파일은 `폴더/` 의 `이름` 처럼 떼어 적는다(DocLinkTests 가 없는 경로를 막는다).
- 화면에 임시로 띄우는 글자는 `Loc.T`·`Loc.F` 로. 새 화면 클래스는 Presentation 의 기존 갈래(Hud·Fx·Meta·Combat)에 둔다.
- 움직임은 웹 프로토타입(`web/prototype/template.html`)·천하 시안(`web/lookdev/cheonha.html`)과 나란히 놓고 견준다.
- 핵심 줄기: Run 씬에 Core `Life` 가 아직 없다(레벨은 OrbLayer 시험 RunXp, 적은 EnemyField 시험 생성, 혼은 저장 안 됨). 'B. 한 생 뼈대'가 끝나야 그 아래 대부분이 풀린다. A 를 끝내면 B 부터.

## 할 차례

### A. 지금 바로 되는 확인(Life 없이, 짧음)

- [x] V-10b 붓 획 자른 텍스처 확인 — 2026-11-04 메뉴 다시 돌림, brushCrops 107줄(새 붓 획 포함, 27은 옛 수), 레벨 7 획이 V-04 화면과 같은 반지름·담채·금빛, 콘솔 에러 0
  - 할 것: 컴파일 확인 → 메뉴를 다시 돌려 Run 씬 WeaponRunner 에 brushCrops 가 채워지는지 → 삼재검법 1·2·3단이 전과 같은 자리·크기로 그어지는지 본다. 메뉴를 안 돌리면 잘린 그림이 512 칸 크기로 늘어나 보인다.
  - 쓸 것: `Art/VFX/Brush/*.json` 의 `crop`·`pivot`, `BrushStroke.shader` `_Rect`, `WeaponRunner.brushCrops`·`CropOf`·`CropMatrix`, `VfxBootstrap`
  - 메뉴: `BloodRain/Bootstrap/붓 획 이펙트(텍스처 설정·머티리얼·삼재검법 1단)` (VfxBootstrap.cs)
  - 끝: brushCrops 27줄, 콘솔 에러 0, 아주 느리게 돌린 1·2·3단 획이 `docs/unity_shots/V-04/samjae_tiers.png` 와 겹쳐 보아 같은 자리·크기
  - 찍기: `docs/unity_shots/V-10b/` 의 `samjae_tiers_cropped.png`

- [x] V-12c 새 붓 획 가져오기 · 먼저: V-10b — 2026-11-04 같은 메뉴로 가져옴, .meta 는 spritePixelsToUnits 소수 끝자리만 바뀌어 그대로 커밋, 콘솔 에러 0
  - 할 것: Brush 에 새로 든 무공 일곱(taiji·chain·claw·qin·spear·talisman·petals) × 3단과 절기 천지인(cheonjiin_main_1·2, cheonjiin_wave_1) 텍스처의 가져오기 설정이 그대로인지 본다. 무공에 붙이는 건 V-06(아직 Unity 항목 아님).
  - 쓸 것: `Art/VFX/Brush/<무공>.json`, 클라우드 `tools/bake_brush.py` 가 만든 `.meta`(guid 는 이름에서)
  - 메뉴: V-10b 와 같은 메뉴 한 번 더
  - 끝: `git status` 에 .meta 가 안 바뀌거나, 바뀌면 그대로 커밋. 콘솔 에러 0
  - 찍기: 없음(인스펙터 확인)

- [x] BGM-03a 배경음 가져오기 설정 — 2026-11-04 `Assets/Editor/BgmImport.cs`(Streaming·Vorbis 70%·Preload 끔·Mono 끔) 컴파일·콘솔 에러 0. 곡이 아직 없어 인스펙터 확인은 곡이 들어온 뒤
  - 할 것: `Assets/Editor` 의 `BgmImport.cs`(AssetPostprocessor) — `Assets/Audio/BGM/*.ogg` 는 Load Type Streaming, Compression Vorbis 품질 70%, Preload Audio Data 끔, Force To Mono 끔. 곡을 트는 쪽은 기획이 정해진 뒤.
  - 쓸 것: `Assets/Audio/BGM` 의 `bgm.json`(id·file·loop·seconds, `tools/postprocess_audio.py` 가 넣음). 아직 곡이 없을 수 있다
  - 메뉴: 없음(가져올 때 자동)
  - 끝: 컴파일·콘솔 에러 0. 곡이 하나라도 있으면 인스펙터에서 네 설정 확인
  - 찍기: 없음

- [x] L-01b1 Loc 컴파일과 간체 글꼴 — 2026-11-04 Loc 와 화면들 컴파일 에러 0, 메뉴 `간체 글꼴(Noto Serif SC SDF)`(HudBootstrap.cs)로 동적 SDF 2048 을 만들어 KR 대체 목록 맨 뒤에 걸음. 시험 글자 두부 없음. 저장소가 무겁지 않게 SC 에셋의 동적 글리프는 비워 둠(실행 때 채움)
  - 할 것: `Presentation/Loc.cs` 와 Loc 로 바꾼 화면들(LevelUpScreen·DiscoveryFx·SecretWatch·SettlementScreen·ChimbangScreen·ChimbangMap·LibraryScreen·ManualNames·TrainingScreen·MetaShop·RegionHud)이 같이 컴파일되는지. `Art/Fonts/NotoSerifSC-Regular.otf` 로 TMP 동적 SDF(`Art/Fonts/NotoSerifSC SDF.asset`, Dynamic, 아틀라스 2048)를 만들어 NotoSerifKR SDF 의 대체 글꼴 맨 뒤에 건다.
  - 쓸 것: `Resources/Data/strings/*`, `Loc.T`·`Loc.F`·`Loc.Lang`
  - 메뉴: `BloodRain/Bootstrap/간체 글꼴(Noto Serif SC SDF)` 를 새로(본문 글꼴 메뉴와 같은 파일에)
  - 끝: TMP 글자 하나에 `血雨江湖 华山 剑气 Inner Energy` 를 넣어 두부(□) 없이 찍힘, 콘솔 에러 0
  - 찍기: `docs/unity_shots/L-01b/` 의 `font_sc.png`

- [x] L-01b2 언어별 넘침: 레벨업·발견 연출 · 먼저: L-01b1 — 2026-11-04 영어에서 인장 글자가 72px 칸을 넘고 머리글과 겹치던 것을 고침(인장·이름·머리글·설명 TMP Auto Size, 인장이 있으면 머리글은 인장 왼쪽까지). 발견 시험 메뉴도 Loc 로. 메뉴 `언어 바꾸기/ko·en·zh-Hans`(LangMenu.cs). 세 언어 칸 안, 콘솔 에러 0
  - 할 것: `Loc.Lang` 을 ko·en·zh-Hans 로 바꿔 레벨업 카드 글이 칸을 넘지 않는지, 영어 문파 인장(이름 전체)이 72px 칸에서 읽히는지 본다. 넘치면 TMP Auto Size(최소 크기 정해서)나 줄임으로 맞춘다.
  - 쓸 것: `LevelUpScreen`, `DiscoveryFx`, `BloodRain/Test/발견 연출 보기(플레이 중)`
  - 메뉴: `BloodRain/Test/언어 바꾸기/ko·en·zh-Hans` 셋을 새로(PlayerPrefs `lang`)
  - 끝: 세 언어 모두 카드·인장·발견 연출 글자가 칸 안
  - 찍기: `docs/unity_shots/L-01b/` 의 `levelup_<ko|en|zh>.png`, `discovery_en.png`

- [x] L-01b3 언어별 넘침: 침방·장경각·정산·연무장·지역 HUD · 먼저: L-01b2 — 2026-11-04 영어에서 넘친 넷을 고침: 침방 카드 이름이 번호와 겹침(이름 칸을 번호 아래로·240px·Auto Size 22~38), 정산 무공 이름이 막대를 덮음(이름 칸 230px·막대 340px·Auto Size)과 밟은 땅 줄·봉인석 줄이 칸 밖으로(한 줄 Auto Size), 연무장 `Projectile Speed`·`Lightfoot Charges`·`Dantian Charge` 가 점을 덮음(MetaShopRow 이름·효과 Auto Size). 벽 지도 지명도 한 줄 Auto Size. 장경각·간체 다섯 화면은 원래 칸 안. 지역 HUD 는 가장 긴 줄(en `The Hundred Thousand Mountains 十萬大山 · Blood Cloud 3.5`)이 795px 로 900px 칸 안이라 고치지 않음(측정만, 스크린샷 없음). 본 것: 간체 장경각의 `尚·条·它·残` 몇 글자가 다른 글꼴 모양으로 보임(대체 순서), 영어 벽 지도 남쪽 지명끼리 조금 겹침. 콘솔 에러 0
  - 할 것: 영어·간체로 침방 카드 본문(280px)·벽 지도 지명(160px)·장경각 '다음 복원' 줄·정산 오른쪽 줄·연무장 항목·지역 HUD 이름이 넘치지 않는지. 넘치면 L-01b2 와 같은 방법.
  - 쓸 것: 각 시험 씬 메뉴(침방·장경각·정산·연무장 시험 씬 만들기)
  - 메뉴: L-01b2 의 언어 바꾸기
  - 끝: 다섯 화면 × en·zh 에서 넘침 없음
  - 찍기: `docs/unity_shots/L-01b/` 의 `<chimbang|library|settlement|training|region>_<en|zh>.png`

- [x] L-08b 번체 글꼴과 언어별 대체 순서 · 먼저: L-01b1 — 2026-11-04 메뉴 `번체 글꼴(Noto Serif TC SDF)` 로 동적 SDF 2048, `Presentation/FontFallback.cs` 가 Loc 언어를 읽을 때 SC·TC 둘의 자리만 바꿈(zh-Hant 면 TC 먼저, YujiBoku 자리는 그대로). 레벨업·침방 번체 두부 없음. `Loc.FromSystem` 은 원래 번체→zh-Hant. 본 것: 번체 침방의 대표 인물이 `唐 Yeonhwa` 처럼 로마자(zh 문자열에 인물 이름이 빠진 듯)
  - 할 것: `Art/Fonts/NotoSerifTC-Regular.otf` 로 TMP 동적 SDF(`NotoSerifTC SDF.asset`, Dynamic, 아틀라스 2048). 간체·번체가 같은 코드포인트를 많이 쓰므로 `Loc.Lang` 이 zh-Hant 면 TC 를, zh-Hans 면 SC 를 KR 대체 목록 앞쪽에 두는 작은 스크립트(Presentation 쪽, 언어를 바꿀 때 `TMP_Settings.fallbackFontAssets` 나 KR 에셋 fallbackFontAssetTable 순서를 바꿈)가 필요하면 만든다. 기기 언어가 번체면 zh-Hant 로 시작하는지(`Loc.FromSystem`).
  - 쓸 것: `Presentation/Loc.cs`, `Resources/Data/strings/zh-Hant.json`
  - 메뉴: `BloodRain/Bootstrap/번체 글꼴(Noto Serif TC SDF)` 를 새로(L-01b1 메뉴와 같은 파일), L-01b2 의 언어 바꾸기에 zh-Hant 하나 더
  - 끝: `Loc.Lang = "zh-Hant"` 로 레벨업 화면·침방 카드에 `血雨江湖 華山 劍氣 輪迴之氣 「一方之主」` 가 두부 없이, 간체로 바꾸면 간체 모양으로 찍힘
  - 찍기: `docs/unity_shots/L-08b/` 의 `levelup_zht.png`·`chimbang_zht.png`

- [x] LOC-01 침방 능력치 이름이 한국어로 나오는지 — 2026-11-05 03:36 PC API: 컴파일 에러 0. Inn 씬 침방(두 번째 환혼, 성질 붙은 후보 셋)에서 `양체 공력 +5%`·`약골 기혈 −25% · 심득 +20%` 처럼 한국어. 타고난 성질 열두 능력치와 StatKind 서른 남짓을 `FormatStat`·`Loc.Stat` 으로 돌려 영어 글자 0(연무장 MetaShop 도 같은 `Loc.Stat`). 본 것: 침방 머리글(제목·부제)이 들보 그림에 묻혀 잘 안 읽히고, 오른쪽 아래 '환혼 강화' 표 글자도 탁자 그림과 겹침. 찍음 `docs/unity_shots/LF-03/chimbang_ko.png`
  - 할 것: 컴파일 확인 → Inn 씬 침방을 열어 타고난 성질 줄과 연무장 줄이 '공력·쾌속'처럼 나오는지 본다(LF-03 때 `Might`·`CooldownReduction` 이 그대로 나왔음).
  - 고친 것(2026-11-05 03시 클라우드): `Loc.Stat` 이 두 인자 `T(key, 영어 키)` 라 한국어에선 영어 키를 돌려줬다. 한 인자 `T(key)` 로 바꿈. strings 넷에 StatKind 나머지 여덟(치명 확률·넉백·정예 피해·화상 피해·폭발 범위·지속 피해·흡혈·회피) 더함. `ScreenLocalizationTests` 가 ui. 키 두 인자 T 를 막음
  - 끝: 침방 카드·연무장에 영어 키가 하나도 없음, 콘솔 에러 0
  - 찍기: `docs/unity_shots/LF-03/` 의 `chimbang_ko.png`

- [x] LF-03b 정산 무공별 피해 막대가 차는지 — 2026-11-05 20:50 PC: 컴파일 에러 0. 소림 몸으로 1분 50초(나한권·창천검기) 뒤 지금 쓰러지기 → 정산 막대 '나한권 81% · 창천검기 19%', 이름 한국어, 콘솔 에러 0. 남은 기혈을 넘긴 몫까지 세지만 막대 비중은 어색하지 않아 EnemyField.Damage 쪽은 그대로 둠. 본 것: 정산 위 왼쪽 OnGUI 디버그 줄이 아직 흐리게 겹침. 찍음 `docs/unity_shots/LF-03/settlement_damage.png`
  - 고친 것(2026-11-05 05시 클라우드): `LifeRunner` 가 정산에 빈 `DamageByWeapon` 을 넘기던 것을 `weapons.Dealt`(WeaponRunner 가 무공 id 별로 센 합)로. 컴파일은 클라우드에서 못 봤다. 테스트 `SettlementWiringTests`
  - 할 것: 컴파일 확인 → 천하 몸으로 무공 둘 이상 들고 1분쯤 싸운 뒤 `지금 쓰러지기` → 정산 왼쪽 막대에 무공 이름과 비중이 나오는지
  - 끝: 막대가 빈칸이 아니고 이름이 한국어, 콘솔 에러 0
  - 본 것으로 남길 것: Dealt 는 남은 기혈을 넘긴 몫도 센다(웹 시안은 뺌). 막대가 어색하면 EnemyField.Damage 쪽에서 깎을지 적는다
  - 찍기: `docs/unity_shots/LF-03/` 의 `settlement_damage.png`

- [ ] V-04d 웹 빌드에서 붓 획 한 장
  - 할 것: WebGL 빌드를 로컬로 띄워 삼재검법·AI 재료 무공이 브라우저에서도 에디터와 같은 색·순서로 그려지는지(셰이더가 WebGL2 에서 빠지지 않는지).
  - 쓸 것: V-04 셰이더·머티리얼, `FxSettings`
  - 메뉴: `BloodRain/WebGL 빌드` (WebGLBuild.cs)
  - 끝: 브라우저 화면에 붓 획·먹 자국·AI 재료가 보이고 브라우저 콘솔에 셰이더 오류 없음
  - 찍기: `docs/unity_shots/V-04/` 의 `webgl.png`
  - 막힘: 2026-11-04 22:37 PC 세션 — 에디터 메뉴로 빌드는 성공(42.3MB, 13분 25초, 장면 3개). 로컬(br 헤더 붙인 python 서버, `tools/serve_webgl.py`)에서 열면 WebGL2 컨텍스트·데이터 로드·DataBoot 로그까지 나온 뒤 첫 프레임에 `RuntimeError: null function` 으로 멈춰 화면이 안 나옴. 셰이더 오류는 URP 기본 `Hidden/CoreSRP/CoreCopy`·`HDRDebugView` 둘뿐(흔한 것). 다음: Development Build + Full stack trace 로 다시 빌드해 어느 함수인지 보기(IL2CPP 코드 스트리핑이 리플렉션으로 쓰는 것을 지웠을 가능성, link.xml)
  (진행: 2026-11-05 03:40 PC API — 메뉴 둘 더함 `WebGL 빌드(개발·스택 추적)`·`WebGL 빌드(릴리스·함수 이름)`(WebGLBuild.cs, 끝나면 설정을 되돌림). `tools/serve_webgl.py` 가 둘째 줄 주석 `Content-Encoding: br` 를 파이썬이 인코딩 선언으로 읽어 안 뜨던 것을 고침(맨 위 utf-8 선언). 개발 빌드(272MB, 7분)도 릴리스+심볼 빌드(47.5MB, 15분)도 헤드리스 크롬(SwiftShader)에서 `null function` 없이 레벨업 화면·Run 이 돎 — 11-04 이후 코드에서 사라졌거나 심볼을 넣으면 안 나는 것. 셰이더 오류는 CoreCopy·HDRDebugView 둘뿐. 붓 획은 헤드리스 화면에서 흐린 노란 원·먹 점만 보여 판단 못함. OnGUI 디버그 줄 한글은 웹에서 글꼴이 없어 비어 보임. 찍음 `docs/unity_shots/V-04/` 의 `webgl_dev.png`·`webgl_release.png`. 남은 것: 심볼 없는 `WebGL 빌드` 로 한 번 더 열어 멈춤이 정말 사라졌는지, GPU 가 있는 크롬(헤드리스 아님)에서 삼재검법 획을 느리게 보며 `webgl.png`)

- [x] V-12d 절기 붓 획 열다섯 가져오기 · 먼저: V-12c — 2026-11-05 20:58 PC: 같은 메뉴 한 번 더, 열다섯 모두 png·.meta 짝 맞음(muyeong 만 4장). .meta 는 spritePixelsToUnits 소수 끝자리만 바뀌어 그대로 커밋, 콘솔 에러 0. 본 것: 커밋된 Run.unity 의 WeaponRunner 가 brushMaterial·삼재 텍스처·brushCrops 가 빈 채였다(LF-01~03 때 씬을 다시 저장하며 빠진 듯). 메뉴가 다시 채워 같이 커밋
  - 할 것: Brush 에 새로 든 절기 열다섯(baekbo·muyeong·cheonroe·pasan·mancheon·byeokryeok·chilbo·liangyi·palseon·heukryong·hyeolma·pacheon·baekhwa·oroe·maehwa, 각 2장 × stroke·wash·gold)의 가져오기 설정이 V-12c 와 같은지 본다. 절기에 붙이는 건 V-06.
  - 쓸 것: `Art/VFX/Brush/<앞 이름>.json`(evolution 칸), 클라우드 `tools/bake_brush.py` 가 만든 `.meta`
  - 메뉴: V-10b 와 같은 메뉴 한 번 더
  - 끝: `git status` 에 .meta 가 안 바뀌거나, 바뀌면 그대로 커밋. 콘솔 에러 0
  - 찍기: 없음

- [x] SFX-02U 효과음 가져오기 확인 · 먼저: 없음 — 2026-11-05 21:37 PC API: 강제 새로 읽기 뒤 60개 모두 Decompress On Load(10초 넘는 것 없음)·Vorbis 70%·Force To Mono, `sfx_levelup_1` guid 그대로, Unity 가 .meta 를 다시 쓰지 않아 git 변경 0. 콘솔 에러 0(경고는 Input Manager 폐기 예고 하나뿐, 효과음과 무관)
  - 할 것: 클라우드가 Unity 없이 넣은 `Assets/Audio/SFX/*.ogg` 의 .meta(`tools/postprocess_sfx.py` 가 만듦: Decompress On Load, 10초 넘는 것만 Compressed In Memory, Vorbis 70%, Force To Mono, Normalize 끔)를 Unity 가 그대로 읽는지. Unity 가 .meta 를 다시 쓰면(serializedVersion 등) guid 가 그대로인지 보고 그 변경을 커밋. 트는 쪽(Audio Random Container, '0.05초 안 4개')은 소리가 다 모인 뒤 따로.
  - 쓸 것: `Assets/Audio/SFX` 의 `sfx.json`(id·category·variants). 지금은 코드 합성 다섯(징 둘·풍경 8음·심장·범종) 60개
  - 메뉴: 없음
  - 끝: 콘솔 에러·경고 0, 인스펙터에서 `sfx_levelup_1` 설정이 위와 같음, guid 안 바뀜
  - 찍기: 없음

### B. 한 생 뼈대 (Run 씬에 Core Life 붙이기 — 여기서부터 아래가 풀린다)

- [x] LF-01 Run 씬에 한 생(Life) 들이기 — 2026-11-05 00:50 PC: `Presentation/LifeRunner.cs`(한 생, Tick·Absorb·돌파 줌·디버그 줄) + `Editor/LifeBootstrap.cs`(메뉴 `Run 씬에 한 생 붙이기`, RunBootstrap 도 부름). 구슬 시험 RunXp 끔, LevelUpScreen 이 Life 의 Level·Loadout·Charges 를 봄. 돌파 줌 도는 동안엔 레벨업 화면을 미루고, 줌이 끝나며 세상 시간을 1로 돌려도 열린 동안은 0으로 붙잡게 고침. 서장 첫 레벨업 카드 한 장·삼류→이류 돌파 줌 확인, 콘솔 에러 0. 찍음 `docs/unity_shots/LF-01/`(life_debug·levelup·breakthrough)
  - 할 것: `Presentation/LifeRunner.cs` 하나가 Run 씬의 한 생을 든다. 시작할 때 `new Life(soul, body, rng)` — soul 은 `RebirthHandoff.Soul ?? new Soul(data)`, body 는 `RebirthHandoff.Body ?? Rebirth.Prologue(data)`. 매 프레임 `life.Tick(Time.unscaledDeltaTime, 플레이어 자리, enemyField.Count)`(LF-02 전까지 돌려준 LifeSecond 는 버린다). OrbLayer.Absorbed 를 `life.Absorb(xp, orbs)` 로 돌리고(시험 RunXp 끔), LevelUpScreen 이 `life.Level`·`life.Charges`·`life.Loadout` 을 보게 바꾼다. `LifeAbsorb.Breakthroughs` 가 있으면 BreakthroughFx 를 그것으로 띄운다(지금은 시험 RunXp 에서 띄움). 플레이어 자리 단위는 `Life.MoveTo` 주석대로(세계 m ↔ 천하 좌표는 `WorldScale`).
  - 쓸 것: Core `Run/Life.cs`(Tick·Absorb·Level·Realm·Minutes·RegionId·Density·Charges·Loadout), `Meta/Soul.cs`, `Meta/Rebirth.cs`
  - 메뉴: `BloodRain/Bootstrap/Run 씬에 한 생 붙이기` 를 새로(RunBootstrap 도 부르게)
  - 끝: 플레이하면 왼쪽 위 디버그 한 줄(분·레벨·경지·지역·혈운 농도)이 오르고, 구슬을 먹으면 `life.Level` 로 레벨업 화면이 뜨고, 삼류 경지에서 돌파 줌이 Life 쪽에서 뜬다. 콘솔 에러 0
  - 찍기: `docs/unity_shots/LF-01/` 의 `life_debug.png`

- [x] LF-02 생성 디렉터로 적 만들기 · 먼저: LF-01 — 2026-11-05 01:40 PC API: LifeRunner 가 `LifeSecond` 의 Spawns 를 생성 고리에(Held 면 안 만듦), Events 는 웹 runEvent 모양대로(혈인 떼 한 줄 벽·빈틈 두 칸, 포위 원, 무리, 정예, 금두꺼비 가까운 고리), 보부상·등롱·보스는 로그만. EnemyField `testSpawn`(Run 메뉴가 끔, 시험 씬은 켠 채). 천하 몸인데 원점이 서장 땅(PropLayer originRegion)이라 지역이 청운진으로 잡히던 것을 고침 — 한 생이 있으면 몸의 출생지가 원점. 서장 2분 시강만 30여 마리, 마교 몸(십만대산)은 6분 디렉터 출력에 마교도·혈사자가 섞임(6분은 기다리지 않고 디렉터를 따로 불러 같은 길로 세움). 정예는 아직 패턴 없이 쫓기만(E-01). 콘솔 에러 0
  - 할 것: LifeRunner 가 `life.Tick` 이 돌려준 `LifeSecond` 로 적을 만든다. `Spawns`(Enemy·BornRealm·Members)는 화면 밖 둘레에 `EnemyField.SpawnAt(def, 자리)`(덩이 사람 수는 TU-04 덩이 길대로), `Events` 는 정예·무리를 그 자리에, 보스 사건은 로그만(BR-01 에서 씀). `Held` 면 안 만든다. EnemyField 의 시험 생성(targetCount)은 `testSpawn` 체크로 꺼 두고, 시험 씬 메뉴들은 켠 채 그대로 둔다.
  - 쓸 것: Core `Spawning/LifeDirector.cs`(LifeSecond·LifeSpawn·LifeEvent), 서장 몸이면 같은 Tick 이 `PrologueSpawns` 표로 준다, `data.Enemy(id)`
  - 메뉴: LF-01 메뉴가 testSpawn 을 끄게
  - 끝: 서장 몸으로 1~3분 놀아 보면 적 종류·수가 웹 프로토타입 서장과 비슷하게 늘고, 천하 몸(침방에서 고른 몸)이면 서 있는 지역의 고유 적이 섞임. 콘솔 에러 0
  - 찍기: `docs/unity_shots/LF-02/` 의 `prologue_2min.png`, `region_spawn.png`

- [x] LF-03 쓰러짐 → 정산 → 객잔 → 침방 → 다시 Run, 혼 저장 · 먼저: LF-01 — 2026-11-05 02:45 PC: `Presentation/Meta/SoulStore.cs`(soul.json·dialogue.json, .tmp 에 쓰고 바꿔 끼움, 깨진 저장은 .bad 로), LifeRunner 가 PlayerVitals.Died 를 받아 천하 몸이면 End→EndLife→저장→정산(Run 씬의 SettlementScreen, loadInnScene), 서장 몸이면 PlayerDown 대로 각성(기혈 1)·처음부터(RestartPrologue 를 RebirthHandoff.PendingLife 로 넘겨 Run 다시 염). Inn 씬은 메뉴 `객잔 씬(침방) 만들기`(침방 + `InnFlow`, 저장된 혼으로 열고 고르면 저장, 서장 안 마친 혼이면 Run 으로). 시험 메뉴 `혼 저장 지우기`·`서장 마친 혼으로 저장`·`지금 쓰러지기(플레이 중)`. 개방 몸으로 Run → 쓰러짐 → 정산(윤회의 기 +50, 잔고 100) → 침방(두 번째 환혼) → 플레이를 껐다 켜도 '2생, 100' 남음. 서장 몸은 Restart 확인. 콘솔 에러 0. 본 것: 정산 처치 0(20초라 아직 못 잡음, EnemyField.Killed 로 셈), 무공별 피해는 WeaponRunner 가 안 세어 빈칸, 침방 성질 줄 능력치 이름이 영어 키(`Might`·`CooldownReduction`) 그대로, 정산·침방 위에 OnGUI 디버그 줄이 겹침. 찍음 `docs/unity_shots/LF-03/`(run_gaebang·settlement_real·chimbang_after)
  - 할 것: (1) `Presentation/Meta/SoulStore.cs` — `Application.persistentDataPath/soul.json` 에 `Soul.ToJson`, 읽기는 `Soul.FromJson`(없으면 새 혼). `DialogueProgress.ToJson` 도 같은 폴더 `dialogue.json` 에(DL-04b 등이 씀). (2) 천하 몸 기혈 0 → `life.End(LifeEndWay.Fallen, 남은 은자)` → `soul.EndLife(record)` → 저장 → SettlementScreen 을 그 summary 로 열기(지금은 시험 혼). (3) 정산 → Inn 씬 → 침방 `ChimbangScreen.Open(soul, rng)` → 고르면 저장 → Run. 서장 몸이면 `life.PlayerDown()` 결과(Restart 는 `life.RestartPrologue`, Awaken 은 각성 이미 시작)만 따르고 정산으로 가지 않는다(서장 끝은 C-10b).
  - 쓸 것: Core `Life.End`·`Record`, `Soul.EndLife`·`OpenRebirth`·`ToJson`·`FromJson`, `LifeSummary`, `Meta/InnDialogue.cs` 의 `DialogueProgress`
  - 메뉴: `BloodRain/Test/혼 저장 지우기`·`BloodRain/Test/지금 쓰러지기(플레이 중)` 를 새로
  - 끝: 침방에서 고른 몸으로 Run → 일부러 쓰러짐 → 정산에 그 생의 숫자 → 침방 → 새 몸으로 Run. 플레이를 멈췄다 다시 켜도 윤회의 기 잔고·환혼 횟수가 남음. 콘솔 에러 0
  - 찍기: `docs/unity_shots/LF-03/` 의 `settlement_real.png`, `chimbang_after.png`

- [x] C-06a 몸 능력치 얹기(덧셈) · 먼저: LF-01 — 2026-11-05 04:55 PC: 남은 것 마무리. 개수 → WeaponRunner.extraAmount(비도술·벽력탄·뇌부 개수에 더함), 지속 → durationMultiplier(장풍 수명). 행낭 카드 은자는 한 생이 있으면 `life.GainSilver`, LifeRunner 는 `_life.End(way)` 로 지갑 값 정산. 천수공·주선공을 넣으면 개수 +1·지속 x1.08 이 무공에 들어가고 GainSilver 50 → 지갑 50 확인, 콘솔 에러 0. 찍음 `docs/unity_shots/C-06/stats_amount_duration.png`(앞선 진행: 쾌속·범위·기혈·호신·걸음·흡기, 소림 둘 시너지·연무장 쾌속 혼 확인 — 03:00 PC)
  - 할 것: 능력치 한 장을 `life.CurrentStats(extra, 기혈/최대, 이번 프레임 움직였나)` 로 받아 몸과 무공에 얹는다. extra = `Synergy.Compute(…, soul.RestoredManuals).StatBonus()` + `life.InsightBonus(처치, 기혈 비율)`. 최대 기혈 → PlayerVitals.MaxHealth, 걸음 → PlayerController, 쿨다운·범위·지속·개수(연무장 몫 포함) → WeaponRunner, 흡기 → OrbLayer 자석. 레벨업으로 고를 때·돌파·기혈 50% 를 넘나들 때·멈춤↔움직임이 바뀔 때 다시 부른다. 레벨업 화면의 은자 50 은 Life 쪽 지갑으로.
  - 쓸 것: Core `Life.CurrentStats`·`InsightBonus`, `Build/Synergy.cs`·`Stats.cs`(StatSheet), `Life.Charges`(연무장 덧셈 포함)
  - 메뉴: 없음(LF-01 메뉴로 붙은 LifeRunner 가 함)
  - 끝: 소림 심법 둘을 고르면 호신 +2·최대 기혈 +15% 가 디버그 줄에 보이고, 쾌속을 찍은 연무장 혼으로 들어가면 무공 쿨다운이 줄어듦. 콘솔 에러 0
  - 찍기: `docs/unity_shots/C-06/` 의 `stats_debug.png`

- [x] S-04a 시너지 기록 · 먼저: C-06a — 2026-11-05 21:37 PC API: 남궁 몸(서장 마친 시험 혼, 복원 비급 없음)에 삼재검법·창천검기를 4단까지 넣으니 LockedManualIds = ssanggeom, RefreshSeals 가 NoteSynergy 로 남기고 쓰러지자 정산 오른쪽에 '찢긴 비급 장경각에 오름 · 쌍검합벽'. 콘솔 에러 0. 찍음 `docs/unity_shots/S-04/settlement_torn_real.png`. 본 것: 정산 위 왼쪽 OnGUI 디버그 줄이 아직 겹침
  - 할 것: 시너지가 바뀔 때마다(레벨업 화면 RefreshSeals 자리) `life.Log.NoteSynergy(…)` 를 부른다. 정산 화면의 '찢긴 비급' 줄이 실제 생의 `LifeSummary.NewTornManuals` 로 나오는지.
  - 쓸 것: Core `LifeLog.NoteSynergy`, `LifeSummary.NewTornManuals`
  - 메뉴: 없음
  - 끝: 비급 층 연환을 처음 맞춘 생을 끝내면 정산에 그 비급 이름이 오름
  - 찍기: `docs/unity_shots/S-04/` 의 `settlement_torn_real.png` (진행: 2026-11-05 21:12 PC — LevelUpScreen.RefreshSeals 가 시너지가 바뀔 때 `LifeRunner.Instance.Life.Log.NoteSynergy(syn)` 를 부르게 함, 컴파일·콘솔 에러 0. 확인은 못 함: 소림 몸에 심법 여섯·나한권+장풍·음공을 4단까지 코드로 넣어도 LockedManualIds 가 비어 찢긴 비급 후보가 안 생김(복원 목록은 빈 채). 남은 것: 잠기는 조합(secret 층 연환, 예: 쌍검합벽 삼재검법+창천검기)으로 다시 맞춰 정산 줄 찍기)

- [x] C-06b 곱하는 진전 효과 · 먼저: C-06a — 2026-11-05 21:40 PC API: LifeRunner.ApplySynergyEffects 가 0.25초마다 `SynergyEffects.Of(syn)` 를 넣음 — 무공 한 방(WeaponRunner.Damage, 남궁 2 검·개방 3 낮은 기혈), 맞는 적(EnemyField.Damage, 곤륜 3 강시), 경공 미끄러짐(PlayerController.DashDistanceMultiplier, 수로채 3), 받는 피해(PlayerVitals.deflect, 무당 3 태극일 때), `Gauge.SetSynergyHarmonyRange`. 시험 메뉴 `진전 채우기(플레이 중)/<문파>` 아홉(SynergyTestMenu.cs). 서장 몸에 남궁을 채우니 삼재검법 한 방 12 → 13.44(x1.12), 무당을 채우니 태극 폭 25 → ±35·태극일 때 피해 10 → 8, 디버그 셋째 줄에 진전 배율. 콘솔 에러 0. 남은 것: 사량발천근 되돌림 피해(부딪힌 적을 아직 모름), 곤륜 2 정신부·마도 2 흡혈·음식은 그 장치가 없어 `LifeRunner.Effects` 만 열어 둠(C-09b 등에서 읽기), 처치 몫 Dealt 는 강시 배율 전 값. 찍음 `docs/unity_shots/C-06/school_mult.png`(디버그 글자가 종이 위라 흐림)
  - 할 것: `SynergyEffects.Of(syn)` 이 내주는 배율을 그 자리에 곱한다 — 남궁 2 검 피해, 개방 3 낮은 기혈 공력, 곤륜 2·3 정신부·강시 피해, 수로채 3 경공 거리, 무당 3 사량발천근, 마도 2 흡혈·음식. 시너지가 바뀔 때마다 `loadout.Gauge.SetSynergyHarmonyRange(Synergy.Compute(…).HarmonyRange)`.
  - 쓸 것: Core `Build/Synergy.cs` 의 `SynergyEffects`, `YinYangGauge`
  - 메뉴: `BloodRain/Test/진전 채우기(플레이 중)/<문파>` — 그 문파 심법을 단계까지 넣어 주는 시험 메뉴
  - 끝: 남궁 2단계 전후로 검 무공 한 방 피해가 배율만큼 달라짐(디버그 숫자), 무당 2단계면 태극 폭이 ±35
  - 찍기: `docs/unity_shots/C-06/` 의 `school_mult.png`

- [ ] C-06c 모양 있는 진전 효과 다섯 · 먼저: C-06b
  - 할 것: 꽃잎(화산 3)·하늘 검기(남궁 3)·독 번짐(당가 3)·독침(당가 5)·경공 끌기(수로채 3)를 WeaponRunner·PlayerController 에. 숫자는 `Tier(effect).Param(…)`(머리 주석에 뜻). 그림은 있는 AI 재료(fx_petal·fx_sword_qi 등)를 AiFxLayers 로.
  - 쓸 것: Core `SynergyEffects`·`Tier(...).Param`, synergies.json
  - 메뉴: C-06b 의 진전 채우기
  - 끝: 다섯이 각각 화면에서 나가고 피해가 들어감. 콘솔 에러 0
  - 찍기: `docs/unity_shots/C-06/` 의 `school_shapes_<huashan|namgung|tang|tang5|suro>.png`
  (진행: 2026-11-05 22:40 PC — 셋 붙임. 남궁 3 하늘 검기 `WeaponRunner.SkySword`(검 계열 무공 3번째 공격마다 8m 안 가장 가까운 적 자리에 0.22초 뒤 반경 1.2m, 한 방 x1.2, 그림 fx_sword_qi_glow), 당가 5 독침 `StepNeedles`(20초마다 화면 안 모든 적에 8, 그림 fx_dagger_ink 초록 먹 80개까지), 수로채 3 끌기 `PlayerController.Drag`(경공 중 1m 안 잡몹을 걸음 x0.9, LifeRunner 가 넣음). 진전 채우기 남궁·당가·수로채 뒤 15초 안에 하늘 검기 3번·독침 4마리·끌림 확인, 콘솔 에러 0. 찍음 `school_shapes_namgung.png`(순간을 맞춘 것은 아님). 남은 것: 화산 3 꽃잎은 무공에 치명타가 없어서, 당가 3 독 번짐·독침 중독은 적 중독 상태가 없어서 못 붙임 — 둘 다 그 장치가 먼저. 끌기는 Family elite·boss 를 뺌. 독침·꽃잎의 '× 공력'은 무공에 공력이 아직 안 곱해져 값 그대로)

- [x] QA-05U 화면 가장자리 땅의 주인 표시 · 먼저: LF-03 — 2026-11-05 22:45 PC: Run 씬에 EdgeIndicators 가 아예 없어서(시험 씬에만) LifeBootstrap.Attach 가 붙이게 하고, 씬을 새로 짓지 않는 메뉴 `Run 씬 한 생 연결만 다시` 로 넣음. LifeRunner.Start 가 `Bind(life.Bosses)`, 묶이면 EdgeIndicators 는 UpdateRealm 을 안 부르고 LifeRunner 가 `LifeAbsorb.WokenBosses` 를 `Woke` 로 넘겨 기둥을 세움(Life 와 둘이 부르면 새로 깬 것이 한쪽에만 보임). 메뉴 `혼에 봉인석 넣기/0·6·7`(SoulBootstrap.cs). 경지 끝까지 올려 보니 봉인석 6 → 일곱 곳만, 7 → 봉마곡까지 여덟 곳 깨어나 기둥 8, 콘솔 에러 0. 확인 못 한 것: 쓰러뜨린 땅의 주인 혈영 표시(보스 처치가 BR-01 뒤). 시험 혼은 봉인석 0 으로 되돌려 둠. 찍음 `docs/unity_shots/QA-05U/edge_7stones.png`(경지를 한꺼번에 올려 돌파 줌 도중)
  - 할 것: `Presentation/Fx/EdgeIndicators.cs` 가 빈 `new BossRecord()` 대신 `EdgeIndicators.Bind(life.Bosses)`(이미 있음)를 받게 LifeRunner 가 부른다.
  - 쓸 것: Core `Life.Bosses`(BossAwakening), `Soul.SealStoneCount`
  - 메뉴: `BloodRain/Test/혼에 봉인석 넣기(0·6·7)` 를 새로(SoulStore 를 고쳐 씀)
  - 끝: 봉인석 6개 혼이면 봉마곡 표시가 안 뜨고 7개면 뜸. 쓰러뜨린 땅의 주인은 혈영 표시로
  - 찍기: `docs/unity_shots/QA-05U/` 의 `edge_7stones.png`

- [ ] RM-02b 혈정 잇기 · 먼저: LF-03
  - 할 것: 정예가 쓰러질 때 `life.KillElite()`(이번에 얻은 혈정 수) 를 부르고 둘레에 '혈정 +N' 짧게. 정산 화면 `SettlementStats.BloodCrystal` 은 전투 쪽이 세지 말고 `LifeRecord.BloodCrystal`.
  - 쓸 것: Core `Life.KillElite`, `LifeRecord.BloodCrystal`, `Soul.BloodCrystal`(저장 `bloodCrystal`), 정예인지는 `EnemyDef.Family == "elite"`
  - 메뉴: 없음
  - 끝: 정예 둘 잡고 쓰러지면 정산 혈정 2, 저장된 혼 잔고도 2 늘어남. 혈월 혼이면 1·2개 번갈아
  - 찍기: `docs/unity_shots/RM-02b/` 의 `settlement_crystal.png`
  (진행: 2026-11-05 22:50 PC — LifeRunner.OnKilled 가 Family elite 면 `_life.KillElite()` 를 부르고 받은 수만큼 그 자리에 붉은 `혈정 +N`(키 ui.settle.crystal, OnGUI 1.2초)을 띄우게 씀. 정산은 원래 `record.BloodCrystal` 을 씀. 컴파일을 요청한 뒤 Unity 메인 스레드가 멈춰 unityMCP 가 응답하지 않아 에디터를 강제로 닫고, -batchmode 로 열어 컴파일 에러 0 만 확인. **플레이 확인은 못 함** — 남은 것: 정예 둘 잡고 쓰러져 정산 혈정 2·혼 잔고 +2, 찍기)

- [ ] R-08U 도전 과제 셋 세기 · 먼저: LF-03
  - 할 것: 적이 쓰러질 때마다 `life.Log.Kill()`(정예·땅의 주인은 KillElite·DefeatBoss 와 따로 한 번 더), 경공을 쓸 때 `life.Log.Dash()`. 정산 화면에 `soul.EndLife` 가 돌려준 `NewUnlocks` 를 '새로 얻은 무공' 줄로(토큰을 unlock 으로 가진 무공 이름). 정신부 `life.Log.UseTalisman()` 은 C-09b 에서.
  - 쓸 것: Core `LifeLog.Kill`·`Dash`, `LifeSummary.NewUnlocks`, weapons.json `unlock`
  - 메뉴: `BloodRain/Test/처치 수 채우기(플레이 중)`(Log 에 4,990 넣기)
  - 끝: 처치 5,000 을 넘긴 생을 끝내면 정산에 '새로 얻은 무공 · 음공', 다음 생 레벨업 풀에 음공이 나옴
  - 찍기: `docs/unity_shots/R-08U/` 의 `new_unlock.png`

- [ ] R-04Ua 문파 고유 능력: 능력치·회피·피해·회복 · 먼저: C-06a
  - 할 것: (1) C-06a 의 `CurrentStats` 에 healthRatio·moving 을 제대로(남궁 공력·수로채 회피). (2) 피격 직전 `Evade.Roll(stats.Evade, rng)` 가 참이면 피해·넉백 없이 넘김(짧은 잔상). (5) 무공 피해에 `SectTrait.DamageMultiplier(적이 중독됐나)`. (6) 은자 × (1 + Bonus[Greed]), 음식 `Drops.FoodHeal(…, 1 + Bonus[FoodHeal])`. (7) 태극일 때 쾌속에 `life.HarmonyCooldownReduction` 을 더하고 상한으로 자름. (9) 마교 몸은 기혈 회복 전부에 `SectTrait.HealingMultiplier`.
  - 쓸 것: Core `Build/SectTrait.cs`, `life.Body.SectTrait`, `Evade`, `Drops.FoodHeal`
  - 메뉴: `BloodRain/Test/몸 바꾸기(문파)` — 침방 없이 그 문파 몸으로 Run
  - 끝: 수로채 몸이 움직일 때 회피가 일어나고(디버그 숫자), 당가 몸 무공이 중독된 적에 더 아프고, 마교 몸 회복이 줄어듦
  - 찍기: `docs/unity_shots/R-04U/` 의 `evade.png`

- [ ] R-04Ub 문파 고유 능력: 독 면역·곤륜 적 걸음 · 먼저: R-04Ua
  - 할 것: (3) 독 장판(독두꺼비)·독인 안개·독 탄 피해에 `SectTrait.PoisonImmune` 이면 0. (8) 곤륜 몸이면 적 걸음에 `SectTrait.EnemySpeedMultiplier(플레이어와 거리, 축척)`. (4) 물 걸음은 R-13U 에서.
  - 쓸 것: Core `SectTrait.PoisonImmune`·`EnemySpeedMultiplier`, `PoisonFogGimmick.HealthLoss(…, poisonImmune)`
  - 메뉴: R-04Ua 의 몸 바꾸기
  - 끝: 당가 몸이 독 장판 위에서 기혈이 안 줄고, 곤륜 몸 가까이 온 적이 느려짐
  - 찍기: `docs/unity_shots/R-04U/` 의 `poison_immune.png`

- [ ] RM-05U 보부상 화면 · 먼저: LF-02
  - 할 것: 생성 디렉터의 보부상 사건(12·24분, 지금은 로그만)에서 시간을 멈추고 보부상 창을 연다. 품목 셋(이름·값·남은 수·지금 은자), 사면 `MerchantVisit.Buy(id, life)`. 결과가 Bought 면 회복약은 `MerchantVisit.PotionHeal(item, 최대 기혈, SectTrait.HealingMultiplier)` 만큼 기혈을 채우고, 취기부는 등롱 취기부와 같은 길(화면 안 구슬 끌기). 다시 고르기 권은 Core 가 `life.Charges` 에 이미 더한다. 은자 모자람·품목 다 삼은 버튼을 흐리게. 닫으면 시간 되돌림.
  - 쓸 것: Core `Build/Merchant.cs`(MerchantVisit, MerchantBuy), balance.json `merchant`, `life.Silver`, 이름 키 `balance.merchant.items.<id>.name`
  - 메뉴: 시험 메뉴 `지금 보부상 부르기`(은자 500 넣고 창 열기)를 Run 메뉴 파일에 하나 더
  - 끝: 500냥으로 회복약 둘을 사면 260냥이 남고 셋째는 안 눌림, 다시 고르기 권을 사면 레벨업 화면 다시 고르기 수가 하나 늚, 콘솔 에러 0
  - 찍기: `docs/unity_shots/RM-05U/` 의 `merchant.png`

- [ ] BGM-06U 배경음 재생기 · 먼저: LF-03, BGM-03a
  - 할 것: Presentation 에 `BgmPlayer`(AudioSource 둘을 번갈아). 매 프레임 `BgmDirector.Tick(dt, state, 돌파 줌 중인가)` 를 부르고 BgmChange 가 오면 FadeSeconds 동안 등가 출력으로 섞어 넘긴다. state 는 화면(Run·Inn·정산·침방·연무장)과 `life.RegionId`·싸우는 땅의 주인 id·서장 여부, `lifeSeconds: life.Minutes * 60`(10분부터 숭산 격해진 벌, BGM-02b)로 만든다. 곡 파일은 `Assets/Audio/BGM` 의 `bgm.json`(postprocess_audio.py 가 만듦) id 로 찾고, 없으면 조용히 넘긴다(로그 한 줄).
  - 쓸 것: Core `World/Bgm.cs`(Bgm.TrackId, BgmDirector, BgmChange), balance.json `bgm`
  - 메뉴: Run·Inn 씬 Bootstrap 메뉴에 BgmPlayer 붙이기
  - 끝: 숭산 곡이 있으면 Run 에서 숭산 곡이 돌고, 원각이 나오면 1초 만에 보스 곡, 정산 화면으로 1초. 곡이 하나도 없어도 콘솔 에러 0
  - 찍기: `docs/unity_shots/BGM-06U/` 의 `bgm_debug.png`(디버그 줄에 지금 곡 id)

### C. 돌파·음양·오의·보갑

- [ ] C-07a 경지 돌파를 Life 로, 돌파 보상 얹기 · 먼저: LF-01
  - 할 것: 돌파 뒤 몸에 보상을 얹는다 — 최대 기혈 +10(`life.CurrentStats()` 에 이미 들어 있음, PlayerVitals 로), `Dash.SetMaxCharges(life.DashMaxCharges(stats))`, 받는 피해에 `life.DamageTakenMultiplier`(Damage.Taken 뒤), 7칸째 무공 자리. 다시 오른 경지면 연출 시계 2배(`Breakthrough.Start` revisit)가 그대로 나오는지.
  - 쓸 것: Core `World/Breakthrough.cs`, `Progression/RealmRewards.cs`, `Life.RealmRewards`·`DashMaxCharges`·`DamageTakenMultiplier`
  - 메뉴: `BloodRain/Test/다음 경지까지 진기(플레이 중)`
  - 끝: 이류 돌파 뒤 경공 충전 칸이 하나 늘고, 일류 뒤 받는 피해가 10% 줄어듦(디버그 숫자)
  - 찍기: `docs/unity_shots/C-07/` 의 `realm_rewards.png`

- [ ] C-07b 깨달음 3택1 화면 · 먼저: C-07a
  - 할 것: `Presentation/Hud/InsightScreen.cs` — 돌파 연출이 끝나는 자리(BreakthroughFx.Ended)에 시간을 멈추고 카드 석 장(한 번 깨친 경지면 넉 장), 고르면 `life.InsightEffects.Take(def, loadout.Gauge, 처치 수)`. 레벨업 카드보다 먼저 뜬다. 매 프레임 `InsightEffects.Tick(dt, 움직임, 최대 기혈)` 의 회복량을 기혈에, 피해 직전 `TryBlock()` 이 참이면 금종조로 막음, 무공 피해·범위·흡기 배율은 WeaponRunner·OrbLayer 에.
  - 쓸 것: Core `Build/Insights.cs`(`Roll`·`RollWithDemonic` — 마지막 인자에 `soul.IsUnlocked` 를 넘겨야 역근이 극성 땅의 주인 처치 뒤에만 나옴, QA-06 2026-11-05), `Soul.Breakthrough(realm)`(후보 수), `Build/InsightEffects.cs`, insights.json
  - 메뉴: `BloodRain/Bootstrap/Run 씬에 깨달음 화면 붙이기`
  - 끝: 이류 돌파 뒤 석 장이 뜨고, 일검파를 고르면 그 무공 피해가 오름. 마교 몸은 한 자리가 마공
  - 찍기: `docs/unity_shots/C-07/` 의 `insight_cards.png`

- [ ] R4-13a 마교 몸 혈공·마공 · 먼저: C-07b, C-08a
  - 할 것: 진기 배율(농도^0.25)과 심마(더 받은 진기 150마다 음 1)는 `life.Absorb` 가 이미 `life.BloodArt` 를 거쳐 하니 Unity 는 셈하지 않는다. 음양 HUD 에 심마로 기울 때 짧은 표시(`BloodArt.Steps` 가 늘면), 깨달음 화면이 마교 몸이면 `Insights.RollWithDemonic` 로 셋 중 하나를 마공으로, 능력치에 `life.InsightBonus(처치, 기혈 비율)`(심마환영은 천마해체 뒤 0부터). 마공 카드 테두리는 붉게.
  - 쓸 것: Core `Progression/BloodArt.cs`, `Build/Insights.cs`(RollWithDemonic), `Build/InsightEffects.cs`, insights.json `demonicInsights`
  - 메뉴: R-04Ua 의 몸 바꾸기(마교)
  - 끝: 십만대산에서 마교 몸이 진기를 먹으면 음양이 음으로 기울고, 돌파 깨달음 셋 중 하나가 마공
  - 찍기: `docs/unity_shots/R4-13U/` 의 `demonic_insight.png`

- [ ] C-08a 음양 태극 문양 HUD · 먼저: C-06b
  - 할 것: `Presentation/Hud/YinYangHud.cs` — 태극 문양이 게이지 값만큼 기울고, 태극 구간(`gauge.HarmonyRange`) 안이면 금빛 테두리, 극단이면 핏빛. 지금 HUD 진전 인장이 LevelUpScreen 안에 있으면 같은 HUD 묶음으로 옮긴다.
  - 쓸 것: Core `YinYangGauge`(Value·Band·HarmonyRange·Leaning)
  - 메뉴: `BloodRain/Bootstrap/Run 씬에 HUD 붙이기` 를 새로(HudBootstrap.cs 에, RunBootstrap 도 부르게)
  - 끝: 양 무공만 고르면 문양이 기울고 극단 띠에서 색이 바뀜
  - 찍기: `docs/unity_shots/C-08/` 의 `yinyang.png`

- [ ] C-08b 기혈역류 연출 · 먼저: C-08a
  - 할 것: 극단에 머물면 역류 — 걸음 × `Backlash.MoveSpeedMultiplier`, 화면 가장자리 핏빛 맥동, 다음 역류 예고 `SecondsUntilNext` 를 문양 둘레 바늘로, 끊기 창 `CanCancel` 동안 경공으로 끊으면 SecretWatch 기록에 역류 끊기 알림(S-04 '역천').
  - 쓸 것: Core `Combat/Backlash.cs`, `Life.Backlash`, `RunRecord`(SecretWatch)
  - 메뉴: 없음
  - 끝: 극단에서 역류가 걸리고 경공으로 끊김, 세 번 연속 끊으면 역천 발견 연출
  - 찍기: `docs/unity_shots/C-08/` 의 `backlash.png`

- [ ] C-09a 오의 버튼(진소운 일검) · 먼저: C-07a
  - 할 것: 오의 충전 HUD(`life.Charge`), 버튼(키보드 Space·휴대폰 오른쪽 아래)을 누르면 `life.UseUltimate()` 가 참일 때 `life.UltimateCasts` 번 쏜다. 한 방 피해 `Ultimates.HitDamage(data, 몸, loadout, 공력, life.UltimatePowerMultiplier, 절기·음양 배율)`. 그림은 진소운 일검 붓 획(삼재검법 3단 획을 크게) 임시.
  - 쓸 것: Core `UltimateCharge.cs`, `Combat/Ultimates.cs`, `Life.UseUltimate`·`UltimateCasts`
  - 메뉴: C-08a 의 `Run 씬에 HUD 붙이기` 가 오의 버튼도 놓게
  - 끝: 충전이 차면 버튼이 켜지고 화면 안 적이 그 피해로 쓰러짐. 정진(레벨업 건너뛰기)이 충전을 채움
  - 찍기: `docs/unity_shots/C-09/` 의 `ultimate.png`

- [ ] R4-10U 천설부·천마해체 오의 · 먼저: C-09a, R-04Ua
  - 할 것: 곤륜 몸 천설부 — 화면 안 모든 적(땅의 주인 포함)을 `UltimateEffects.FreezeSeconds(ult)` 초 멈춤(걷기·패턴 시계 멈춤, 6장 기절과 같이), 한 방 피해는 `UltimateEffects.HitDamageOn(ult, enemy.Family, 시작 무공 한 방, life.UltimatePowerMultiplier)`(강시 계열만 들어감). 마교 몸 천마해체 — `life.UseUltimate()` 가 켠 `life.Dissolution` 이 켜져 있는 동안 플레이어 피해 × `DamageMultiplier`, 매 프레임 기혈 `HealthCostPerSecond(최대 기혈) × 세상 dt` 소모, 맞힌 피해마다 `UltimateEffects.LifestealHeal(피해, 능력치 흡혈 + LifestealBonus, SynergyEffects.LifestealMultiplier, SectTrait.HealingMultiplier)` 회복. 몸에 붉은 기운(남은 초).
  - 쓸 것: Core `Combat/UltimateEffects.cs`(R4-10), `Life.Dissolution`, characters.json ultimates `cheonseol_bu`·`cheonma_haeche`
  - 메뉴: R-04Ua 의 몸 바꾸기(곤륜·마교)
  - 끝: 천설부로 강시만 쓰러지고 나머지는 3초 멈춤. 천마해체 8초 동안 피해 두 배·기혈이 줄고 때리면 참
  - 찍기: `docs/unity_shots/R4-10U/` 의 `cheonseol_bu.png`, `cheonma_haeche.png`

- [ ] R-15U 문파 오의 다섯(천뢰낙·사자후·만천화우·태극무량·팔방풍우) · 먼저: C-09a
  - 할 것: C-09a 오의 버튼이 몸의 오의를 쏘게. `life.UseUltimate()` 뒤 매 프레임 `life.UltimatePulses` 만큼(한 박자마다 `UltimateCasts` 번) `life.Ultimate` 모양으로 때린다 — 천뢰낙 `StrikePoints(화면 사각형, rng)` 열두 곳·`ShockSeconds`, 사자후 `InRadius(거리, 축척)` 안·`StunSeconds`, 만천화우 화면 전체(1초마다 박자)·`Poisons`, 팔방풍우 `Directions(바라보는 쪽)` 여덟 갈래에 장풍 탄 `Volleys` 번. 태극무량은 받는 피해에 `life.DamageTakenMultiplier`(5초 동안 0)를 이미 곱하면 되고, `ReflectsProjectiles` 동안 몸 둘레 적 탄을 되돌린다. 모양 숫자는 2026-11-05 R-15b 로 Core 에 들어옴 — 천뢰낙 `InStrike(낙뢰 자리, 적, 축척)`, 사자후 그림 `WaveRadiusAt(경과, 축척)`, 만천화우 `PoisonTickDamage(공력)`·`PoisonTickSeconds`·`PoisonSeconds`, 태극무량 `GuardRadius(축척)`, 팔방풍우 `Shot`·`VolleyGapSeconds`·`Knockback`. 하연 몸의 역류도 같이: 바라보는 쪽 화면 끝까지 `InSweep(몸, 바라보는 쪽, 적, 화면 끝 거리, 축척)` 띠 안 적을 때리고 `WaveSpeed`(마천패 역류 파도와 같은 단위)로 끝 쪽으로 민다. 천설부·천마해체는 R4-10U 줄
  - 쓸 것: Core `Combat/UltimateCast.cs`, `Life.Ultimate`·`UltimatePulses`, `Ultimates.HitDamage`
  - 메뉴: R-04Ua 의 `몸 바꾸기(문파)` + `BloodRain/Test/오의 가득`
  - 끝: 소림·개방·남궁·당가·무당 몸에서 오의가 5장 표 모양으로 나감
  - 찍기: `docs/unity_shots/R-15U/` 의 `<오의 id>.png`

- [ ] C-09b 등롱·음식·은자 주머니·정신부 · 먼저: C-06a
  - 할 것: 등롱(맵 소품)을 깨면 `Drops.RollLantern(data.Balance.Drops, rng)` 하나 — bun·duck 은 `Drops.FoodHeal(rule, kind, stats.FoodHeal)` 회복, silver_bag 은 `RollSilverBag`(탐욕·혈월 배율은 줍는 쪽), chwigi_talisman, jeongsin_talisman 은 `Balance.Drops.JeongsinFreezeSeconds` 동안 화면 안 적 정지 + `life.Log.UseTalisman()`·`RunRecord.UseTalisman`. 잡몹 은자는 `EnemyDropsSilver`.
  - 쓸 것: Core `Build/Drops.cs`, balance.json drops
  - 메뉴: `BloodRain/Test/등롱 놓기(플레이 중)`
  - 끝: 등롱 열 개를 깨면 다섯 가지가 섞여 나오고 각각 효과가 듦
  - 찍기: `docs/unity_shots/C-09/` 의 `lantern_drops.png`

- [ ] QA-02U 의서 은빛 등롱 · 먼저: C-09b
  - 할 것: 은빛 등롱을 놓을 때 `lifeLog.ManuscriptsHere` 가 참일 때만(혈월 천하, cheonha.json `cycle.bloodmoon.manuscriptsOnlyHere`). 주우면 `lifeLog.FindManuscript()`(1회차면 false 라 세지 않음), 셌으면 SecretWatch 기록에 의서 알림.
  - 쓸 것: Core `LifeLog.ManuscriptsHere`·`FindManuscript`
  - 메뉴: C-09b 의 등롱 놓기 + LF-03 의 혼 저장(회귀한 혼을 만드는 시험 메뉴 `BloodRain/Test/혼 회귀시키기`)
  - 끝: 1회차 저장으로 들어가면 은빛 등롱이 안 보이고, 회귀한 저장에서 보임
  - 찍기: `docs/unity_shots/QA-02U/` 의 `silver_lantern.png`

- [ ] C-09d 보갑과 각성 · 먼저: C-09b
  - 할 것: 정예가 보갑을 떨군다(쌍강시 대형은 한 쌍에 하나). 열면 `Chest.Open(data, loadout, 레벨, rng, extraUpgrades)` — 강화 목록이면 짧은 목록 화면, 각성(Awakened)이면 J-01U 각성 연출(AwakeningFx) 끝에 `Loadout.Evolve`·`life.Awaken(evolution)`. 장강수로채 진전 4단계면 extraUpgrades 1.
  - 쓸 것: Core `Build/Chest.cs`, `Awakening.cs`, balance.json chest, evolutions.json
  - 메뉴: `BloodRain/Test/보갑 놓기(플레이 중)`, 각성 조건을 맞춘 시험 몸(삼재검법 Lv8 + 짝 심법)
  - 끝: 조건이 안 맞으면 강화 목록, 맞으면 각성 연출 뒤 무공 이름이 절기로 바뀜
  - 찍기: `docs/unity_shots/C-09/` 의 `chest_upgrade.png`, `chest_awaken.png`

- [ ] C-09e 절기 모양 넷과 오뢰정법 · 먼저: C-09d
  - 할 것: WeaponRunner 에 각성한 무공의 절기 모양을 붙인다. 숫자는 `EvolutionDef.Param`(waveSpeed·waveRadius·waveLifeSeconds, projectileSpread·Speed·Radius·LifeSeconds, bounceRange, explodeRadius·Damage, chainDelaySeconds·chainShrink·chainDamage). 기획서 9장 '절기로 바뀐 뒤 날아가는 것' 표에 있는 시작 무공 짝 넷부터, 다음으로 뇌부 절기 오뢰정법(사거리 ×1.5 안 서로 다른 적 `strikes`+연무장 개수 곳, 반경 ×`radiusMultiplier`, 맞은 적마다 `lightningRange` 안 다음 적으로 `lightningChains` 번 `lightningFalloff` 배, 늘 감전).
  - 쓸 것: Core `EvolutionDef.Param`, evolutions.json params, 웹 프로토타입 같은 이름 함수
  - 메뉴: C-09d 의 시험 몸
  - 끝: 각성 뒤 다섯 절기가 웹 프로토타입과 같은 모양으로 나감
  - 찍기: `docs/unity_shots/C-09/` 의 `evolved_<id>.png`

- [ ] C-09c 절기 셋 효과 · 먼저: C-09e
  - 할 것: 벽력천화 마지막 연쇄 폭발 자리에 `fireSeconds` 불바다(반경 = 폭발 × `fireRadiusMultiplier`, 안의 적 화상 다시 채움), 천뢰검진 검기 원에 닿은 적마다 `lightningChance` 로 낙뢰(× `lightningDamage`, `lightningRange` 안 `lightningChains` 번 `lightningFalloff` 배 — 오뢰정법과 같은 그리기), 만천성지 폭발은 치명타일 때만(`explodeOnCrit`).
  - 쓸 것: Core `EvolutionDef.Param`, 웹 프로토타입 `fireField`·`lightning`
  - 메뉴: C-09d 의 시험 몸(세 절기 각각)
  - 끝: 셋이 웹 프로토타입과 같은 효과
  - 찍기: `docs/unity_shots/C-09/` 의 `evo_effects.png`

- [ ] R-12Ua 무공 일곱 중 넷(`random_arc`·`orbiting_blades`·`pull_chain`·`forward_claws`) · 먼저: C-06a
  - 할 것: WeaponRunner 에 네 갈래. 성장 숫자는 Core `WeaponGrowth`, 날아가는 것·넉백은 weapons.json(기획서 9장 '날아가는 것과 넉백'). 붓 획은 V-12c 텍스처(taiji·chain·claw). 숫자가 weapons.json 에 비어 있는 무공은 건너뛰고 `막힘:` 한 줄.
  - 쓸 것: Core `Combat/WeaponGrowth`, weapons.json, `docs/design/m3_check.md` '무공 16·절기 16'
  - 메뉴: `BloodRain/Test/무공 넣기(플레이 중)/<id>`
  - 끝: 넷이 Lv1·Lv5·Lv8 에서 웹 프로토타입과 같은 모양·개수로 나감
  - 찍기: `docs/unity_shots/R-12U/` 의 `<id>.png`

- [ ] R-12Ub 무공 일곱 중 셋(`ring_wave`·`pierce_thrust`·`petal_scatter`) · 먼저: R-12Ua
  - 할 것: R-12Ua 와 같은 방법. 붓 획 qin·spear·petals.
  - 쓸 것: R-12Ua 와 같음
  - 메뉴: R-12Ua 의 무공 넣기
  - 끝: 셋이 웹 프로토타입과 같음
  - 찍기: `docs/unity_shots/R-12U/` 의 `<id>.png`

- [ ] R-07U 진전 소림 4·개방 4 · 먼저: C-06b
  - 할 것: 한 생에 `new Core.Build.SchoolSummons()` 하나, 프레임마다 `summons.Tick(SynergyEffects, 기혈/최대, 세상 dt)`. `ArhatStarted` 면 몸 둘레 나한 허상 `Tier("arhat_ring").Param("images")` 개를 `ArhatSecondsLeft` 동안, 닿는 적은 `summons.ArhatPush(fx, 거리, 보스인가, 축척)` 만큼 밀어냄(피해 없음, 2026-11-05 R-07b 데이터). `AlliesSpawned` 만큼 개방 제자를 `AlliesSecondsLeft` 동안(적이 노리지 않음), `AllyVolleys` 만큼 제자마다 가장 가까운 적에게 장풍 Lv1 탄 한 대 — 피해·넉백은 `SchoolSummons.AllyShot(fx, 공력)`.
  - 쓸 것: Core `Build/SchoolSummons.cs`, synergies.json
  - 메뉴: C-06b 의 진전 채우기(소림·개방 4)
  - 끝: 소림 넷 + 기혈 50% 밑 → 허상, 판에 세 번까지 / 개방 넷 → 3분 뒤 셋
  - 찍기: `docs/unity_shots/R-07U/` 의 `arhat.png`, `allies.png`

- [ ] UI-01U 화면 편의 규칙(기획서 12장 '레벨업 카드 규칙'·'화면 편의 규칙') · 먼저: C-06
  - 할 것: 레벨업 카드를 세 줄(이름+Lv a→b 또는 '새' / 바뀌는 수치 하나 / 배지)로, 긴 설명은 마우스 올리기·길게 누르기. 레벨업 옆(세로는 아래)에 무공 6칸·심법 6칸, 고르려는 카드가 켜는 칸을 빛냄. 패드·방향키 포커스, 다시 고르기·파기·정진 단축키 표시, '남은 N'과 이어 고르기. 땅의 주인 체력바(설정에서 끔). 일시정지에 무공별 피해·초당 피해, 설정 열기. 내 이펙트 투명도 0~100%와 적 탄·예고를 위 레이어로. 조합표 탭·자동 고르기·조준·깜빡임 끄기·혈우 연출 세기는 하나씩 따로 커밋해도 됨. 닫힌 뒤 무적·자동 고르기 순서는 backlog UI-01a·b 가 정해질 때까지 비워 둔다.
  - 쓸 것: Presentation `LevelUpScreen`, HUD, 일시정지 화면, 설정(있는 것), Core `WeaponRunner.Dealt`(무공별 피해), strings 네 언어(새 문구는 ui. 키)
  - 메뉴: 없음
  - 끝: 컴파일·콘솔 에러 0, 레벨업 카드 한 장이 세 줄 안, 투명도 0% 에서 적 탄이 보임
  - 찍기: 레벨업 화면(옆 칸 보임), 일시정지 화면

### D. 땅의 주인·정예·서장

- [ ] BR-01 패턴 러너 틀과 원각 1막 · 먼저: LF-02
  - 할 것: `Presentation/Combat/PatternRunner.cs` — `BossPatterns` 시간표를 돌려 예고(바닥 그림자·선·원)와 판정을 그리는 틀 하나. 땅의 주인·정예·장삼이 모두 이걸 쓴다. `life.Bosses.Fight` 가 생기면 원각(`data.BossPatternSet("wongak")`: vajra_cone·eighteen_arhat_ring·bell_drop_x3)을 `Fight.MaxHealth` 로 세운다. 무공 피해가 보스에 들어가게(감전·넉백은 보스 제외 — W-01 남은 것).
  - 쓸 것: Core `BossPatterns`, `World/BossAwakening.cs`(Fight·InFight·SpawnTargetScale), bosspatterns.json `wongak`
  - 메뉴: `BloodRain/Test/땅의 주인 부르기(플레이 중)/wongak`
  - 끝: 원각 1막 세 패턴이 웹 시안 순서·예고 시간대로 돌고 맞으면 피해. 보스전 동안 잡몹 목표가 줄어듦
  - 찍기: `docs/unity_shots/BR-01/` 의 `wongak_<패턴>.png`

- [ ] BR-02 원각 2막과 처치 · 먼저: BR-01
  - 할 것: 기혈 30% 에서 2막(bosspatterns.json wongak phase2), 0 이면 `life.DefeatBoss(남은 은자)` → `BossKill`(봉인석·문파 해금·윤회의 기·혈정·보갑) 을 짧은 붓글씨로, 보갑이면 C-09d 보갑. 혼에 `Soul` 이 기록하는지 정산에서 확인.
  - 쓸 것: Core `Life.DefeatBoss`, `BossKill`
  - 메뉴: BR-01 과 같음
  - 끝: 원각을 쓰러뜨리면 봉인석 하나·소림 해금, 그 생을 끝내면 정산·장경각(봉인석 잔고)에 반영
  - 찍기: `docs/unity_shots/BR-02/` 의 `wongak_kill.png`

- [ ] C-10a 강시 포쾌 장삼 · 먼저: BR-01
  - 할 것: PatternRunner 로 `jang_sam`(rope_throw `bind_line` — 맞으면 `RootSeconds` 걷기 못함, 경공으로 끊김 / whistle_summon 시강 소환 / triple_slam `jump_slam` — Count 번, 뛸 때마다 그 순간 플레이어 자리 그림자). 최대 기혈 `Prologue.BossMaxHealth(boss)`, 2막 없음.
  - 쓸 것: Core `Run/Prologue.cs`, bosspatterns.json `jang_sam`, `PlayerController.Bind`(이미 있음)
  - 메뉴: `BloodRain/Test/땅의 주인 부르기(플레이 중)/jang_sam`
  - 끝: 세 패턴이 웹 프로토타입 장삼과 같은 순서·간격
  - 찍기: `docs/unity_shots/C-10/` 의 `jang_sam.png`

- [ ] C-10b 서장 10분 통과 · 먼저: C-10a, LF-03
  - 할 것: 서장 흐름을 끝까지 — 5:00 정예 강시 도부수(`data.ElitePatternSet("gangsi_axeman")` 부채꼴), 장삼 사건(`life.Prologue.Step` 이 Boss)이면 장삼, 피해는 `life.DamagePrologueBoss(hp, max, dmg)`(바닥 10% 에서 각성), 0 이면 `life.KillPrologueBoss()` → `soul.EndLife(record)` → 컷 `Prologue.CurrentCut`/`NextCut`(그림은 없으면 빈 칸 + 자막) → `soul.OpenRebirth` → `Prologue.ChooseFirst` → 침방. 진소운 기혈 0 은 LF-03 의 PlayerDown 길.
  - 쓸 것: Core `Life`(IsPrologue·Prologue·DamagePrologueBoss·KillPrologueBoss·RestartPrologue), `Spawning/PrologueDirector.cs`
  - 메뉴: `BloodRain/Test/서장 시계 넘기기(플레이 중)/5분·8분30초`
  - 끝: 새 혼으로 서장 10분을 끝까지 놀면 장삼 → 컷 → 첫 환혼 침방까지 끊김 없음
  - 찍기: `docs/unity_shots/C-10/` 의 `prologue_end.png`

- [ ] E-01 숭산 정예 셋 · 먼저: BR-01
  - 할 것: PatternRunner 로 동인(`jump_slam` 3연타), 계율원 강시 승려(`projectile_fan` 세 갈래, 걸으며 쏨), 강시 방장 대리(`circles` + Aim `self`, 맞으면 `StunSeconds` 걷기·경공 못 함, 무공은 나감). LF-02 가 정예 사건으로 세운 적에 붙인다.
  - 쓸 것: `data.ElitePatternSet("dongin"|"gyeyul_monk"|"acting_abbot")`, bosspatterns.json elites
  - 메뉴: `BloodRain/Test/정예 부르기(플레이 중)/<id>`
  - 끝: 셋이 웹 프로토타입 eliteAI 와 같은 모양
  - 찍기: `docs/unity_shots/E-01/` 의 `<id>.png`

- [ ] R-01Ua 정예: 장판·갈고리 세 파·방패 돌진 · 먼저: E-01
  - 할 것: PatternRunner 에 더함 — 착지·판정 자리에 남는 장판(`PoolRadius`·`PoolDamagePerSecond`·`LeaveSeconds`, 겹쳐도 한 번, `BossPatterns.InPool`), 수적 두목 `bind_line` 세 파(`Waves`·`WaveIntervalSeconds`, 파마다 다시 겨눔), 철갑 수채 무사 `line` 방패 돌진(몸째 `ProjectileSpeed` 로 달리고 `RecoverySeconds` 굳음).
  - 쓸 것: bosspatterns.json elites(poison_toad_king·tang_dart_gangsi·dogin_elder·pirate_chief·ironclad_fort_guard), 기획서 6장 '정예 패턴 수치'
  - 메뉴: E-01 의 정예 부르기
  - 끝: 다섯이 기획서 6장 줄대로
  - 찍기: `docs/unity_shots/R-01U/` 의 `<id>.png`

- [ ] R-01Ub 정예: 검진·수귀 부르기·쌍강시 대형 · 먼저: R-01Ua
  - 할 것: 검진 도사 `closing_ring` 검 다섯(`RingOffsets(p, ClosingRadius(p, t), 도사 쪽 각도)`, 검마다 `HitIntervalSeconds`), 수귀 우두머리 `summon`(수귀가 물 밖에서도 나와 따라감), 흑백 쌍강시 대형은 한 쌍에 시간표 하나(검은 몸 먼저, 한 몸이 누우면 선 몸이 두 번).
  - 쓸 것: bosspatterns.json elites(sword_array_taoist·water_ghost_chief·twin_gangsi_great), `EnemyDef.Twin`
  - 메뉴: E-01 의 정예 부르기
  - 끝: 셋이 기획서 6장 줄대로
  - 찍기: `docs/unity_shots/R-01U/` 의 `<id>.png`

- [ ] R4-06U 정예: 화산·곤륜 설원·십만대산 아홉 · 먼저: R-01Ub
  - 할 것: PatternRunner 로 아홉을 돌린다. 새로 필요한 것: 화산 장로의 강시 `cone` 세 파(`Waves` 3·`WaveIntervalSeconds` 0.3, 같은 방향, 멈춰 섬), 설인 왕 `projectile_fan` 한 발(지름 `Width`), 빙혈의 제자 `clones`(설무향 빙분신을 정예에 — 진짜 하나만 그림자, Count 는 진짜 포함 3), 설랑 왕 `summon`(설랑 떼 다섯, 양옆으로 돌아 들어옴)과 `line` 돌진을 번갈아, 혈사자·설랑 왕 `line` 은 철갑 수채 무사 방패 돌진과 같은 길, 혈영회 대주 `blink_slam` 세 번, 산귀 `circles` 세 곳(±8m).
  - 쓸 것: bosspatterns.json elites(plum_ghost_captain·rockfall_mountain_ghost·huashan_elder_gangsi·yeti_king·binghyeol_disciple·snow_wolf_king·blood_lion_elite·four_guardian_gangsi·hyeolyeong_captain), 기획서 6장 '화산·곤륜 설원·십만대산 정예 아홉'
  - 메뉴: E-01 의 정예 부르기
  - 끝: 아홉이 기획서 6장 줄대로
  - 찍기: `docs/unity_shots/R4-06U/` 의 `<id>.png`

- [ ] R-14Ua 당소혜(당가타) · 먼저: BR-02
  - 할 것: PatternRunner 로 `dang_sohye`(half_map_poison_fog `HalfFieldFog.Choose/Covers`, dart_fan_x3, summon_golden_centipede). 2막 `new BossSplit(p, 남은 기혈)`(몸마다 `Damage`, 매 프레임 `Tick` 이 돌려준 몸을 일으킴, 분열 동안 패턴 Tick 멈춤, 자리 `RingOffsets(p, p.Scatter, 각도)`).
  - 쓸 것: Core `BossPatterns`·`BossSplit`·`HalfFieldFog`, bosspatterns.json `dang_sohye`
  - 메뉴: BR-01 의 땅의 주인 부르기/dang_sohye
  - 끝: 1막 셋·2막 분열이 천하 시안과 같음
  - 찍기: `docs/unity_shots/R-14U/` 의 `dang_sohye.png`

- [ ] R-14Ub 마천패(장강) · 먼저: R-14Ua
  - 할 것: `ma_cheonpae`(chain_hook_pull `ChainReach`·`InChain`·`ChainPullOffset` — 경공이면 끊김, water_pillar, summon_water_ghost).
  - 쓸 것: Core `BossPatterns`, bosspatterns.json `ma_cheonpae`
  - 메뉴: 땅의 주인 부르기/ma_cheonpae
  - 끝: 세 패턴이 천하 시안과 같음
  - 찍기: `docs/unity_shots/R-14U/` 의 `ma_cheonpae.png`

- [ ] R-14Uc 혈허진인(무당산) · 먼저: R-14Ub
  - 할 것: `hyeolheo`(yin_yang_split_field — 받는 피해에 `BossPatterns.SplitFieldDamageTaken(p, dx, dy, 예고 때 보스→플레이어 각도, 켜진 뒤 초, gauge.Leaning)`, taiji_inhale_exhale `InhalePull`(경공 중엔 안 끎)·`InBurst`, seven_star_sword_drop `BigDipperOffsets(p, 플레이어→보스 각도)`).
  - 쓸 것: Core `BossPatterns`, bosspatterns.json `hyeolheo`, `YinYangGauge.Leaning`
  - 메뉴: 땅의 주인 부르기/hyeolheo
  - 끝: 세 패턴이 천하 시안과 같음
  - 찍기: `docs/unity_shots/R-14U/` 의 `hyeolheo.png`

- [ ] R4-02Ua 낙매검선의 검혼(화산) · 먼저: R-14Uc
  - 할 것: PatternRunner 로 `huashan_sword_soul`(petal_spiral — `SpiralShotDegrees(p, 플레이어 쪽 각도, 갈래, 발)` 를 `SpiralShotSeconds` 마다, screen_slash — 일섬 뒤 `RecoverySeconds` 굳음, summon_plum_sword_ghost — 매화검기 `RadialBurstDegrees(p, 0, 파)`).
  - 쓸 것: Core `BossPatterns`(R4-02 모양 함수), bosspatterns.json `huashan_sword_soul`
  - 메뉴: BR-01 의 땅의 주인 부르기/huashan_sword_soul
  - 끝: 세 패턴이 천하 시안과 같음
  - 찍기: `docs/unity_shots/R4-02U/` 의 `huashan_sword_soul.png`

- [ ] R4-02Ub 빙혈 설무향(곤륜) · 먼저: R4-02Ua
  - 할 것: `seol_muhyang`(blizzard_narrow_view — 눈보라 덮개 `ViewScaleAt`, 카메라는 그대로 / ice_clones_x4 — `new BossClones(p, rng)`, 진짜만 그림자 스프라이트, 분신 `Hit` 이면 깨짐 연출, `Standing` 이 거짓이면 지움 / ice_spike_chain — `ChainCircleOffset`·`ChainCircleSeconds`·`InChainCircle`). 2막 싸움터 `ArenaRadiusStep(p, r, dt, CampfireGimmick.Inside(…))`, `OutsideArena` 면 0.5초마다 피해(어느 보스 2막인지는 bosspatterns.json 에서 확인).
  - 쓸 것: Core `BossPatterns`·`BossClones`·`CampfireGimmick`, bosspatterns.json `seol_muhyang`
  - 메뉴: 땅의 주인 부르기/seol_muhyang
  - 끝: 세 패턴과 2막 싸움터가 천하 시안과 같음
  - 찍기: `docs/unity_shots/R4-02U/` 의 `seol_muhyang.png`

- [ ] R4-02Uc 혈영회주 혁련휘(십만대산)와 분신 · 먼저: R4-02Ub
  - 할 것: `hyeokryeon_hwi`(blink_x4_slam — `BlinkOffsets`·`BlinkSeconds`·`BlinkSlamSeconds`, giant_blood_palm, mirror_clone). mirror_clone 이 부른 분신은 `Mimicry.For(enemy, pattern)` 의 mimic(가진 무공 모두·피해 절반)을 EnemyField `StepMimics` 길로(C-02b 남은 것). 혈월이면 `EmpowerDamage`·`EmpowerSpeed` 를 모든 적에. 분신 피해 배율은 backlog C-14(사람 결정 대기) — 결정 전엔 데이터 값 그대로.
  - 쓸 것: Core `BossPatterns`·`Mimicry`, bosspatterns.json `hyeokryeon_hwi`, enemies.json blood_shadow_clone
  - 메뉴: 땅의 주인 부르기/hyeokryeon_hwi
  - 끝: 네 번 순간이동 내려찍기, 분신이 내 무공 모두를 절반 피해로 씀
  - 찍기: `docs/unity_shots/R4-02U/` 의 `hyeokryeon_hwi.png`

- [ ] R4-03U 종장 러너 · 먼저: R4-02Uc
  - 할 것: 차례·기혈·단계 시간표는 Core `Combat/FinalFight`(`Patterns` 가 단계마다 시간표)를 PatternRunner 로. 혈의 맥 짚는 손 `InClosingRing(p, 판정 뒤 초, dx, dy)` 가 참이면 0.5초마다 피해(경공 무적이면 통과), 역병 구름 `CloudOffsets`·`CloudDrift(p, 구름→플레이어, dt, moveBase)`·`CloudAlive`, 혈해 피바다 섬 `IslandRadius`·`OnIsland`(섬 자리는 backlog R4-07 이 정해질 때까지 싸움터에 고르게 아무렇게나), 혈영 솟음 `FinalFight.RisingShadow(n)`·`RisingShadowHealth`, 진혈마 혈우 `RainDropOffsets`·`RainWaveSeconds`(파마다 플레이어 자리로 다시), 진엔딩 컷 `CheckTrueEnding`(조건은 `soul.TrueEndingMetDuringFinal(목 노인 대화, 몸 문파, 이번 생 의서)` — 이번 생이 아직 혼 장부에 없어서). 40분을 넘으면 단계 하나씩 끊어 (진행:) 표시.
  - 쓸 것: Core `Combat/FinalFight.cs`·`BossPatterns`(R4-03 모양 함수), bosspatterns.json `final`
  - 메뉴: `BloodRain/Test/종장 부르기(플레이 중)/<단계>`
  - 끝: 단계마다 모양이 천하 시안과 같고, 마지막 단계를 넘기면 진엔딩 조건 판정
  - 찍기: `docs/unity_shots/R4-03U/` 의 `<단계>.png`

- [ ] R4-04U 엔딩 고르기 화면 · 먼저: R4-03U, LF-03
  - 할 것: 봉인으로 생을 마친 뒤 `soul.EndingPending` 이면 정산 다음에 엔딩 고르기 `Presentation/Meta/EndingScreen.cs`. 버튼은 `soul.EndingsOpen(목 노인 대화 끝)`(1회차는 봉인 하나라 고르기 없이 바로 봉인 엔딩 컷). 고르면 `soul.ChooseEnding(id, …)` 뒤 그 엔딩 컷·곡(기획서 14장 '엔딩 3곡', 곡이 없으면 무음), 봉인이면 백중의 밤으로(회귀), 진엔딩이면 `JourneyOver` 라 침방이 닫힌 객잔으로. 진혈마 10% 컷은 `FinalFight.CheckTrueEnding(비율, soul.TrueEndingMet(…))`. 목 노인 대화 끝은 `soul.MokTalkDone(dialogueProgress.MokManuscriptLinesSeen)`(R4-08, 2026-11-05).
  - 쓸 것: Core `Meta/Soul.cs`(EndingPending·EndingsOpen·ChooseEnding·JourneyOver·TrueEndingMet), `Combat/FinalFight.cs`
  - 메뉴: `BloodRain/Test/엔딩 앞 혼 만들기(1회차·회귀)` 를 새로
  - 끝: 1회차 혼은 바로 봉인 엔딩 컷 → 회귀, 회귀한 혼은 고르기 버튼이 뜸
  - 찍기: `docs/unity_shots/R4-04U/` 의 `ending_choose.png`

- [x] R4-05U 모드 고르기(2026-11-04 클라우드 M4 지역) — 2026-11-05 05:10 PC: `Presentation/Meta/ModePicker.cs` — InnFlow 가 침방 앞에 띄움(열린 모드 둘 이상일 때만, 1~4 키), 고른 id 는 `RebirthHandoff.Mode`, 일일 도전·보스 연전은 열려도 잠긴 버튼(`ModePicker.Playable`). `Loc.Mode`, 정산 화면 붉은 줄에 `summary.NewModes` → '새 모드가 열렸다'(ko.json `ui.settle.new_modes`·`ui.mode.title`·`ui.mode.soon`). 천하·혈월·일일 연 혼으로 띄워 일일은 안 눌리고 혈월을 고르면 Mode=bloodmoon 확인, 콘솔 에러 0. 정산 줄은 눈으로 못 봄. 찍음 `docs/unity_shots/R4-05U/mode_pick.png`. 원래 줄: 객잔에서 생을 시작할 때 `Core.Meta.Modes.OpenModes(soul)` 가 둘 이상이면 모드 고르기(이름은 strings `cheonha.modes.<id>.name`). 정산 화면에 `summary.NewModes` 를 '새 모드가 열렸다'로. 혈월 천하는 `soul.IsBloodMoon` 이 이미 셈하니 고르기만, 일일 도전·보스 연전은 아직 규칙이 없어 버튼만 두고 잠가 둔다

- [ ] R4-13b 세 지역 기믹 중 자리가 필요 없는 셋(낙석·눈보라·붉은 달) · 먼저: LF-01
  - 할 것: 서 있는 지역(`life.RegionId`)이 화산이면 낙석 — `FallingRocksGimmick.FallsBetween` 이 1 이상인 프레임에 `Places(def, 플레이어, rng, 축척)` 세 곳에 그림자, telegraphSeconds 뒤 `Hits` 면 `Damage(def, 종류, 최대 기혈)`(잡몹은 쓰러짐). 곤륜 설원이면 눈보라 — `WeatherGimmick.ActiveAt(def, 한 생 초)` 동안 화면 가장자리 흰 덮개 `ViewScale`, 플레이어·적 걸음 `PlayerMoveSpeed`·`EnemyMoveSpeed`. 십만대산이면 붉은 달 — 같은 `WeatherGimmick` 으로 적 걸음, 하늘을 붉게. 혈제단·매화 더미·모닥불·빙판은 자리가 정해진 뒤(backlog R4-07).
  - 쓸 것: Core `World/Gimmicks2.cs`(FallingRocksGimmick·WeatherGimmick), gimmicks.json `fallingRocks`·`blizzard`·`redMoon`
  - 메뉴: `BloodRain/Bootstrap/Run 씬에 지역 기믹 붙이기`(R-13U 와 같은 메뉴, 없으면 새로)
  - 끝: 화산에서 8초마다 바위 셋, 곤륜에서 60초에 눈보라 20초, 십만대산에서 180초에 붉은 달 30초
  - 찍기: `docs/unity_shots/R4-13U/` 의 `falling_rocks.png`, `blizzard.png`, `red_moon.png`

- [ ] R4-08U 은빛 등롱에서 의서 · 먼저: 없음
  - 할 것: 혈월 천하에서 은빛 등롱을 부수면 `life.BreakSilverLantern(rng)` 를 부르고, 참이면 '단목현의 의서' 낙관 한 번(글은 strings, 새 문구면 ui. 키 네 언어). 생이 끝나면 의서 수는 LifeRecord 로 혼에 들어가고, 객잔 귀환 때 목 노인 '단목현의 의서' 줄(dialogue mok manuscript_1~7)이 InnDialogue 에서 저절로 나온다.
  - 쓸 것: Core `Life.BreakSilverLantern`, cheonha.json `cycle.bloodmoon.manuscriptChance`(0.15), `DialogueProgress.MokManuscriptLinesSeen`
  - 메뉴: 없음
  - 끝: 컴파일·콘솔 에러 0. 디버그로 의서 확률을 1 로 두고 등롱을 부수면 의서 1, 객잔에서 manuscript_1 줄
  - 찍기: 객잔 목 노인 의서 줄

### E. 객잔·대사

- [ ] RM-03b 객잔 본채: 시설 문 · 먼저: LF-03
  - 할 것: Inn 씬 `Presentation/Meta/InnScreen.cs` — 시설 일곱 문(main_hall·training·library·apothecary·smithy·cave·haomun_board)을 `Inn.IsOpen(soul, id)` 로 켜고 끔, 누르면 `Inn.CanEnter` 인 것만 침방·연무장 화면을 연다(없는 시설은 '아직', 열렸는데 CanEnter 가 거짓이면 `ui.inn.under_repair` — M2 의 장경각, 할 일은 A-07U). 약방·대장간은 `Inn.Buildable` 이면 '짓기(혈정 N)' → `Inn.TryBuild` → 저장. 정산 뒤 `LifeSummary.NewFacilities` 가 있으면 한 줄 알림. 마당에 머무는 대표 인물은 `Inn.Residents(soul, 대사 진행)`(합류한 사람, 회귀하면 빔).
  - 쓸 것: Core `Meta/Inn.cs`, cheonha.json `inn.facilities`, LF-03 SoulStore
  - 메뉴: `BloodRain/Bootstrap/Inn 씬 본채 짓기`
  - 끝: 새 혼은 침방·연무장만 열리고, 혈정 있는 혼은 약방 짓기 버튼이 켜져 지으면 열림
  - 찍기: `docs/unity_shots/RM-03b/` 의 `inn.png`

- [ ] DL-04b 침방 대사·카드 글 · 먼저: LF-03
  - 할 것: 침방을 열 때 `var talk = InnDialogue.Bedroom(DataBoot.Data, soul.OpenRebirth(rng), progress, rng)` → `talk.Open()` → `talk.Show()` → 다시 고르기마다 `talk.Reroll()`(draw.Reroll 을 따로 부르지 않음) → 카드 `talk.Pick(i)` → 보낼 때 `talk.Send()`, 다음 판은 `talk.Chosen`. 돌려준 목 노인 줄을 말풍선에, 카드 소개·몸 묘사는 `talk.Cards[i].Intro`/`Body`(키 `IntroKey`/`BodyKey`).
  - 쓸 것: Core `Meta/InnDialogue.cs`, dialogue.json, LF-03 의 dialogue.json 저장
  - 메뉴: 침방 시험 씬 만들기(이미 있음)
  - 끝: 첫 환혼 때 목 노인 첫 줄, 여자 몸 카드에 맞는 묘사, 다시 고르면 카드 글이 바뀜
  - 찍기: `docs/unity_shots/DL-04b/` 의 `first_rebirth.png`, `female_card.png`

- [ ] DL-05b 정산 자막 · 먼저: LF-03
  - 할 것: 정산을 열 때 `Soul.EndLife` 바로 뒤 `InnDialogue.Settlement(data, soul, record, summary, facts, rng)`(facts 는 `ReturnFacts` 에 `FallenToShadow`·`LifeSeconds`). 화면 맨 아래 붓글씨 한 줄. 한국어는 `caption.Text`, 다른 말은 키 `dialogue.settlement.lines.<id>.text`(혈월이면 `.bloodMoon`)에 lord_first 는 `caption.Region`·`Lord`, any_3 은 `caption.Sect`·`Seconds` 를 `Loc.F` 로.
  - 쓸 것: Core `InnDialogue.Settlement`, dialogue.json settlement
  - 메뉴: 정산 시험 씬 만들기(이미 있음)
  - 끝: 쓰러진 생·혈월 생에서 다른 자막
  - 찍기: `docs/unity_shots/DL-05b/` 의 `caption.png`

- [ ] DL-03b1 객잔 귀환·마당 말풍선 · 먼저: RM-03b, DL-05b
  - 할 것: `Soul.EndLife` 뒤 `InnDialogue.OnReturn(data, soul, record, summary, progress, facts, rng)`(`facts.MetHermit` 은 그 생에 기연 은거기인을 마쳤는지) → 목 노인 `Mok`(많아야 둘)·운낭 `Unnang`(하나)·대표 인물 `Leader` 차례로 말풍선, `Opens` 에 haomun_board 가 오면 하오문 게시판 문을 연다. 삼류 첫걸음 때 `InnDialogue.YardLine(data, soul, body, progress, rng)` 를 대표 인물 머리 위 2초. 연무장을 열 때 `InnDialogue.Training(data, progress, "open")`.
  - 쓸 것: Core `InnDialogue.OnReturn`·`YardLine`·`Training`, `ReturnFacts`, dialogue.json `speakers`
  - 메뉴: 없음(RM-03b 화면에)
  - 끝: 첫 귀환에 목 노인 둘·운낭 하나가 차례로, 저장 뒤 다시 들어오면 같은 줄이 되풀이 안 됨
  - 찍기: `docs/unity_shots/DL-03b/` 의 `return.png`

- [ ] DL-03b2 서장 대사 · 먼저: C-10b
  - 할 것: Core `Run/PrologueCues` — 판 시작 `Start()`, 매 프레임 `Watch(life)`, `Life.Tick` 결과를 `OnSecond(second)`, 정예 처치 `OnEliteKilled(id)`. 돌려준 `PrologueCueItem` 을 `Delay` 초 뒤 띄움(offscreen 이면 화면 아래 가운데, `IsHint` 면 `prologue.hints` 를 가운데 아래 크게). 장삼 처치 뒤 컷 두 장 자막.
  - 쓸 것: Core `Run/PrologueCues.cs`, dialogue.json prologue, `docs/story/prologue.md`
  - 메뉴: 없음
  - 끝: 서장 10분 동안 start·first_levelup·boss_warning·awaken 줄이 한 번씩
  - 찍기: `docs/unity_shots/DL-03b/` 의 `prologue_cue.png`

- [ ] DL-06b 땅의 주인 컷·한 줄 · 먼저: BR-02
  - 할 것: 보스전 경고 직후 `InnDialogue.LordMeet(data, soul, lifeLog, region, progress)` → `Cuts`(만남 2컷, 비면 건너뜀, 건너뛰기 가능) 다음 `BloodMoon` 줄, 또는 이름 붓글씨 아래 `Echo` 한 줄. 기혈 30% 2막 예고 때 머리 위 `Phase2` 2초(시간 안 멈춤). 처치 뒤 `lifeLog.KillBoss(region)` → `InnDialogue.LordKill(data, lifeLog, kill, progress)` → 처치 4컷(`cut.Art`, `cut.Lines`, `cut.Alt` 면 `….alt.lines.<n>.text`).
  - 쓸 것: Core `InnDialogue.LordMeet`·`LordKill`, dialogue.json lords
  - 메뉴: BR-01 의 땅의 주인 부르기
  - 끝: 원각 첫 만남 2컷 → 싸움 → 2막 한 줄 → 처치 4컷. 두 번째 만남은 Echo 한 줄
  - 찍기: `docs/unity_shots/DL-06b/` 의 `wongak_meet.png`

- [ ] DL-07b 인트로 컷신 「혈우」 · 먼저: 없음
  - 할 것: 처음 실행(혼 저장이 없을 때) Boot 씬 뒤 `DataBoot.Data.Dialogue.Intro.Cuts` 를 차례로 — 그림 `Assets/Art/Cutscenes/<cut.Art>.jpg`, `cut.Seconds` 뒤 다음 컷(누르면 바로), 줄은 화면 아래 가운데(`Whisper` 면 옅게, 말하는 사람 이름은 달지 않음). 다른 말은 키 `dialogue.intro.cuts.<id>.lines.<n>.text`. 컷 12에 게임 제목 붓글씨를 Unity 글자로. 건너뛰기 단추, 제목 화면에 '인트로 다시 보기'.
  - 쓸 것: dialogue.json `intro`, `docs/story/intro.md`(소리 칸)
  - 메뉴: 없음
  - 끝: 12컷이 약 71초에 끝나고 서장으로, 건너뛰면 바로 서장
  - 찍기: `docs/unity_shots/DL-07b/` 의 `intro_06.png`, `intro_12.png`

- [ ] DL-10b 종장 입장 컷·상대 한 줄·엔딩 고르기 · 먼저: DL-06b
  - 할 것: 봉마곡에 들어설 때 `FinalDialogue.FinalEnter(data, soul, progress)` 컷(비면 건너뜀). `FinalFight` 를 시작할 때와 `Defeat()` 뒤마다 `FinalDialogue.FinalOpponent(data, soul, fight)` — `Shadow` 면 이름 붓글씨 아래, 아니면 보스 머리 위 2초(시간 안 멈춤), `BloodMoon` 이 있으면 이어서 한 줄. 종장을 깨 `soul.EndingPending` 이면 `FinalDialogue.EndingChoices(data, soul, mokTalkDone)` 로 고르는 화면(1회차는 봉인 하나라 바로), 고르면 `EndingCuts(data, id)` 컷 → `soul.ChooseEnding(id, mokTalkDone)` → 저장. 계승 속삭임 줄은 `Whisper` 라 옅게. 진혈마 10%(DL-11, 2026-11-05): `fight.CheckTrueEnding(기혈 비율, soul.TrueEndingMetDuringFinal(…))` 가 true 면 싸움을 멈추고 `FinalDialogue.TrueEndingInterrupt(data)` 컷 1 → 고르는 화면 → `fight.ChooseAtTrueCut(id)`. false(의심)면 `EndingCutsAfterTrueCut(data)`, true(봉인·계승)면 싸움으로 돌아가 마저 쓰러뜨린 뒤 `EndingCuts(data, id)`.
  - 쓸 것: Core `Meta/FinalDialogue.cs`, `Combat/FinalFight.cs`, dialogue.json `final`·`endings`, `docs/story/final.md`·`endings.md`
  - 메뉴: BR-01 의 땅의 주인 부르기(혈마 단목현)
  - 끝: 입장 컷 둘 → 혈영 일곱마다 혈영 줄 → 혈의·혈해·진혈마 단계 줄 → 봉인 4컷 → 2회차 객잔
  - 찍기: `docs/unity_shots/DL-10b/` 의 `enter.png`, `ending_choice.png`

- [ ] A-07U 객잔의 장경각 문(수리 중) · 먼저: RM-03b
  - 할 것: Inn 씬 객잔 마당에 장경각 문 자리를 하나 둔다(문 그림은 아직 없으니 침방 바탕 위 글자 띠로). `Inn.IsOpen` 이 거짓이면 문을 안 보이고, 참인데 `Inn.CanEnter` 가 거짓이면 문 위에 `ui.inn.under_repair`(수리 중)를 띄우고 눌러도 안 열린다. LibraryScreen(시험 씬 LibraryTest)은 M3 까지 객잔에서 부르지 않는다.
  - 쓸 것: Core `Meta/Inn.cs`(IsOpen·IsUnderRepair·CanEnter), cheonha.json `inn.facilities` 의 `underRepair`, 문자열 `ui.inn.under_repair`
  - 메뉴: 시험 메뉴 `서장 마친 혼으로 저장` 뒤 원각을 쓰러뜨린 혼을 만드는 메뉴가 없으면 하나 더(`원각 잡은 혼으로 저장`)
  - 끝: 서장만 마친 혼은 문이 안 보이고, 원각 잡은 혼은 '수리 중' 문이 보이며 눌러도 화면이 안 바뀜, 콘솔 에러 0
  - 찍기: `docs/unity_shots/A-07U/` 의 `library_door.png`

### F. 뒤로 미룬 것(조건이 풀리면)

- [ ] R-13U 세 지역 기믹(독안개·물·태극 바닥)과 수귀 · 먼저: backlog R-10(사람 결정 대기)
  - 할 것: gimmicks.json 그대로 Core `Gimmicks` 판정을 지역에 놓는다. 물 지형 걸음에 `WaterGimmick.MoveSpeedMultiplier(…, ignoresWater: SectTrait.IgnoresWater)`(R-04U (4)), 수귀(`OnlyInWater`)는 물에서만 솟음(C-02b 남은 것). 놓는 자리·수는 R-10 이 정해진 뒤.
  - 쓸 것: Core `World/Gimmicks.cs`(PoisonFogGimmick·WaterGimmick …), gimmicks.json, cheonha.json regions
  - 메뉴: `BloodRain/Bootstrap/Run 씬에 지역 기믹 붙이기`
  - 끝: 당가타 독안개·장강 물·무당산 태극 바닥이 그 지역에서만, 수귀가 물에서 솟음
  - 찍기: `docs/unity_shots/R-13U/` 의 `<기믹>.png`

- [ ] S-04b 발견 기록 알리기·기연 배치 · 먼저: C-08b, C-09b, C-09d
  - 할 것: SecretWatch 의 RunRecord 에 정신부·음식·보갑·의서·깬 장을 알린다(그 기능이 붙은 자리에서 한 줄씩). 기연 사건 맵 배치는 기획서 20장에 놓는 자리가 있으면 그대로, 없으면 `막힘:` 한 줄.
  - 쓸 것: Core `Build/Secrets.cs`(RunRecord·SecretChecker), secrets(기획서 20장)
  - 메뉴: 없음
  - 끝: 비전 하나를 실제 플레이로 찾아 발견 연출이 뜸
  - 찍기: `docs/unity_shots/S-04/` 의 `found_real.png`

- [ ] ST-03Ua 스토어 스크린샷 틀과 되는 장면 둘 · 먼저: LF-02
  - 할 것: Editor 메뉴 `BloodRain/Store/스크린샷 n번 세우기`(1~10) — Run 씬 플레이 중 `StoreShotStage.Build(DataBoot.Data, shot, defs.Seed)` 의 `Life` 로 바꿔 끼우고 `Enemies` 수만큼 화면 둘레에 채움. 먼저 되는 9번(침방 `Candidates` 석 장)·2번(강시 떼)만.
  - 쓸 것: `Assets/Editor/StoreShots/store_shots.json`, Core `Run/StoreShotStage.cs`·`StoreShotDefs.FromJson`
  - 메뉴: 위 메뉴 열 개(`Assets/Editor/StoreShots` 에 새 파일 `StoreShotMenu.cs`)
  - 끝: 1920×1080 으로 장면마다 10장
  - 찍기: `docs/unity_shots/ST-03/<no>_<id>/` 의 `01.png` ~ `10.png`

- [ ] ST-03Ub 나머지 스토어 장면 · 먼저: ST-03Ua, BR-02, C-07b, C-09e
  - 할 것: 보스 장면은 `Patterns`·`BossHealth` 를 이어받아 원각, 레벨업 장면은 `LevelUpOffers` 석 장으로 레벨업 화면. 피해 숫자가 너무 덮으면 그 장면만 줄임.
  - 쓸 것: ST-03Ua 와 같음
  - 메뉴: ST-03Ua 의 메뉴
  - 끝: 열 장면 모두 10장씩(사람이 고름)
  - 찍기: `docs/unity_shots/ST-03/<no>_<id>/`

- [ ] B-12U 원화 Sprite 다시 들여오기 확인(2026-11-05 클라우드 B-12) (진행: 2026-11-05 00:55 PC — 7장 모두 Sprite/Single/PPU 100, 밉맵·Repeat 규칙대로 들어옴, Unity 가 채운 spriteID 커밋. 남은 것: Run 씬에서 시강·혈강시 크기·마당 타일 깔림 보기. 다만 지금 적은 ClusterRenderer 덩이로 그려 원화 Sprite 를 쓰는 코드가 아직 없다 — 붙이는 일이 따로 필요한지 클라우드가 정할 것)
  - 할 것: 클라우드가 meta 값만 바꾼 원화 7장(Enemies 둘, Props, Tiles, Map, Backgrounds, Cards)을 Unity 가 Sprite 로 다시 들여오는지 본다. spriteID 가 빈 칸이라 Unity 가 채워 meta 를 다시 쓴다(그 변경은 커밋).
  - 쓸 것: `tools/fix_art_meta.py`(규칙은 머리글), 테스트 `ArtMetaTests`
  - 끝: 시강·혈강시가 Run 씬에서 SpriteRenderer 로 1.28m(PPU 100) 크기, 마당 타일이 이어 깔림. 크기가 몸(1m 안팎)과 안 맞으면 PPU 를 바꾸지 말고 daily 에 적는다(기획서 13장 숫자라 사람 몫)
  - 찍기: `docs/unity_shots/B-12/`

### 옮겨서 닫은 것(2026-11-04 UT-01)
- [x] S-04 2026-11-04 — 레벨업 띠·인장·HUD 인장·발견 연출·장경각·정산 찢긴 비급은 이미 끝(옛 줄 원문). 남은 것을 S-04a(NoteSynergy)·S-04b(기록 알리기·기연 배치)로 옮김. 장경각을 객잔에 붙이는 것은 RM-03b·A-07
- [x] C-06 2026-11-04 — 레벨업 화면·다시 고르기·파기·정진·행낭 카드는 끝(옛 줄 원문). 남은 '몸에 능력치 얹기'를 C-06a·C-06b·C-06c 로 쪼갬
- [x] C-07·C-08·C-09·C-10 2026-11-04 — 각각 C-07a·b, C-08a·b, C-09a·b·d·e(+C-09c), BR-01·C-10a·C-10b 로 쪼갬. 내용은 옛 줄 원문
- [x] C-02b 2026-11-04 — 행동 갈래는 수귀·혈영회주 분신 빼고 끝(옛 줄 원문). 수귀는 R-13U, 혈영회주는 R4-02Uc 로
- [x] V-04 2026-11-04 — 셰이더·AI 재료·먹 자국·삼재 2·3단·간소화는 끝(옛 줄 원문). 남은 웹 빌드 화면은 V-04d
- [x] W-01 2026-11-04 — 뇌부·감전·낙뢰는 끝(옛 줄 원문). 오뢰정법은 C-09e, 연무장 개수·범위는 C-06a, 레벨업 풀 확인은 R-08U(해금), 보스 감전 거르기는 BR-01
- [x] L-01b·L-08b(꼴만 바꿈)·R-01U·R-04U·R-12U·R-14U·R4-02U·R4-03U·R4-04U·DL-03b·ST-03U 2026-11-04 — 위 '할 차례'에서 a·b·c 로 쪼갬

## 끝난 것(UT-01 전 순서 그대로)

- [x] B-00 기본 템플릿이 넣은 안 쓰는 패키지 정리: com.unity.ads, com.unity.analytics, com.unity.purchasing, com.unity.xr.legacyinputhelpers, com.unity.multiplayer.center, com.unity.collab-proxy 제거 후 콘솔 오류 0 확인 (WebGL 용량 때문) → 2026-10-30 여섯 개 지움. 패키지 46개, 콘솔 오류 0. ads가 띄우던 Mobile Dependency Resolver 창도 이제 안 뜬다
- [x] B-10 Unity 프로젝트 기본 설정: Packages에 2D URP·Input System·com.unity.nuget.newtonsoft-json·Test Framework 확인, Assets/Scenes에 Boot·Run·Inn 빈 씬, Assets/Scripts/Presentation·Sim·Platform 폴더와 asmdef(BloodRain.Core 참조) → 2026-10-30 패키지 넷 확인. `BloodRain/Bootstrap/기본 씬 만들기` 메뉴(Assets/Editor/SceneBootstrap.cs)로 Boot·Run·Inn 씬과 빌드 목록(Boot 0번). Sim·Platform·Presentation·Editor asmdef. 입력 처리는 Both(Input System 백엔드 켬)
- [x] B-11 데이터 로더: Resources/Data의 JSON(`GameData.FileNames` 에 적힌 것 전부, 지금 12개)을 TextAsset으로 읽어 GameData.FromJson에 넘기는 `Presentation/DataBoot.cs`. Boot 씬 플레이 시 콘솔에 무공 16종 이름이 찍히는지 확인 → 2026-10-30 `DataBoot.Data` 로 한 번 읽어 둔다. Boot 씬 플레이 때 "파일 12개, 무공 16종" 확인, 오류 0. Boot 씬 배치는 `BloodRain/Bootstrap/Boot 씬에 DataBoot 붙이기` 메뉴
- [x] B-13 `Assets/Scripts/Core/Build/` 의 .cs 다섯 개(Insights·Loadout·Secrets·Stats·Synergy)에 .meta 가 저장소에 없다. 루트 .gitignore 의 `build/` 가 Windows(대소문자 무시)에서 `Build/` 폴더를 막고 있었다(2026-10-30 클라우드 세션이 `/build/` 로 고침). Unity 를 열어 생긴 .meta 다섯 개를 `git status` 로 확인하고 커밋만 하면 된다 → 2026-10-30 Unity 를 여니 .meta 다섯 개가 생겼고 그대로 커밋. 콘솔 오류 0
- [x] J-01U 절기 각성 연출: `docs/design/vfx.md` '절기 각성 연출' 표대로 Fullscreen Pass(흑백 구멍), 카메라 확대·흔들림(언스케일), 이름 붓질(TextMeshPro + Yuji Boku SDF), 낙관, 파동 링. 웹 프로토타입 `#awaken` 과 나란히 놓고 비교. 판정은 Core J-01이 끝난 뒤 연결 → 2026-10-30 `Presentation/Fx/AwakeningFx.cs` 가 Core `Awakening` 의 장면 신호대로 그린다: 흑백 구멍(Full Screen Pass `BloodRain/AwakenHole`, 탁본 뒤집기 포함)·0.86배 확대·트라우마² 흔들림(설정 배율·움직임 줄이기)·먹 알갱이 70개·무공 칸 글자 포물선·금빛 균열 3겹·흰 물감 띠 7획 위 한자 붓질(글자마다 0.11초)·絶 낙관 1.8→1배·먹 고리. 시험 씬은 `BloodRain/Test/각성 연출 시험 씬 만들기`(Space 긴, S 짧은, N 다음 절기). 이 메뉴가 URP 2D 파이프라인(`Assets/Settings`, 그동안 비어 있었다)과 흑백 효과를 안 받는 겹 카메라(렌더러 1번, 레이어 AwakenOverlay), Yuji Boku SDF(절기 한자+絶秘傳 56자 고정 아틀라스)도 만든다. 남은 것: 한글 이름·재료 줄(한글 글꼴 미정), 소리 4겹, 파동 고리는 스크린샷으로 못 잡아 눈 확인 필요, 웹 `#awaken` 과 나란히 비교는 사람 눈으로
- [x] M-03 강호 지도 화면: 혈운 셰이더·먹 길·깃발(기획서 21장). 배경 이미지(art_queue `map_central_plains`)가 들어온 뒤에 시작 — 천하 구조로 바뀌어 안 함(2026-10-29). 지도는 침방 벽 지도(TU-12)로 옮김

### 천하 (2026-10-29 결정. 기획서 7·12·15·21장, `cheonha.json`)

줄마다 적은 Core 입구 파일은 클라우드 세션이 만드는 중이다. 그 파일이 아직 저장소에 없으면 그 줄은 건너뛴다(막힘 아님). 숫자는 직접 적지 말고 `cheonha.json` 과 Core 에서 읽는다. 움직임은 웹 시안 `web/lookdev/cheonha.html` 과 나란히 놓고 비교한다.

- [x] TU-01 축척 카메라: 카메라 직교 크기를 7장 화면 대각선 식(36m × 2.25^(경지 순번 + 0.35 × 경지 안 진행))에 맞춘다. 직교 크기 = 대각선 × 세로 ÷ √(가로² + 세로²) ÷ 2, 16:9면 대각선 × 0.245. 레벨이 오를 때 튀지 않게 보간. 플레이어·지금 경지 적·무공 이펙트의 크기·속도·사거리에 배율을 곱하는 곳을 Sim 한 군데로 모은다(15장 '배율 한 곳에서'). 시험 씬에서 키 하나로 경지를 올려 삼류~현경 일곱 화면을 스크린샷으로 남긴다. Core 입구: `Core/World/Scale.cs`. 먼저: B-10, B-11 → 2026-10-30 배율은 `Sim/WorldScale.cs` 한 곳(Unity 1 = 세계 m, `Length`·`Speed` 로 경지 m → 세계 m, 생긴 경지를 넘기면 그 경지 배율). 화면 대각선은 축척 지수를 언스케일 0.35초 시간 상수로 따라가 튀지 않고, 돌파 연출용 `DiagonalOverride` 자리를 둠. `Presentation/ScaleCamera.cs` 가 직교 크기 = 대각선 ÷ √(1+aspect²) ÷ 2(16:9 에서 36m → 8.82). 시험 씬은 `BloodRain/Test/축척 시험 씬 만들기`(R/F 경지, L/K 레벨, 1~7, WASD 이동은 장막 안으로 되돌림). 일곱 화면 스크린샷 `docs/unity_shots/TU-01/`(현경 대각선 4670.9m). 콘솔 오류 0
- [x] TU-02 돌파 줌: 7장 '돌파 연출' 표대로 1.4초(연출 시계는 언스케일). 0~0.2초 세상 시간 0.05배와 흑백 심상(J-01U의 Fullscreen Pass 재사용), 0.2~1.4초 카메라 직교 크기와 세계 배율을 천천히 출발해 천천히 멈추게 함께 보간, 0.8초에 쓸림 신호, 1.4초 동안 적 생성 멈춤, 끝나면 깨달음 3택1 호출 자리. 돌파 순간 공간 해시 격자를 2.25배로 다시 짓는 호출 자리도 둔다. 값은 `cheonha.json` `breakthrough`·`crush`. Core 입구: `Core/World/Scale.cs`. 먼저: TU-01 → 2026-10-30 `Presentation/Fx/BreakthroughFx.cs` 가 Core `Breakthrough` 시계(언스케일)를 돌린다. 세상 시간 0.05배는 Core 주석대로 1.4초 내내. 흑백 심상은 AwakenHole 전역값으로 0~0.2초 다 켜고 0.3초에 걷힘(플레이어 둘레만 색). 카메라 대각선(`WorldScale.DiagonalOverride`)과 세계 배율(`WorldScale.RealmFactorOverride`)을 같은 진행값으로 함께 넘김(0.8초에 일류→절정 대각선 273.4m, 배율 ×7.59 확인). 신호 자리: `RealmFactorChanged`(공간 해시 다시 짓기), `Swept`(0.8초, 반경 164m), `Ended`(깨달음 3택1), `SpawnHeld`. 시험 씬 B 키. 스크린샷 `docs/unity_shots/TU-02/`. 다시 오른 경지 0.7초 줄임은 Play(revisit) 로 넘기지만 눈 확인은 아직
- [x] TU-03 밟힘과 쓸림: 아래 경지에서 생긴 적은 이동을 멈춘 가벼운 목록으로 옮겨 작게 그리고, 닿으면 먹물로 터지며 쓰러진다(피해 없음, 진기 0.3). 아래 경지의 진기 구슬·음식·은자는 지나가면 주워지고 작은 등롱은 밟으면 깨진다. 돌파 0.8초에 화면 대각선 0.6배 안의 아래 경지 적이 한꺼번에 쓸려 나가는 연출. 적 시뮬레이션(backlog C-02)이 아직이면 시험 씬에 적을 직접 놓고 확인. Core 입구: `Core/World/Scale.cs`(밟힘·쓸림 대상). 먼저: TU-02 → 2026-10-31 `Sim/CrushField.cs`(밟힘 목록·닿으면 쓰러짐·돌파 쓸림), 시험 씬 ScaleTest 에서 E(적 무리)→B(돌파)로 확인: 삼류→이류 쓸림 23명·닿아서 1명·진기 +7.2, 구슬·등롱은 밟아야 주워짐. 먹물 번짐 `Fx/InkBurst.cs`. 스크린샷 `docs/unity_shots/TU-03/`(02:37 세션이 하고 이 줄만 못 고쳐 04:37 세션이 적음)
- [x] TU-04 덩이 렌더러: 적 그림 한 장을 덩이당 최대 64번 겹쳐 그리는 인스턴싱(Graphics.RenderMeshInstanced, 적 아틀라스 하나). 덩이 크기는 경지별 1·4·9·16·28·44·64, 그리는 사람 수 = ceil(덩이 크기 × 남은 기혈 비율). 덩이 안 자리는 시드로 고정해 사람 수가 줄어도 남은 사람이 튀지 않게 하고, 빠지는 사람은 먹물 디졸브. 강시 콩콩 트윈은 사람마다 박자를 조금씩 어긋나게. 현경 덩이 600개(64명씩)에서 PC 60fps 확인. Core 입구: `Core/World/Scale.cs`(덩이 그릴 사람 수). 먼저: TU-01 → 2026-10-31 `Fx/ClusterRenderer.cs` + `ClusterInk.shader`(시드 고정 자리, 먹물 디졸브, 콩콩 박자 어긋남). `BloodRain/Test/덩이 시험 씬 만들기`, 현경 600덩이×64명 에디터 평균 65fps. 스크린샷 `docs/unity_shots/TU-04/`. 겹친 사람 앞뒤 정렬과 크기·간격은 적 데이터·아틀라스가 오면 다시
- [x] TU-05 혈우 장막 셰이더: 태어난 자리를 가운데 두고 반경(그 경지 첫 화면 대각선 × 2.4)에 붉은 빗줄기 벽을 그리는 셰이더(흐르는 빗줄기 노이즈, 안쪽으로 갈수록 옅게 번짐, 주홍 #B3261E 하나). 돌파 때 반경이 새 값으로 밀려나며 빗줄기가 갈라지는 연출을 TU-02 시계에 맞춘다. 현경에서 걷힌다. Core 입구: `Core/World/Scale.cs`(장막 반경). 먼저: TU-02 → 2026-10-31 셰이더 `BloodRain/BloodCurtain` + `Fx/BloodCurtain.cs`. 반경은 Core `BoundaryRadius` 그대로, 빗줄기는 반경/100 칸 두 겹(언스케일 시계라 돌파 중에도 흐름). 돌파 때 반경을 `ZoomProgress` 로 밀고 sin(π×진행값)으로 부채꼴 11갈래 갈라짐, 현경 돌파는 1.6배로 밀려나며 옅어져 걷힘(끝나면 꺼짐 확인). 시험 씬 ScaleTest 에 붙임(메뉴로 다시 지음, 붉은 원 대신). 스크린샷 `docs/unity_shots/TU-05/`. 빗줄기 굵기·속도는 사람 눈 확인 필요
- [x] TU-06 장막 밀어내기와 너머 그림자: 장막에 다가가면 플레이어를 안쪽으로 밀어낸다(세기는 Core 값). 장막 너머로 윗 경지의 거대한 혈운 군세가 검은 실루엣으로만 어른거리게(덩이 렌더러를 한 단계 큰 배율로 재사용). 플레이어 이동(backlog C-01)이 아직이면 시험용 이동 스크립트로. Core 입구: `Core/World/Scale.cs`(밀어내기). 먼저: TU-04, TU-05 → 2026-10-31 시험 씬 이동(ScaleTestDriver)이 걸음 속도에 `PushVelocity` 를 더한 뒤 `KeepInside`. 서 있어도 띠 안이면 밀려 들어가고(86m에 세우면 3초 뒤 77.9m), 밖으로 곧장 걸으면 삼류 84.6m(= `StallDistance`)에서 멈춤. `PushStrength` 는 드라이버 속성으로 열어 둠(TU-10 붉은 번짐용). 그림자는 `Fx/CurtainShadows.cs` 가 덩이 렌더러를 '지금 경지 + 1' 로 160덩이 장막 둘레에 띄우고, 기혈 비율을 7초 주기로 오르내려 먹물 디졸브로 사라졌다 나타남. 윗 경지 덩이가 화면에 비해 커지는 만큼(초절정 37%) 밖으로 물려 장막 안으로 안 넘어옴. 돌파 중엔 BreakthroughFx 장막 반경을 따르고 현경에선 꺼짐 확인. 메뉴로 시험 씬 다시 지음. 스크린샷 `docs/unity_shots/TU-06/`(삼류·초절정 장막 앞). 실루엣 진하기·떠도는 빠르기는 사람 눈 확인 필요
- [x] TU-07 지형 단계 1, 소품 층: 경지마다 소품 층(나무·집·탑·산)을 두고 시드로 지역 둘레에 뿌려 격자로 찾는다. 화면에서 2픽셀보다 작아진 소품은 그리지 않는다(웹 시안과 같게). 지역 위치는 `regions[].pos` × 세계 크기(4,000 x 2,600m). 소품 그림이 아직 없으니 웹 시안의 도형 소품을 옮겨 임시로. Core 입구: `Core/World/Regions.cs`(지역 위치·둘레), `Scale.cs`(화면 대각선). 먼저: TU-01 → 2026-10-31 자리는 `Sim/PropField.cs`(웹 buildProps 를 옮김: 지역마다 전각 1·집 14·나무 160, 마을 70곳, 나무 6000, 산줄기 다섯, 땅 다각형 밖은 뺌, 200m 격자, 칸이 900개를 넘으면 산·전각만). 시드 7 에서 5,716개. 그리기는 `Fx/PropLayer.cs` 가 종류·벌마다 재질 하나로 인스턴싱, 규칙은 웹과 같게(2px 미만 안 그림, 2~6px 옅게, 화면 짧은 변 0.4배 넘으면 옅게, 산은 대각선 140→700m 에서 떠오름). 그림은 웹 도형을 코드로 그린 임시 텍스처. Core 좌표(아래로 y)↔Unity(위로 y)는 `Sim/WorldCoords`(Origin 자리가 Unity 0,0) 한 곳에서 바꾼다. 시험 씬은 청운진 한가운데가 출생지. 스크린샷 `docs/unity_shots/TU-07/`(삼류 나무, 일류 집·전각, 초절정 점처럼, 현경 산). 땅 다각형은 웹 임시값이라 지도 그림이 오면 다시
- [x] TU-08 지형 단계 2, 수묵 지도 섞기: 화면 대각선에 비해 너무 작아진 소품 층은 수묵 지도 텍스처(art_queue `map_central_plains`)로 크로스페이드해 넘기고, 바닥 타일도 경지가 오를수록 지도 쪽으로 섞는다. 지도 텍스처는 세계 좌표(삼류 m)에 맞춰 깐다. 지도 그림이 들어오기 전엔 임시 텍스처로. Core 입구: `Core/World/Scale.cs`, `Regions.cs`. 먼저: TU-07 → 2026-10-31 `Fx/MapLayer.cs`. 들어와 있던 `Assets/Art/Map/map_central_plains.jpg`(1536×1024)를 세계 4,000×2,600m 판에 그대로 늘려 깔고(위끝이 세계 y 0), 불투명도 0.35+0.65×먼 값(대각선 140→700m). 마당 바닥은 웹 groundTile 을 옮긴 8m 칸 타일을 세계 좌표에 붙여 깔고 불투명도 = 가까운 값(대각선 80m 에서 1, 260m 에서 0). 소품은 크기가 대각선의 1/150→1/300 사이에서 지도로 넘어감(나무 초절정, 집 화경, 전각 현경). 지도 그림에 산이 이미 있어 그림이 있으면 도형 산은 안 그림. 그림이 없으면 땅 다각형만 그린 임시 텍스처. 스크린샷 `docs/unity_shots/TU-08/`(삼류 바닥, 일류 바닥이 걷히며 지도, 초절정 지도 위 집, 현경 지도). 지도 해상도가 4,000m 에 1,536px 라 초절정부터 조금 뭉개짐. 소품 땅 다각형(PropField)은 아직 웹 값이라 지도 그림의 바다·강과 안 맞을 수 있음 — 사람 눈 확인
- [x] TU-09 지역 HUD: 위 가운데 경과 시간 아래에 지금 지역 이름(한글과 한자)과 혈운 농도(12장 '전투 HUD 배치'). 화면 빗줄기 굵기도 혈운 농도를 따른다(지역 사이에선 섞인 값). Core 입구: `Core/World/Regions.cs`(서 있는 지역, 혈운 농도). 먼저: B-11 → 2026-10-31 `Hud/RegionHud.cs` 가 플레이어 자리를 `WorldCoords.FromUnity` 로 바꿔 Core `Nearest`·`Density`(봉인석 수 넘김, 봉마곡 숨김)를 읽고, 위 가운데에 경과 시간(m:ss)과 그 아래 "청운진 靑雲鎭 · 혈운 0.8" 을 쓴다. 농도 2 이상은 웹처럼 주홍 굵게. 화면 혈우 `Hud/ScreenRain.cs`(UI 그래픽)는 웹 식 그대로(수 30+26D, 길이 12+3D, 굵기 0.6+0.55D, 불투명도 min(0.42, 0.13+0.05D), 언스케일 시계), 농도는 섞인 값을 매 프레임 받음. 글꼴은 `BloodRain/Bootstrap/본문 글꼴(Noto Serif KR SDF)` 메뉴가 동적 SDF 둘(Noto Serif KR, 대체 Yuji Boku Dynamic)을 만든다. HUD 캔버스는 `HudBootstrap.CreateHud()` 로 축척 시험 씬에 붙음(메뉴로 다시 지음). 스크린샷 `docs/unity_shots/TU-09/`(청운진 0.8, 장강 수로 1.4, 십만대산 4.3). 콘솔 오류 0. 시험 씬 경과 시간은 씬 연 뒤 시간(생 시계가 오면 바꿀 것), 빗줄기 굵기·진하기는 사람 눈 확인 필요
  막힘: 2026-10-31 20:38 PC 세션 — 한글 글꼴이 프로젝트에 없다(Yuji Boku SDF 는 한자 56자 고정 아틀라스). 지역 이름(한글) TMP 글꼴을 정해 Assets 에 넣어 주면 한다
  풀림: 2026-10-31 21:14 예약 세션 — 기획서 12장 본문 글꼴 Noto Serif KR(OFL)을 `Assets/Art/Fonts/NotoSerifKR-Regular.otf`(+`NotoSerifKR-OFL.txt`, .meta)로 넣음. 한글 전부와 한자 대부분이 있다. TMP 는 동적(Dynamic) SDF 로 만들고 대체 글꼴에 Yuji Boku(동적, ttf 에서 새로)를 걸면 데이터의 글자가 다 찍힌다(빠진 건 丐·罩 둘뿐, `FontCoverageTests`). 이번에 만든 SDF 는 J-01U 한글 이름·재료 줄에도 쓴다
- [x] TU-10 화면 가장자리 표시: 화면 밖 땅의 주인·정예·금두꺼비 방향 화살표, 땅의 주인이 깨어날 때 그 지역 위로 솟는 혈운 기둥, 혈우 장막이 가까우면 그쪽 가장자리가 붉게 번지는 화면 효과(가까울수록 진하게). Core 입구: `Core/World/BossAwakening.cs`(깨어난 보스 지역), `Scale.cs`(장막까지 거리). 먼저: TU-05 → 2026-10-31 `Fx/EdgeIndicators.cs`. 장막 번짐은 Core `PushStrength` × 0.55 주홍을 출생지 반대쪽 화면 끝 바깥 35%에 그라디언트로(일류 장막 앞 0.80 확인). 경지가 바뀔 때마다 `BossAwakening.UpdateRealm` 을 불러 새로 깨어난 지역에 혈운 기둥(화면 대각선 0.35배 높이, 1.2초에 솟고 옅게 남음)을 세우고, 깨어난 땅의 주인이 화면 밖이면 그쪽 끝에 맥박 치는 붉은 화살표(청운진 출생 초절정: 7곳 깨어남, 화면 밖 6개). 정예·금두꺼비는 아직 위치를 줄 Core 가 없어 `AddMarker(세계 위치, 색)` 자리만 둠. 스크린샷 `docs/unity_shots/TU-10/`. 기둥이 가늘고 옅어 사람 눈 확인 필요
- [x] TU-11 침방 화면 1, 후보 카드: 배경 `bg_chimbang` 위 목 노인 앞에 환생 후보 카드 셋(넷째 몸을 샀으면 넷). 카드마다 문파 산문 그림(art_queue `card_sect_*`), 문파 이름, 대표 인물, 타고난 성질, 태어나는 땅의 혈운 농도. 다시 고르기 버튼(남은 횟수). 고르면 그 몸으로 한 생 시작. 그림이 없으면 단색 카드로. 웹 시안 환생 화면과 비교. Core 입구: `Core/Meta/Rebirth.cs`(후보 추첨·몸 만들기), `Core/Meta/Soul.cs`(해금 문파·환혼 강화 레벨). 먼저: B-11 → 2026-10-31 `Presentation/Meta/ChimbangScreen.cs`. 후보 추첨·다시 고르기·몸 만들기는 Core `RebirthDraw` 그대로(후보 수 = `Soul.CandidateCount`, 다시 고르기 = `RerollsPerRebirth`, 첫 환혼이면 세 문파 고정에 버튼 숨김). 카드는 웹 openRebirth 와 같은 줄: "OO 제자"(여자 몸이면 (여)), 태어나는 땅·혈운(2 이상 주홍 굵게), 시작 무공·기혈·걸음, 대표 인물(없으면 흐리게), 성질(이름·한자·능력치, 웹 STAT_KO 표기). 1~4 키·클릭으로 고르고 R 로 다시 고르기. 고르면 몸을 `RebirthHandoff.Body` 에 두고 `Chosen` 을 부름(`loadRunScene` 켜면 Run 씬). 그림은 `Assets/Art/Backgrounds/bg_chimbang`, `Assets/Art/Cards/card_sect_<문파 id>`(png·jpg)가 있으면 시험 씬 메뉴가 찾아 건다. 지금은 없어 문파별 단색 카드. 시험 씬 `BloodRain/Test/침방 시험 씬 만들기`(모든 문파에서 뽑고 다시 고르기 +2). 넷째 몸(후보 4)과 다시 고르기, 곤륜 여자 몸 고르기(기혈 105·음양 -25) 확인. 스크린샷 `docs/unity_shots/TU-11/`. 목 노인은 그림이 오기 전이라 이름만 있고, 웹 시안과 나란히 비교는 사람 눈으로
- [x] TU-12 침방 화면 2, 벽의 중원 지도: 배경 벽 빈자리에 `map_central_plains` 를 건다. 밟은 땅은 먹으로, 못 가 본 땅은 붉은 구름 아래 지명만 흐릿하게, 봉인석을 가져온 땅은 혈운이 걷히고 문파 깃발. 후보 카드에 커서가 가면 그 문파 자리에 붉은 점과 절정·초절정 장막 원을 먹선으로 그리고, 원 안에 드는 땅의 주인을 표시. 지명·깃발·원은 모두 Unity에서 얹는다(21장 '침방의 중원 지도'). Core 입구: `Core/Meta/Soul.cs`(밟은 땅·봉인석), `Core/World/Scale.cs`(장막 반경), `Regions.cs`(지역 위치). 먼저: TU-11 → 2026-10-31 `Presentation/Meta/ChimbangMap.cs`(+ `InkRing.cs` 먹선 원). 침방 오른쪽 벽에 `map_central_plains` 를 640×416 족자로 건다(세계 4,000×2,600 비율, 지역 자리 = pos × 판 크기). `Soul` 의 밟은 땅은 진한 먹 지명, 못 가 본 땅은 농도만큼 진한 붉은 구름 아래 흐린 지명, 봉인석을 가져온 땅은 구름 없이 旗. 봉마곡은 `Regions.Visible(봉인석 수)` 로 숨김. 카드에 커서가 가면(`EventTrigger`, 시험은 `ChimbangScreen.Hover(i)`) 태어나는 곳에 붉은 점, 절정 장막 원(실선)·초절정(점선)을 Core `BoundaryRadius` 로, 초절정 원 안 땅의 주인은 "지명 · 보스 이름" 주홍. 지도 밖으로 나가는 원은 RectMask2D 로 자름. 카드는 280 폭으로 줄여 왼쪽에. 스크린샷 `docs/unity_shots/TU-12/hover_tang.png`(4번째 생, 밟은 땅 넷, 천추석). 판이 작아 숭산·개방 총타·청운진 지명이 겹치고 초절정 원은 거의 판 밖이라, 지도를 크게 띄우는 확대 보기나 지명 겹침 정리가 필요할 수 있음 — 사람 눈 확인
- [x] TU-13 정산 화면: 생존 시간, 오른 경지, 밟은 땅, 처치 수(덩이 속 사람 수로 셈), 무공별 피해 비중 막대, 혈정, 윤회의 기 내역(진기 × 혈운 농도, 땅의 주인, 처음 밟은 땅, 처음 오른 경지, 남은 은자 절반, 성질 배율)을 한 줄씩 차오르게. 쓰러짐과 좌화는 같은 화면이고 끝나면 객잔으로. Core 입구: `Core/Meta/RebirthQi.cs`(항목별 내역), `Core/Meta/Soul.cs`(잔고에 더하기). 먼저: B-11 → 2026-10-31 `Presentation/Meta/SettlementScreen.cs`. `Show(LifeRecord, LifeSummary, SettlementStats, 혼 잔고)`: 왼쪽에 생존 시간·오른 경지·밟은 땅(곳 수와 이름)·처치·혈정과 무공별 피해 비중 막대(위 6개), 오른쪽에 Core `RebirthQiResult.Lines` 순서로 윤회의 기 내역(진기×혈운 농도·땅의 주인 처음/다시·처음 밟은 땅·처음 오른 경지·남은 은자 절반·성질 배율·혈월 보상)을 0.35초 간격으로 한 줄씩, 숫자는 0.4초 동안 차오르고(언스케일) 합계·혼 잔고, 새 봉인석·새 문파는 주홍. 쓰러짐·좌화·봉인은 제목만 다르다. Enter/Space 한 번은 다 채우기, 다시 누르거나 "객잔으로" 버튼이면 `Finished`(`loadInnScene` 켜면 Inn 씬). Core 장부에 없는 생존 시간·처치(덩이 사람 수)·혈정·무공별 피해는 `SettlementStats` 로 전투 쪽이 넘긴다. 시험 씬 `BloodRain/Test/정산 시험 씬 만들기`(당가·둔재, 지어낸 한 생: 928+300+150+300+120, 성질 +30% 539, 합계 2,337 확인). 스크린샷 `docs/unity_shots/TU-13/`
- [x] TU-14 연무장 화면: 윤회의 기 잔고와 10장 연무장 20항목(레벨, 다음 비용, 효과), 사기와 전부 환불. 같은 구매 칸으로 침방 아래에 환혼 강화 두 줄(넷째 몸, 다시 고르기)도 붙인다. Core 입구: `Core/Meta/Soul.cs`(잔고·연무장 레벨·환혼 강화), `Core/Build/Stats.cs`(연무장 비용 공식, balance.json `training`). 먼저: B-11 → 2026-10-31 `Presentation/Meta/TrainingScreen.cs` + 구매 칸 `MetaShop.cs`(`MetaShopRow`: 이름·레벨 점·레벨당 효과·다음 비용 버튼, 최대면 "다 익힘", 모자라면 흐림). 연무장 20항목을 두 칸 10줄로, 잔고, 전부 환불(+쓴 만큼). 이름은 10장 표(공력…정진), 쾌속은 −로 보임. 아래에 환혼 강화 두 줄. 침방 화면에도 지도 아래 같은 칸으로 환혼 강화 두 줄(산 효과는 다음 환혼부터). 비용·환불은 Core `Soul` 그대로: 6,000 에서 공력 둘(200+440)·기혈 하나(240)·다시 고르기 하나(400) 사면 4,720, 다음 공력 780 확인. 시험 씬 `BloodRain/Test/연무장 시험 씬 만들기`(윤회의 기 6,000). 스크린샷 `docs/unity_shots/TU-14/`
- [x] TU-15 돌파 줌 이어 받기: `BreakthroughFx.Play` 가 연출 중이면 false 로 버리는데, 연출 도중 또 한 경지를 넘으면(큰 구슬·쓸림 진기) Core `Breakthrough.Extend(toLevel)` 로 끝만 옮기게 한다. 화면 대각선은 Core 가 튀지 않게 맞춰 주고, 새 경지 글자·`RealmFactorChanged` 는 `ToRealm` 을 다시 읽어 바꾼다. Core 입구: `Core/World/Breakthrough.cs`, `Core/Run/Life.cs`(Absorb 가 같은 식으로 부름). 먼저: TU-02 → 2026-10-31 `BreakthroughFx.Play` 가 연출 중이면 `Extend` 로 이어 받는다. 세계 배율 지수와 장막 반경도 Core 대각선과 같은 식으로 출발점을 다시 맞추고, 새 경지면 `RealmFactorChanged` 를 다시 부름. 장막 값(`CurtainRadius·Alpha·Split`)은 이제 BreakthroughFx 가 셈해 BloodCurtain 이 읽는다. 시험 씬에서 연출 중 B: 삼류→이류 0.6초에 일류로 이어 받아 대각선 44.42m·배율 ×1.234·장막 114.4m 가 그대로 이어지고 일류(182.25m)로 끝남, 초절정→화경 0.9초에 현경으로 이어 받으면 장막이 그 자리에서 밀려나며 걷힘. 스크린샷 `docs/unity_shots/TU-15/`

### M1 코어 프로토타입 (backlog C-01~C-10 에서 옮김, 2026-10-30)

천하 놀이판(위 TU)보다 뒤에 한다. 숫자는 balance.json·weapons.json 과 Core 에서 읽고, 움직임은 웹 프로토타입(`web/prototype/template.html`)과 나란히 놓고 비교한다.

- [x] C-01 플레이어 이동(WASD·패드), 카메라 추적, 경공 대시(0.25초 4m, 무적 0.2초, 2회 충전. balance.json `dash`). 먼저: B-10, B-11 → 2026-10-31 대시 규칙은 Core `Combat/Dash.cs`(웹 doDash 와 같게: 충전 소모, duration 동안 distance/duration 속도로 바라보는 쪽, 무적 iframes, 대시 중 재사용 불가, rechargeSeconds 마다 하나, 최대 충전 늘면 바로 채움) + `DashTests` 7개. `Presentation/PlayerController.cs`: WASD·방향키·패드 왼쪽 스틱 걷기, Space·왼쪽 Shift·패드 A 대시, 걸음 4.6 경지 m/s × 몸 걸음 배율(웹 값, balance.json 에 아직 없음), 걸음·대시 모두 WorldScale 로 세계 m, 장막 PushVelocity·KeepInside. 카메라는 ScaleCamera 가 따라감. `BloodRain/Bootstrap/Run 씬 짓기(플레이어·카메라)` 가 Run 씬(카메라·플레이어·혈우 장막·소품/지도·지역 HUD)을 지음. 플레이로 대시 4.00m·충전 2→1·카메라 추적, W 3.5초에 16m 확인. 스크린샷 `docs/unity_shots/C-01/`. 머리 위 경공 충전 점은 아직(HUD C-08 쪽에서), 기본 걸음 4.6 은 balance.json 으로 옮길 것(클라우드) → 2026-10-31 23시 클라우드 세션이 `stats.moveBase` 로 옮기고 PlayerController·Run 씬의 인스펙터 칸은 지움(Core `StatsDef.MoveBase`)
- [x] C-02 적 시뮬레이션: 구조체 배열 + Burst 잡(추적 + 밀어내기), 강시 콩콩 박자 이동, 오브젝트 풀, 적 300마리 60fps 확인. 혈월 천하(회귀)면 적 기혈·속도에 `Life.EnemyHealthMultiplier`·`EnemySpeedMultiplier` 를 곱한다. 먼저: C-01 → 2026-11-01 `Sim/EnemySim.cs`: 살아 있는 적만 앞쪽에 둔 NativeArray<EnemyAgent>(죽으면 맨 뒤 것을 그 자리로, 꺼낼 때마다 새 Id). 한 프레임에 움직임 잡(곧장·콩콩·도망·직선 돌진, 넉백 0.02^dt, 대각선 1.5배 넘게 멀어지면 가는 쪽 0.75배 거리로 되돌림) → 격자 해시 잡 → 밀어내기 잡(3x3 칸 이웃 12까지, 이전 위치만 읽어 순서 무관), 모두 Burst 병렬. 콩콩은 웹과 같게 0.6초 박자 중 72% 동안만 뛰고, 덩이 렌더러가 그 박자로 포물선을 그린다(`ClusterView.HopBeat`). `Presentation/EnemyField.cs` 가 enemies.json speed/100 × moveBase 를 WorldScale 로 바꾸고 `UseLife(Life)` 로 혈월 기혈·속도 배율을 곱한다. 생성 고리·되돌리기 거리는 balance.json spawnTarget. 지금 생성은 목표 수 채우기 시험용(시강·혈강시·혈인·혈무사·동갑시, `[` `]` 반·두 배, C 비우기)이고 진짜 흐름은 C-10. Run 씬 메뉴가 '적 무리'를 붙인다. burst·collections·mathematics 를 manifest 에 직접 적음. 300마리 시뮬 0.14ms·에디터 200fps, 3000마리 0.69ms·125fps. 스크린샷 `docs/unity_shots/C-02/`. 혈무사 예고 돌진 같은 다른 갈래는 아직 곧장 쫓기, 플레이어와 충돌·접촉 피해는 C-03, 돌파 때 아래 경지 적을 밟힘 목록(CrushField)으로 넘기는 연결은 아직
- [x] C-03 격자 공간 해시 충돌(원 대 원), 접촉 피해 0.35초 간격. 먼저: C-02 → 2026-11-01 `EnemySim` 이 Step 끝에 최종 위치로 격자를 다시 짓고(`RebuildGrid`), `QueryCircle(가운데, 반경, 결과)` 가 원 대 원(거리 < 반경 + 적 반경)으로 찾는다. 가장 큰 적 반경만큼 둘레 칸을 더 봐서 큰 적도 안 빠진다. 무작위 원 200번을 전수 검사와 맞춰 빠진 것 0. 접촉은 `Contacts`: 플레이어 원에 닿은 적 가운데 적마다 쿨다운(contact.sameEnemyIntervalSeconds 0.35)이 끝난 것만, 아래 경지에서 생긴 덩이는 Core `Scale.DealsContactDamage` 로 뺌. 받는 쪽은 새 `Presentation/PlayerVitals.cs`(몸의 MaxHealth, max(contact.minDamage, 피해 − 방어), 대시 무적·맞은 뒤 무적 0.5초(웹 값, balance.json 에 아직 없음), 맞으면 흰 번쩍, 쓰러지면 `Died`, 시험 중엔 다시 채움). Run 씬 메뉴가 플레이어에 붙임. 플레이 7초에 5번 맞아 32 피해(무적 0.5초가 막음), 붙은 적의 쿨다운은 0~0.35 안. 스크린샷 `docs/unity_shots/C-03/`. 넉백·적 피해 받기는 무공 쪽(C-04), 맞은 뒤 무적 값을 balance.json 으로 옮길지는 클라우드 쪽에서 정할 것
- [x] C-04 무공 실행기: behavior별 컴포넌트(front_arc, homing_dagger, orbit_slash, aura …) 처음 8종. Core 입구: `WeaponMath`(Core/Combat/EnemyScaling.cs 안)·`Loadout`. 먼저: C-03 → 2026-11-01 같은 세션에 나머지 4갈래도: homing_dagger(가까운 적 1→4명, 초속 12m, 관통 1→3, 따라가기), slow_projectile(장풍, 초속 5m, 다 뚫음, 넉백 5, Lv6 2연발), random_rapid(섬광지, 초당 4→9발), random_bomb(벽력탄, 0.6초 뒤 반경 2→3m 폭발, 떨어질 자리 표시). 투사체는 WeaponRunner 안 목록(상한 limits.projectiles 1500, 한 프레임에 한 명씩 맞힘). 8종 모두 Lv8 로 켜서 8초에 비도 3344·장풍 2970·섬광지 693·벽력탄 7740 피해, 에디터 176fps, 콘솔 오류 0. 스크린샷 `docs/unity_shots/C-04/projectiles.png`. 남은 것은 절기(evolved) 모양, 중독·화상·느려짐 같은 적 상태(독무 느려짐·벽력탄 화상·비도 Lv6 중독), 넉백 저항, 능력치 배율(C-06 과 함께), 이펙트 그림 (앞 4갈래: 2026-11-01 `Presentation/Combat/WeaponRunner.cs` 에 쿨다운·지연 타격·무공별 피해 합(`Dealt`)과 4갈래 — front_arc(가까운 적 쪽 반원, Lv4 앞뒤, Lv7 3연), melee_combo(연타 3→5), orbit_slash(한 바퀴 칼날, 바퀴마다 한 번, Lv5 2회전), aura(0.5초마다 초당 피해 절반, 반경 2→3.5). 피해는 `WeaponMath.DamageAt`, 판정은 `EnemySim.QueryCircle`, 피해·넉백·쓰러짐은 새 `EnemyField.Damage`. Run 씬에 '무공'(몸의 시작 무공, 시험은 Ctrl+1~4). 네 개 켜고 13초에 300마리 무리에서 진기 123·레벨 5, 콘솔 오류 0. 스크린샷 `docs/unity_shots/C-04/`. 남은 것: homing_dagger·slow_projectile·random_rapid·random_bomb(투사체 풀), 넉백 저항(knockbackResist), 능력치 배율은 C-06 Loadout 과 함께, 이펙트는 임시 부채꼴)
- [x] C-05 진기 구슬 드롭·흡수, 400개 넘으면 합치기. Core 입구: `Core/Progression/RunXp.cs`. 먼저: C-03 → 2026-11-01 규칙은 `Sim/OrbField.cs`(웹 dropOrb·흡수와 같게: 자석 반경 stats.magnetBase 안에 들면 끝까지 끌림, 끌리는 속도 9 + 4×나이, 0.5m 안이면 흡수, limits.orbs 400 을 넘으면 오래된 100개를 합쳐 플레이어 둘레 5m 에 큰 구슬 하나, `PullAll` 은 취기부용). 길이·속도는 경지 m 를 WorldScale 로 바꿔 넘김. Core `BalanceDef` 에 `Limits`·`Orbs`(balance.json limits·orbs 를 읽을 자리가 없었음)를 더하고 `BalanceTests` 에 13장 '진기 1 / 5 / 25'·'400개' 대조 테스트 하나(933개 통과). `Presentation/Fx/OrbLayer.cs` 가 `EnemyField.Killed`(새 `Kill(index)`·`KillInside`)에서 구슬을 떨어뜨리고 13장 청·녹·적 색으로 인스턴싱해 그림, 흡수는 `Absorbed` 이벤트로 내보내고 Life 가 붙기 전까진 시험용 `RunXp` 에 넣어 레벨 표시. 적별 처치 진기는 정하는 중(P-02)이라 모두 작은 구슬(1). Run 씬 메뉴가 '진기 구슬'을 붙임, 시험 키 K 일격·O 구슬 500·M 모두 끌기. 확인: 61명 쓰러뜨려 구슬 61 → 모두 끌어 흡수, 진기 61·레벨 4. 500개 뿌리면 합침 2번, 302개, 값 합 500 그대로. 스크린샷 `docs/unity_shots/C-05/`. 흡수 소리·반짝임, 구슬 그림은 아직
- [x] C-05b 진기 구슬 숫자를 데이터에서 읽기. 2026-11-01 balance.json `orbPickup`(absorbRadius 0.5·pullSpeedBase 9·pullSpeedPerSecond 4·mergeBatch 100·mergeDistance 5, 길이는 경지 m)을 Core `Balance.OrbPickup` 으로 읽는다. `Sim/OrbField.MergeBatch` 상수와 `Presentation/Fx/OrbLayer.cs` 의 `absorbRealmMeters`·`pullBase`·`pullPerSecond`·`mergeRealmMeters` 기본값을 이 값으로 바꾼다(값은 그대로라 화면은 안 바뀌어야 한다). 먼저: C-05 → 2026-11-01 PC: OrbField.MergeBatch 를 생성자 인자로, OrbLayer 가 Awake 에서 Balance.OrbPickup 넷을 읽는다. 플레이로 100·0.5·9/4·5 확인
- [x] C-02c 떨어진 적을 옮기는 거리. `Presentation/EnemyField.cs` 는 `_sim.Step(…, diag * st.RecycleBeyondDiagonal, diag * st.RingMaxDiagonal)` 로 0.75배에 두는데 웹 프로토타입은 0.65배였다. 2026-11-01 balance.json `spawnTarget.recycleToDiagonal`(0.65)·Core `RecycleToDiagonal` 을 만들었으니 마지막 인자를 `diag * st.RecycleToDiagonal` 로 바꾼다. 먼저: C-02 → 2026-11-01 PC: EnemyField 되돌리기 거리를 st.RecycleToDiagonal(0.65)로
- [x] C-02d 혈인·금두꺼비 사라지는 시간과 혈무사 돌진. 2026-11-01 enemies.json `lifeSeconds`(혈인 14·금두꺼비 25)·`dash`(혈무사: 5m 안 예고 0.6초, 0.5초 동안 9m/s, 2~3.5초 쉼)를 Core `EnemyDef.LifeSeconds`·`EnemyDef.Dash` 로 읽는다. `Sim/EnemySim.LineLifeSeconds` 상수를 데이터로 바꾸고, 금두꺼비(Flee)는 지금 끝까지 남으니 25초 뒤 사라지게. telegraph_dash 는 EnemySim 에 아직 없어 그때 이 값을 쓴다. 먼저: C-02 → 2026-11-01 PC: EnemyAgent.MaxLife 에 EnemyDef.LifeSeconds 를 넣고 EnemySim 이 갈래 상관없이 지나면 걷는다(LineLifeSeconds 상수 없앰). 금두꺼비도 25초 뒤 사라짐. 플레이로 혈인 14 확인. 돌진(Dash)은 같은 날 C-02b 에서 붙임
- [x] C-03b 플레이어 몸 크기를 데이터에서. 2026-11-01 balance.json `stats.playerBodyRadius`(0.4, Core `Stats.PlayerBodyRadius`)를 만들었다. `Presentation/EnemyField.cs` 의 `_playerBody = 0.8f`(지름)와 PlayerController `bodyRealmMeters` 기본값을 반지름 x 2 로 읽게 바꾼다(값은 같다). 먼저: C-03 → 2026-11-01 PC: EnemyField·PlayerController 가 Stats.PlayerBodyRadius x 2 를 지름으로 읽는다(0.8 그대로)
- [x] C-02e 넉백 줄어드는 비율을 데이터에서. 2026-11-01 balance.json `enemyStatus.knockbackKeepPerSecond`(0.02, Core `EnemyStatus.KnockbackKeepPerSecond`). `Sim/EnemySim.cs` 의 `math.pow(0.02f, Dt)` 를 이 값으로(잡에 float 하나 넘기기). 값은 같다. 먼저: C-02 → 2026-11-01 PC: EnemySim.Step 에 knockKeepPerSecond 인자, MoveJob 이 KnockKeep(=값^dt)을 받는다
- [x] C-04b WeaponRunner 가 박아 둔 성장 숫자(Lerp(3,5)·Lerp(1,4)·Lerp(1,3)·Lerp(4,9)·반경 3.5·장풍 크기 1.6·벽력탄 반경 1.5)를 Core `Combat/WeaponGrowth`(Hits·Projectiles·Pierce·Cooldown·Range·Area·HasMilestone)로 바꾼다. 같은 값이라 화면은 그대로이고, 창천검기는 기획서 '범위 +40%'(areaLv8 1.4)가 새로 붙는다. 취선봉(random_arc)을 넣을 때도 휘두르기 1→3회·범위 +30% 를 여기서 읽는다. 2026-11-01 클라우드 세션이 weapons.json 에 areaLv8·취선봉 hits 를 더하고 기획서 9장 표와 대조 테스트(`WeaponGrowthTests`)를 붙임. 같이: 탄속·반경·수명·따라가기(Shoot 의 12·0.25·1.2·6, 5·0.55·2.6, 18·0.16·0.6)는 `Def.Shot`, 넉백(3·4·2.5·5·3)은 `Def.Knockback`, 벽력탄 7m·0.6초는 `Def.SeekRange`·`Def.FuseSeconds`, 연달아 치는 간격(0.12·0.08·0.15)·창천검기 한 바퀴 0.36초·독무 0.5초(hitDmg = dmg * 0.5f)는 `Def.GapSeconds`·`Def.SweepSeconds`·`Def.TickSeconds`, 새 동작 레벨(level >= 4·7·5·6)은 `WeaponGrowth.HasMilestone`(2026-11-01 weapons.json·기획서 9장 '날아가는 것과 넉백'). 웹은 비도·섬광지 넉백이 1.2인데 WeaponRunner 는 0이다, 데이터대로 1.2 로. 적 상태를 넣을 때 벽력탄 화상 2초는 `Def.BurnSeconds`, 비도 중독 2초·초당 한 방의 25%는 `Def.PoisonSeconds`·`Def.PoisonDamage`(2026-11-01). 지속 피해 간격 0.5초·둔화 풀림 초당 0.6은 `Balance.EnemyStatus`, 독무에 닿은 적 1초 중독은 poison_mist `Def.PoisonSeconds` → 2026-11-01 PC: WeaponRunner 의 Lerp·상수를 WeaponGrowth(Hits·Projectiles·Pierce·Range·Area·Cooldown·HasMilestone 이름으로)와 Def.Shot·Knockback·GapSeconds·SweepSeconds·TickSeconds·SeekRange·FuseSeconds 로 바꿈. 창천검기 범위 x1.4(Lv8), 창천검기 넉백 2→2.5, 비도·섬광지 넉백 1.2. 여덟 무공 Lv8 플레이 10초 에러 0. 화상·중독은 적 상태 칸이 생길 때 BurnSeconds·PoisonSeconds·PoisonDamage 로(남음)
- [x] C-12 혈마의 그림자(7장 '생이 끝나는 세 가지', backlog T-42): 현경 30분 뒤 `Life.Shadow`(Core `BloodShadow.Strength`)로 죽지 않고 밀리지 않는 그림자 하나. 속도 상한 없음. 단계가 오르면(`BloodShadow.TeleportsOn(def, 이전, 지금)`) `teleportWarningSeconds`(0.5초) 동안 몸 뒤 `teleportGap`(1m, 현경 축척) 자리에 핏자국 예고를 그린 뒤 그 자리로 옮긴다(이미 더 가까우면 그대로). 화면 위 적 피해에 `EnemyDamageMultiplier` — 2026-11-03 04:45 PC: `Presentation/BloodShadowRunner.cs`(단계는 `UseLife(life)` 면 `Life.ShadowStep`, 아니면 step 필드; 걸음 MoveSpeed/100 × moveBase, 상한 없음, 밀리지 않음·무공에 안 맞음, 닿으면 HitIntervalSeconds 마다 ContactDamage; 단계가 오르면 0.5초 동안 몸 뒤 1m 자리에 핏빛 예고가 번진 뒤 옮김, 이미 더 가까우면 그대로; `EnemyField.damageMultiplier` 에 EnemyDamageMultiplier — EnemyField 의 플레이어 피해가 모두 `HitPlayer` 를 거쳐 곱해짐). 메뉴 `BloodRain/Bootstrap/Run 씬에 혈마의 그림자 붙이기`(RunBootstrap 도 부름), 시험 `BloodRain/Test/혈마의 그림자 단계 올리기(플레이 중)`. 플레이로 1단계가 18m 를 초속 3.9m 로 따라와 0.5초마다 30씩 9번, 8m 떨어진 뒤 3단계로 올리니 예고 뒤 순간이동 1번, 적 피해 x1.2 확인 `docs/unity_shots/C-12/teleport_warn.png`, 콘솔 에러 0. 거리는 지금 경지 축척(현경에서만 나오니 같음). 남은 것: Run 씬에 Life 가 붙으면 `UseLife` 부르기, 그림은 임시 검붉은 원
- [x] 2026-11-01 C-06b 서장 첫 레벨업 — LevelUpScreen 이 서장 몸이면 첫 번만 RollFirstLevelUp, 카드 한 장 가운데·다시 고르기·파기 숨김(정진은 남김). 플레이로 첫 판 1장(창천검기)·다음 판 3장과 버튼 확인, 콘솔 에러 0 (backlog T-43): 서장(청운진)의 첫 레벨업만 `Loadout.RollFirstLevelUp(rng, luck, unlocked, data.Chapter("prologue"))` 로 뽑는다. 카드가 한 장(새 무공)뿐이라 레벨업 화면이 카드 한 장도 가운데에 그릴 수 있어야 하고, 다시 고르기·파기는 숨긴다. 그 뒤는 보통 `RollLevelUp`
- [x] V-04c 공통 텍스처 크기: 2026-11-02 클라우드 — VfxBootstrap 이 Common/common.json 의 `sizeMeters`({ink_splat: 1.4, ground_ink: 3}) 꼴을 못 읽어 먹 튐·바닥 먹 자국이 PPU 100(한 장 5.12m)으로 들어와 있었다. `Sizes` 가 그 꼴도 읽게 고치고 .meta 10개의 PPU 를 365.71·170.67 로 손으로 맞춰 둠. 이름 앞머리가 겹치면 먼저 나온 짧은 이름을 잡아 fx_mist_ink_2(3.5m)가 fx_mist_ink(3m) 크기로 들어가던 것도 가장 긴 이름을 잡게 고치고 .meta 를 146.29 로(테스트 `VfxImportSettingsTests`). PC 에서 Tools 의 VfxBootstrap 을 한 번 돌려 컴파일·.meta 가 그대로인지 확인. V-04 먹 튐·바닥 자국이 몸 크기에 맞게 보이는지 (2026-11-02 20:41 PC: VfxBootstrap 을 다시 돌려도 컴파일 에러 0, .meta PPU 그대로(ink_splat 365.71·ground_ink 170.67·fx_mist_ink_2 146.29). Run 씬에서 몸(0.8m 붉은 원) 옆에 먹 튐·바닥 자국을 놓아 보니 먹 튐 먹이 몸만 하고 바닥 자국은 몸 두 배쯤이라 크기는 맞다 `docs/unity_shots/V-04c/scale.png`. 다만 ink_splat 은 1.4m 칸에서 먹이 3분의 1쯤만 차서 작은 편 — 더 크게 튀어야 하면 sizeMeters 를 2 가까이로)
- [x] V-11b 먹 재료 다시 키잉(backlog V-11, 2026-11-02 클라우드): `import_vfx_ai.py` 가 먹 알파에 감마 0.75 를 건다. PC 에서 `python tools/import_vfx_ai.py --reink --inbox E:/Unity/BloodRain` 로 반영된 먹 재료 9장(fx_mist_ink·_2·_3, fx_bomb_ink_1·_2, fx_dagger_ink, fx_fist_ink, fx_palm_ink, fx_staff_ink)을 art_inbox 원본에서 다시 만들고(ai.json 에 inkGamma 0.75 가 붙음), `--gamma-preview fx_mist_ink fx_mist_ink_2 fx_mist_ink_3` 로 원본 | 전 | 후 한 장을 docs/vfx_preview 에. 원본이 없는 것은 건너뛰고 daily 에 적는다. Unity 는 png 만 바뀌니 다시 가져오기만 (2026-11-02 20:40 PC: --reink 로 아홉 장 모두 art_inbox 원본에서 다시 만듦, 건너뛴 것 없음. ai.json 아홉 줄에 inkGamma 0.75, 덮임이 1~4%p 늘었다. 원본|전|후 `docs/vfx_preview/ink_gamma_2026-11-02.png`, 미리보기 `docs/vfx_preview/ai_2026-11-02_2038.png`. Unity 다시 가져오기 뒤 콘솔 에러 0)
- [x] C-02b 수적 갈고리 머리 반경: enemies.json `hook.radius` 0.3m 를 정했고 EnemyField 가 그 값을 읽게 고쳐 둠(없으면 0.25). Unity 에서 컴파일·한 번 맞아 보기만 (2026-11-02 23:3x 예약 세션) — 2026-11-03 01:03 PC: 컴파일 에러 0, 플레이 6초에 날아가는 갈고리 Radius 0.3·묶임 1번 확인
- [x] C-02c 혈영 분신 상시 무공: `MimicWeapon` 에 `Interval`(다음 피해까지 초)·`Aura` 를 더했다(2026-11-03 21시 클라우드). EnemyField `StepMimics` 가 `Cooldown` 대신 `Interval` 로 돌리고, `Aura` 면 탄·사거리 대신 분신 몸 둘레(무공 range, 경지 m) 안에 있을 때 Interval 마다 한 번 맞히게. 잡몹 분신은 시작 무공에 독무가 없어 지금은 차이가 없고, 혈영회주가 부르는 분신(가진 무공 모두)을 붙일 때 같이 해도 된다 (2026-11-03 22:44 PC 완료 — `StepMimics` 가 Cooldown 대신 `Interval` 로 돌리고, `Aura` 면 탄·사거리 대신 분신 몸 둘레(무공 range + 플레이어 몸 반경) 안일 때 Interval 마다 `HitPlayer` 한 번. 플레이에서 분신 데이터에 AllWeapons 를 잠깐 켜고 Loadout 에 독무를 넣어(삼재검법 Interval 2.4·독무 Interval 1, Aura) 1.2m 둘레 분신 셋이 9.3초에 46번(삼재검법만이면 8.7초에 11번 — 셋 × 8.7/2.4 와 맞음), 콘솔 에러 0 `docs/unity_shots/C-02c/aura_clones.png`. 잡몹 분신은 시작 무공이 상시가 아니면 그대로)
- [x] V-05a 이펙트 단계 셈을 Core 로: `WeaponRunner.BrushTier` 가 마일스톤 수만 세서 마일스톤 없는 무공(섬광지·벽력탄·독무 등 7종)은 늘 1단이었다. vfx.md 규칙(없으면 Lv4·Lv7, 하나면 Lv7 기준으로 2단 또는 3단 문턱)을 Core `WeaponGrowth.VisualTier(def, level)` 로 만들어 둠(2026-11-03 23시 클라우드). BrushTier 를 `WeaponGrowth.VisualTier(s.Def, s.level)` 한 줄로 바꾸고 컴파일만 보면 된다. 삼재검법은 결과가 같다. (2026-11-04 00:4x PC: BrushTier 를 `WeaponGrowth.VisualTier(s.Def, s.level)` 한 줄로 바꿈, 컴파일·콘솔 에러 0)

## 옛 줄 원문(2026-11-04 UT-01 전의 열린 항목. 지난 진행 기록 — 체크 칸 없음, 할 일은 위 '할 차례')

- 원문 S-04 시너지 표시: 레벨업 카드 연환 띠·문파 인장, HUD 진전 인장, 비전·기연 발견 연출, 장경각 도감 화면(기획서 20장, 웹 프로토타입 동작 참고). 비급 해금(2026-11-01 S-06): `Synergy.Compute`·`Preview` 에 `soul.RestoredManuals` 를 늘 넘긴다(안 넘기면 비급까지 다 켜짐). 잠긴 것은 `LockedLinks`·`SchoolState.LockedTiers`·`LinkHint.Locked` 로 '?' 띠·인장, 시너지가 바뀔 때마다 `LifeLog.NoteSynergy`, 장경각에 찢긴 비급 목록과 복원 버튼(`Soul.CanRestore`·`TryRestore`·`NextRestoreCost`), 정산 화면에 `LifeSummary.NewTornManuals` (진행: 2026-11-02 00:40 PC 첫 판 — `LevelUpScreen` 카드 오른쪽 위 문파 인장(이름 두 자 + 고른 뒤 진전 수/다음 단계, 새 단계에 닿으면 핏빛), 카드 가운데 연환 띠(`Synergy.Preview` 로 켜지면 핏빛 '연환 완성 · 이름', 짝만 있으면 먹 띠 '짝 N단계까지', 잠긴 비급이면 '? 찢긴 비급'), 화면 왼쪽 위 HUD 진전 인장(켜진 문파·잠긴 단계 '?'·연환 수). `restored` 는 `RebirthHandoff.Soul`(침방이 고른 혼, 없으면 빈 목록)에서 늘 넘긴다. 플레이로 완성·짝·잠김 세 띠와 인장 확인. 남은 것: 비전·기연 발견 연출, 장경각 도감·복원 화면, `LifeLog.NoteSynergy`(Life 가 붙으면 RefreshSeals 자리에서), 정산 화면 찢긴 비급) (2026-11-02 02:40 PC: 발견 연출 — `Presentation/Fx/DiscoveryFx.cs`(먹빛 탁본이 좌우로 펼쳐지듯 뒤집혀 덮고 이름·한자, 붉은 낙관 秘傳/奇緣이 쾅 찍힘, 2초 머문 뒤 접힘, 언스케일·여럿이면 차례로)와 `Presentation/Hud/SecretWatch.cs`(RunRecord·SecretChecker 를 들고 0.5초마다·레벨업 화면이 닫힌 직후·`Poke()` 뒤에 판정, 찾은 것은 `Found`, 기연은 `ShowEncounter`). 레벨업 붙이기 메뉴가 둘을 Run 씬에 놓고, 시험 메뉴 `BloodRain/Test/발견 연출 보기(플레이 중)`. 플레이로 역천(역류 끊기 3연속을 Record 에 넣고 Poke)이 판정되는 것, 연출 화면 `docs/unity_shots/S-04/discovery.png` 확인. 남은 것: 정신부·음식·보갑·역류 끊기·의서·깬 장을 Record 에 알리기(그 기능이 Run 씬에 붙을 때), 기연 사건 맵 배치, 장경각 도감·복원 화면, `LifeLog.NoteSynergy`, 정산 화면 찢긴 비급·새 쪽 표시) (2026-11-03 03:05 PC 장경각·정산 — `Presentation/Meta/LibraryScreen.cs`(장경각 藏經閣: 윤회의 기·봉인석 잔고, 찢긴 수·복원 수와 다음 복원 값, 비급 층 시너지 전부를 두 줄 도감으로. 찢긴 것은 핏빛 이름·설명·복원 버튼(`Soul.CanRestore` 로 켜고 끔, 봉인석이 모자라면 '봉인석 N개 필요'), 복원한 것은 금빛 '복원함', 못 본 것은 '?' 로 가림), 이름은 새 `Meta/ManualNames`(연환 id·'문파:단계' → 이름·한자·설명). 시험 씬 메뉴 `BloodRain/Test/장경각 시험 씬 만들기`. 정산 화면은 `LifeSummary.NewTornManuals` 가 있으면 오른쪽 아래에 '찢긴 비급 장경각에 오름 · 이름들'(시험 한 생에 둘 넣음). 플레이로 9,000 → 복원 → 7,000(다음 값 봉인석 3·3,000) → 둘째 복원 → 4,000, 같은 것 다시는 거절 확인 `docs/unity_shots/S-04/library.png`·`settlement_torn.png`, 콘솔 에러 0. 장경각을 객잔(Inn 씬)에 붙이는 것은 객잔 화면이 생길 때. 남은 것: Record 알리기, 기연 사건 맵 배치, `LifeLog.NoteSynergy`(Run 씬에 Life 가 붙을 때))
  막힘: 2026-10-30 PC 세션 — 얹을 자리(레벨업 카드 C-06, HUD C-08)가 아직 없다. C-06·C-08 뒤에 하거나 장경각 도감 화면만 따로 떼어 주면 그것부터 한다 → 2026-11-02 풀림: C-06 첫 판 레벨업 화면에 카드 띠·인장을 얹고 HUD 인장도 그 화면이 같이 그린다(C-08 HUD 가 생기면 옮김)
- 원문 C-06 레벨업 화면(비급 책장 카드 3~4장, 다시 고르기·파기·정진). Core 입구: `Loadout`, `Synergy.cs`(카드 미리보기). 먼저: C-04, C-05. 횟수: 2026-11-01 Core `Build/LevelUpCharges.cs`(정진은 `UltimateCharge.OnSkip()`, 파기는 `Loadout.Banish` 와 함께 쓴다): `Life.Charges` 가 연무장 + 몸의 성질 + 특성 몫(balance.json levelUp.traitRerolls, 진소운 2)을 들고 시작하고, `Reroll()`·`Banish(loadout, id)`·`Skip(charge)`·`Add(…)`(다시 고르기 권)·`Can(…)` 으로 버튼을 켜고 끈다. 서장(진소운)은 Life 가 아니라면 `LevelUpCharges.Start(rules, 진소운, 연무장 보너스)`. 고른 뒤 진전 능력치(소림 호신 +2·기혈 +15%, 화산 치명 +5%, 개방 음식 +50%, 당가 지속 +20%, 수로채 은자 +30%)는 `Synergy.Compute(…).StatBonus()` 를 `Life.CurrentStats(extra)` 의 extra 에 더한다(2026-11-01). 곱하는 진전 효과(남궁 2 검 피해, 개방 3 낮은 기혈 공력, 곤륜 2·3 정신부·강시 피해, 수로채 3 경공 거리, 무당 3 사량발천근, 마도 2 흡혈·음식)는 `SynergyEffects.Of(syn)` 이 숫자로 내준다. 꽃잎(화산 3)·하늘 검기(남궁 3)·독 번짐(당가 3)·독침(당가 5)·경공 끌기(수로채 3)처럼 모양이 있는 효과는 `Tier(effect).Param(…)` 로 숫자를 읽는다(머리 주석에 뜻) (진행: 2026-11-01 PC 첫 판 — `Presentation/Hud/LevelUpScreen.cs` 와 메뉴 `BloodRain/Bootstrap/Run 씬에 레벨업 화면 붙이기`(RunBootstrap 도 부름). 레벨이 오르면 시간을 멈추고 RollLevelUp 카드 3~4장(갈래·이름·단계·새 동작/음양), 1~4·클릭으로 고르면 Loadout.Apply 와 WeaponRunner.Learn, 다시 고르기·파기·정진 버튼은 LevelUpCharges(지금은 특성 몫만, 연무장 덧셈은 Life 가 붙을 때). 플레이로 카드·다시 고르기·고르기 확인. 남은 것: 심법 능력치·Synergy StatBonus/SynergyEffects 를 몸에 얹기, 은자·음식 카드 효과, 레벨은 지금 OrbLayer 시험용 RunXp) (2026-11-02 클라우드: 은자·음식 카드 몫 balance.json `levelUp.fallbackCard`(은자 50 + 기혈 30, 음식 배율 안 곱함) → Core `BalanceDef.LevelUp.FallbackCard`, 기획서 8장) (2026-11-02 00:45 PC: 행낭 카드가 `FallbackCard` 대로 은자 50(`LevelUpScreen.Silver`, Life 가 붙으면 그쪽으로)·기혈 +30(`PlayerVitals.Heal`, 최대 넘지 않음), 카드에 '은자 50 · 기혈 +30'. 플레이로 기혈 50→80·은자 50 확인. 남은 것: 심법 능력치·시너지 StatBonus/SynergyEffects 를 몸에 얹기 — Run 씬에 Life(또는 몸 능력치 자리)가 생겨야 함)
- 원문 C-07 경지 돌파 연출(흑백 심상 2초) + 깨달음 3택1. Core 입구: `Insights.cs`, `World/Breakthrough.cs`. 돌파 보상(최대 기혈 +10, 경공 충전 +1, 오의 위력 +25%, 7칸째, 받는 피해 -10%, 오의 2연속)은 `Progression/RealmRewards.cs` 가 숫자로 모아 `Life.RealmRewards` 로 내준다(2026-11-01). 몸에 얹는 값은 2026-11-01 Life 가 내준다: `Life.CurrentStats()`(최대 기혈 +10 포함, PlayerVitals.MaxHealth 로), 돌파 뒤 `Dash.SetMaxCharges(life.DashMaxCharges(stats))`, `UltimatePowerMultiplier`(오의 피해에 곱함), `DamageTakenMultiplier`(Damage.Taken 뒤에 곱함), `UltimateCasts`(현경이면 한 번 눌러 두 번). 깨달음 효과는 `Build/InsightEffects.cs`(2026-11-01): `InsightPicks.Choose` 로 고른 것을 `Take(def, loadout.Gauge, 지금 처치 수)` 로 넘기면 역근은 그 자리에서 걸리고, 무공 피해·범위는 `WeaponDamageMultiplier(id)`·`WeaponRangeMultiplier(w)`, 흡기는 `MagnetMultiplier`, 능력치 덧셈은 `Life.InsightBonus(처치, 기혈 비율)`(심마 횟수는 혈공에서) 을 `Life.CurrentStats(extra)` 로. 생에서는 `Life.InsightEffects` 를 쓴다(시작 무공·천마해체가 이미 이어져 있다), 매 프레임 `Tick(dt, 움직임, 최대 기혈)` 이 운기조식 회복량을 돌려주고, 피해 직전 `TryBlock()` 이 true 면 금종조로 막는다. 먼저: C-06
- 원문 C-08 음양 태극 문양 HUD, 기혈역류 연출. Core 입구: `YinYangGauge`, `Backlash.cs`. 먼저: C-06. 태극 구간 폭은 `gauge.HarmonyRange`(몸 값과 진전 중 넓은 쪽, 2026-11-01). 시너지가 바뀔 때마다 `loadout.Gauge.SetSynergyHarmonyRange(Synergy.Compute(…).HarmonyRange)` 를 불러 무당 2단계 ±35 를 건다. 역류 중 걸음 `Backlash.MoveSpeedMultiplier`, 끊기 창 `CanCancel`, 다음 역류 예고 `SecondsUntilNext`
- 원문 C-09 오의 버튼(진소운 일검), 보갑, 절기 그릇과 각성 연출(J-01U·vfx.md), 절기 4종. Core 입구: `UltimateCharge.cs`, `Awakening.cs`. 먼저: C-07. 보갑 열기는 `Build/Chest.cs` 의 `Chest.Open(data, loadout, 레벨, rng, extraUpgrades)` 가 각성(Awakened) 아니면 강화 목록(이미 적용됨)을 돌려준다(2026-11-01). 강화 개수는 balance.json chest, 장강수로채 진전 4단계면 extraUpgrades 1. 각성이면 연출 끝에 `Loadout.Evolve`. 오의 피해는 `Combat/Ultimates.cs`(2026-11-01): `Ultimates.HitDamage(data, 몸, loadout, 공력, life.UltimatePowerMultiplier, 시작 무공의 절기·음양 배율)` 이 한 방 피해, `life.UseUltimate()` 가 true 면 `life.UltimateCasts` 번 쏜다 등롱·음식·은자 주머니(2026-11-01): Core `Build/Drops.cs` — `RollLantern(data.Balance.Drops, rng)` 가 bun·duck·chwigi_talisman·jeongsin_talisman·silver_bag 중 하나, `FoodHeal(rule, kind, stats.FoodHeal)`, `RollSilverBag`(탐욕·혈월 배율은 줍는 쪽이 곱함), 잡몹 은자는 `EnemyDropsSilver`. 정예 혈정은 `drops.eliteBloodCrystal` 절기 수치: 2026-11-01 evolutions.json `params`(EvolutionDef.Param) 에 프로토타입이 쓰는 아홉 절기 값(범위 배율·튕김·연쇄·분신 수·기절 초 …). 기획서와 다른 넷은 backlog E-02 절기 4종의 검기·권경 탄속·반경·수명, 폭발 반경·피해, 연쇄 간격·줄어듦은 `EvolutionDef.Param`(waveSpeed·waveRadius·waveLifeSeconds, projectileSpread·projectileSpeed·projectileRadius·projectileLifeSeconds, bounceRange, explodeRadius·explodeDamage, chainDelaySeconds·chainShrink·chainDamage. 2026-11-01 evolutions.json·기획서 9장 '절기로 바뀐 뒤 날아가는 것'). 정신부 정지 5초는 `Balance.Drops.JeongsinFreezeSeconds`(기획서는 '화면 안 적', 프로토타입은 모든 적을 멈춘다)
- 원문 C-10 서장 10분 통과: 스폰 디렉터 + 강시 포쾌 장삼 보스. Core 입구: `Run/Prologue.cs`, `Spawning/PrologueDirector.cs`(2026-11-01, 한 생 LifeDirector 와 같은 모양: 매초 `Second(분, 레벨, 살아 있는 수)` 가 만들 적·사건을 준다. 8:30~9:00 은 곡선이 0, 돌파 때 `OnBreakthrough()`, 각성 파동 뒤 `Hold(balance awakening.spawnPauseSeconds)`, 장삼전 잡몹 배율 `TargetScale` 은 P-02 가 정해질 때까지 1)). 장삼 패턴은 bosspatterns.json `jang_sam`(2026-11-01): `new BossPatterns(data.BossPatternSet("jang_sam"))` 로 원각과 같은 시간표, 2막 없음. 새 모양 둘 — `bind_line`(포승줄: 사슬처럼 날아가 맞으면 `RootSeconds` 동안 걷지 못함, 경공으로 끊김), `jump_slam`(내려찍기: Count 번 뛰고 뛸 때마다 그 순간 플레이어 자리에 그림자, Resolve 는 첫 착지, 뒤 착지는 TelegraphSeconds + WaveIntervalSeconds 간격). 호루라기는 시강 소환(summon). 장삼 최대 기혈은 `Prologue.BossMaxHealth(boss)`(enemies.json 6,000 그대로, 땅의 주인 식·농도 안 씀). 5:00 정예 강시 도부수의 큰 도끼 휘두르기(부채꼴)는 `data.ElitePatternSet("gangsi_axeman")` 를 같은 `BossPatterns` 로 돌린다(bosspatterns.json elites, 2막 없음). 먼저: C-01~C-09 (2026-11-04 클라우드 M1-02: 이제 Core `Life` 하나로 묶인다. `new Life(soul, Rebirth.Prologue(data), rng)` 면 `life.IsPrologue`, `Tick` 이 서장 표(`PrologueSpawns`)로 매초 생성·사건을 주고 장삼 사건이 나오면 `life.Prologue.Step` 이 Boss. 진소운 기혈 0 → `life.PlayerDown()`(Restart 면 `life.RestartPrologue(rng)` 로 새 판, 혼에 안 남음 / Awaken 이면 고정 긴 각성이 이미 시작됨, 기혈 1). 장삼 피해는 `life.DamagePrologueBoss(hp, max, dmg)`(바닥 10% 에서 각성), 0 이면 `life.KillPrologueBoss()` → `soul.EndLife(record)` → 컷 `Prologue.CurrentCut`/`NextCut` → `soul.OpenRebirth` → `Prologue.ChooseFirst`)
- 원문 E-01 숭산 정예 셋 패턴: 동인 몸통 박치기 3연타(`jump_slam`, 장삼 내려찍기와 같은 모양), 계율원 강시 승려 장풍(`projectile_fan` 세 갈래, 걸으면서 쏨), 강시 방장 대리 범종 기절(`circles` + Aim `self`: 제 몸 둘레 원이 따라다니고 맞으면 `StunSeconds` 0.8초 걷기·경공 못 함, 무공은 나감). 수치는 bosspatterns.json elites, `data.ElitePatternSet(id)` 를 `BossPatterns` 로 돌린다(2026-11-01). 웹 프로토타입 eliteAI 가 같은 데이터로 돈다. 먼저: C-10
- 원문 R-01U 당가타·장강·무당산 정예 아홉 패턴(기획서 6장 '정예 패턴 수치' 아홉 줄, bosspatterns.json elites, 2026-11-04 클라우드). E-01 과 같은 틀(`data.ElitePatternSet(id)` → `BossPatterns`)에 더할 것: 착지·판정 자리에 남는 장판(`PoolRadius`·`PoolDamagePerSecond`·`LeaveSeconds`, 겹쳐도 한 번, 판정은 `BossPatterns.InPool`), 수적 두목 갈고리 `bind_line` 세 파(`Waves`·`WaveIntervalSeconds`, 파마다 다시 겨눔, `RootSeconds` 경공으로 끊김, 시간표의 판정은 첫 파), 철갑 수채 무사 방패 돌진 `line`(판정 때 몸째 줄을 따라 `ProjectileSpeed` 로 달리고 `RecoverySeconds` 굳음), 검진 도사 `closing_ring` 검 다섯 자루(`RingOffsets(p, ClosingRadius(p, t), 도사 쪽 각도)`, 검마다 `HitIntervalSeconds`), 수귀 부르기 `summon`(수귀가 물 밖에서도 나오고 우두머리를 따라감), 흑백 쌍강시 대형은 한 쌍에 시간표 하나(검은 몸 먼저 한 번씩, 한 몸이 누우면 선 몸이 두 번). 웹 프로토타입은 숭산만이라 그대로. 먼저: E-01
- 원문 R-12U 무공 일곱(`random_arc`·`orbiting_blades`·`pull_chain`·`forward_claws`·`ring_wave`·`pierce_thrust`·`petal_scatter`)과 그 절기 모양을 WeaponRunner 에(docs/design/m3_check.md '무공 16·절기 16'). 성장 숫자는 Core `WeaponGrowth`, 날아가는 것·넉백이 비어 있는 여섯은 웹 WP-01 로 손맛을 본 뒤. 먼저: C-04b
- 원문 R-13U 세 지역 기믹(독안개·물·태극 바닥, gimmicks.json 그대로, Core `Gimmicks` 판정)을 지역에 놓기. 지금 Presentation·Sim 에 `Gimmicks` 를 부르는 곳이 없다. 수귀(물에서만 솟음)는 물 지형 뒤. 놓는 자리·수는 backlog R-10 이 정해진 뒤
- 원문 R-14U 땅의 주인 셋(당소혜·마천패·혈허진인) 패턴 러너: bosspatterns.json 그대로 `BossPatterns` 시간표, 모양 판정은 R-11 Core 함수(11-04 끝남): 흑백 `BossPatterns.SplitFieldDamageTaken(p, dx, dy, 예고 때 보스→플레이어 각도, 켜진 뒤 초, gauge.Leaning)` 를 받는 피해에 곱함, 당소혜 2막은 `new BossSplit(p, 남은 기혈)`(몸마다 `Damage`, 매 프레임 `Tick` 이 돌려준 몸을 일으킴, 분열한 동안 패턴 Tick 멈춤, 자리 `RingOffsets(p, p.Scatter, 각도)`), 독안개 `HalfFieldFog.Choose/Covers`, 칠성검진 `BigDipperOffsets(p, 플레이어→보스 각도)`, 태극권 `InhalePull`(경공 중에도 끌지는 기획서에 없음, 웹은 경공 중 안 끎)·`InBurst`, 사슬 `ChainReach`·`InChain`·`ChainPullOffset`(경공이면 끊김). 원각 러너가 있으면 같은 틀로
- 원문 R4-02U 땅의 주인 셋(낙매검선의 검혼·빙혈 설무향·혈영회주 혁련휘) 패턴 러너(2026-11-04 클라우드 M4 지역): 시간표는 `BossPatterns`, 모양은 R4-02 Core 함수. 꽃잎 나선 `SpiralShotDegrees(p, 플레이어 쪽 각도, 갈래, 발)` 를 `SpiralShotSeconds` 마다, 일섬 뒤 `RecoverySeconds` 굳음, 매화검기 `RadialBurstDegrees(p, 0, 파)`, 눈보라 덮개 `ViewScaleAt`(카메라는 그대로), 빙분신 `new BossClones(p, rng)`(진짜만 그림자 스프라이트, 분신 `Hit` 이면 깨짐 연출, `Standing` 이 거짓이면 지움), 얼음 가시 `ChainCircleOffset`·`ChainCircleSeconds`·`InChainCircle`, 2막 싸움터 `ArenaRadiusStep(p, r, dt, CampfireGimmick.Inside(…))`·`OutsideArena` 면 0.5초마다 피해, 순간이동 `BlinkOffsets`·`BlinkSeconds`·`BlinkSlamSeconds`, 혈월 `EmpowerDamage`·`EmpowerSpeed` 를 모든 적에. 먼저: R-14U(같은 러너 틀)
- 원문 R4-03U 종장 러너(2026-11-04 클라우드 M4 지역): 차례·기혈·단계 시간표는 `Combat/FinalFight`(`Patterns` 가 단계마다 시간표), 모양은 R4-03 Core 함수. 혈의 맥 짚는 손 `InClosingRing(p, 판정 뒤 초, dx, dy)` 가 참이면 0.5초마다 피해(경공 무적이면 통과), 역병 구름 `CloudOffsets`·`CloudDrift(p, 구름→플레이어, dt, moveBase)`·`CloudAlive`, 혈해 피바다 섬 `IslandRadius`·`OnIsland`(섬 자리는 R4-07 이 정해질 때까지 싸움터에 고르게 아무렇게나), 혈영 솟음 `FinalFight.RisingShadow(n)`·`RisingShadowHealth`, 진혈마 혈우 `RainDropOffsets`·`RainWaveSeconds`(파마다 플레이어 자리로 다시), 진엔딩 컷 `CheckTrueEnding`. 먼저: R-14U
- 원문 R4-04U 엔딩 고르기 화면(2026-11-04 클라우드 M4 지역): 봉인으로 생을 마친 뒤 `soul.EndingPending` 이면 정산 다음에 엔딩 고르기. 버튼은 `soul.EndingsOpen(목 노인 대화 끝)`(1회차는 봉인 하나라 고르기 없이 바로 봉인 엔딩 컷), 고르면 `soul.ChooseEnding(id, …)` 뒤 그 엔딩 컷·곡(14장 '엔딩 3곡'), 봉인이면 백중의 밤으로(회귀), 진엔딩이면 `JourneyOver` 라 침방이 닫힌 객잔으로. 진혈마 10% 컷은 `FinalFight.CheckTrueEnding(비율, soul.TrueEndingMet(…))`. 목 노인 대화 끝은 R4-08 전까지 false
- 원문 C-02b EnemyField 행동 갈래를 붙일 때 숫자는 `EnemyDef` 에서 읽는다(2026-11-01 Core 에 더함): Telegraph(혈무사 0.6·혈영자객 1.0), ProjectileDamage(혈궁수 8), ZoneDamagePerSecond(혈술사 6), AuraRadius·AuraBonus(기수 5m +20%), IgnoresTerrain(비강), OnlyInWater(수귀), ReviveSeconds(흑백 쌍강시 10), OnHitSlowSeconds(빙강시 2), OnDeath(목인나한·독두꺼비), Reward(금두꺼비 은자 100·구슬). 기획서 6장 표와 대조 테스트 `EnemyTableTests` (진행: 2026-11-01 PC 혈무사 telegraph_dash — EnemyMove.Dash·DashPhase(쫓기→예고→돌진→쉼), 수치는 EnemyDef.Telegraph·Dash(5m·0.6초·0.5초 9m/s·2~3.5초), 예고 동안 붉게 깜빡임(이펙트 전 임시). 플레이로 34마리가 네 단계를 도는 것 확인. 2026-11-01 22:50 PC 기수 buff_aura — EnemyAgent.AuraRadius·AuraBonus·Buff, EnemyField.ApplyAuras 가 매 프레임 깃발 둘레(auraRadius 5 경지 m) 다른 적에 +0.2(겹쳐도 큰 값 하나, 기수끼리는 안 받음), 이동·돌진 속도와 접촉 피해에 (1+Buff). 받는 적은 핏빛으로 물듦(임시), 시험 생성표에 기수 1. 플레이로 기수 넷 둘레 91마리 모두 반경 안·+0.2 확인. 덤으로 ClusterView 색이 프레임마다 겹쳐 섞이던 것(혈무사 예고 뒤 붉은 기가 남음)을 종류 색에서 다시 얹게 고침. 남은 것: 혈영자객 순간이동, 혈궁수 탄, 혈술사 장판, 비강·수귀, 쌍강시 부활, 빙강시 둔화(2초는 있으나 얼마나 느려지는지가 기획서 6장·enemies.json 에 없다 — 클라우드 세션이 값을 정해 줘야 함), 쓰러질 때 남기는 것, 금두꺼비 보상) (2026-11-02 클라우드: 숫자 채움 — 빙강시 `onHitSlowMultiplier` 0.65, 혈궁수 `EnemyDef.Shoot`(6~9m 지킴, 2초, 초속 8m, 12m, 반경 0.25), 혈영자객 `Mark`(반경 1.2m, 4초 주기, 예고는 Telegraph 1.0), 혈술사 `Pool`(반경 1.5m, 6초, 4초마다, 3개까지), 금두꺼비 `Reward.Orbs`·`OrbXp`(12알×5). 기획서 6장 표 아래 문단, 테스트 EnemyTableTests. 같은 날 목인나한·독두꺼비 `EnemyDef.Death`(나뭇조각 2.5m 적 40·플레이어 6 / 독 장판 1.2m 4초 초당 5), 비강 `Curve`(옆 1.5m, 1.6초). 지역 고유 적 나머지 행동(수귀 끌기·수적 갈고리·매화검귀 꽃잎·설랑 측면·설인 투척·마교도 창진·혈영 분신·혈사자·금선지네·독인 안개)도 같은 날 숫자 채움: `Pull`·`Hook`·`Shoot`(Count·Spread, KeepMin·Max 0 이면 쫓으며 쏨)·`Group`·`Mimic`·`RoarSeconds`(혈사자는 Dash + 포효 오라)·`ZoneRadius`. 기획서 6장 '지역별 고유 적' 아래 목록) (2026-11-02 00:50 PC 쏘는 적 — `EnemyMove.Keep`(kite_shoot: KeepMax 보다 멀면 다가오고 KeepMin 보다 가까우면 물러남), EnemyAgent 에 KeepMin·KeepMax·ShootRange·ShootInterval·ShotLeft·Fire, 시뮬 잡이 Interval 마다 Range 안이면 Fire 를 세우고 EnemyField 가 `EnemyDef.Shoot`(Count·Spread 포함)대로 탄을 만든다. 탄은 Range 날아가면 사라지고 몸에 닿으면 ProjectileDamage(무적이면 지나감), 그림은 임시 점. Shoot 가 있으면 갈래와 상관없이 쏘니 매화검귀·설인도 같은 길. 시험 생성표에 혈궁수 4. 플레이 6초에 혈궁수 일곱 중 다섯이 6~9m 띠 안, 탄 6발 날아가고 맞음 확인. 같은 차례에 빙강시 둔화 — 접촉 피해가 실제로 들어가면 `PlayerController.Slow(onHitSlowSeconds, onHitSlowMultiplier)`(다시 닿으면 시간만 처음부터, 대시는 안 느려짐), 시험 생성표에 빙강시 4. 플레이로 닿자마자 2초·0.65배 확인. 금두꺼비 보상 — 쓰러지면 OrbLayer 가 `Reward.Orbs`×`OrbXp`(12알×5)를 둘레 1.5m 에 쏟는다(은자 100 은 Life 가 붙을 때). 생성표 밖 적도 SpawnAt 이 가중치 0 으로 종류 표에 붙여 `EnemyField.DefOf(kind)` 로 읽힌다. 플레이로 구슬 12알 확인. 남은 것: 혈영자객 표식·순간이동, 혈술사 장판, 비강 곡선·수귀, 쌍강시 부활, 쓰러질 때 남기는 것(목인나한·독두꺼비), 지역 고유 적의 끌기·갈고리·창진·분신·포효·안개) (2026-11-02 02시 클라우드: 쌍강시 되살림 숫자 — `EnemyDef.Twin`(Gap 1m·정예 대형 1.5m, ReviveHealth 0.5)과 ReviveSeconds 10(정예 대형에도 넣음). 한쪽이 쓰러지면 그 자리에 눕고 남은 쪽이 10초 버티면 일어남, 둘 다 누우면 끝, 되살아난 몸은 구슬을 다시 떨구지 않음, 정예 한 쌍은 보갑 하나. 기획서 6장 '지역별 고유 적' 문단) (2026-11-02 20:46 PC 혈술사 피 웅덩이 — EnemyField `StepPools`: `EnemyDef.Pool` 이 있는 적이 4초마다 그때 플레이어 발밑에 반경 1.5m(경지 m) 웅덩이를 6초, 한 혈술사당 3개까지(넘으면 오래된 것부터). 밟고 있으면 1초마다 ZoneDamagePerSecond 6(기수 깃발이면 배율) 한 번, 겹쳐도 한 번, 무적이면 Hit 이 거름. 화면 밖(대각선 절반 너머) 혈술사는 안 깖, 깐 적이 쓰러져도 웅덩이는 남음, 그림은 임시 핏빛 원(sortingOrder 2 — 바닥 위지만 적 덩이보다 위에 그려짐, 이펙트 붙일 때 정렬 다시). 시험 생성표에 혈술사 3. 플레이로 웅덩이가 깔리고 적을 다 비운 뒤 3.3초에 4번 맞음 확인 `docs/unity_shots/C-02b/blood_pool.png`, 콘솔 에러 0. 남은 것: 혈영자객 표식·순간이동, 비강 곡선·수귀, 쌍강시 부활, 쓰러질 때 남기는 것(목인나한·독두꺼비), 지역 고유 적의 끌기·갈고리·창진·분신·포효·안개) (2026-11-02 20:48 PC 쓰러질 때 남기는 것 — `EnemyField.Kill` 이 `EnemyDef.Death` 가 있는 적을 모아 두고 `ResolveDeaths` 가 프레임마다 푼다(Damage 고리 안에서 자리 번호가 꼬이지 않게). 목인나한 splinter_burst 는 반경 2.5m 안 다른 적에 40·플레이어에 6 한 번(연쇄로 터지면 다음 프레임에 이어서), 터진 자리를 0.3초 갈색으로 비춤. 독두꺼비 poison_pool 은 그 자리에 반경 1.2m 독 장판 4초, 밟으면 초당 5 — 피 웅덩이와 같은 줄이라 둘을 함께 밟아도 1초에 한 번(큰 값). 시험 생성표에 목인나한·독두꺼비 4씩. 플레이로 목인나한 옆 시강·혈강시(8·20)가 나뭇조각에 쓰러지고, 독 장판이 깔리는 것 확인 `docs/unity_shots/C-02b/death_pool.png`, 콘솔 에러 0. 남은 것: 혈영자객 표식·순간이동, 비강 곡선·수귀, 쌍강시 부활, 지역 고유 적의 끌기·갈고리·창진·분신·포효·안개) (2026-11-02 20:53 PC 혈영자객 표식·순간이동 — EnemyField `StepMarks`: `EnemyDef.Mark` 가 있는 적은 처음 나타나면 바로, 그 뒤 표식부터 4초마다 그때 플레이어 발밑에 반경 1.2m 붉은 표식(임시 원), Telegraph 1초 뒤 그 자리로 옮기고 그 순간 표식 안이면 접촉 피해 14 한 번. 시험 생성표에 혈영자객 2. 플레이로 6m 떨어진 혈영자객이 표식 1초 뒤 플레이어 자리로 옮겨 맞히는 것 확인 `docs/unity_shots/C-02b/mark.png`, 콘솔 에러 0. 남은 것: 비강 곡선·수귀, 쌍강시 부활, 지역 고유 적의 끌기·갈고리·창진·분신·포효·안개) (2026-11-02 22:46 PC 비강 곡선 — `EnemyMove.Curve`(fly_curve): 쫓으면서 옆으로 `EnemyDef.Curve` Amplitude(경지 m→세계 m)·Period 대로 흔들림, 옆 속도는 sin 의 미분이라 앞으로 가는 빠르기는 데이터 그대로, 위상은 나올 때 무작위. Curve 가 없으면 Chase. 시험 생성표에 비강 4. 따로 돌린 EnemySim 하나로 옆 자리 -1.58~+1.42m·1.6초 주기 확인, 플레이로 비강 여섯이 흔들리며 다가오는 것과 콘솔 에러 0 확인. ignoresTerrain 은 아직 지형 막힘이 없어 할 일 없음. 남은 것: 수귀, 쌍강시 부활, 지역 고유 적의 끌기·갈고리·창진·분신·포효·안개) (2026-11-02 22:48 PC 흑백 쌍강시 되살림 — `EnemyDef.Twin` 이 있으면 SpawnAt 이 플레이어 쪽에 직각으로 Gap m 옆에 짝을 하나 더 세우고 둘을 짝으로 묶는다. 한쪽이 쓰러지면 그 자리에 눕고(임시 잿빛 원, 일어나기 1초 전 깜빡임) 짝이 ReviveSeconds 10초 버티면 최대 기혈 × ReviveHealth 0.5 로 일어나 다시 짝, 둘 다 누우면 끝. 되살아난 몸은 `EnemyAgent.NoDrop` 이라 OrbLayer 가 구슬을 안 떨군다. 시험 생성표에 쌍강시 2. 플레이로 5m 옆 쌍을 하나 쓰러뜨리니 누운 원이 뜨고 `docs/unity_shots/C-02b/twin_fallen.png`, 10초 뒤 기혈 30/60·NoDrop 으로 일어남, 하나가 누운 채 다른 쪽을 쓰러뜨리면 둘 다 사라짐 확인, 콘솔 에러 0. 정예 대형 한 쌍 보갑 하나는 보갑이 붙을 때(C-09). 남은 것: 수귀, 지역 고유 적의 끌기·갈고리·창진·분신·포효·안개) (2026-11-02 22:51 PC 수적 갈고리 — EnemyField `StepHooks`: `EnemyDef.Hook` 이 있는 적은 플레이어가 Range 7m 안이고 쉬는 시간이 끝나면 그 순간 방향을 굳혀 Telegraph 0.5초 서서 붉은 예고선, 그 뒤 초속 14m 갈고리를 Range 까지 던짐(그림은 적에서 갈고리 머리까지 먹빛 줄, 임시). 몸에 닿으면 새 `PlayerController.Bind(0.8초)`(걷기·경공 못 함, 무공은 나감, 무적이면 지나감), 다음까지 Interval 5초, 처음 나온 적은 0~5초 사이 아무 때나. 시험 생성표에 수적 3. 플레이로 수적 둘이 17초에 5번 묶고 BindLeft 0.79 확인, 날아가는 줄 `docs/unity_shots/C-02b/hook.png`, 콘솔 에러 0. 갈고리 머리 반경은 데이터에 없어 혈궁수 탄과 같은 0.25m 로 박아 둠 — 클라우드 세션이 enemies.json hook 에 radius 를 정해 주면 그걸로. 남은 것: 수귀(물 지형이 아직 없음), 지역 고유 적의 끌기·창진·분신·포효·안개) (2026-11-02 22:54 PC 독인 독안개·혈사자 포효 — 독인 poison_aura: `EnemyDef.ZoneRadius` 1.5m 안이면 초당 ZoneDamagePerSecond 4(기수·포효 배율), 웅덩이와 같은 줄이라 겹쳐도 1초에 한 번 큰 값, 몸을 따라다니는 옅은 초록 원(임시). 혈사자 charge_roar: MoveOf 가 Dash 로 돌려 혈무사와 같은 돌진(6m·0.6초 예고·0.6초 10m/s·3~4초 쉼), 돌진이 끝나 쉬기로 들어서는 순간 포효해 `RoarSeconds` 4초 동안만 반경 5m 적에 +0.25(기수 깃발과 같은 ApplyAuras 길, RoarSeconds 0 인 기수는 늘 켜짐). 시험 생성표에 독인 3·혈사자 2. 플레이로 독인 1.2m 옆에서 4씩 두 번 맞고, 혈사자 포효 뒤 둘레 일곱이 +0.25, 포효 29.98초 → 꺼짐 33.98초로 정확히 4초 확인 `docs/unity_shots/C-02b/roar_zone.png`(왼쪽 초록 안개, 가운데 핏빛 물든 무리), 콘솔 에러 0. 남은 것: 수귀(물 지형이 아직 없음), 설랑 측면·마교도 창진·금선지네 줄(무리 생성), 혈영 분신) (2026-11-03 01:05 PC 설랑 flank_pack — `EnemyMove.Flank`: CommitRange(3m, 경지→세계 m)보다 멀면 플레이어 쪽 방향을 `Group.FlankAngle`(60도)만큼 돌려 감아 들고, 안이면 곧장 덮친다. SpawnAt 이 한 마리를 부르면 둘레 1m 안에 `Group.Size`(5) 마리가 한꺼번에 서고 왼쪽·오른쪽(+60·-60)을 번갈아 받는다. 시험 생성표에 설랑 1. 플레이로 오른쪽 10m 에 부른 무리 다섯이 +60·-60 으로 갈리고, 생성표 무리들도 두 날개(방위 +35° 무리·-105° 무리)로 벌어져 들어오는 것 확인, 콘솔 에러 0. 그림은 아직 다른 적과 같은 임시 덩이라 화면으로는 구별이 어렵다. 남은 것: 수귀(물 지형), 마교도 창진·금선지네 줄, 혈영 분신) (2026-11-03 01:07 PC 금선지네 snake_line — 머리 한 마리를 부르면 플레이어 반대쪽으로 `Group.Spacing`(0.45m) 간격 마디 7개가 늘어서고, 마디는 새 `EnemyMove.Follow` 로 바로 앞 마디 자리를 간격만큼 떨어져 따라감(많이 벌어지면 1.5배까지 빨리, 멀어진 적 되돌리기에서 빠짐). EnemyField `StepFollow` 가 매 프레임 앞 마디 자리를 넣고, 가운데 마디가 쓰러지면 그 앞 마디로 줄을 잇고, 머리가 쓰러지면 남은 마디를 Chase 로 돌린다(`SnakeScattered`). 시험 생성표에 금선지네 1. 플레이로 위 9m 에 부른 지네가 무공에 마디 넷을 잃고도 남은 셋이 머리를 따라오고(밀어내기 때문에 실제 간격은 0.66~0.77m), 머리를 쓰러뜨리니 셋 모두 Chase 로 바뀜 확인, 콘솔 에러 0. 마디 그림은 임시 덩이. 남은 것: 수귀(물 지형), 마교도 창진, 혈영 분신) (2026-11-03 01:09 PC 마교도 spear_line — 머리 한 명을 부르면 플레이어 쪽에 수직으로 `Group.Spacing`(0.8m) 간격 +1·-1·+2·-2·+3 칸에 `Group.Size`-1(5) 명이 서고, 같은 `EnemyMove.Follow` 로 매 프레임 '머리 자리 + 플레이어 쪽 수직 × 제 칸' 을 지키며 머리와 함께 전진(줄은 늘 플레이어를 마주 보게 돈다, `FollowLink.Offset`). 머리가 쓰러지면 지네처럼 흩어져 쫓는다(기획서에 없는 부분, 지네와 맞춤). 시험 생성표에 마교도 1. 플레이로 왼쪽 아래 11m 에서 부른 줄이 5.8초 뒤 플레이어 1m 앞까지 와서도 옆 칸 1.08·-0.63·1.57·-2.02·2.70m, 앞뒤 어긋남 0.5m 안으로 한 줄 유지 확인(플레이어 둘레 무리 밀어내기로 칸이 조금 흔들림), 콘솔 에러 0. 남은 것: 수귀(물 지형), 혈영 분신) (2026-11-03 02시 클라우드: 혈영 분신 Core 준비 — `Mimicry.Weapons(data, character, loadout, mimic)` 가 분신이 쓸 무공 목록(무공 id·레벨·한 번 피해·쿨다운)을 준다. 잡몹은 시작 무공 하나에 0.3배·쿨 2배, 혈영회주 mirror_clone 이 부른 분신은 `Mimicry.For(enemy, pattern)` 로 패턴의 mimic(가진 무공 모두·피해 절반·쿨 그대로, `AllWeapons`)을 쓴다. 피해는 무공 기본 피해×배율이고 심법 보너스는 안 받음. 플레이어에게 들어갈 때는 `Damage.Taken` 으로 호신을 뺀다. 남은 것: WeaponRunner 가 '쏘는 쪽=적, 맞는 쪽=플레이어'로 무공을 돌리는 길) (2026-11-03 02:3x 클라우드: 절기로 각성한 무공도 분신은 바탕 무공으로 흉내 낸다 — Mimicry 가 Evolved 를 보지 않으니 WeaponRunner 쪽도 절기 모양을 붙이지 않으면 됨) (2026-11-03 20:55 PC 혈영 분신 잡몹 — WeaponRunner 를 거꾸로 돌리는 대신 EnemyField `StepMimics` 가 `Mimicry.Weapons`(Loadout 은 레벨업 화면 것, 없으면 시작 무공 Lv1)를 적마다 쿨다운으로 돌린다. 날아가는 무공(shot 이 있거나 random_bomb·lightning·rapid)은 사거리(최소 6m) 안이면 그 무공 탄속·반경의 적 탄 하나를 플레이어 쪽으로, 붙어 치는 무공은 그 무공 사거리 + 몸 반경 안이면 바로 한 번(HitPlayer, 무적이면 거름), 사거리 밖이면 쿨을 쓰지 않고 기다림, 감전 중엔 안 씀. 쿨다운 0(독무)은 안 씀. 플레이로 6m 둘레에 부른 분신 셋이 다가와 8초에 21번 쓰고 5번 맞힘(나머지는 맞은 뒤 무적), 기혈 100→64, 콘솔 에러 0. 피해 배율은 C-14 결정을 따라 데이터만 바꾸면 됨. 날아가는 무공 쪽은 시작 무공이 삼재검법이라 플레이로는 못 봄. 그림은 임시(탄 점·덩이). 남은 것: 수귀(물 지형), 혈영회주가 부르는 분신(보스가 붙을 때 `Mimicry.For(enemy, pattern)`))
- 원문 V-04 붓 획 셰이더와 이펙트 틀(backlog V-04, docs/design/vfx.md '만드는 법'): URP 2D 셰이더 `_Progress`(각도 마스크로 획이 그어짐, 호는 텍스처 가운데 기준 각, 찌르기는 +x 방향 진행)·`_Dissolve`(Common/noise_1 문턱으로 흩어짐), 층별 색(stroke 먹 #141414, wash 청회 #4F7F7A, gold #C9A227). 담채는 wash 텍스처가 커지며 옅어짐, 먹 튐 파티클(Common/ink_splat_*), 바닥 먹 자국 데칼 풀(Common/ground_ink_*, 8초에 마름). 텍스처는 `Assets/Art/VFX/Brush`·`Common`(Sprite, 크기는 각 json sizeMeters, Pixels Per Unit = 512/sizeMeters). 삼재검법 1단 하나만 붙여 웹 빌드 화면 한 장 AI 재료(`Assets/Art/VFX/AI/ai.json`, `art_pipeline.md` '적용')도 같은 셰이더로: ai_ink 는 stroke 층 먹, ai_glow 는 gold 층 금빛 + core 층 흰빛을 더하기 섞기(Blend SrcAlpha One)로, 그어짐은 json `progress` 방향(radial·down·up·right·left). AI/fx_lightning_glow·fx_mist_ink 가 들어오면 같이 시험 (진행: 2026-11-01 PC — 셰이더 `Art/Shaders/BrushStroke.shader`(_Progress 호 각도 마스크·찌르기 +x, _Dissolve noise_1 문턱, _Color·_Alpha), 메뉴 `BloodRain/Bootstrap/붓 획 이펙트…`(VfxBootstrap: Brush·Common 127장 Sprite·PPU 512/sizeMeters·Clamp, `Art/VFX/BrushStroke.mat`, Run 씬 WeaponRunner 에 삼재검법 1단 세 장 연결). 삼재검법 앞 베기가 0.1초 그어지고 0.06초 뒤 0.24초 흩어진다, 에디터 슬로모션 화면에서 획이 그어지는 것 확인. 2026-11-01 22:55 PC AI 재료 — 셰이더 `_Kind` 2 radial·3 down·4 up·5 left 더함(1 은 right 겸 찌르기), `_DstBlend` 로 섞기 고름, 같은 메뉴가 Art/VFX/AI 가져오기 설정(PPU 512/sizeMeters)과 더하기용 `Art/VFX/BrushStrokeAdd.mat` 를 만든다, `VfxBootstrap.KindOf(progress)`. fx_mist_ink(먹, right)·fx_lightning_glow(gold+core 더하기, down)를 Run 씬에 놓고 Progress 1·0.5 로 화면 확인: 그어지는 방향 맞음. 다만 밝은 종이 바닥에서 더하기 빛은 옅은 크림색으로 묻힌다 — 번개는 먹 테두리나 바닥을 어둡게 하는 층이 같이 있어야 보일 것(art_pipeline·vfx.md 에서 정할 일 → 2026-11-02 클라우드: 빛 재료에 `shade` 층(먹 받침)을 더함. `AI/fx_lightning_glow_shade.png`, ai.json layers 에 shade. 받침을 BrushStroke.mat(보통 섞기, 먹색)로 gold·core 보다 먼저 같은 Progress·Dissolve 로 그리면 됨. art_pipeline '적용'). 무공에는 아직 안 물림. 2026-11-02 00:46 PC: 받침 붙임 — `Presentation/Fx/AiFxLayers.cs`(AI 재료 한 장을 stroke·shade(보통 섞기, 먹) → gold → core(더하기) 순서로 겹쳐 그리고 Progress·Dissolve·Kind 를 층 모두에 같은 값으로, 셰이더가 _Color 를 쓰니 층 색은 블록으로). 종이 바닥에 번개 받침 없음/있음을 나란히 놓으니 받침 쪽만 금빛이 또렷하다 `docs/unity_shots/V-04/lightning_shade.png`(위 Progress 1, 아래 0.55 위에서부터 그어짐). 같은 메뉴를 다시 돌려 새 AI png 아홉 장 가져오기 설정(PPU)도 맞춤. 남은 것: 무공(뇌부 낙뢰 등)에 AiFxLayers 물리기, 담채(wash)·먹 튐 파티클·바닥 먹 자국 데칼, 획이 적 덩이 뒤에 깔리는 정렬, 웹 빌드 화면 한 장) (2026-11-03 03:16 PC 먹 튐·바닥 먹 자국 첫 판 — `Presentation/Fx/InkMarks.cs`: `EnemyField.Killed` 마다 Common/ink_splat_* 하나를 플레이어 반대쪽(±25°)으로 0.1초에 펴고 0.45초에 흩어지게, ground_ink_* 를 그 자리에 깔아 8초에 걸쳐 마르게(처음 진하다 끝에 빨리 옅어지고 뒤 절반은 _Dissolve). 덩이 렌더러 재질(ClusterInk, _Tint·_Dissolve)을 텍스처마다 복사해 RenderMeshInstanced, renderQueue 로 바닥 자국 2995(지도·소품 위, 적 아래)·먹 튐 3001. 크기는 적 반지름 × 3.2(튐)·3.0(자국), 프레임당 새것 12·자국 240·튐 300 상한, 덜 마른 자국 가까이엔 겹쳐 깔지 않음. 메뉴 `BloodRain/Bootstrap/Run 씬에 먹 자국 붙이기`(RunBootstrap 도 부름). 플레이로 처치마다 자국·튐이 쌓이는 것과 따로 깐 자국 여섯 `docs/unity_shots/V-04/ink_marks.png` 확인, 콘솔 에러 0(시험하며 timeScale 0 에서 화면을 찍으니 `Dash.Tick` 이 dt 범위 예외를 냈는데 시험 탓). 다만 자국이 밝은 종이 바닥에서 옅은 회색으로 보이고 먹 튐은 적 무리에 묻혀 잘 안 보인다 — 진하기·크기는 눈으로 더 맞출 것. 남은 것: 무공에 AiFxLayers, 담채, 정렬, 웹 빌드 화면, 휴대폰 간소화 설정에서 양 줄이기) (2026-11-03 20:50 PC 무공에 AI 재료 — WeaponRunner 에 `AiFx` 목록(VfxBootstrap 이 ai.json 36장 전부를 이름·sizeMeters·progress·층 텍스처로 채움)과 한 번 뜨는 그림 `Spark`(그어지기 → 머묾 → 뒤 45% 흩어짐, 층 순서 stroke·shade 먹 → gold·core 빛). 나한권 연타마다 fx_fist_ink(바라보는 쪽 사거리 절반, ±20° 흔들림), 창천검기 도는 칼날 끝에 fx_sword_qi_glow, 독무 틱마다 fx_mist_ink_3 을 몸에 붙여 청회 #4F7F7A 담채로 옅게 두 장 겹침, 비도·장풍·섬광지 투사체는 fx_dagger_ink·fx_palm_ink·fx_finger_glow 를 날아가는 쪽으로 돌려 그림(옛 원은 재료가 없을 때만), 벽력탄 터질 때 fx_bomb_ink_1·2 번갈아 + fx_bomb_glow. 정렬: 붓 획·AI 재료·옛 도형을 복사한 재질로 renderQueue 3002(먹)·3003(빛)에 그려 적 덩이(3000) 위로 올림. 플레이로 Lv5 일곱 무공 피해·그림 확인 `docs/unity_shots/V-04/weapon_ai_fx.png`·`weapon_ai_fx_2.png`, 콘솔 에러 0. 크기(비도 1.4m·섬광지 1.8m·장풍 반경×3.6)는 눈으로 더 맞출 것, 벽력탄 터짐·창천검기는 스크린샷에 못 잡혀 눈 확인 필요. 남은 것: 웹 빌드 화면, 휴대폰 간소화 설정에서 양 줄이기, 삼재검법 2·3단) (2026-11-03 22:47 PC 삼재검법 2·3단 — WeaponRunner `BrushTier` 가 마일스톤 수로 단을 셈(Lv4 앞뒤 베기부터 2단, Lv7 3연 베기부터 3단), 2단은 samjae_t2_* 본 획 + 담채(청회 #4F7F7A, 알파 0.55 에서 옅어지며 1.12배로 커짐, 흩어짐은 절반만), 3단은 samjae_t3_* 본 획 + 담채 + 금빛 비백(#C9A227, 보통 섞기 — 종이 바닥에서 더하기는 묻히므로). 층 순서 담채 → 본 획 → 금빛. 크기·펼침은 samjae.json(6.456m·2.4 / 7.1m·2.5), VfxBootstrap 같은 메뉴가 텍스처를 채움. 아주 느리게 돌려 1·2·3단 획을 나란히 띄운 화면 `docs/unity_shots/V-04/samjae_tiers.png`(왼쪽 1단, 위 2단, 오른쪽 3단 금빛 가장자리), 콘솔 에러 0. 담채가 밝은 바닥에서 옅게 보여 진하기는 눈으로 더 맞출 것. 남은 것: 웹 빌드 화면, 휴대폰 간소화 설정에서 양 줄이기) (2026-11-04 00:44 PC 효과 간소화 — `Presentation/Fx/FxSettings`(PlayerPrefs `fx_simplified`, 정한 적 없으면 휴대폰에서만 켬): 켜면 삼재검법 담채 층을 안 그리고 InkMarks 의 프레임당 새것·자국·튐 상한을 3분의 1(4·80·100)로. 에디터 메뉴 `BloodRain/Test/효과 간소화`(체크 표시). 플레이로 켠 채 자국 300·튐 400 을 깔아 80·100 에서 멈추는 것 확인, 콘솔 에러 0. 독무 안개 두 장 겹침은 그대로 — 휴대폰에서 무거우면 한 장으로. 설정 화면이 생기면 거기 붙일 것. 남은 것: 웹 빌드 화면)
- 원문 W-01 뇌부(`random_lightning`)를 WeaponRunner 에 붙인다(2026-11-02 클라우드가 숫자를 정함, 기획서 9장 '날아가는 것과 넉백' 뇌부 줄). `range`(9m) 안의 서로 다른 적 `WeaponGrowth.Projectiles`(2→5)+연무장 개수 곳을 고르고, 바닥에 표식을 띄운 뒤 `Def.FuseSeconds`(0.4초)에 반경 `Def.StrikeRadius`(1.2m) x 연무장 범위 안을 친다. 적이 없으면 쿨다운을 쓰지 않고 기다린다. 넉백 `Def.Knockback` 0. `HasMilestone(def, lv, "감전")`(Lv5)부터 맞은 적이 `Def.ShockSeconds`(0.5초) 멈춘다(보스 제외). 떨어지는 순간 AiFxLayers 번개(fx_lightning_glow, 먹 받침 포함, down)를 그 자리에 0.4초 안쪽으로 그어지게. 절기 오뢰정법(evolutions.json five_thunders params, 기획서 9장 '절기로 바뀐 뒤 날아가는 것')은 사거리 x1.5 안 서로 다른 적 `strikes`(5)+연무장 개수 곳 동시, 반경 x`radiusMultiplier`(2), 맞은 적마다 `lightningRange`(3m) 안 다음 적으로 `lightningChains`(2)번 `lightningFalloff`(0.6)배씩 옮는 번개(천뢰검진 낙뢰와 같은 갈래 그리기), 늘 감전. 레벨업 풀에 뇌부가 나오는지(곤륜 몸이면 처음부터) 확인 (진행: 2026-11-02 04:40 PC 뇌부 — WeaponRunner `random_lightning`: 9m 안 가까운 40 가운데 서로 다른 n 곳에 금빛 바닥 표식, 0.4초 뒤 반경 1.2m 피해·넉백 0, 적이 없으면 Fire 가 false 를 돌려 쿨다운을 쓰지 않음. 감전은 `EnemyAgent.Stun`(남은 초 동안 걷기·쏘기 멈춤, 넉백은 받음)을 새로 두고 Lv5부터 0.5초. 낙뢰 그림은 fx_lightning_glow 세 층(받침 BrushStroke → 금·흰 BrushStrokeAdd, _Kind 3 아래로)을 RenderMesh 로 0.08초 내리꽂고 0.36초에 흩어짐, VfxBootstrap 이 텍스처를 물림. 시험 키 Ctrl+9. 플레이로 Lv5 뇌부 피해·감전 걸린 적·낙뢰 그림 확인 `docs/unity_shots/W-01/strike.png`, 콘솔 에러 0. 남은 것: 절기 오뢰정법(WeaponRunner 에 절기 모양이 아직 없음 — 절기 실행 항목 C-09 와 같이), 연무장 개수·범위, 레벨업 풀 확인(뇌부 unlock 이 seal_talisman_10_total 이라 해금 기록이 붙어야 나옴), 보스가 무리에 들어오면 감전 거르기)
- 원문 C-09c 절기 셋 효과(E-02, 2026-11-01): 벽력천화는 마지막 연쇄 폭발 자리에 `fireSeconds`(5초) 불바다(반경 = 그 폭발 × `fireRadiusMultiplier`), 안에 든 적의 화상을 계속 다시 채움. 천뢰검진은 검기 원에 닿은 적마다 `lightningChance` 로 낙뢰(한 방 × `lightningDamage`), `lightningRange` 안 다음 적으로 `lightningChains` 번 `lightningFalloff` 배씩. 만천성지 폭발은 치명타일 때만(`explodeOnCrit`). 모두 `EvolutionDef.Param`, 웹 프로토타입 `fireField`·`lightning` 이 같은 식. 먼저: C-09
- 원문 L-01b 현지화 글꼴·Loc 컴파일(2026-11-04 클라우드): `Presentation/Loc.cs`(Resources/Data/strings 를 읽는 정적 클래스)가 컴파일되는지 보고, `Art/Fonts/NotoSerifSC-Regular.otf` 로 TMP 동적 SDF 에셋(`NotoSerifSC SDF.asset`, Dynamic, 아틀라스 2048)을 만들어 NotoSerifKR SDF 의 대체 글꼴 맨 뒤에 건다(간체 글자가 KR 에 없으면 SC 로). 아무 TMP 글자에 `血雨江湖 华山 剑气 Inner Energy` 를 넣어 두부(□) 없이 찍히는지 한 장 `docs/unity_shots/L-01b/`. 레벨업 화면(`Hud/LevelUpScreen`)·발견 연출(`Fx/DiscoveryFx`, `Hud/SecretWatch`)을 `Loc.T`·`Loc.F` 로 바꿔 둠(2026-11-04 클라우드 L-05): 컴파일 에러가 없는지, PlayerPrefs `lang` 을 en·zh-Hans 로 바꿔(코드나 콘솔에서 `Loc.Lang = "en"`) 레벨업 카드가 넘치지 않는지 언어마다 한 장. 영어 인장(문파 이름 전체)이 72px 칸에서 읽히는지도. 2026-11-04 클라우드 L-05 로 정산(`Meta/SettlementScreen`)·침방(`ChimbangScreen`·`ChimbangMap`)·장경각(`LibraryScreen`·`ManualNames`)·연무장(`TrainingScreen`·`MetaShop`)·지역 HUD(`Hud/RegionHud`)도 `Loc` 로 바꿈(`Loc.Sect`·`Loc.Region` 같은 데이터 이름 도우미를 Loc.cs 에 더함): 같이 컴파일되는지, 영어로 침방 카드 본문(280px 칸)·벽 지도 지명(160px)·장경각 '다음 복원' 줄·정산 오른쪽 줄이 넘치지 않는지 한 장씩
- 원문 L-08b 번체 글꼴(2026-11-04 클라우드 L-08): `Art/Fonts/NotoSerifTC-Regular.otf` 로 TMP 동적 SDF 에셋(`NotoSerifTC SDF.asset`, Dynamic, 아틀라스 2048)을 만든다. 대체 글꼴 순서는 언어마다 달라야 한다(간체·번체가 같은 코드포인트를 쓰는 글자가 많아 먼저 걸린 글꼴 모양으로 찍힘): `Loc.Lang` 이 zh-Hant 면 TC 를, zh-Hans 면 SC 를 KR 대체 목록 앞쪽에 두는 작은 스크립트(Presentation 쪽, 언어를 바꿀 때 `TMP_Settings.fallbackFontAssets` 또는 KR 에셋의 fallbackFontAssetTable 순서를 바꿈)가 필요하면 만든다. `Loc.Lang = "zh-Hant"` 로 레벨업 화면·침방 카드에 `血雨江湖 華山 劍氣 輪迴之氣 「一方之主」` 가 두부 없이 찍히는지 한 장 `docs/unity_shots/L-08b/`. 기기 언어가 번체면 zh-Hant 로 시작하는지(`Loc.FromSystem`)
- 원문 BGM-03a 배경음 가져오기 설정(2026-11-04 클라우드 배경음): `Assets/Editor` 에 AssetPostprocessor 하나 — `Assets/Audio/BGM/*.ogg` 는 Load Type Streaming, Compression Vorbis(품질 70%), Preload Audio Data 끔, Force To Mono 끔. 곡은 `tools/postprocess_audio.py` 가 넣고 목록은 같은 폴더 `bgm.json`(id·file·loop·seconds). 아직 곡이 없으니 컴파일·콘솔 에러 0 만 본다. 곡을 트는 쪽(지역에 따라 바꾸기)은 기획이 정해진 뒤
- 원문 RM-02b 혈정 잇기(2026-11-04 클라우드 M2 로드맵): 정예가 쓰러질 때 `Life.KillElite()`(이번에 얻은 혈정 수를 돌려줌)를 부르고, 정산 화면 `SettlementStats.BloodCrystal` 은 전투 쪽이 세지 말고 `LifeRecord.BloodCrystal` 을 쓴다. 혼 잔고는 `Soul.BloodCrystal`(저장 `bloodCrystal`). 혈월이면 정예 몫이 1·2개 번갈아 나온다
- 원문 V-10b 붓 획 자른 텍스처(2026-11-04 클라우드 V 굽기): `bake_brush.py` 가 Brush 114장을 획 있는 쪽만 잘라 구움(픽셀 34%, 너비·높이 4의 배수). json 에 `crop`·`pivot`. 바뀐 것: `BrushStroke.shader` 에 `_Rect`(기본 0,0,1,1 — AI·Common 은 그대로), `WeaponRunner.brushCrops`·`CropOf`·`CropMatrix`(사각형을 잘린 자리로 옮겨 줄임), `VfxBootstrap`(brushCrops 채움, 피벗 Custom = json pivot, Tight). .meta 의 피벗은 클라우드가 손으로 맞춰 둠. 할 일: 컴파일 확인 → 메뉴 `BloodRain/Bootstrap/붓 획 이펙트…` 다시 돌려 Run 씬 WeaponRunner 에 brushCrops 27줄이 들어가는지 → 삼재검법 1·2·3단이 전과 같은 자리·크기로 그어지는지 화면 한 장(`docs/unity_shots/V-04/samjae_tiers.png` 와 견줌). 메뉴를 안 돌리면 잘린 그림이 512 칸 크기로 늘어나 보인다
- 원문 DL-03b 대사 띄우기(2026-11-04 클라우드 대사): 문장·조건은 `Resources/Data/dialogue.json`(`DataBoot.Data.Dialogue`), 고르기는 Core `Meta/InnDialogue.cs`. (1) 객잔 귀환: `Soul.EndLife` 바로 뒤 `InnDialogue.OnReturn(data, soul, record, summary, progress, facts, rng)` → 목 노인 `Mok`(많아야 둘)·운낭 `Unnang`(하나)·대표 인물 `Leader` 를 차례로 말풍선, `Opens` 에 haomun_board 가 오면 하오문 게시판을 연다. `ReturnFacts` 는 그 생의 여자 몸·그림자·하오문 밀서·새 시설. `DialogueProgress` 는 `ToJson`/`FromJson` 으로 혼 저장 옆에 같이 둔다 (2) 마당: 삼류 첫걸음 때 `InnDialogue.YardLine(data, soul, body, progress, rng)` 를 대표 인물 머리 위 2초 (3) 서장: `dialogue.prologue.lines` 를 when(start, first_levelup, realm:iryu, boss_warning, awaken …, docs/story/prologue.md) 사건마다 한 번, offscreen 이면 화면 아래 가운데(2026-11-04 M1-03: 고르기는 Core `Run/PrologueCues` — 판 시작 `Start()`, 매 프레임 `Watch(life)`, `Life.Tick` 결과를 `OnSecond(second)`, 정예 처치 `OnEliteKilled(id)`. 돌려준 `PrologueCueItem` 을 `Delay` 초 뒤에 띄운다. `IsHint` 면 안내 문구 `prologue.hints`(오의를 써라·음양), 화면 가운데 아래에 크게), 장삼 처치 뒤 컷 두 장(`cuts`) 자막 (4) 땅의 주인: 2막 예고 때 `phase2Ko`, 다시 만날 때 `echoKo`, 첫 만남·처치 컷(`cuts`, alt 는 `stone:<지역>` 봉인석을 지니면) (5) 연무장 `InnDialogue.Training(data, progress, "open"…)`, 정산 자막은 `settlement.lines` 위에서부터 맞는 하나. 글자는 모두 Unity 에서 얹고 말하는 사람 이름은 `speakers`. 화면이 아직 없는 것(객잔 본채 등)은 그 화면이 생길 때
- 원문 DL-04b 침방 대사·카드 글 띄우기(2026-11-04 클라우드 대사 2): 침방을 열 때 `var talk = InnDialogue.Bedroom(DataBoot.Data, soul.OpenRebirth(rng), progress, rng)`. 순서대로 `talk.Open()` → `talk.Show()` → (다시 고르기 버튼마다 `talk.Reroll()`, draw.Reroll 을 따로 부르지 않는다) → 카드를 누르면 `talk.Pick(i)` → 보낼 때 `talk.Send()`, 다음 판은 `talk.Chosen` 몸으로. 각 호출이 돌려준 목 노인 줄을 차례로 말풍선에(현지화 키 `dialogue.bedroom.lines.<id>.text`). 카드 i 의 소개·몸 묘사는 `talk.Cards[i].Intro`/`Body`(키는 `IntroKey`/`BodyKey`), 다시 고르면 `talk.Cards` 가 바뀐다. `DialogueProgress` 는 DL-03b 와 같은 저장을 쓴다. 침방 화면 스크린샷 한 장(첫 환혼·여자 몸 카드) `docs/unity_shots/DL-04b/`
- 원문 DL-05b 정산 자막 띄우기(2026-11-04 클라우드 대사 2): 정산 화면을 열 때 `Soul.EndLife` 바로 뒤 `InnDialogue.Settlement(data, soul, record, summary, facts, rng)`(facts 는 DL-03b 의 `ReturnFacts` 에 `FallenToShadow`·`LifeSeconds` 를 채워서 같이 씀). 화면 맨 아래 붓글씨 한 줄. 한국어는 `caption.Text` 그대로, 다른 말은 키 `dialogue.settlement.lines.<id>.text`(혈월이면 `caption.BloodMoon` 이 참이고 `.bloodMoon`)에 lord_first 는 지역·땅의 주인 이름(`caption.Region`·`Lord`), any_3 은 문파 이름·버틴 시간(`caption.Sect`·`Seconds`)을 Loc.F 로
- 원문 DL-06b 땅의 주인 컷·한 줄 띄우기(2026-11-04 클라우드 대사 2): 보스전 경고 직후 `InnDialogue.LordMeet(data, soul, lifeLog, region, progress)` → `Cuts`(만남 2컷, 비면 건너뜀, 언제든 건너뛰기 가능) 다음 `BloodMoon` 줄, 또는 컷 없이 이름 붓글씨 아래 `Echo` 한 줄. 기혈 30% 2막 예고 때 머리 위에 `Phase2` 2초(시간 안 멈춤). 쓰러뜨린 바로 뒤 `lifeLog.KillBoss(region)` 결과로 `InnDialogue.LordKill(data, lifeLog, kill, progress)` → 처치 4컷(컷 이미지는 `cut.Art`, 자막은 `cut.Lines`, `cut.Alt` 면 현지화 키가 `….alt.lines.<n>.text`). 봉마곡 혈영 일곱은 `data.Dialogue.Lord(id).EchoKo`
- 원문 RM-03b 객잔 시설 잇기(2026-11-04 클라우드 M2 로드맵): Inn 씬이 `Core.Meta.Inn` 으로 시설 문을 연다 — `Inn.IsOpen(soul, id)`(main_hall·training·library·apothecary·smithy·cave·haomun_board), 약방·대장간은 `Inn.Buildable` 이면 '짓기(혈정 N)' 버튼 → `Inn.TryBuild`. 정산 뒤 `LifeSummary.NewFacilities` 가 있으면 객잔에서 한 줄 알림. 객잔 마당 NPC 는 `Inn.Residents(soul)`(대표 인물 id, 2026-11-05 QA-10b 로 `Inn.Residents(soul, progress)` 가 됨). M2 에서 장경각이 어떻게 보일지는 backlog A-07(사람 결정 대기) 뒤
- 원문 V-12c 새 붓 획 가져오기(2026-11-04 클라우드 V 굽기): Brush 에 무공 일곱(taiji·chain·claw·qin·spear·talisman·petals) × 3단과 절기 천지인(cheonjiin_main_1·2, cheonjiin_wave_1) 텍스처·json 이 새로 들어옴. `.meta` 는 bake_brush.py 가 VfxBootstrap 과 같은 설정(guid 는 이름에서)으로 만들어 둠. V-10b 와 함께 메뉴 `BloodRain/Bootstrap/붓 획 이펙트…` 를 돌려 가져오기 설정이 그대로인지(.meta 가 바뀌면 그대로 커밋), 콘솔 에러가 없는지만 본다. 무공에 붙이는 건 V-06
- 원문 R-04U 문파 고유 능력 잇기(2026-11-04 클라우드 M3 Core): Core `Build/SectTrait.cs`, 몸은 `life.Body.SectTrait`. (1) 능력치는 `Life.CurrentStats(extra, healthRatio: 지금 기혈/최대, moving: 이번 프레임 입력이 있었나)` — 남궁 공력·수로채 회피가 그때그때 바뀌니 기혈이 50% 를 넘나들 때와 멈추고 움직일 때 다시 부른다. (2) 피격 직전 `Evade.Roll(stats.Evade, rng)` 가 참이면 피해·넉백 없이 넘김(짧은 잔상 정도). (3) 독안개·독 장판·독 탄 피해에 `SectTrait.PoisonImmune` 이면 0(`PoisonFogGimmick.HealthLoss(…, poisonImmune)`). (4) 물 지형 걸음에 `WaterGimmick.MoveSpeedMultiplier(…, ignoresWater: SectTrait.IgnoresWater)`. (5) 무공 피해에 `SectTrait.DamageMultiplier(적이 중독됐나)`. (6) 은자 줍기 × (1 + Bonus[Greed]), 음식은 `Drops.FoodHeal(…, 1 + Bonus[FoodHeal])`. (7) 태극일 때 쾌속에 `Life.HarmonyCooldownReduction`(무당 0.2, 나머지 0.1)을 더하고 상한으로 자름. 오의 충전 2배는 `Life.Charge` 가 이미 셈함. (8) 곤륜: 적 걸음에 `SectTrait.EnemySpeedMultiplier(플레이어와 거리, 축척)`. (9) 마교: 기혈 회복 전부(회복·음식·흡혈·영약)에 `SectTrait.HealingMultiplier`. 화산 치명 +10% 는 (1)의 Bonus[CritChance] 로 옴. 먼저: Run 씬에 Life 가 붙어야 함(C-06 남은 것과 같음)
- 원문 ST-03U 스토어 스크린샷 다시 찍기(2026-11-04 클라우드 스토어 2): 장면 열 장의 시작 상태는 `Assets/Editor/StoreShots/store_shots.json`, 그 상태로 한 생을 세우는 건 Core `Run/StoreShotStage.Build(DataBoot.Data, shot, defs.Seed)`(`StoreShotDefs.FromJson` 으로 읽음). 할 일: Editor 메뉴 `BloodRain/Store/스크린샷 n번 세우기`(1~10) — Run 씬 플레이 중 그 `Life` 로 바꿔 끼우고, `Enemies` 수만큼(주된 적 `Enemy`, 없으면 지역 표대로) 화면 둘레에 채움, 보스 장면은 `Patterns`·`BossHealth` 를 이어받아 원각을 세움, 레벨업 장면은 `LevelUpOffers` 석 장으로 레벨업 화면을 띄움, 9번은 침방에 `Candidates` 석 장. 1920×1080 으로 장면마다 10장씩 `docs/unity_shots/ST-03/<no>_<id>/` 에 저장(사람이 고름). 피해 숫자가 너무 덮으면 그 장면만 설정으로 줄임. M2 전투 화면이 서기 전이면 되는 장면(9번 침방, 2번 강시 떼)부터
- 원문 R-07U 진전 소림 4·개방 4 띄우기(2026-11-04 클라우드 M3 Core 나): 한 생에 `new Core.Build.SchoolSummons()` 하나, 프레임마다 `summons.Tick(그때 SynergyEffects, 기혈/최대 기혈, 세상 dt)`. `ArhatStarted` 면 몸 둘레에 나한 허상 열여덟(`Tier("arhat_ring").Param("images")`)을 `ArhatSecondsLeft` 동안 그리고 닿는 적을 밀어냄(세기·둘레는 R-07b 결정 전이라 임시값 1.8m·8, 보스 제외). `AlliesSpawned` 만큼 개방 제자를 `AlliesSecondsLeft` 동안(피해·움직임은 R-07b 전 임시로 장풍 Lv1 1초마다). 레벨업 화면·HUD 진전 인장은 이미 4단계를 셈. 확인: 소림 넷 모아 기혈 50% 밑으로 → 허상, 판에 세 번까지 / 개방 넷 → 3분 뒤 셋
- 원문 R-08U 도전 과제 셋 세기 잇기(2026-11-04 클라우드 M3 Core 나): 적이 쓰러질 때마다 `life.Log.Kill()`(잡몹·정예·땅의 주인 모두, 정예·보스는 KillElite·DefeatBoss 와 별도로 한 번 더), 경공을 쓸 때 `life.Log.Dash()`, 정신부를 쓸 때 `life.Log.UseTalisman()`(비전용 `RunRecord.UseTalisman` 도 그대로 부름). 정산 화면은 `soul.EndLife(...)` 가 돌려준 `NewUnlocks`(kills_5000_one_run → 음공, dash_300_one_run → 이화창, seal_talisman_10_total → 뇌부, 토큰을 unlock 으로 가진 무공을 찾아 이름)를 '새로 얻은 무공' 줄로 띄운다
- 원문 QA-05U 화면 가장자리 땅의 주인 표시(2026-11-04 클라우드 QA-05): `Presentation/Fx/EdgeIndicators.cs` 51줄이 빈 `new BossRecord()` 로 BossAwakening 을 만든다. 쓰러뜨린 땅·봉인석 수를 몰라 혈영(다시 만난 땅의 주인)과 봉마곡 표시가 틀린다. 지금 생의 혼에서 `new BossRecord(soul.BossesKilled, soul.SealStoneCount)` 를 넘기거나, Life 가 가진 BossAwakening(`life.Bosses`)을 그대로 쓴다 — `EdgeIndicators.Bind(life.Bosses)` 를 만들어 둠(안 부르면 시험용 빈 기록). 혼 기록이 있는 저장으로 들어가 봉마곡 표시가 봉인석 일곱일 때만 뜨는지 확인
- 원문 QA-02U 의서 은빛 등롱(2026-11-04 클라우드 QA-02): 은빛 등롱을 놓을 때 `lifeLog.ManuscriptsHere` 가 true 일 때만(혈월 천하, cheonha.json `cycle.bloodmoon.manuscriptsOnlyHere`). 주우면 `lifeLog.FindManuscript()`(1회차면 false 라 세지 않음). 완료: 1회차 저장으로 들어가 등롱이 안 보이고, 회귀한 저장에서 보이면 끝
