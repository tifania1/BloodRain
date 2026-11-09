# 크레딧 고지 (TT-02)

크레딧 화면(기획서 12장 화면 목록 '크레딧')에 무엇을 적는지, 왜 그렇게 적는지 모아 둔 문서다. 데이터는 `unity/BloodRain/Assets/Resources/Data/credits.json`, 화면에 흘릴 줄은 Core `CreditsDef.Roll`, 확인은 테스트 `CreditsTests`. 약관은 2026-11-09 대화 세션 보조(TT-02)가 원문으로 읽었다. 못 연 곳은 맨 아래 '출처'에 적었다.

목록은 '쓰기로 한 것'이 아니라 **지금 게임 빌드에 실제로 들어가는 것**에서 뽑는다. 글꼴 파일·패키지·소리 갈래가 새로 들어오면 `CreditsTests` 가 credits.json 에 그 줄이 없다고 알려 준다.

## 지금 들어 있는 것

| 갈래 | 무엇 | 라이선스 | 크레딧에 꼭 적어야 하나 | credits.json |
|---|---|---|---|---|
| 글꼴 | Noto Serif KR·SC·TC (`Art/Fonts`) | SIL OFL 1.1 | 예. 글꼴을 같이 내보내는 사본마다 저작권 줄과 라이선스 원문을 둬야 한다(따로 된 글 파일, 사람이 읽는 머리말, 쉽게 볼 수 있는 메타데이터 중 하나면 됨, OFL 2조) | 저작권 줄은 글꼴 파일 이름표 0번 그대로 `© 2017-2024 Adobe (http://www.adobe.com/).`, 원문 한 벌 |
| 글꼴 | Yuji Boku (`Art/Fonts`) | SIL OFL 1.1 | 예 | `Copyright 2021 The Yuji Project Authors (https://github.com/Kinutafontfactory/Yuji)` |
| 글꼴 | Liberation Sans (`TextMesh Pro/Fonts`, TMP 기본 글꼴) | SIL OFL 1.1 | 예. `TextMesh Pro/Resources` 의 기본 글꼴 에셋이 이 파일을 쓰고, Resources 는 통째로 빌드에 들어간다 | OFL 파일 머리의 저작권 두 줄(Google 2010, Red Hat 2012, 예약 이름 포함) |
| 엔진 | Unity | Unity 이용약관 | 아니오. Personal 판은 시작할 때 'Made with Unity' 화면이 뜨는 것으로 끝난다(추정). 관례로 이름만 적는다 | 이름만, 라이선스 없음 |
| 패키지 | URP, Input System, Burst, Collections, Mathematics, Timeline, uGUI(TextMesh Pro 포함), AI Navigation | Unity Companion License | 아니오 | 적지 않음. Collections·Input System·Mathematics·Timeline·AI Navigation 은 받은 패키지의 LICENSE.md 로 확인, URP·uGUI 는 엔진에 딸려 받아 보지 못함(같은 라이선스로 추정) |
| 패키지 | `com.unity.nuget.newtonsoft-json` 3.2.2 — Newtonsoft.Json, Json.Net.Unity3D | 묶음 자체는 Unity Companion License, 안에 든 두 라이브러리는 MIT | 예. MIT 는 '모든 사본에 저작권 고지와 허가 고지'. Core 가 런타임에 쓴다 | `Copyright (c) 2007 James Newton-King`, `Copyright (c) 2016 SaladLab`, MIT 원문 한 벌 |
| 패키지 | `com.coplaydev.unity-mcp` (MCP for Unity) | MIT | 예. 에디터 도구지만 `MCPForUnity.Runtime` 어셈블리(플랫폼 제한 없음)가 플레이어 빌드에 같이 들어간다 | `Copyright (c) 2025 CoplayDev`. 나중에 빌드에서 빼면 이 줄도 지운다 |
| 효과음 | 다섯 개 전부 코드 합성(`Assets/Audio/SFX/sfx.json` 의 route 가 모두 synth) | 우리가 만든 것 | 아니오 | 없음 |
| 배경음 | 아직 `Assets` 에 없음(audio_queue 에서 뽑아만 둠) | — | — | 없음 |

