# 웹 판이 받는 양 (WEB-01, 2026-11-19)

사람이 dev.bloodrain.pages.dev 를 열었다가 "로딩이 안 돼"라고 했다(2026-11-19 09:10). 판은 멀쩡했고 받는 양이 컸다:
data 70MB(회사 회선 28초) + wasm 10MB(12초). 목표는 받는 양 절반.

## 클라우드에서 잰 것 (빌드 보고 없이 어림)

클라우드에는 Unity 가 없어 Build Report 를 못 본다. 대신 빌드에 들어가는 파일을 저장소에서 따라가 어림했다:
Build Settings 의 씬 셋(Boot·Run·Inn) + `Resources/` 전부에서 시작해 `.unity/.prefab/.asset/.mat…` 의 guid 참조를 끝까지 따라감 → 374개.
텍스처는 원본 PNG 가 아니라 Unity 가 싣는 크기로 셈(DXT5 1바이트/픽셀, 4의 배수가 아니면 RGBA32 4바이트/픽셀, 밉맵 ×4/3).

| 덩이 | 원본 크기 | 비고 |
|---|---|---|
| 글꼴 원본 넷(Noto Serif KR·SC·TC, Yuji Boku) | 35.7MB | TMP 동적 글꼴은 원본 파일을 통째로 싣는다. 압축(gzip 어림)해도 27.5MB |
| 소품 낱장 `Art/Props/Parts` 44장 | 어림 49.9MB | 44장 모두 가로·세로가 4의 배수가 아니라 RGBA32 로 실림(어림) |
| 이펙트 `Art/VFX` | 어림 26.8MB | DXT 로 실림. 다음 차례 후보 |
| NotoSerifKR SDF.asset 에 저장된 아틀라스 | 4.7MB | `ClearDynamicDataOnBuild` 라 빌드에서는 비워짐 |
| 배경음 | 0 | 아직 picked 곡이 없어 `Audio/BGM` 이 비어 있다(짐작과 달랐음) |
| 효과음 60개 | 0.55MB | 작다 |

`Art/Props/` 의 큰 시트(1300~1400px 열넷)와 캐릭터 시트(`Characters/Sheets`)는 어디서도 참조하지 않아 빌드에 안 들어간다.

## 한 것 (2026-11-19 19시 세션)

1. **글꼴을 쓰는 글자만 남김** — `tools/subset_fonts.py`. 원본은 `tools/fonts_src/`(빌드 밖)로 옮기고, `Assets/Art/Fonts/` 에는 같은 이름·같은 .meta 로 줄인 판을 둠.
   남기는 글자: Resources/Data JSON 전부 + Scripts .cs 에 나오는 글자 + 영문·문장부호 + (KR 만) KS X 1001 한글 2350자.
   35.8MB → 4.5MB. 데이터에 새 글자를 넣었는데 글꼴에 없으면 `FontSubsetTests`·`FontCoverageTests` 가 실패한다 → `pip install fonttools` 후 `python tools/subset_fonts.py`.
2. **소품 낱장을 4의 배수로** — `tools/pad_mult4.py` 로 44장에 투명 여백 1~3px. 기준점은 가운데라 반 픽셀 안에서 그대로.
   `split_props.py` 도 앞으로 4의 배수로 자른다. `ArtMetaTests.소품_낱장은_가로세로가_4의_배수다`.

어림으로는 data 가 30MB 넘게 준다. 실제 값은 PC 의 다음 웹 빌드에서 잰다(unity_tasks WEB-01U).

## 다음 후보 (재 본 뒤에)

