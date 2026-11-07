# 아트·이펙트 생산 줄

2026-11-01 사람이 정함: "아트·이펙트를 자동으로 생산·검수·적용. 무협에 걸맞게 화려하게, 셰이더와 아트를 함께 써서 최대한 예쁘게."
이 문서는 예약 세션이 그대로 따라 하는 순서다. 이펙트 모양 규칙은 `vfx.md`, 그림체·색은 기획서 13장.

## 세 갈래

| 무엇 | 어떻게 | 누가, 언제 | 비용 |
|---|---|---|---|
| 붓 획·먹 튐·바닥 먹 자국·낙관 바탕·노이즈 | 코드로 굽기 `tools/bake_brush.py` | 클라우드 개발 세션(backlog V 항목) | 없음 |
| 코드로 잘 안 나오는 재료: 안개·연기 덩이, 번개 가지, 불꽃 결, 꽃잎, 검기 빛, 진법 문양(글자 없이), 폭발 먹 | ChatGPT 웹 이미지(회사 PC 크롬의 Claude 확장) → `tools/import_vfx_ai.py` | 사람이 있을 때, 또는 이미지 세션 | Plus 구독 안 |
| 캐릭터·적·보스·배경·카드 원화 | ChatGPT 웹 이미지 → `tools/postprocess_art.py` | 위와 같음 | 위와 같음 |
| 움직임과 화려함 | 셰이더(V-04 붓 획 셰이더 등)·파티클·트윈이 위 텍스처를 움직인다 | PC Unity 세션(`unity_tasks.md`) | 없음 |

**예쁘게 만드는 원칙.** 텍스처 한 장은 '붓으로 그린 한 순간'이다. 화려함은 셰이더가 만든다. 그어짐(_Progress), 흩어짐(_Dissolve), 일렁임(노이즈로 UV 흔들기), 겹쳐 그리기(먹 위에 금빛, 담채 번짐), 더하기 빛을 겹친다. 그래서 AI 재료도 굽기 텍스처와 같은 꼴(층마다 흰색 + 알파 512px, 색은 셰이더)로 바꿔 같은 셰이더로 그린다.

## 큐 상태

`art_queue/queue.jsonl` 한 줄이 이미지 한 장이다.

`todo` → `generated`(PC `art_inbox/` 에 받음) → (이펙트 재료) `imported` / (원화) `picked` → `imported`

- 검수에서 떨어지면 `rejected` + `reason` + `tries` 가 붙는다.
- `tries` 가 3 미만이면 프롬프트를 고쳐 `todo` 로 되돌린다. 고친 내용은 `note` 에 덧붙인다.
- 3번 떨어지면 그대로 두고 daily 의 '아침에 확인할 것'에 올린다.

## 문구

| 종류 | 앞머리 | 바탕 | 대화방(`chat`) |
|---|---|---|---|
| 원화 | `{STYLE}` (`art_queue/STYLE.txt`) | 단색 #F2EBDD 또는 장면 | 계열마다 하나 |
| `vfx_ink` | `{VFX_INK}` (`VFX_INK.txt`) | 완전한 흰색. 어두운 만큼 덮임 | 이펙트 재료(먹) |
| `vfx_glow` | `{VFX_GLOW}` (`VFX_GLOW.txt`) | 완전한 검은색. 밝은 만큼 덮임, 하얄수록 흰빛 | 이펙트 재료(빛) |

- 문구 뒤에는 `Element: …` 로 무엇을 그릴지 한두 문장만 덧붙인다.
- 주홍은 적·위험 표시에만 쓰니 플레이어 무공 재료에는 넣지 않는다.
- 이펙트 재료 줄에는 아래 값을 함께 적는다.
  - `progress`: 그어지는 방향. radial · down · up · right · left
  - `sizeM`: 게임 속 긴 변(m)
  - `use`: 어디에 쓰는지
  - 하늘에서 내리치는 번개처럼 끝이 화면에 닿아야 맞는 재료는 `edgeOk: true`

## 이미지 세션이 하는 일 (회사 PC 크롬, 예약 '혈우강호 이미지 생산' 밤 21:47·23:47·01:47)

