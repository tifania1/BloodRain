# 효과음 대기열

`queue.jsonl` 한 줄 = 효과음 하나. 목록의 근거는 `docs/design/sfx_list.md`, 생산 줄은 `docs/design/sfx_research.md` 4절.

| 칸 | 뜻 |
|---|---|
| id | Unity 에서 부를 이름. 결과는 `Assets/Audio/SFX/<id>.ogg`(여러 벌이면 `<id>_1.ogg`…) |
| use | 어떤 사건에 울리는지 |
| category | 갈래. sfx_list.md 의 절과 같다: weapon·enemy·reward·scene·ambient (ui 는 UI 소리가 정해지면) |
| route | synth(코드 합성, 클라우드) · ai(Stable Audio 후보, PC) · layer(여러 재료를 겹침) |
| seconds | 뽑을 길이(초). AI 는 `tools/sfx_sa3.py --seconds` 에 넣는다 |
| max_s | (있으면) 다듬을 때 최대 길이. 없으면 `postprocess_sfx.py` 의 갈래 기본값 |
| loop | 이어 돌리는 소리(빗소리) |
| prompt | ai·layer 의 AI 몫 영어 문구. 끝은 'Single, dry, close-mic, no music, no reverb tail.'(루프는 빼고 'seamless loop') |
| recipe | synth·layer 에서 무엇을 어떻게 만들지(한국어) |
| layers | layer 의 재료: from ai·synth·record, what, offset_s(늦게 시작할 초)·gain_db(이득), record 는 file(재료 경로). ai 재료 후보는 `<save_dir>/ai/` 에 둔다. 겹치기는 `python tools/layer_sfx.py`(후보 `<id>_mix_NN.wav`, todo → generated) |
| candidates | 후보 수. ai·layer 8, synth 5(칠 때마다 조금씩 흔든 벌) |
| seed | 첫 후보의 시드. 다음 후보는 +1 |
| save_dir | 후보를 두는 곳 `art_inbox/sfx/<id>/`(커밋 안 함). 다듬은 후보는 그 아래 `trimmed/` |
| status | todo → generated(후보가 save_dir 에 있음) → picked(사람이 고름, `picked` 에 후보 번호) → imported, 버리면 rejected |
| picked | 고른 후보 번호(1부터, trimmed/ 이름의 번호와 같음) |

다듬기: `python tools/postprocess_sfx.py --candidates` (generated 줄 후보 → trimmed/), `python tools/postprocess_sfx.py` (picked → Assets/Audio/SFX + sfx.json).
테스트 `SfxQueueTests` 가 sfx_list.md 와 이 파일의 id 가 서로 빠짐없이 같은지 본다.