- 원화 텍스처의 밉맵: 빌드에 드는 원화 다수가 밉맵을 켜 놨다(소품 낱장 44장 포함, ×4/3). 지도를 크게 줄여 볼 때 말고는 필요 없다.
- 이펙트 텍스처 최대 크기: VFX 가 DXT 로도 27MB 어림. WebGL 만 1024 로 낮출 수 있는지.
- wasm 10MB: Managed Stripping Level 이 비어 있다(기본값). WebGL 만 'Medium' 으로 올려 보기 — 리플렉션으로 읽는 데이터 클래스가 잘리지 않게 `Assets/link.xml` 이 Core 를 통째로 남긴다(2026-11-19 20시 넣음, 테스트 `LinkXmlTests`). 올리고 재는 일은 unity_tasks WEB-02U.
- TMP 기본 에셋(LiberationSans SDF·글꼴·EmojiOne 1.7MB)과 Unity 로고 2.7MB: `TextMesh Pro/Resources` 는 통째로 빌드에 들어간다. 2026-11-19 22시 Resources 밖으로 옮기고 기본 글꼴을 NotoSerifKR SDF 로(테스트 `TmpSettingsTests`). 로고 끄기와 확인은 unity_tasks WEB-04U.
- 소품 아틀라스 21.3MB(DXT5 는 brotli 로 잘 안 줄어듦): 2026-11-19 23시 WebGL 만 Crunch(DXT5Crunched·품질 50) 덮어쓰기(테스트 `AtlasWebGlTests`). 화질과 크기는 unity_tasks WEB-05U. Crunch 는 받은 뒤 CPU 가 풀어 로딩 끝에 조금 더 걸린다.
- 컷 그림 22장(1536x1024): 2의 거듭제곱이 아닌데 밉맵이 켜져 WebGL 에서 RGBA32 로 실렸을 것. 2026-11-20 00시 밉맵 끔(테스트 `ArtMetaTests.컷_그림은_밉맵을_끈다`, 확인 unity_tasks WEB-06U).
- 인물 낱장 21장(`Art/Characters`)도 2의 거듭제곱이 아니고 밉맵이 켜져 있다. 경지가 오르면 멀리 보니 밉맵은 남겨야 하고, 소품처럼 아틀라스로 묶는 게 길(PC 가 WEB-06U 에서 형식부터 본다).
- 배경음이 들어오면: WebGL 에서 Vorbis 낮은 품질 + 'Load In Background'/스트리밍.

## 빌드 보고 (2026-11-19 20:50 PC, fe369ea 릴리스 batchmode 2분 21초)

**합계 76.1 → 37.6MB**(data.br 70 → 27.2MB, wasm.br 10.4MB 그대로). 올린 조각은 21.1 + 7.6MB 두 개.

PC 에서 본 것:
- 줄인 글꼴: 동적 글꼴 넷 'Clear Dynamic Data' 함. 세 언어 데이터·문구 글자를 FontEngine 으로 글꼴마다 대 보니 빠진 글자 0. 에디터 Run 씬 한국어·간체·번체 레벨업 카드, 웹 판 타이틀·칸 고르기에서 네모 없음.
- 소품 낱장은 4의 배수로 맞췄는데도 **RGBA32 로 실렸다**. WebGL 은 밉맵을 켠 텍스처가 2의 거듭제곱이 아니면 DXT 로 못 누른다(시험: 768x640·744x640 + 밉맵 → RGBA32, 512x256·1024² → DXT5, 740x640 밉맵 끔 → DXT5).
  밉맵을 끄면 경지가 오를 때 소나무·초가가 하얗게 자글거려서(`docs/unity_shots/WEB-01U/` 의 `props_nomip_realm1.png` 와 `props_mip_realm1.png`) 끄지 않고 **스프라이트 아틀라스 한 장**(`Art/Props/PropParts.spriteatlasv2`, 4096 · 밉맵 · DXT5, 메뉴 `BloodRain/Bootstrap/소품 아틀라스 짓기(WebGL 압축)`, Sprite Packer V2)으로 묶었다. PropLayer 는 `textureRect` 로 UV 를 잡아 그대로 맞음(`web01_props_atlas_realm0.png`).
  `tools/pad_mult4.py` 는 이제 꼭 필요하진 않지만 해가 없어 둔다.
- 올린 뒤 웹 판이 'Invalid binary data file size' 로 멈췄다: `split_webgl_data.py` 의 `?v=` 가 wasm 시각이었는데 코드가 안 바뀐 빌드라 wasm 이 다시 안 쓰여 표가 그대로 → 브라우저가 옛 조각을 캐시에서 씀. Build 폴더에서 가장 늦은 파일 시각으로 바꿈.

큰 것 스무 개(압축 전):

