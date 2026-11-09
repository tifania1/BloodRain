# 혈우강호(BloodRain) — 작업 규칙

무협 세계관의 탑다운 2D 호드 서바이벌(뱀서류). Unity 6000.3, 2D URP. Steam 먼저, 모바일 이식.
기획서 원본은 Claude Docs(https://claude.ai/artifact/PXv4A8V2tESbrUdkETAWdk), 저장소 사본은 `docs/design/gdd.md`.
이 파일을 읽는 세션은 대부분 예약으로 깨어난 새 세션이다. 대화창 내용은 못 본다고 생각하고 이 파일과 docs/ 만 믿는다.

## 시작할 때 이 순서로 읽는다
1. `docs/feedback.md` — 사람이 남긴 지시. backlog보다 먼저다.
2. `docs/backlog.md` — 위에서부터 안 끝난 항목.
3. 오늘 `docs/daily/YYYY-MM-DD.md` (한국 시간 기준 날짜) — 앞선 세션이 오늘 한 일.

## 한 세션에서 하는 일
- 예약 세션(2026-11-10부터. 구독을 Pro 로 내려 주 164번에서 30번으로 줄임 — 2026-11-09 사람): 평일은 20:13 개발 → 22:37 PC Unity → 23:47 이미지·배경음 → 다음 날 06:21 정리. 주말은 13:13·20:13 개발, 22:21 정리, 22:37 PC Unity, 23:47 이미지·배경음. 이미지·배경음 세션은 잠금을 잡지 않는다(`docs/design/art_pipeline.md`·`audio_pipeline.md`). PC API 루프(`tools/agent_loop/README.md`)는 콘솔 무료 크레딧이 끝나는 2026-11-13 에 끈다. 평일 낮은 사람이 회사 일에 쓰니 비워 둔다.
- 세션이 적으니 한 번 돌 때 아껴 쓴다: 개발 세션은 Unity 세션이 같은 밤에 이어받을 수 있는 Core·데이터 일(`먼저:` 를 푸는 것)을 먼저 집는다.
- 겹침 방지: `docs/session_lock.md` 가 있고 적힌 시작 시각이 55분 안이면 다른 세션이 일하는 중이니 아무것도 하지 말고 끝낸다. 아니면 시작 시각을 적어 커밋·푸시하고 시작하고, 끝낼 때 지운다. 푸시가 거절되면(다른 세션이 먼저 잡음) 끝낸다.
- 한 세션은 45분 동안 일하고 50분 안에 정리까지 끝낸다. backlog 위에서부터 여러 항목을 해도 되고, 항목 하나를 끝낼 때마다 커밋·푸시한다. 끝낸 항목은 `[x]`, 날짜, 한 줄 결과를 적는다. 덜 끝났으면 그 항목 끝에 "(진행: 어디까지)"를 적는다.
- "하는 중" 표시가 붙은 항목, '먼저:' 가 안 끝난 항목은 건너뛴다. "하는 중" 표시가 하루 넘게 묵었고 그사이 관련 커밋이 없으면 표시를 지우고 해도 된다.
- backlog 의 클라우드 몫이 바닥나면 로드맵(기획서 17장)의 지금 단계와 다음 단계에서, 기획서에 이미 적힌 것만 클라우드가 할 수 있는 크기로 쪼개 backlog 에 5~10개 넣고(제목 줄에 '로드맵에서 채움, 날짜') 그것부터 한다. 기획서에 없는 새 기획·새 숫자가 필요하면 지어내지 말고 '사람 결정 대기'로 적되 추천안 하나를 붙인다. 사람 결정 대기는 `docs/decisions.md` 에도 한 줄 더한다(정해지면 지운다). Unity 몫은 unity_tasks 로 옮긴다(2026-11-04 사람이 정함).
- 그날 첫 세션이 `docs/daily/YYYY-MM-DD.md` 를 만들고 맨 위에 "아침에 확인할 것"을 **3줄 이하**로 적는다. 이후 세션은 아래에 덧붙인다.
- 문서가 실제 코드·데이터와 어긋난 것을 보면 그 자리에서 고친다.

## 브랜치와 푸시
- 작업은 항상 `claude/dev` 로 올린다: `git push origin HEAD:claude/dev`. force push 금지.
- 푸시 전에 `dotnet test core/BloodRain.sln` 이 통과해야 한다. 웹 프로토타입·기획서·데이터만 고쳤어도 마찬가지다(테스트가 template.html·gdd.md·JSON을 읽는다). dotnet 이 없으면:
  `curl -sSL https://dot.net/v1/dotnet-install.sh -o /tmp/di.sh && bash /tmp/di.sh --channel 8.0 --install-dir ~/.dotnet && export PATH=~/.dotnet:$PATH`
  이 주소가 403 으로 막힌 컨테이너가 있다(2026-11-04 여러 세션). 그때는 `apt-get install -y dotnet-sdk-8.0` 으로 깐다.
- 세션을 시작하면 클론한 저장소에서 `git config core.hooksPath tools/hooks` 를 한 번 한다. 그러면 푸시할 때마다 `tools/hooks/pre-push` 가 테스트(Core를 고쳤으면 Unity 조건 빌드, 웹·데이터를 고쳤으면 웹 빌드까지)를 돌리고 실패하면 푸시를 막는다. 잠금 파일·daily 만 바뀐 푸시는 바로 통과한다. `--no-verify` 로 건너뛰지 않는다. 실패한 커밋이 올라가면 Actions 가 실패하고 사람에게 메일이 간다.
- `main` 승격은 정리 세션(평일 06:21, 주말 22:21)만 한다. 아래 셋이 모두 맞을 때만 `git push origin claude/dev:main`.
  1. 테스트 실패 0
  2. `gh run list --branch claude/dev --workflow core-tests.yml --limit 1 --json conclusion` 가 success
  3. fast-forward 가능(`git merge-base --is-ancestor origin/main origin/claude/dev`)

## 코드 구조
- `unity/BloodRain/Assets/Scripts/Core/` — 게임 규칙. **UnityEngine 금지**(asmdef noEngineReferences). C# 9, .NET Standard 2.1 범위 안에서만 쓴다.
  Unity 는 nullable 을 켜지 않으니 참조형에 `?` 를 쓰는 파일은 맨 위에 `#nullable enable` 을 둔다(CI가 nullable 끈 빌드로 확인).
  `core/BloodRain.Core` 가 이 폴더를 그대로 컴파일하고 `core/BloodRain.Core.Tests` 가 테스트한다. 클라우드에는 Unity가 없으니 규칙은 전부 여기서 만들고 검증한다.
- `unity/BloodRain/Assets/Resources/Data/*.json` — weapons·passives·evolutions(무공·심법·절기), characters(서장 진소운·대표 인물), enemies(적·정예·땅의 주인), balance, synergies, insights(깨달음), chapters(서장·한 생 흐름), cheonha(천하 한 장: 축척·지역·문파·타고난 성질·환혼·윤회의 기), bosspatterns(땅의 주인 패턴), gimmicks(지역 기믹·날씨), dialogue(대사, 원문은 `docs/story/`). 목록은 `GameData.FileNames`. Unity와 테스트가 같은 파일을 읽는다.
  현지화: 데이터의 한국어가 원문이고 영어·간체는 `Resources/Data/strings/<lang>.json`(Core `Strings`, 키 규칙은 그 파일 맨 위, 용어는 `docs/design/glossary.md`). 이름·설명을 바꾸거나 새로 넣으면 그 키가 빠졌다고 테스트가 알려 준다.
  값을 바꾸면 `docs/design/gdd.md` 의 같은 표도 고친다. 테스트(`BalanceTests`, `CheonhaDataTests` 등)가 기획서 숫자와 맞는지 확인한다.
- 게임 구조는 '천하'(2026-10-29 결정): 죽을 때마다 다른 문파 제자의 몸으로 환생(차시환혼), 경지마다 세상이 2.25배 작아지는 중원 한 장 맵, 생이 끝나면 윤회의 기로 능력치. 기획서 5·7·10·21장. 옛 '장 선택 + 강호 지도' 안은 버렸다.
- `gdd.md` 를 고치면 `docs/design/gdd_changes.md` 에 한 줄(날짜, 장·절, 무엇을)을 남긴다. 원본 문서(Claude Docs)에는 대화 세션이 모아서 옮긴다.
- Unity 에디터가 필요한 일(씬, 프리팹, 셰이더, 플레이 검수)은 직접 하려 하지 말고 `docs/unity_tasks.md` 의 '할 차례' 알맞은 절(의존 순서 자리)에 `- [ ]` 항목으로 적는다(첫 줄 `먼저:`, 밑줄 할 것·쓸 것·메뉴·끝·찍기 — 그 파일 맨 위 '항목 읽는 법'). 밤에 PC Unity 세션이 PC의 Unity(unityMCP)로 처리한다. PC가 꺼져 있거나 다른 프로젝트 Unity가 떠 있으면 건너뛴다. 토·일 22:37 차례는 마지막으로 올린 뒤 Unity 쪽이 바뀌었으면 WebGL 빌드를 https://dev.bloodrain.pages.dev 에 올린다(진짜 게임을 브라우저로 해 보는 곳, 2026-11-09 — unity_tasks '항목 읽는 법').
- `web/prototype/` — 브라우저 프로토타입. 원본은 `template.html` 하나이고 데이터 JSON을 빌드 때 그대로 넣는다. 빌드 결과는 커밋하지 않는다.
  푸시하면 CI(`web-prototype.yml`)가 묶어서 Cloudflare Pages에 올린다(claude/dev는 미리보기 주소, main은 공개 주소 https://bloodrain-proto.pages.dev). 설명은 그 폴더 README.

## 이미지와 이펙트
- 생산·검수·적용 순서는 `docs/design/art_pipeline.md`, 이펙트 모양 규칙은 `docs/design/vfx.md`. 이펙트는 셰이더와 아트를 함께 써서 최대한 예쁘게(2026-11-01 사람).
- 클라우드 세션은 이미지를 생성하지 않는다(이미지 서비스가 막혀 있다). 필요한 이미지는 `art_queue/queue.jsonl` 에 `status: "todo"` 줄로 추가한다. 원화는 `{STYLE}`, 이펙트 재료는 `{VFX_INK}`(흰 바탕 먹)·`{VFX_GLOW}`(검은 바탕 빛)로 시작하고 기획서 '13. 아트 디렉션'의 틀을 따른다. ChatGPT 웹(회사 PC 크롬의 Claude 확장)으로 뽑는다.
- 코드로 되는 이펙트 텍스처(붓 획·먹 튐·자국·노이즈)는 `tools/bake_brush.py` 로 굽는다.
- 같은 캐릭터·같은 계열 적은 같은 `chat` 값을 준다(같은 대화방에서 뽑아야 그림체가 맞는다).
- 이미지에 글자를 굽지 않는다. 모든 글자는 Unity에서 얹는다.
- 배경음은 Gemini '음악 만들기'로 밤 생산 세션이 자동으로 뽑는다(2026-11-04·05). 필요한 곡은 `audio_queue/queue.jsonl` 에 `todo` 줄로, 문구는 `{STYLE}`(`audio_queue/STYLE.txt`)로 시작. 순서·다듬기는 `docs/design/audio_pipeline.md`.
- 효과음은 직접 만든다(2026-11-05 사람). 음이 있는 소리는 코드로 합성, 나머지는 AI 후보 + 녹음 재료로 층을 쌓는다. `docs/design/sfx_research.md`, backlog SFX.

## 글쓰기
- 문서·커밋 메시지·주석은 한국어. 사람이 실제로 말하듯 자연스럽게. 과장 없이.
- 김용·고룡 등 특정 작가의 고유 무공 이름(항룡십팔장, 독고구검 등)은 쓰지 않는다. 장르 공통어나 새로 지은 이름만.

## 하지 말 것
- 비밀값(API 키, 비밀번호, 라이선스 파일)을 커밋하지 않는다. `tools/agent_loop/config.json` 은 .gitignore 에 있다.
- PC의 `ANTHROPIC_API_KEY` 환경변수는 회사 키다. 이 프로젝트에서 쓰지 않는다.
- 확률형 뽑기, 능력치 판매 같은 '힘을 파는 결제'는 어떤 형태로도 만들지 않는다(기획서 16장).
