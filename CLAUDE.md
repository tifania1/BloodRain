# 혈우강호(BloodRain) — 작업 규칙

무협 세계관의 탑다운 2D 호드 서바이벌(뱀서류). Unity 6000.3, 2D URP. Steam 먼저, 모바일 이식.
기획서 원본은 Claude Docs(https://claude.ai/artifact/PXv4A8V2tESbrUdkETAWdk), 저장소 사본은 `docs/design/gdd.md`.
이 파일을 읽는 세션은 대부분 예약으로 깨어난 새 세션이다. 대화창 내용은 못 본다고 생각하고 이 파일과 docs/ 만 믿는다.

## 시작할 때 이 순서로 읽는다
1. `docs/feedback.md` — 사람이 남긴 지시. backlog보다 먼저다.
2. `docs/backlog.md` — 위에서부터 안 끝난 항목.
3. 오늘 `docs/daily/YYYY-MM-DD.md` (한국 시간 기준 날짜) — 앞선 세션이 오늘 한 일.

## 한 세션에서 하는 일
- 예약 세션은 평일 밤(19~05시)과 주말에 매시간 돈다. 평일 06:21·주말 22:21 은 정리 세션, 매일 밤 20:37·22:37·00:37·02:37·04:37 은 PC Unity 세션이다. 평일 낮은 사람이 회사 일에 쓰니 비워 둔다.
- 겹침 방지: `docs/session_lock.md` 가 있고 적힌 시작 시각이 55분 안이면 다른 세션이 일하는 중이니 아무것도 하지 말고 끝낸다. 아니면 시작 시각을 적어 커밋·푸시하고 시작하고, 끝낼 때 지운다. 푸시가 거절되면(다른 세션이 먼저 잡음) 끝낸다.
- 한 세션은 45분 동안 일하고 50분 안에 정리까지 끝낸다. backlog 위에서부터 여러 항목을 해도 되고, 항목 하나를 끝낼 때마다 커밋·푸시한다. 끝낸 항목은 `[x]`, 날짜, 한 줄 결과를 적는다. 덜 끝났으면 그 항목 끝에 "(진행: 어디까지)"를 적는다.
- "하는 중" 표시가 붙은 항목, '먼저:' 가 안 끝난 항목은 건너뛴다. "하는 중" 표시가 하루 넘게 묵었고 그사이 관련 커밋이 없으면 표시를 지우고 해도 된다.
- backlog가 바닥나면 일을 지어내지 않는다. 테스트 보강, 문서 정리, 밸런스 시뮬레이션만 한다.
- 그날 첫 세션이 `docs/daily/YYYY-MM-DD.md` 를 만들고 맨 위에 "아침에 확인할 것"을 **3줄 이하**로 적는다. 이후 세션은 아래에 덧붙인다.
- 문서가 실제 코드·데이터와 어긋난 것을 보면 그 자리에서 고친다.

## 브랜치와 푸시
- 작업은 항상 `claude/dev` 로 올린다: `git push origin HEAD:claude/dev`. force push 금지.
- 푸시 전에 `dotnet test core/BloodRain.sln` 이 통과해야 한다. dotnet 이 없으면:
  `curl -sSL https://dot.net/v1/dotnet-install.sh -o /tmp/di.sh && bash /tmp/di.sh --channel 8.0 --install-dir ~/.dotnet && export PATH=~/.dotnet:$PATH`
- `main` 승격은 정리 세션(평일 06:21, 주말 22:21)만 한다. 아래 셋이 모두 맞을 때만 `git push origin claude/dev:main`.
  1. 테스트 실패 0
  2. `gh run list --branch claude/dev --workflow core-tests.yml --limit 1 --json conclusion` 가 success
  3. fast-forward 가능(`git merge-base --is-ancestor origin/main origin/claude/dev`)

## 코드 구조
- `unity/BloodRain/Assets/Scripts/Core/` — 게임 규칙. **UnityEngine 금지**(asmdef noEngineReferences). C# 9, .NET Standard 2.1 범위 안에서만 쓴다.
  Unity 는 nullable 을 켜지 않으니 참조형에 `?` 를 쓰는 파일은 맨 위에 `#nullable enable` 을 둔다(CI가 nullable 끈 빌드로 확인).
  `core/BloodRain.Core` 가 이 폴더를 그대로 컴파일하고 `core/BloodRain.Core.Tests` 가 테스트한다. 클라우드에는 Unity가 없으니 규칙은 전부 여기서 만들고 검증한다.
- `unity/BloodRain/Assets/Resources/Data/*.json` — weapons·passives·evolutions(무공·심법·절기), characters(서장 진소운·대표 인물), enemies(적·정예·땅의 주인), balance, synergies, insights(깨달음), chapters(서장·한 생 흐름), cheonha(천하 한 장: 축척·지역·문파·타고난 성질·환혼·윤회의 기), bosspatterns(땅의 주인 패턴), gimmicks(지역 기믹·날씨). 목록은 `GameData.FileNames`. Unity와 테스트가 같은 파일을 읽는다.
  값을 바꾸면 `docs/design/gdd.md` 의 같은 표도 고친다. 테스트(`BalanceTests`, `CheonhaDataTests` 등)가 기획서 숫자와 맞는지 확인한다.
- 게임 구조는 '천하'(2026-10-29 결정): 죽을 때마다 다른 문파 제자의 몸으로 환생(차시환혼), 경지마다 세상이 2.25배 작아지는 중원 한 장 맵, 생이 끝나면 윤회의 기로 능력치. 기획서 5·7·10·21장. 옛 '장 선택 + 강호 지도' 안은 버렸다.
- `gdd.md` 를 고치면 `docs/design/gdd_changes.md` 에 한 줄(날짜, 장·절, 무엇을)을 남긴다. 원본 문서(Claude Docs)에는 대화 세션이 모아서 옮긴다.
- Unity 에디터가 필요한 일(씬, 프리팹, 셰이더, 플레이 검수)은 직접 하려 하지 말고 `docs/unity_tasks.md` 에 `- [ ]` 한 줄로 적는다. 밤에 PC Unity 세션이 PC의 Unity(unityMCP)로 처리한다. PC가 꺼져 있거나 다른 프로젝트 Unity가 떠 있으면 건너뛴다.
- `web/prototype/` — 브라우저 프로토타입. 원본은 `template.html` 하나이고 데이터 JSON을 빌드 때 그대로 넣는다. 빌드 결과는 커밋하지 않는다.
  푸시하면 CI(`web-prototype.yml`)가 묶어서 Cloudflare Pages에 올린다(claude/dev는 미리보기 주소, main은 공개 주소 https://bloodrain-proto.pages.dev). 설명은 그 폴더 README.

## 이미지
- 이미지를 직접 생성하지 않는다(클라우드에서는 이미지 서비스가 막혀 있다).
- 필요한 이미지는 `art_queue/queue.jsonl` 에 `status: "todo"` 줄로 추가한다. 프롬프트는 `{STYLE}` 로 시작하고 기획서 '13. 아트 디렉션'의 틀을 따른다. 사람이 있을 때 ChatGPT 앱에서 뽑는다.
- 같은 캐릭터·같은 계열 적은 같은 `chat` 값을 준다(같은 대화방에서 뽑아야 그림체가 맞는다).
- 이미지에 글자를 굽지 않는다. 모든 글자는 Unity에서 얹는다.

## 글쓰기
- 문서·커밋 메시지·주석은 한국어. 사람이 실제로 말하듯 자연스럽게. 과장 없이.
- 김용·고룡 등 특정 작가의 고유 무공 이름(항룡십팔장, 독고구검 등)은 쓰지 않는다. 장르 공통어나 새로 지은 이름만.

## 하지 말 것
- 비밀값(API 키, 비밀번호, 라이선스 파일)을 커밋하지 않는다. `tools/agent_loop/config.json` 은 .gitignore 에 있다.
- PC의 `ANTHROPIC_API_KEY` 환경변수는 회사 키다. 이 프로젝트에서 쓰지 않는다.
- 확률형 뽑기, 능력치 판매 같은 '힘을 파는 결제'는 어떤 형태로도 만들지 않는다(기획서 16장).