| 크기 | 에셋 |
|---|---|
| 21.3MB | Art/Props/PropParts.spriteatlasv2 (소품 46장) |
| 6.0MB | Art/Map/map_central_plains.jpg — Run `MapLayer.map`(usePaintedMap 꺼져 안 그림)·Inn 침방 벽 지도 `ChimbangScreen.wallMap` 이 들고 있었다. 2026-11-19 21시 둘 다 비우고 침방도 `Biomes.MakeMap` 천하도를 걸게 고침(확인 unity_tasks WEB-03U) |
| 2.7MB | 내장 Splash Screen Unity Logo |
| 1.5MB | Art/Fonts/YujiBoku-Regular.ttf |
| 1.4MB | Art/Fonts/NotoSerifKR-Regular.otf |
| 1.3MB | Art/Cutscenes/sb_prologue_needle.jpg |
| 1.3MB | Art/Cutscenes/sb_prologue_burnout.jpg |
| 1.0MB | TextMesh Pro LiberationSans SDF.asset |
| 1.0MB | Art/Fonts/YujiBoku SDF.asset |
| 768.6KB | Art/Cards/card_sect_shaolin.jpg |
| 768.6KB | Art/Backgrounds/bg_chimbang.jpg |
| 768.6KB | Art/Backgrounds/bg_title.jpg |
| 753.6KB | Art/Fonts/NotoSerifSC-Regular.otf |
| 659.6KB | Art/Fonts/NotoSerifTC-Regular.otf |
| 525.8KB | Resources/unity_builtin_extra |
| 344.5KB | TextMesh Pro LiberationSans.ttf |
| 341.5KB | TextMesh Pro EmojiOne.png |
| 270.4KB | DamageNumbersPro Saw-Glow.asset |
| 262.6KB | URP SMAA AreaTex.tga |
| 256.1KB × 여럿 | Art/VFX/AI 의 512² 빛·먹 재료(DXT5) |

분류: 텍스처 70.3MB(89.8%), 소리 0.96MB, 셰이더 0.95MB, 그 밖 5.8MB. 다음 후보: 옛 지도 6MB 빼기(WEB-03U), Splash 로고(유료 판이 아니면 못 뺌), 아틀라스 최대 크기를 WebGL 만 2048(화질 보고), TMP 기본 글꼴·EmojiOne.

## 빌드 보고 (2026-11-19 23:00 PC, b5d4966 + Medium·로고 끔, 릴리스 batchmode 11분 21초)

**합계 37.6 → 30.6MB**(data.br 27.2 → 22.4MB, wasm.br 10.4 → 9.1MB). data 가 Cloudflare 25MiB 상한 안이라 조각을 안 나눈다.

- WEB-02U: WebGL 만 Managed Stripping Medium. wasm.br 10,884,188 → 9,591,706 바이트(-1.2MB). `link.xml` 로 Core 를 남겨 둔 채, 로컬 GPU 크롬 서장·레벨업·천하도까지 콘솔 오류 0. 처음 Medium 빌드는 IL2CPP 를 새로 돌려 11분.
- WEB-03U: `map_central_plains.jpg`(6MB) 빠짐.
- WEB-04U: Splash 로고(2.7MB)·LiberationSans SDF(1.0MB)·LiberationSans.ttf·EmojiOne 빠짐.
- 남은 큰 것: 소품 아틀라스 21.3MB(압축 전), YujiBoku 원본 1.5MB·SDF 1.0MB, NotoSerifKR 1.4MB, 서장 컷 두 장 1.3MB씩, DNP Saw-Glow 270KB(`usePro` 를 안 쓰면 뺄 수 있음). 분류: 텍스처 60.3MB(88.9%), 소리 0.96MB, 셰이더 0.94MB, 그 밖 5.4MB.

## 빌드 보고 (2026-11-20 00:45 PC, 0045f31 + 소품 Crunch·컷 밉맵 끔, 릴리스 batchmode 1분 20초)

**합계 30.6 → 27.4MB**(data.br 22.4 → 19.1MB, 22,404,261 → 19,054,888 바이트, wasm.br 9.1MB 그대로).