1. **작업 폴더와 잠금.** 사람이 Unity 를 쓰는 `E:\Unity\BloodRain` 은 건드리지 않고, 따로 둔 작업 폴더 `E:\Unity\BloodRain-art`(`git worktree`, origin/claude/dev 에 detach)에서 한다.
   - `docs/session_lock.md` 는 잡지 않는다. 개발 세션을 막지 않으려는 것이다.
   - 이미지 세션은 큐·`Assets/Art`·`docs/vfx_preview`·daily 만 건드린다. `art_inbox` 는 PC 원래 폴더 것을 쓴다.
   - 올리기 전에 `git fetch` + `git rebase origin/claude/dev`.
   - `queue.jsonl` 이 충돌하면 origin 쪽을 받고, 이번 세션이 바꾼 줄의 status 만 다시 고친다.
   - **한 번만(backlog PR-02, 2026-11-08):** 그리기 전에 `python tools/postprocess_art.py --redo-bg`(art_inbox 원본으로 스프라이트·초상·소품 배경을 다시 지움) → `python tools/split_props.py` → 초상 하나·소품 낱장 하나를 어두운 바탕에 얹어 얼굴·만두 속이 안 비치는지 본다. 원본이 없다고 나온 그림만 `--redo-bg --from-assets --only <id>`. 끝나면 backlog PR-02 를 닫고 이 줄을 지운다.
2. **연결 확인.** 크롬 확장이 연결돼 있는지(`list_connected_browsers`) 보고, chatgpt.com 에 로그인돼 있는지 확인한다. 아래 중 하나라도 해당하면 바로 멈추고 daily 에 한 줄 남긴다.
   - 확장이 연결돼 있지 않다.
   - 로그인 화면이 뜬다.
   - 사람 확인(CAPTCHA)이 뜬다. 우회하지 않는다.
   - "이미지 생성 한도" 같은 안내가 나온다.
3. **새 대화방.** 큐의 `chat_url` 이 있으면 그 대화방에서 뽑는다. 없는 계열은 세션이 ChatGPT '무협 서바이벌' 프로젝트 안에 새 대화방을 만들어도 된다(2026-11-04 사람이 입력창 문제를 풀어 둠).
   - 조건: 프로젝트 화면의 '새 채팅' 입력창이 비어 있을 때만 쓴다. 글이 한 글자라도 있으면 사람 것이니 건드리지 말고, 그 계열은 건너뛰고 daily 에 한 줄 남긴다.
   - 첫 요청에 "이 대화방에서는 앞으로도 같은 그림체·규칙으로 <계열> 그림을 계속 부탁할 거야" 를 붙인다.
   - 대화방이 생기면 그 주소를 같은 `chat` 의 모든 줄에 `chat_url` 로 적는다.
4. **뽑기.** `todo` 를 위에서부터, 같은 `chat` 끼리 묶어 한 번에 최대 8장 뽑는다.
   - 문구는 앞머리를 파일 내용으로 바꿔 넣는다.
   - 생성이 끝날 때까지 기다린다. 한 장에 1~2분 걸린다. 입력창 옆 정지 버튼이 사라지면 끝난 것이다.
5. **받기.**
   - 이미지를 눌러 보기 창을 열고, 오른쪽 위 다운로드 버튼을 누른다.
   - 받은 파일은 PC `C:\Users\BaxXR\Downloads` 에 ChatGPT 가 지은 한글 이름으로 떨어진다. 방금 받은 png 를 PowerShell 로 `art_inbox/<save_as>` 이름으로 옮긴다.
   - 옮겼으면 그 줄을 `generated` 로 바꾼다.
   - 받으려고 연 탭은 끝나면 닫는다.
6. **기계 검수와 반영.** 작업 폴더 `E:\Unity\BloodRain-art` 에서 돌린다.
   - 이펙트 재료: `python tools/import_vfx_ai.py --inbox E:/Unity/BloodRain`. 바탕이 단색인지, 덮임 양, 가장자리 걸림, 먹에 색이 섞였는지를 본다. 통과하면 `Assets/Art/VFX/AI/<id>_stroke.png`(먹) 또는 `<id>_gold.png`·`<id>_core.png`·`<id>_shade.png`(빛과 그 밑 먹 받침)와 `ai.json` 에 들어가고 미리보기 `docs/vfx_preview/ai_<날짜>.png` 가 나온다. 먹 알파에는 감마 0.75 를 걸어 옅은 자락(안개 끝)을 살린다(ai.json 줄의 `inkGamma`, 2026-11-02 V-11). 감마를 바꾸면 `--reink --inbox …` 로 이미 반영된 먹 재료를 원본에서 다시 키잉하고, `--gamma-preview <id>…` 로 전후를 한 장에 본다.
   - 원화: `postprocess_art.py`. 배경은 그림 가장자리에서 이어진 곳만 지운다(2026-11-08 PR-02 — 예전엔 배경색과 비슷하면 얼굴·흰 옷까지 지웠다). 스프라이트·초상·소품은 안쪽에 갇힌 배경색 그대로인 넓은 덩이(팔과 몸 사이, 수레바퀴 살 사이)도 지운다. UI 바탕(`ui`)은 틀 안쪽 종이를 남기고, 정사각으로 만들지 않는다. 소품 시트(`props_pickups`·`props_items`·`props_breakables`)는 들인 뒤 `python tools/split_props.py` 로 낱장 `Props/Parts/` 를 다시 만든다(새 시트를 낱장으로 쓰려면 그 파일 SHEETS 에 프롬프트 차례대로 이름을 더함). 스토어 시안(`store`)은 배경도 크기도 손대지 않고 `Assets/Art/Store/` 에 jpg 로 두며, 마무리는 사람이 한다. 무엇을 뽑는지는 `art_list.md`.
