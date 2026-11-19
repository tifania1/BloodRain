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
- 배경음이 들어오면: WebGL 에서 Vorbis 낮은 품질 + 'Load In Background'/스트리밍.