## 소리와 음악 — 들어올 때 넣을 줄

`CreditsTests.게임에_든_소리_갈래마다_고지를_다시_본다` 가 막으면 여기서 고른다. 넣을 때는 credits.json `sections` 맨 끝에 `"id": "sound"` 묶음을 만들고 strings 세 언어에 키를 더한다(번체는 `tools/make_hant.py`). 글귀는 상점 페이지 AI 공개 문구(decisions ST-01a)가 정해지면 그쪽에 맞춘다.

### Stable Audio 3 Small SFX (route `ai`, covers `sfx:ai`)
- 라이선스: Stability AI Community License(최종 수정 2024-07-05). Stable Audio 3 모델 카드가 이 라이선스(`stable-audio-community`)를 가리킨다.
- **'Powered by Stability AI' 표기는 결과물 소리만 넣은 게임에는 필요 없다(약관 해석).** 표기 조항 IV.a 는 "Stability AI Materials 나 Derivative Work 를, 또는 그것을 쓰는 제품·서비스를 남에게 내놓을 때" 걸린다. V 장 정의에서 Derivative Work 는 "do not include the output of any Model" 이고, 게임은 모델을 싣지도 돌리지도 않는다.
- 그래도 지킬 것: 모델을 상업 목적으로 쓰면 Stability 에 등록해야 한다(III, stability.ai/community-license) — **사람이 할 일**. 개인·계열 합산 연매출이 USD 100만을 넘으면 라이선스가 끝난다(개인 명의로 내기로 함, SFX-05). 결과물로 생성 모델을 학습시키면 안 된다(IV.b).
- 상표: IV.c(i) 가 "IV(a) 가 요구할 때 말고는 Stability AI 이름·표장을 쓰지 말라"고 한다. 표기 의무가 없으니 크레딧에 'Powered by Stability AI'나 로고는 넣지 않는다. 모델 이름도 빼고 사실만 적는다.
- 글자 인코더 T5Gemma 는 Gemma 이용약관을 따른다. 결과물을 내놓는 데 걸리는 조항은 찾지 못했다(원문은 이번에 안 읽음, 확인 필요).
- 준비한 줄: `{"id": "sfx_ai", "name": "", "useKo": "효과음 일부는 생성형 AI 로 만든 소리를 다듬어 썼다", "copyright": "", "license": "", "covers": ["sfx:ai"]}` — 이름 칸이 비어 있어 화면에는 쓰임 줄만 나온다. 영어 "Some sound effects were made with generative AI and then edited", 간체 "部分音效由生成式 AI 制作后再加工".

### 녹음 재료 겹쳐 굽기 (route `layer`, covers `sfx:layer`)
- Sonniss GDC Game Audio Bundle: 상업 게임에 고쳐 써도 되고 출처 표기 의무가 없다(`docs/design/sfx_research.md` 3절, 묶음마다 든 License.pdf). 이번에는 sonniss.com 이 브라우저 확인 화면으로 막혀 원문을 다시 열지 못했다.
- Sony Pictures Sound Effects Series, SoundFX 아카이브, source 피아노: 사람이 산 라이브러리(`docs/design/sfx_sources.md`). **표기 조건은 산 라이선스 문서를 사람이 한 번 본다(확인 필요).** 보통은 표기 없이 작품 안에 섞어 넣는 것까지 되고 원본을 그대로 내놓는 것만 안 된다.
- DayDreamSound: 안내문에 "출처를 표기하진 않아도 되지만, 무단 재배포를 금지합니다".
- 준비한 줄(의무는 없고 고마움 표시): `{"id": "sfx_layer", "name": "Sonniss GDC Game Audio Bundle", "useKo": "효과음 녹음 재료", "copyright": "", "license": "", "covers": ["sfx:layer"]}`. 산 라이브러리 문서에 표기 조건이 있으면 그 이름도 여기에.