7. **눈 검수.** 미리보기를 직접 열어 본다. 미리보기는 원본 | 게임 색 | 그어짐 50% | 흩어짐 40% 순서다. 아래 중 하나라도 해당하면 `import_vfx_ai.py --reject <id> "<이유>"` 로 떨어뜨린다.
   - 글자나 낙관이 들어 있다.
   - 모양이 무엇인지 안 읽힌다.
   - 그림체가 수묵과 동떨어졌다.
   - 그어짐 방향이 이상하다.
8. **올리기.** 원화는 Unity 가 meta 를 만든 뒤 `python tools/fix_art_meta.py` 로 Sprite 설정을 맞춘다(B-12, 안 하면 `ArtMetaTests` 가 막는다). `dotnet test` 가 통과하면 커밋·푸시한다. daily 에는 몇 장 뽑았고 몇 장 떨어졌는지를 한 줄로 적는다. 커밋 메시지는 UTF-8 로 저장한 파일을 `git commit -F <파일>` 로 넘긴다 — 2026-11-07 01:47 세션 커밋(1237296)이 Windows 셸에서 `-m` 한글이 cp949 로 읽혀 메시지가 깨졌다(파일 내용은 멀쩡).

## 적용 (Unity)

- 텍스처는 층마다 흰색 + 알파 512px 이고 색은 셰이더가 입힌다. 굽기 텍스처(Brush)는 획 있는 쪽만 잘려 있고 자리는 json `crop`·`pivot`(셰이더 `_Rect`, V-10). AI·Common 은 자르지 않는다.
  - 굽기 층: stroke 먹 #141414 · wash 청회 #4F7F7A · gold #C9A227
  - AI 빛 재료 층: gold 는 금빛을 더하고, core 는 흰빛을 더한다. shade 는 빛 밑에 먼저 까는 먹 받침(보통 섞기, 먹 #141414)이다. 그어짐·흩어짐은 gold 와 같은 값으로 묶는다.
    더하기 빛만 그리면 밝은 종이 바닥에서 크림색으로 묻힌다(2026-11-01 PC 화면). 받침은 `import_vfx_ai.py` 가 gold 를 13px 넓히고 9px 흐려 0.55배로 만든다(`--reshade` 로 이미 반영된 것도 다시 만듦). 견줌: `docs/vfx_preview/glow_shade_paper.png`(왼쪽 받침 없음, 오른쪽 받침).
- 크기는 같은 폴더 json 의 `sizeMeters`, PPU = 512 ÷ sizeMeters.
- Unity 를 열지 않고 `.meta` 를 만들 때는 기존 먹·빛 재료 meta 를 본뜨되 `guid` 는 꼭 새로 뽑는다(`python -c "import uuid;print(uuid.uuid4().hex)"`). guid 가 겹치거나 에셋 없이 meta 만 남으면 `MetaGuidTests` 가 막는다.
- 붓 획 셰이더와 이펙트 틀은 backlog V-04 / `unity_tasks.md` V-04 에서 만든다.
  - `_Progress` 는 호면 각도, 찌르기면 +x 로 그어진다. AI 재료는 `ai.json` 의 `progress` 방향(가운데서 바깥, 위→아래 …)을 쓴다.
  - `_Dissolve` 는 `Common/noise_1` 문턱으로 흩어진다.
- 무공에 붙이는 일은 backlog V-05·V-06.
- 화면 검수: PC Unity 세션이 무공마다 스크린샷을 `docs/unity_shots/` 에 남기고 웹 시안과 나란히 본다. 움직임의 손맛은 사람이 웹 빌드로 본다(`feedback.md`).

## 이용약관 메모

OpenAI 약관에는 "출력물을 자동·프로그램으로 추출하지 말 것"이라는 조항이 있다. 사람은 이 점을 알고 자동 생산을 택했다(2026-11-01).

세션은 아래를 지킨다.
- 한 번에 8장, 하룻밤 30장 안쪽으로 뽑는다.
- 한도 안내나 사람 확인이 뜨면 바로 멈춘다.
- 사람 확인을 우회하지 않는다.
- 계정 설정을 바꾸지 않는다.
