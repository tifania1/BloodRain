# 효과음 직접 만들기 — 방법 비교와 생산 줄 제안 (2026-11-05 조사)

2026-11-05 09:01 사람: "효과음도 좀 싸구려 느낌이니 만들어서 써야겠어." 그래서 BGM-08(효과음 출처)을 '유료 묶음을 산다'에서 '직접 만든다'로 바꿨다. 이 문서는 그때 대화 세션이 조사한 것이다. 할 일은 backlog SFX-01~, 사람이 정할 것은 `decisions.md`(docs 에 있음) SFX-05.

대상: `docs/design/sfx_list.md` 의 M2 효과음 26개(+ 기획서에 없는 8갈래). 출처로 확인하지 못한 것은 **(추정)** 으로 적었다.

## 한 줄 결론

종·징·풍경·방울처럼 음이 있는 소리는 코드로 합성하고(클라우드에서 끝남), 휘두름·타격·벼락·독안개·비명 같은 소리는 AI로 후보를 많이 뽑은 뒤 CC0 녹음 재료를 겹쳐 다듬는다. AI는 **PC 로컬 Stable Audio 3 Small SFX(무료)** 와 **ElevenLabs API(유료, 월 $6부터)** 를 하룻밤 같은 문구로 뽑아 사람이 들어 보고 하나를 고른다. 두 길 다 API로 돌아서 브라우저 자동화가 필요 없다.

## 1. 지금 소리가 싸게 들리는 이유