### Gemini 배경음 (covers `bgm:gemini`, `Assets/Audio/BGM` 에 ogg 가 생기면)
- Google 서비스 약관(시행 2026-07-30): "Some of our services allow you to generate original content. Google won't claim ownership over that content." 표기를 요구하는 조항은 없다.
- 생성형 AI 금지 정책(최종 수정 2024-12-17): "Misrepresenting the provenance of generated content by claiming it was created solely by a human, in order to deceive" 금지. 그러니 크레딧에 '작곡: 사람 이름'만 적고 AI 곡임을 숨기면 안 된다.
- **판매용 빌드에 넣어도 되는지는 그대로 '확인 필요'** — decisions BGM-02a(스토어를 열기 전 사람이 Gemini 앱 약관 원문 확인). 이번에 읽은 두 문서에도 상업 사용을 직접 허락하는 문장은 없다.
- 준비한 줄: `{"id": "bgm_gemini", "name": "Google Gemini", "useKo": "배경음은 생성형 AI 로 만든 곡을 골라 다듬었다", "copyright": "", "license": "", "covers": ["bgm:gemini"]}`. 영어 "Music was generated with AI, then selected and edited", 간체 "背景音乐由生成式 AI 制作，经挑选和加工".

## 그림
ChatGPT 웹으로 뽑은 그림이 게임에 많이 든다. 이번 범위(TT-02)에 없었고 OpenAI 약관 페이지가 브라우저 확인 화면으로 막혀 읽지 못했다. 표기 요구가 있는지는 확인 필요(알려진 바로는 없음, 추정).

## 만든 사람
`makers[0].name` 은 사람이 정할 때까지 빈 값이고 화면은 그 묶음을 건너뛴다(2026-11-09 사람). 이름을 한글로 넣으면 StringCatalog 가 옮길 글로 잡아서 영어·중국어 표기를 strings 에 따로 넣어야 테스트가 통과한다(로마자, 한자 이름은 사람에게 묻는다 — 용어집 5번).

## 출처
- Stability AI Community License 원문(Stable Audio 3 Small SFX 미러에 든 사본): https://huggingface.co/cocktailpeanut/stable-audio-3-small-sfx-base/raw/main/LICENSE.md
- 같은 라이선스 2024 판 사본: https://open.windriver.com/info/uni-license-list/licenses/stability-ai-community-2024.html
- Stable Audio 3 모델 카드(라이선스 이름·Gemma 구성요소): https://huggingface.co/stabilityai/stable-audio-3-medium , https://huggingface.co/Comfy-Org/stable-audio-3
- Google 서비스 약관: https://policies.google.com/terms , 생성형 AI 금지 정책: https://policies.google.com/terms/generative-ai/use-policy , 2024-05-22 부터 안 쓰는 옛 생성형 AI 추가 약관: https://policies.google.com/terms/generative-ai
- Unity Newtonsoft 패키지(안의 `Third Party Notices.md`): https://download.packages.unity.com/com.unity.nuget.newtonsoft-json/-/com.unity.nuget.newtonsoft-json-3.2.2.tgz
- MCP for Unity(LICENSE, `MCPForUnity/Runtime` 의 asmdef): https://github.com/CoplayDev/unity-mcp
- Unity Companion License: https://unity.com/legal/licenses/unity-companion-license
- SIL Open Font License: https://openfontlicense.org/
- 못 연 곳(권한 요청이 시간 안에 답을 못 받았거나 브라우저 확인 화면): https://stability.ai/community-license-agreement , https://stability.ai/license , https://sonniss.com/gdc-bundle-license/ , https://openai.com/policies/row-terms-of-use/
