# 배경음 생산 줄

2026-11-04 사람이 정함: "배경음은 제미나이를 통해서 생성하도록 하자."
곡 목록과 분위기는 기획서 14장, 곡마다의 문구와 진행 상태는 `audio_queue/queue.jsonl`. 이 문서는 예약 세션과 사람이 그대로 따라 하는 순서다.

## 큐

`audio_queue/queue.jsonl` 한 줄이 곡 하나다. 테스트는 `AudioQueueTests`.

| 값 | 뜻 |
|---|---|
| `id` | `bgm_…`(배경음) 또는 `sting_…`(몇 초짜리 짧은 곡). 지역은 `bgm_region_<cheonha.json 지역 id>`, 땅의 주인은 `bgm_boss_<enemies.json 보스 id>`, 종장은 `bgm_final_<단계>` |
| `use` | 쓰는 곳(화면·지역·싸움) |
| `milestone` | `M2`·`M3`·`later`. 기획서 17장 범위. **M2 줄부터 뽑는다** |
| `length` | `short`(Gemini 'Fast', 약 30초) · `full`(Pro·Thinking, 최대 3분) |
| `loop` | 이어 붙여 도는 곡인지. true 면 문구에 `loopable` 을 넣고 다듬을 때 이음매를 크로스페이드한다 |
| `cut_s` | (있으면) 받은 곡의 앞 몇 초만 쓴다. 짧은 곡에만 |
| `chat` | 같이 뽑을 묶음. 같은 묶음은 Gemini 같은 대화에서 뽑아 소리 결을 맞춘다 |
| `prompt` | `{STYLE}`(`audio_queue/STYLE.txt`) + `Track: …`. 영어로만 |
| `save_as` | PC 에 받을 곳 `art_inbox/audio/<id>.mp3` |
| `status` | `todo` → `generated`(받음) → `picked`(사람이 듣고 고름) → `imported`(ogg 로 `Assets/Audio/BGM/` 에 넣음). 떨어지면 `rejected` + `reason` |

- 문구에 실제 작곡가·가수·곡 이름을 쓰지 않는다. 악기·빠르기(bpm)·분위기만 쓴다.
- 가사는 넣지 않는다. 합창이 필요하면 'wordless … drone' 처럼 말 없는 저음으로 쓴다.
- 같은 곡을 다시 뽑을 때는 문구를 고치고 `note` 에 무엇을 고쳤는지 덧붙인다.

## 다듬기 `tools/postprocess_audio.py`

`picked` 줄의 MP3 를 읽어 ogg 로 `unity/BloodRain/Assets/Audio/BGM/<id>.ogg` 에 넣고, 목록 `bgm.json` 을 고친 뒤 `imported` 로 바꾼다. 파이썬 표준 라이브러리와 ffmpeg(libvorbis 포함)만 쓴다.

    python tools/postprocess_audio.py [--dry-run] [--only <id>] [--inbox E:/Unity/BloodRain/art_inbox]

| 단계 | 하는 일 | 값 |
|---|---|---|
| 무음 자르기 | 앞뒤로 문턱 아래만 이어지는 곳을 자르고 조금 남김 | -50 dBFS, 50ms |
| 자르기 | `cut_s` 가 있으면 앞에서 그만큼만, 끝 페이드아웃 | 0.5초 |
| 루프 이음매 | `loop` 곡은 끝 꼬리를 앞머리에 등가 출력 크로스페이드로 겹치고 꼬리는 버림. 마지막 샘플 다음이 원곡에서 바로 이어지던 샘플이 된다. 루프가 아닌 곡은 끝 50ms 페이드아웃 | 2초(곡 길이 1/4 이 상한) |
| 소리 크기 | 곡 전체에 같은 이득. 통합 소리 크기를 목표로 맞추되, 올릴 때는 실제 최고점이 천장을 넘지 않는 데까지만 | -18 LUFS, -1.5 dBTP |
| 저장 | Vorbis 48kHz 스테레오 | 품질 6 |

- `bgm.json` 은 도구가 쓴다(곡마다 file·use·loop·seconds·sourceLufs·gainDb). 손으로 고치지 않는다. `AudioQueueTests` 가 imported 줄·bgm.json·ogg 파일이 서로 맞는지 본다.
- 테스트: `python -m unittest discover -s tools/tests`(CI 와 pre-push 훅이 돌림). ffmpeg 가 없으면 MP3 → ogg 한 바퀴는 건너뛴다.
- 3분 곡 하나에 약 10초 걸린다.
- 소리 크기 값은 효과음과 섞어 들어 보고 바꿀 수 있다. 바꾸면 이미 넣은 곡도 `picked` 로 되돌려 다시 돌린다.