`template.html` 의 `sfx()` 는 소리마다 발진기 하나에 음높이를 지수로 내리고 음량을 지수로 줄이는 것뿐이다. 잡음 성분도 없고, 층도 없고, 매번 똑같다. sfxr 류가 싸게 들리는 이유도 같다. 잘 만든 효과음은 **어택(첫 타격)·몸통(무게·음색)·꼬리** 를 따로 만들어 같은 순간에 맞춰 겹치고, 수백 번 들어도 지겹지 않게 여러 벌을 만들어 엔진이 섞어 튼다([Violet Recording](https://violetrecording.com/how-to-design-weapon-sounds/)). 코드 합성도 이 원칙을 따르면 쓸 만해진다. 쇠붙이 소리는 정수배가 아닌 배음 여러 개를 각자 다른 길이로 울리고, 가까운 배음 두 개가 맥놀이를 만들게 하면 된다. 한국 범종을 측정한 배음 13개와 맥놀이 쌍으로 재현한 예가 있다([Beepscript 범종](https://beepscript.pages.dev/case-study-temple-bell.html), [Karjalainen 외 DAFx 2002](https://dafx.de/paper-archive/details/g31AEhtQ6ww5tzEZDddvkg)). 소리를 데이터가 아니라 '과정'으로 보는 절차 음향의 교과서는 Farnell 의 *Designing Sound*([MIT Press](https://mitpressbookstore.mit.edu/book/9780262014410)).

## 2. 비교표

| 방법 | 짧은 타격음 품질 | 판매용 게임 라이선스 | 비용 | 자동화 | 받는 꼴 | 한 벌 맞추기 | 걸리는 점 |
|---|---|---|---|---|---|---|---|
| **ElevenLabs SFX v3** | 좋음(추정). v3 는 여러 갈래 문구·목소리 섞인 소리에 강하다고 함 | 유료 요금제만. 게임 사용 명시 | Starter $6/월 3만 크레딧, 길이 지정 시 초당 40크레딧 | API `POST /v1/sound-generation` | API: MP3(Starter), PCM 은 Pro 이상. 반복 소리는 MP3만 | 같은 문구·`prompt_influence` 로 어느 정도. 시드 없음(추정) | 낱개 파일·효과음 라이브러리로 배포 금지 |
| **Stable Audio 3 Small SFX (PC 로컬)** | 미확인. 직접 들어 봐야 함 | Community License: 연매출 $1M 미만 조직은 상업 사용 가능, 등록 필요 | 무료(전기·GPU) | ComfyUI(v0.22+) 또는 파이썬 라이브러리 | WAV 등 원하는 대로(추정) | 시드 고정으로 재현 가능 | 회사 이름으로 내면 매출 기준이 걸림. HF 로그인·약관 동의 필요 |
| stableaudio.com / Stability API | 위와 비슷하거나 낫다(추정) | 같은 Community License | 웹 $12/월~, 무료 50크레딧/월(2차 출처) | 웹 또는 API | — | — | 로컬이 되면 굳이 필요 없음 |
| **Unity AI Sound Generator** | 미확인. 모델 이름 비공개 | "AI 결과물을 쓴 게임을 팔아도 된다". 단 베타, "평가·시험용" | 소리 하나 약 26크레딧. Personal 월 1,000크레딧(약 38개), 14일 체험 뒤 유료 구독 값은 미확인 | 에디터 안. 스크립트 호출은 미확인 | WAV, Assets 폴더 | 참조 소리로 변형 생성 가능 | 베타, 크레딧 값이 예고 없이 바뀔 수 있음 |
| Adobe Firefly 효과음 | 미확인 | 문자→효과음, 목소리→효과음이 IP 면책 목록에 있음(2025-09-30부터, 해당 요금제) | Adobe 요금제 크레딧. 개당 값 미확인 | 브라우저(API 미확인) | 미확인 | 목소리로 박자 맞추기 가능 | 우리 계정 없음(추정). 자동화 어려움 |
| **Artlist** (이미 구독 중) | 라이브러리 품질 좋음 | **게임은 Enterprise 요금제 필요**. Artlist 가 만든 AI 결과물도 같은 '자산'으로 묶임 | 별도 계약 | MCP·웹 | — | — | 지금 요금제로는 게임에 못 씀 |
| Meta AudioGen, MMAudio | 연구용 | **CC BY-NC 4.0** (상업 불가) | — | 로컬 | — | — | 판매 게임에 못 씀 |
| fal.ai 경유 (PC unityMCP `generate_audio`) | 모델에 따름 | fal 을 거칠 때 원래 모델 조건이 어떻게 넘어오는지 미확인 | ElevenLabs v3 초당 $0.002, SA3 SFX 개당 $0.0283 | unityMCP 에 이미 도구가 있음(fal 키 필요) | 모델에 따름 | — | 라이선스 사슬이 흐림 |
| **코드 합성** (numpy) | 종·징·풍경·나무 딸깍은 좋음. 비명·빗소리는 약함(추정) | 우리 것 | 무료 | 클라우드에서 끝남 | 원하는 대로 | 매우 좋음(같은 식, 시드) | 사람 귀로 다듬는 시간이 듦 |
| **Sonniss GDC 묶음** | 전문 녹음 | 개인·상업 사용·수정 가능, 출처 표기 없음. 원본 그대로 판매 금지 | 무료 | 처음 한 번 사람이 받음 | WAV | 재료로만 씀 | AI 학습 사용 금지 |
| Freesound CC0 | 들쭉날쭉 | CC0 만 쓴다. CC BY 는 표기, BY-NC·Sampling+ 는 뺀다 | 무료 | API(미리듣기는 토큰, 원본은 OAuth2, 하루 2,000회) | 원본/미리듣기 | 재료로만 씀 | 파일마다 라이선스 확인 |
| Kenney Interface Sounds | UI 용으로 충분 | CC0 | 무료 | 한 번 받음 | 100개 | — | 흔한 소리라 무협 맛이 없음 |

## 3. 길마다 꼭 알아야 할 조항

- **ElevenLabs.** 효과음 약관 Exhibit 4 §2: 결과물을 "games, applications" 에 넣어도 되고, 상업 사용은 "where your plan permits commercial use" 일 때만. 결과물은 "combined with other material" 이어야 하고 "isolated audio files or samples" 로 내놓거나 효과음 라이브러리에 넣으면 안 된다(2026-10-09 갱신). 무료 요금제는 상업 라이선스가 없고, 유료 기간에 만든 것은 "indefinitely" 쓸 수 있다. 베타 기능 결과물은 상업 사용 불가. 길이 0.5~30초, `loop` 옵션, 모델 `eleven_text_to_sound_v3`(v2 는 2027-03-01 제거). 공유(Community Sharing)는 꺼 둔다. 사운드트랙과 함께 효과음을 따로 팔거나, 모드용으로 원본 ogg 를 풀어 두는 것은 걸릴 수 있다(추정 해석).
- **Stability AI.** 연매출 USD 1,000,000 을 넘으면 라이선스가 끝나고 Enterprise 가 필요하다. 상업 목적이면 Stability 에 등록해야 하고, 결과물은 우리 소유다. 결과물로 생성 모델을 학습시키면 안 된다. 모델이나 모델을 쓰는 제품을 배포할 때는 "Powered by Stability AI" 를 보여야 하는데, 결과물 소리만 넣는 게임에도 걸리는지는 확인 못 함(추정: 안 걸림). 매출은 "regardless of the source" 라서 **회사(벡스인텔리전스) 이름으로 내면 회사 전체 매출로 따진다.** Small SFX 는 4억 5,900만 파라미터라 RTX 급 GPU 면 넉넉하다(추정). 글자 인코더 T5Gemma 에는 Gemma 이용약관도 붙는다.
- **Unity AI.** 판매는 허락하지만 "development aid rather than a fully production-ready service" 로 쓰라고 하고, 결과물에 Unity AI 메타데이터를 붙인다. 책임은 개발자에게 있다.
- **Artlist.** 고객센터: "If you are creating a game, then you need an Enterprise plan." Pro 라이선스는 앱·소프트웨어·게임을 맞춤 Enterprise 계약 쪽에 둔다. Artlist AI 도구에 효과음 생성이 있는지는 확인 못 함.
- **Sonniss.** "use and modify the licensed sound effects for personal and commercial projects … without attribution", "may not sell any of the sound effects as they come". 2015~2024 묶음을 한꺼번에 풀었고 AI·ML 학습 사용은 금지(Zeli 보도). Sonniss 소리를 AI 참조 입력으로 넣는 것도 피한다(추정).
- **Steam.** 2026-01 개정된 설문은 게임과 함께 나가서 플레이어가 듣는 AI 결과물(소리 포함)을 적게 한다. 기획서 16장에 이미 있는 항목이다.

## 4. 추천 생산 줄

### 소리 갈래별로 길을 나눈다

| 길 | id |
|---|---|
| 코드 합성(클라우드) | `sfx_xp_absorb`(풍경, 5음 음계로 8음을 미리 굽는다), `sfx_enemy_gangsi_land` 의 방울 층, `sfx_levelup`·`sfx_boss_appear`(징), `sfx_gimmick_bell`·`sfx_awaken_wave` 의 범종 층(맥놀이), `sfx_low_health_heart`, UI 딸깍(목탁·나무) |
| AI 후보 + 층 쌓기 | 무공 9개(검·검기·권·장풍·봉·비도·섬광지·벼락·독무), `sfx_enemy_gangsi_land` 의 쿵, `sfx_enemy_hyeolin`, `sfx_chest_open`, 각성 흡기·균열·낙관, `sfx_breakthrough_brush`, `sfx_amb_blood_rain`(반복) |
| 녹음 재료(Sonniss·CC0) | 위 소리의 어택 층(쇠 울림, 나무 깨짐, 큰북 등), 붓·종이·도장. 붓 긋기·도장 찍기는 사람이 휴대폰으로 직접 녹음해도 좋다(선택) |

### 순서

1. **큐.** 배경음처럼 `sfx_queue/queue.jsonl` 한 줄에 효과음 하나(id, 길: synth/ai/layer, 문구(영어), 길이, 후보 수, 시드, 겹칠 재료, status todo→generated→picked→imported). → 2026-11-05 만듦(SFX-01, 칸 설명 `sfx_queue/README.md`).
2. **코드 합성(클라우드).** `tools/` 에 numpy 합성기(배음 표·맥놀이 쌍·배음별 감쇠·친 세기별 밝기). `bake_brush.py` 처럼 시드를 받아 같은 결과를 굽고, 칠 때마다 배음을 조금씩 흔든 변형을 4~5벌 굽는다. → 2026-11-05 `tools/synth_sfx.py`(SFX-02).
3. **AI 후보(PC, 밤 API 루프나 이미지 세션 뒤).** id 마다 후보 8~12개. Stable Audio 는 ComfyUI HTTP API 에 시드만 바꿔 넣고(추정: 워크플로 JSON 그대로), ElevenLabs 는 `duration_seconds` 를 정하고 v3 로 부른다. 키는 PC API 루프의 config.json 처럼 커밋하지 않는 곳에 둔다. 문구에는 "single, dry, close-mic, no music, no reverb tail" 같은 말을 붙인다(추정: 꼬리·잡음이 줄어듦).
4. **기계 다듬기.** `postprocess_audio.py` 를 본떠 효과음용을 만든다. 앞 무음을 잘라 첫 타격이 5ms 안에 오게(반응감), 최대 길이 자르기와 끝 페이드, 30Hz 아래 컷, 대부분 모노(탑다운 2D, 추정), 최고점 -1 dBTP, Vorbis 48kHz(→ 2026-11-05 `tools/postprocess_sfx.py`, SFX-03: 최고점 -1 dBTP 로 맞추고 갈래 이득은 사람 몫으로 0 에서 시작). 0.4초보다 짧은 소리는 통합 LUFS 가 잘 안 잡혀서 갈래별 최고점·단기 소리 크기로 맞춘다(추정).
5. **기계 검수.** 길이 범위, 잘림(클리핑), 앞 무음, 같은 id 후보 중 길이·밝기(스펙트럼 중심)가 튀는 것을 떨어뜨려 사람에게 4개만 보인다(추정 방법).
6. **사람 귀.** 후보를 듣고 `docs/feedback.md` 에 "sfx_wpn_sword 3·5·7 고름" 식으로 적는다. 같은 화면에서 이름을 가리고 들을 수 있는 작은 청취 페이지를 웹 프로토타입 미리보기에 붙이면 빠르다. → 2026-11-05 `web/lookdev/sfx_listen.html`(SFX-04).
7. **층 쌓기.** 고른 AI 몸통에 Sonniss·CC0 어택과 꼬리를 겹치는 조리법(JSON: 파일, 시작 오프셋, 이득, 음높이, 필터)을 스크립트로 구워 언제든 다시 만들 수 있게 한다.
8. **Unity.** id 마다 3~5벌을 Audio Random Container(Unity 6 기본, Shuffle 또는 Avoid Repeating Last, 음높이·음량 무작위 폭)에 넣고 기존 '0.05초 안 4개' 제한과 같이 쓴다. 진기 흡수 음계는 실행 중 음높이를 올리는 대신 미리 구운 8음을 차례로 튼다(음색이 안 뭉개짐, 추정).
9. **섞어 듣기.** 배경음 -18 LUFS 에 맞춰 갈래별 이득 표를 사람이 게임 안에서 정한다.

### 비용 어림

M2 26개 × 후보 12개 × 평균 1.5초 × 40크레딧 ≈ 18,700크레딧, 빗소리 30초 × 4개 = 4,800크레딧. ElevenLabs Starter 한 달(첫 달 $1, 이후 $6)로 된다. 다만 API 로 받으면 MP3 128kbps 라 무손실이 필요하면 Pro($99)다. Stable Audio 로컬은 0원.

## 5. 사람이 해야 하는 일

- 귀: 후보 고르기, 게임 안에서 소리 크기 맞추기. 기계가 대신 못 한다.
- 결제·계정: ElevenLabs 구독과 API 키 발급, Hugging Face 로그인 후 Stable Audio 3 약관 동의, Stability 상업 사용 등록, (쓰면) fal 키.
- 받기: Sonniss 묶음(수십 GB)은 처음 한 번 사람이 받아 PC 에 둔다.
- Steam 설문의 AI 항목 답.

## 6. 사람에게 물을 것

1. ~~BGM-08 을 '직접 만든다'로 바꿀지~~ → 2026-11-05 09:01 사람이 '직접 만든다'로 정함.
2. 게임을 누구 이름으로 내는지(개인, 회사, 따로 세운 곳). Stability 매출 기준이 여기에 달렸다.
3. 밤 세션이 도는 PC 의 GPU. ComfyUI 를 그 PC 에 깔아도 되는지.
4. ElevenLabs 를 시험해 볼지(첫 달 $1). 무손실이 필요하면 Pro 까지 갈지.
5. 시험 순서: 대표 5개(검, 권격, 벼락, 독안개, 혈인 비명)를 두 AI 로 같은 문구로 뽑아 이름을 가리고 들어 보는 하룻밤 비교부터 할지.
6. 붓·도장·종이 소리를 직접 녹음할 의향이 있는지.

## 5. PC 에 깐 것 (2026-11-05 밤, SFX-07)

| 무엇 | 어디 |
|---|---|
| ComfyUI 0.44.0 (git clone) | `E:\Unity\BloodRain-sfx\ComfyUI` |
| 파이썬 3.12 venv(uv), torch 2.11.0+cu128, CUDA 됨(RTX 3080 Ti 12GB) | `E:\Unity\BloodRain-sfx\venv` |
| Stable Audio 3 Small SFX 2.1GB | `ComfyUI\models\checkpoints\stable_audio_3_small_sfx.safetensors` |
| 글 인코더 T5Gemma 1.1GB | `ComfyUI\models\text_encoders\t5gemma_b_b_ul2.safetensors` |

- 모델 파일은 Comfy-Org/stable-audio-3(잠금 없음)에서 받았다. 공식 저장소 stabilityai/stable-audio-3-small-sfx 약관은 사람이 같은 날 동의했다.
- 저장소 밖 폴더라 커밋되지 않는다. ComfyUI 는 쓸 때만 띄우고 끝나면 끈다(회사 PC GPU).
- 부르는 도구는 `tools/sfx_sa3.py`(그래프·설정·주의점은 파일 머리에). 시험: 대표 다섯(검 휘두름·권격·벼락·독안개·혈인 비명)을 4개씩, 묶음마다 2~6초. 결과는 PC `E:\Unity\BloodRain\art_inbox\sfx\sa3_try\`(커밋 안 됨).
- 시험에서 본 것: 원하는 길이의 두 배로 나온다(빈 잠재 노드가 SA1 비율로 셈) → 도구가 절반을 넣는다. 벼락은 최고점이 0dB 에 닿고 독안개는 -18dB 안팎으로 작다 → SFX-03 다듬기 도구가 갈래별로 맞춘다. 비명·독안개는 앞에 0.2~0.45초 빈 곳이 있다 → 앞 무음 자르기 필요.

## 출처

- ElevenLabs 효과음 기능: https://elevenlabs.io/docs/overview/capabilities/sound-effects
- ElevenLabs API: https://elevenlabs.io/docs/api-reference/text-to-sound-effects/convert
- ElevenLabs 효과음 약관(Exhibit 4): https://elevenlabs.io/sound-effects-terms
- ElevenLabs 상업 사용 FAQ: https://elevenlabs.io/docs/help-center/legal/can-i-publish-the-content-i-generate-on-the-platform/llms.txt
- ElevenLabs 요금: https://www.elevenlabs.io/pricing
- fal ElevenLabs v3: https://fal.ai/models/elevenlabs/sound-effects/v3/llms.txt
- Stability 라이선스: https://stability.ai/license
- Stability Community License 원문 사본: https://open.windriver.com/info/uni-license-list/licenses/stability-ai-community-2024.html
- Stable Audio 3 발표: https://stability.ai/news-updates/meet-stable-audio-3-the-model-family-built-for-artistic-experimentation-with-open-weight-models
- Stable Audio 비교 설명: https://stability.ai/explainers/stable-audio-vs-competitors-licensing-export-rights-and-self-hosting-compared
- The Decoder(SA3 Small SFX 459M): https://the-decoder.com/stability-ai-launches-stable-audio-3-0-with-up-to-six-minute-tracks-and-open-weights/
- ComfyUI SA3 지원: https://blog.comfy.org/p/stable-audio-3-day-0-support?open=false , https://huggingface.co/Comfy-Org/stable-audio-3
- SA3 로컬 설치 안내: https://aivideosensei.com/guides/stable-audio-3-local-setup
- fal SA3 Small SFX: https://fal.ai/models/fal-ai/stable-audio-3/small/sfx/base/text-to-audio/llms.txt
- Stable Audio 요금(Stability KB): https://kb.stability.ai/knowledge-base/stable-audio-subscription-plans-and-credit-system , 무료 요금제(2차): https://www.usagepricing.com/blueprint/activity/stability-ai-2026-10-23-stable-audio-free-plan
- Unity 상업 판매 FAQ: https://support.unity.com/hc/en-us/articles/50616781946388-Can-I-commercially-sell-a-game-created-using-Unity-AI
- Unity 크레딧: https://docs.unity.com/en-us/ai/credits/credits-about , 요금: https://unity.com/products
- Unity Sound Generator: https://docs.unity3d.com/Packages/com.unity.ai.assistant@2.1/manual/Sound/sound-overview.html
- Unity Audio Random Container: https://docs.unity.com/en-us/engine/6000.3/manual/audio/random-container/ui
- Adobe Firefly 제품 설명(면책 표): https://helpx.adobe.com/nz/legal/product-descriptions/adobe-firefly.html , 출시 기사: https://thenextweb.com/news/adobe-firefly-audio-music-speech-sound-effects
- Artlist 게임 라이선스: https://artlist.freshdesk.com/support/solutions/articles/43000659339-what-license-do-i-need-to-use-artlist-assets-in-my-game- , 라이선스: https://help-center.artlist.io/privacy-terms/artlist-license/ , 영상 밖 사용: https://artlist.freshdesk.com/support/solutions/articles/43000669720-does-my-license-cover-audio-outside-of-a-video-production-
- AudioGen 모델 카드: https://huggingface.co/spaces/Nymbo/MusicGen/raw/main/model_cards/AUDIOGEN_MODEL_CARD.md
- MMAudio 가중치: https://huggingface.co/AEmotionStudio/mmaudio-models/blob/main/README.md
- Sonniss 라이선스: https://sonniss.com/gdc-bundle-license/ , 2026 묶음: https://bedroomproducersblog.com/2026/03/16/sonniss-gdc-2026-bundle/ , 10년치 공개: https://zeli.app/en/story/49262841
- Freesound API: https://freesound.org/docs/api/overview.html , 라이선스 종류: https://en.wikipedia.org/wiki/Freesound
- Kenney Interface Sounds: https://kenney.nl/assets/interface-sounds
- Steam AI 공개 개정(2026-01-16): https://www.pcgamer.com/software/ai/steam-updates-ai-disclosure-form-to-specify-that-its-focused-on-ai-generated-content-that-is-consumed-by-players-not-efficiency-tools-used-behind-the-scenes/
- 층 쌓기: https://violetrecording.com/how-to-design-weapon-sounds/
- 범종 합성: https://beepscript.pages.dev/case-study-temple-bell.html , https://dafx.de/paper-archive/details/g31AEhtQ6ww5tzEZDddvkg
- Farnell, *Designing Sound*: https://mitpressbookstore.mit.edu/book/9780262014410

열지 못한 곳(권한 요청이 시간 안에 답을 못 받음): https://artlist.io/license , https://gdc.sonniss.com/ . 그 내용은 위 고객센터·라이선스 페이지와 보도로 대신했다.
