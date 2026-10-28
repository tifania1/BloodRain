# 혈우강호(BloodRain) — 작업 규칙

무협 세계관의 탑다운 2D 호드 서바이벌(뱀서류). Unity 6000.3, 2D URP. Steam 먼저, 모바일 이식.
기획서 원본은 Claude Docs(https://claude.ai/artifact/PXv4A8V2tESbrUdkETAWdk), 저장소 사본은 `docs/design/gdd.md`.
이 파일을 읽는 세션은 대부분 예약으로 깨어난 새 세션이다. 대화창 내용은 못 본다고 생각하고 이 파일과 docs/ 만 믿는다.

## 시작할 때 이 순서로 읽는다
1. `docs/feedback.md` — 사람이 남긴 지시. backlog보다 먼저다.
2. `docs/backlog.md` — 위에서부터 안 끝난 항목.
3. 오늘 `docs/daily/YYYY-MM-DD.md` (한국 시간 기준 날짜) — 앞선 세션이 오늘 한 일.

## 한 세션에서 하는 일
- 겹침 방지: `git log -1 --format=%ct origin/claude/dev` 가 20분 이내면 아무것도 하지 말고 끝낸다.
- backlog 항목 **1~2개만**. 끝낸 항목은 `[x]`, 날짜, 한 줄 결과를 적는다.
- backlog가 바닥나면 일을 지어내지 않는다. 테스트 보강, 문서 정리, 밸런스 시뮬레이션만 한다.
- 그날 첫 세션이 `docs/daily/YYYY-MM-DD.md` 를 만들고 맨 위에 "아침에 확인할 것"을 **3줄 이하**로 적는다. 이후 세션은 아래에 한 줄씩 덧붙인다.
- 문서가 실제 코드·데이터와 어긋난 것을 보면 그 자리에서 고친다.

## 브랜치와 푸시
- 작업은 항상 `claude/dev` 로 올린다: `git push origin HEAD:claude/dev`. force push 금지.
- 푸시 전에 `dotnet test core/BloodRain.sln` 이 통과해야 한다. dotnet 이 없으면:
  `curl -sSL https://dot.net/v1/dotnet-install.sh -o /tmp/di.sh && bash /tmp/di.sh --channel 8.0 --install-dir ~/.dotnet && export PATH=~/.dotnet:$PATH`
- `main` 승격(그날 마지막 세션 또는 06:30 정리 세션): 아래 셋이 모두 맞을 때만 `git push origin claude/dev:main`.
  1. 테스트 실패 0
  2. `gh run list --branch claude/dev --workflow core-tests.yml --limit 1 --json conclusion` 가 success
  3. fast-forward 가능(`git merge-base --is-ancestor origin/main origin/claude/dev`)

## 코드 구조
- `unity/BloodRain/Assets/Scripts/Core/` — 게임 규칙. **UnityEngine 금지**(asmdef noEngineReferences). C# 9, .NET Standard 2.1 범위 안에서만 쓴다.
  `core/BloodRain.Core` 가 이 폴더를 그대로 컴파일하고 `core/BloodRain.Core.Tests` 가 테스트한다. 클라우드에는 Unity가 없으니 규칙은 전부 여기서 만들고 검증한다.
- `unity/BloodRain/Assets/Resources/Data/*.json` — 무공·심법·절기·캐릭터·적·밸런스 데이터. Unity와 테스트가 같은 파일을 읽는다.
  값을 바꾸면 `docs/design/gdd.md` 의 같은 표도 고친다. 테스트(`BalanceTests`)가 기획서 숫자와 맞는지 확인한다.
- Unity 에디터가 필요한 일(씬, 프리팹, 셰이더, 플레이 검수)은 직접 하려 하지 말고 `docs/unity_tasks.md` 에 `- [ ]` 한 줄로 적는다. PC 루프(tools/agent_loop)가 처리한다.
- `web/prototype/` — 브라우저 프로토타입(한 장짜리 HTML). 데이터 JSON을 빌드 때 그대로 넣으므로 JSON을 바꾸면 `python tools/build_web_prototype.py` 를 다시 돌려 `index.html` 도 같이 커밋한다. 설명은 그 폴더 README.

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