## 이용 조건 (BGM-02, 2026-11-04 조사)

이 클라우드 세션은 구글 도메인(support.google.com·policies.google.com·blog.google·gemini.google)을 직접 열 수 없어서, 웹 검색 결과 요약으로만 확인했다. 원문은 사람이 회사 PC 브라우저로 한 번 열어 보고 아래 '확인할 것'을 채운다.

| 확인한 것 | 내용 | 출처 |
|---|---|---|
| 길이·모델 | 'Fast' 모델은 Lyria 3 로 약 30초, 'Thinking'·'Pro' 는 Lyria 3 Pro 로 최대 3분 | [Gemini 음악 생성 소개](https://gemini.google/gb/overview/music-generation/), [9to5Google 2026-03-25](https://9to5google.com/2026/03/25/gemini-lyria-3-pro/) |
| 한도 | Lyria 3 Pro: Google AI Plus 하루 10곡, Pro 20곡, Ultra 50곡. 만 18세 이상, 한국어 지원 | 위 두 곳, [MusicRadar](https://www.musicradar.com/music-tech/a-fun-unique-way-to-express-yourself-google-adds-ai-music-creation-app-lyria-3-to-its-gemini-assistant) |
| 받는 꼴 | MP3·MP4 로 받음. 모든 곡에 SynthID 워터마크(귀에 안 들리는 표식) | [Gemini 음악 생성 소개](https://gemini.google/gb/overview/music-generation/) |
| 소유 | 개발자용 Gemini API 추가 약관은 "만든 결과물의 소유권을 Google 이 주장하지 않는다". 같은 결과가 남에게도 나올 수 있음 | [Gemini API Additional Terms](https://ai.google.dev/gemini-api/terms) |
| 앱 약관 | 소비자용 Gemini 앱 약관에서 음악의 상업적 사용을 직접 허락하는 문장은 검색으로 찾지 못함. Google 소개 글은 '개인 표현용'으로 소개하고, 생성형 AI 금지 정책·서비스 약관을 지키라고만 함 | [MusicRadar](https://www.musicradar.com/music-tech/a-fun-unique-way-to-express-yourself-google-adds-ai-music-creation-app-lyria-3-to-its-gemini-assistant) |
| 2차 출처 | "유료 구독(Plus·Pro·Ultra)은 개인·상업 용도로 쓸 수 있다"는 글이 있으나 Google 원문 인용이 없음. Google API 약관 5(e)(받은 콘텐츠의 판매·배포 제한)가 걸리는지 묻는 개발자 포럼 글엔 공식 답이 없음 | [terms.law](https://terms.law/ai-output-rights/gemini/), [Google AI 개발자 포럼](https://discuss.ai.google.dev/t/commercial-use-and-youtube-publishing-rights-for-lyria-3-5-music-generated-via-paid-gemini-api/189157) |
| 저작권 | 사람 손이 거의 없는 AI 결과물은 (미국 기준) 저작권 보호를 받기 어렵다. 남이 같은 곡을 써도 막기 어렵고, 사운드트랙 별도 판매(기획서 16장)도 이 점을 안고 간다 | [terms.law](https://terms.law/ai-output-rights/gemini/) |
| Steam | AI 로 만든 음악은 스토어 등록 때 콘텐츠 설문에 적는다(기획서 16장에 이미 있음) | 기획서 16장 |

**정리.** 개발 중 시안·임시곡으로 쓰는 데는 걸릴 게 없다. 판매용 빌드에 그대로 넣어도 되는지는 앱 약관 원문을 못 봐서 **애매하다** → 아래 '사람 결정 대기' 1.

## 사람 결정 대기

1. **Gemini 곡을 판매용 빌드에 넣을지.** 추천: M2 까지는 Gemini 곡을 시안·임시곡으로 쓰며 방향을 고르고, 스토어 페이지를 열기 전(M2 끝)에 사람이 Gemini 앱 약관(policies.google.com/terms, Gemini 앱 개인정보·약관 도움말)을 직접 읽어 상업적 사용 문장을 확인한다. 확인이 안 되면 고른 곡을 레퍼런스로 작곡가에게 맡기거나, 상업 사용을 분명히 허락하는 도구로 다시 만든다. 사운드트랙 별도 판매는 그 뒤에 정한다.
2. **'10분 이후 편곡 한 겹 추가'(옛 14장).** Gemini 는 악기별 파일을 주지 않아 층을 쌓을 수 없다. 추천: 지역마다 '기본'과 '격해진' 두 벌을 같은 대화에서 뽑아(같은 빠르기·조) 10분에 4초 크로스페이드로 바꾼다. 먼저 숭산 하나로 시험해 귀로 들어 보고 정한다. 정해지기 전에는 한 벌만 뽑는다.