- WEB-05U: 소품 아틀라스(4096² 밉맵) WebGL 만 DXT5Crunched 품질 50 — 21.3 → 2.1MB(압축 전). 청운진 이류에서 소나무·초가 테두리 뭉개짐·색띠 못 느낌(`docs/unity_shots/WEB-05U/` 의 `web05_props_crunch_realm1.png`), 품질 50 그대로 둠. dev 판 GPU 크롬 타이틀까지 오류 0.
- WEB-06U: 컷 그림은 가져올 때 2048x1024 로 늘려져 전에도 DXT1 이었다(RGBA32 아님). 밉맵 몫만 빠져 한 장 1.3 → 1.0MB. 서장 바늘 컷 흐림 없음.
- 인물 낱장 18장은 가져올 때 256² 라 DXT5 + 밉맵으로 잘 눌림 — 아틀라스로 묶을 까닭 없음.
- 남은 큰 것(압축 전): 소품 아틀라스 2.1MB, YujiBoku 원본 1.5MB·SDF 1.0MB, NotoSerifKR 1.4MB, 서장 컷 두 장 1.0MB씩, 카드·배경 jpg 셋 768KB씩, Art/VFX/AI 512² 재료 256KB씩 여럿. 분류: 텍스처 40.4MB(84.2%), 소리 0.96MB, 셰이더 0.94MB, 그 밖 5.4MB.

## 빌드 보고 (2026-11-21 01:10 PC, 대화 세션 — 27.7 → 31.2MB)
- 늘어난 것: 침방 산문 그림이 한 장 → 아홉 장(CB-01U), 땅의 주인 원화 여섯 장(EF-02U, 512² DXT5 + 밉맵 341KB씩), 기연 소품 다섯(아틀라스 안).
- 산문 그림은 WebGL 512 로 눌렀는데 1024x1536 → **341x512 라 4의 배수가 아니어서 RGB24(512KB씩)** 로 실렸다(덮어쓰기 전 2048 이면 1024x1536 DXT1 768KB). 원본을 680x1024 로 줄여 WebGL 512 에서 340x512 DXT1(87KB)이 되게 함 — 다음 빌드에서 확인. 줄일 크기를 정할 때는 '최대 크기로 줄인 뒤에도 4의 배수'인지 볼 것.

## 빌드 보고 (2026-11-21 02:20 PC, 대화 세션 — 28.5 → 31.1MB, v=1795195069)
- 늘어난 것: 객잔 본채·연무장 배경 원화 두 장이 처음 씬에 물림(IN-01U, 1536x1024 DXT1 768KB씩). data.br 21.3MB, wasm.br 9.8MB.
- 두 장은 메뉴 화면 뒤에 어둡게 깔리는 그림이라 WebGL 만 DXT1Crunched 품질 60 으로 바꿈(원본 크기 그대로 — 1024 로 줄이면 1024x683 이 4의 배수가 아니라 RGB24 로 실린다). 다음 빌드에서 확인.
- 02:48 빌드에서 두 장을 crunch 하니 data.br 21.3 → 20.5MB.

## 컷 그림 비율(2026-11-21 03:45 대화 세션)
- 컷 그림(`Art/Cutscenes/sb_*.jpg`, 모두 1536x1024)이 가져올 때 '2의 거듭제곱으로 늘림'(nPOTScale 1)이라 2048x1024 로 늘어나, 그림 비율을 텍스처 크기로 재는 컷 화면(서장 끝 `CutScreen`, 인트로 `IntroScreen`)이 가로로 1.33배 늘린 그림을 띄웠다. 스물두 장 모두 nPOTScale 0(그대로)으로 — 1536x1024 는 4의 배수라 웹 DXT 도 그대로 된다. 인트로 열두 장은 웹만 DXT1Crunched 55(03:36 빌드에서 data.br 20.5 → 24.1MB, 늘림을 끈 다음 빌드에서 다시 잼).
- 03:51 빌드(v=1795200829): 늘림을 끄니 data.br 24.1 → 23.0MB, 합계 32.8MB(인트로 열두 장이 3MB 안팎). 헤드리스 크롬 타이틀까지 오류 0.
- 04:21 빌드(v=1795202644) 32.8MB. 05:14 빌드(v=1795205291): data.br 23.0 → 23.5MB, 합계 33.2MB — 지역 바닥 원화 셋(512 타일)·화산 몸 그림 둘(256)이 더해짐. 헤드리스 크롬 타이틀까지 오류 0.
- 06:07 빌드(v=1795208449): data.br 23.5 → 23.5MB, 합계 33.2MB — 바닥 잔무늬·HUD 원상은 코드로 그려 크기가 거의 그대로. 헤드리스 크롬 타이틀까지 오류 0.
- 06:51 빌드(v=1795211087) 33.2MB. 오류 0. PC 브라우저 창에서 새 혼 → 서장 → 천하도까지 확인(IMGUI 글자 고침이 웹에서도 먹음).
