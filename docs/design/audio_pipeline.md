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
