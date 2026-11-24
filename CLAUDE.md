# 혈우강호(BloodRain) — 작업 규칙

무협 세계관의 탑다운 2D 호드 서바이벌(뱀서류). Unity 6000.3, 2D URP. Steam 먼저, 모바일 이식.
기획서 원본은 Claude Docs(https://claude.ai/artifact/PXv4A8V2tESbrUdkETAWdk), 저장소 사본은 `docs/design/gdd.md`.
이 파일을 읽는 세션은 대부분 예약으로 깨어난 새 세션이다. 대화창 내용은 못 본다고 생각하고 이 파일과 docs/ 만 믿는다.

## 시작할 때 이 순서로 읽는다
1. `docs/feedback.md` — 사람이 남긴 지시. backlog보다 먼저다.
2. `docs/backlog.md` — 위에서부터 안 끝난 항목.
3. 오늘 `docs/daily/YYYY-MM-DD.md` (한국 시간 기준 날짜) — 앞선 세션이 오늘 한 일.

## 한 세션에서 하는 일
- 예약 세션(2026-11-17 사람이 구독을 다시 올려 11-09 전 빈도로 되돌림, 주 150번쯤): 개발 세션은 평일 밤 19:13~05:13 매시간, 토·일은 매시간(:13, 22시만 빼고). 평일 06:21·주말 22:21 은 정리 세션, 매일 밤 20:37·22:37·00:37·02:37·04:37 은 PC Unity 세션, 21:47·23:47·01:47 은 이미지·배경음 생산 세션(`docs/design/art_pipeline.md`·`audio_pipeline.md`, 잠금을 잡지 않음). PC API 루프(`tools/agent_loop/README.md`)는 콘솔 무료 크레딧이 끝나 2026-11-13 에 껐고 되살리지 않았다. 평일 낮은 사람이 회사 일에 쓰니 비워 둔다.
- **다음 단계(2026-11-24 사람이 화면 집중을 풂)**: 11-13~11-24 숭산 한 생만 다듬던 '화면 집중'을 끝내고, 숭산 뒤로 미뤄 둔 기획 업그레이드와 다음 지역 화면으로 넘어간다. 개발 세션은 backlog 맨 위 '다음 단계' 절을, PC Unity 세션은 unity_tasks '0-1. 다음 단계' 절을 위에서부터 한다. 화면 집중 절에 남은 열린 항목(사람 눈이 필요한 GR-01U·HF-03U·VF-02U 세기 등)은 지우지 않고 그대로 이어 간다. 숭산에서 맞춘 화면 수준(review `docs/design/review_2026-11-13.md`, unity_tasks 0절 끝난 항목들)이 다른 지역의 기준이다. 기획 업그레이드 안은 `docs/design/roguelike_benchmark.md`(사람이 '추천 순서대로 전부'로 정함, 숫자는 그 문서 추천값). 아래 옛 절(M0·천하 구조·M1 등)의 열린 항목은 '다음 단계' 절이 바닥난 뒤에 보고, 이미 된 것은 닫는다.
- 개발 세션은 같은 밤 PC Unity 세션이 이어받을 수 있는 Core·데이터 일(`먼저:` 를 푸는 것)을 먼저 집는다.
- 겹침 방지: `docs/session_lock.md` 가 있고 적힌 시작 시각이 55분 안이면 다른 세션이 일하는 중이니 아무것도 하지 말고 끝낸다. 아니면 시작 시각을 적어 커밋·푸시하고 시작하고, 끝낼 때 지운다. 푸시가 거절되면(다른 세션이 먼저 잡음) 끝낸다.
- 한 세션은 45분 동안 일하고 50분 안에 정리까지 끝낸다. backlog 위에서부터 여러 항목을 해도 되고, 항목 하나를 끝낼 때마다 커밋·푸시한다. 끝낸 항목은 `[x]`, 날짜, 한 줄 결과를 적는다. 덜 끝났으면 그 항목 끝에 "(진행: 어디까지)"를 적는다.
- "하는 중" 표시가 붙은 항목, '먼저:' 가 안 끝난 항목은 건너뛴다. "하는 중" 표시가 하루 넘게 묵었고 그사이 관련 커밋이 없으면 표시를 지우고 해도 된다.
- backlog 의 클라우드 몫('다음 단계' 절과 그 아래 옛 절)이 바닥나면 로드맵(기획서 17장)의 지금 단계와 다음 단계에서, 기획서에 이미 적힌 것만 클라우드가 할 수 있는 크기로 쪼개 backlog 에 5~10개 넣고(제목 줄에 '로드맵에서 채움, 날짜') 그것부터 한다. 기획서에 없는 새 기획·새 숫자가 필요하면 지어내지 말고 '사람 결정 대기'로 적되 추천안 하나를 붙인다. 사람 결정 대기는 `docs/decisions.md` 에도 한 줄 더한다(정해지면 지운다). Unity 몫은 unity_tasks 로 옮긴다(2026-11-04 사람이 정함).
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
- `unity/BloodRain/Assets/Resources/Data/*.json` — weapons·passives·evolutions(무공·심법·절기), characters(서장 진소운·대표 인물), enemies(적·정예·땅의 주인), balance, synergies, insights(깨달음), chapters(서장·한 생 흐름), cheonha(천하 한 장: 축척·지역·문파·타고난 성질·환혼·윤회의 기), bosspatterns(땅의 주인 패턴), gimmicks(지역 기믹·날씨), dialogue(대사, 원문은 `docs/story/`), credits(크레딧 고지, 출처는 `docs/design/credits.md`), chronicle(천하 연표 사건, 설계안 `docs/design/cheonha_expansion_full.md`). 목록은 `GameData.FileNames`. Unity와 테스트가 같은 파일을 읽는다.
  현지화: 데이터의 한국어가 원문이고 영어·간체는 `Resources/Data/strings/<lang>.json`(Core `Strings`, 키 규칙은 그 파일 맨 위, 용어는 `docs/design/glossary.md`). 이름·설명을 바꾸거나 새로 넣으면 그 키가 빠졌다고 테스트가 알려 준다.
  값을 바꾸면 `docs/design/gdd.md` 의 같은 표도 고친다. 테스트(`BalanceTests`, `CheonhaDataTests` 등)가 기획서 숫자와 맞는지 확인한다.
- 게임 구조는 '천하'(2026-10-29 결정): 죽을 때마다 다른 문파 제자의 몸으로 환생(차시환혼), 경지마다 세상이 2.4배 작아지는 중원 한 장 맵(2026-11-20 전엔 2.25), 생이 끝나면 윤회의 기로 능력치. 기획서 5·7·10·21장. 옛 '장 선택 + 강호 지도' 안은 버렸다.
- `gdd.md` 를 고치면 `docs/design/gdd_changes.md` 에 한 줄(날짜, 장·절, 무엇을)을 남긴다. 원본 문서(Claude Docs)에는 대화 세션이 모아서 옮긴다.
- Unity 에디터가 필요한 일(씬, 프리팹, 셰이더, 플레이 검수)은 직접 하려 하지 말고 `docs/unity_tasks.md` 의 '할 차례' 알맞은 절(의존 순서 자리)에 `- [ ]` 항목으로 적는다(첫 줄 `먼저:`, 밑줄 할 것·쓸 것·메뉴·끝·찍기 — 그 파일 맨 위 '항목 읽는 법'). 밤에 PC Unity 세션이 PC의 Unity(unityMCP)로 처리한다. PC가 꺼져 있거나 다른 프로젝트 Unity가 떠 있으면 건너뛴다. 매일 04:37 차례는 마지막으로 올린 뒤 Unity 쪽이 바뀌었으면 WebGL 빌드를 https://dev.bloodrain.pages.dev 에 올린다(진짜 게임을 브라우저로 해 보는 곳, 2026-11-09 — unity_tasks '항목 읽는 법').
- 화면을 볼 때(2026-11-17): Run 씬 Play 나 웹 dev 판에서 **F2 시험 메뉴**(`Hud/TestPanel.cs`, 개발 판에서만 붙음)로 무공·경지·문파 몸을 바로 바꾼다. 코드로는 `TestPanel.Raise(id, 레벨)`·`TestPanel.SwitchBody(문파 id)`·`TestPanel.Awaken(절기)`(Lv8 무공 칸을 누르면 절기를 깨움, 2026-11-20). 에디터 시험 중 `Application.runInBackground` 를 켜지 않는다 — PlayerSettings 에 써져 다음 웹 빌드에 들어간다(2026-11-20 한 번 그랬음). 지도는 M(`Hud/WorldMapHud.cs`).
- 에셋 스토어에서 산 것(2026-11-17): DOTween Pro·Text Animator·All In 1 Sprite Shader·Damage Numbers Pro 가 PC 의 Unity 프로젝트에만 있다. **저장소가 공개라 커밋하지 않는다**(`.gitignore`). 넣은 것·쓰는 곳·다시 넣는 법은 `docs/design/store_assets.md`. 그 에셋을 쓰는 코드는 클라우드에서 컴파일을 볼 수 없으니 고쳤으면 unity_tasks 에 컴파일 확인을 남긴다.
- **PC 저장소에 커밋 안 된 변경을 남기지 않는다**(2026-11-18): Unity 가 고친 글꼴 에셋·ProjectSettings 가 남아 PC Unity 세션 셋이 연달아 건너뛴 일이 있었다. WebGL 빌드를 하면 Unity 가 글꼴 SDF 둘과 ProjectSettings 를 다시 쓴다. 세션을 끝낼 때(빌드했으면 빌드 뒤에) PC 에서 `git add -u` 를 하고 `git status` 를 본다 — 줄바꿈만 달라진 것은 이걸로 사라지고, 남은 것은 "Unity 저장 뒤 설정"으로 커밋한다.
- 바닥과 지도(2026-11-17): 바닥은 지역마다 다른 타일(`Fx/Biomes.cs` — 원화 타일이 있는 아홉 지역은 지역 바탕빛 위에 원화를 옅게 얹고, 없는 지역은 코드로 그린 타일. 원화가 새로 오면 `Resources/Art/Tiles` 에 넣고 `Biomes.ArtOf` 표에 한 줄), 천하도와 침방 벽 지도는 지역 자료에서 그린다(`Biomes.MakeMap` — 옛 그림 `Art/Map/map_central_plains` 는 쓰지 않고 씬에서도 참조하지 않음, 2026-11-19). 적 몸놀림은 `ClusterMotion` 일곱 갈래.
- `web/prototype/` — 브라우저 프로토타입. 원본은 `template.html` 하나이고 데이터 JSON을 빌드 때 그대로 넣는다. 빌드 결과는 커밋하지 않는다.
  푸시하면 CI(`web-prototype.yml`)가 묶어서 Cloudflare Pages에 올린다(claude/dev는 미리보기 주소, main은 공개 주소 https://bloodrain-proto.pages.dev). 설명은 그 폴더 README.

## 이미지와 이펙트
- 생산·검수·적용 순서는 `docs/design/art_pipeline.md`, 이펙트 모양 규칙은 `docs/design/vfx.md`. 이펙트는 셰이더와 아트를 함께 써서 최대한 예쁘게(2026-11-01 사람).
- **이펙트는 셰이더로만 만든다(2026-11-17 사람: GPT 구독을 해제함)**. 새 이펙트는 `FxInk.shader` 의 식(모양 × 잡음 침식 × 색 램프)과 `tools/bake_brush.py` 로 굽는 텍스처로만 만들고, `art_queue` 에 `{VFX_INK}`·`{VFX_GLOW}` 줄을 새로 넣지 않는다(이미 들여온 재료는 그대로 쓴다). 규칙은 `docs/design/vfx.md` '셰이더로만'.
- 원화(인물·적·배경·소품·컷)는 **Artlist 웹 앱**으로 뽑는다(2026-11-17 사람, ChatGPT 구독 해제). 크레딧이 들지 않는 이미지 모델만 쓰고, 순서는 `docs/design/art_pipeline.md` '이미지 세션이 하는 일 — Artlist 판'. 첫 차례는 시험 넉 장만 뽑고 사람이 그림체를 볼 때까지 멈춘다. 아래 줄의 'ChatGPT 웹'은 이 줄로 바뀐 것이다. Artlist 의 스톡 효과음·음악은 게임에 쓰지 않는다(라이선스, art_pipeline '이용약관 메모').
- 클라우드 세션은 이미지를 생성하지 않는다(이미지 서비스가 막혀 있다). 필요한 이미지는 `art_queue/queue.jsonl` 에 `status: "todo"` 줄로 추가한다. 원화는 `{STYLE}`, 이펙트 재료는 `{VFX_INK}`(흰 바탕 먹)·`{VFX_GLOW}`(검은 바탕 빛)로 시작하고 기획서 '13. 아트 디렉션'의 틀을 따른다. ChatGPT 웹(회사 PC 크롬의 Claude 확장)으로 뽑는다.
- 코드로 되는 이펙트 텍스처(붓 획·먹 튐·자국·노이즈)는 `tools/bake_brush.py` 로 굽는다.
- 같은 캐릭터·같은 계열 적은 같은 `chat` 값을 준다(같은 대화방에서 뽑아야 그림체가 맞는다).
- 이미지에 글자를 굽지 않는다. 모든 글자는 Unity에서 얹는다.
- 배경음은 Gemini '음악 만들기'로 밤 생산 세션이 자동으로 뽑는다(2026-11-04·05). 필요한 곡은 `audio_queue/queue.jsonl` 에 `todo` 줄로, 문구는 `{STYLE}`(`audio_queue/STYLE.txt`)로 시작. 순서·다듬기는 `docs/design/audio_pipeline.md`.
- 효과음은 직접 만든다(2026-11-05 사람). 음이 있는 소리는 코드로 합성, 나머지는 AI 후보 + 녹음 재료로 층을 쌓는다. `docs/design/sfx_research.md`, backlog SFX.
  **2026-11-20 바뀜(사람이 SFX-18a 추천대로)**: 타격·휘두름·발소리는 에셋 스토어에서 사 둔 소리 묶음(Punch Sound Pack·Ultimate Sound FX Bundle·Footsteps Sound Pack 등, `docs/design/store_assets.md`)에서 고르고, 무공 고유음만 직접 만든다. 산 소리 파일도 **공개 저장소에 커밋하지 않는다**(PC 의 .gitignore 폴더에서 쓰고, 고른 목록과 쓰는 곳만 문서에). 이펙트 묶음은 쓰지 않는다(이펙트는 셰이더로만 그대로).

## 글쓰기
- 문서·커밋 메시지·주석은 한국어. 사람이 실제로 말하듯 자연스럽게. 과장 없이.
- 김용·고룡 등 특정 작가의 고유 무공 이름(항룡십팔장, 독고구검 등)은 쓰지 않는다. 장르 공통어나 새로 지은 이름만.

## 하지 말 것
- 비밀값(API 키, 비밀번호, 라이선스 파일)을 커밋하지 않는다. `tools/agent_loop/config.json` 은 .gitignore 에 있다.
- PC의 `ANTHROPIC_API_KEY` 환경변수는 회사 키다. 이 프로젝트에서 쓰지 않는다.
- 확률형 뽑기, 능력치 판매 같은 '힘을 파는 결제'는 어떤 형태로도 만들지 않는다(기획서 16장).
