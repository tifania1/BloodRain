# 사람이 직접 띄우는 클라우드 세션의 규칙

claude.ai/code 에서 사람이 직접 연 세션이 backlog 줄기 하나를 맡을 때 이 순서를 따른다. 예약 세션과 동시에 돌 수 있으니 잠금 대신 '하는 중' 표시로 나눠 갖는다.

## 시작
1. 공개 저장소 https://github.com/tifania1/BloodRain (브랜치 claude/dev). 이 세션에 저장소가 없으면 add_repo(owner tifania1, repo BloodRain, access push)로 붙이고 클론한다. 푸시가 안 되면 막힌 것을 보고하고 끝낸다.
2. 저장소를 받는다.

       git fetch origin && git checkout -B claude/dev origin/claude/dev
       git config core.hooksPath tools/hooks

3. CLAUDE.md 를 읽고 그 규칙을 지킨다. 지킬 것은 아래와 같다.
   - 문서·커밋은 한국어로 쓴다.
   - 데이터 숫자를 바꾸면 기획서 숫자도 같이 맞춘다.
   - 테스트가 통과한 뒤에 푸시한다.
   - force push 하지 않고, main 에 올리지 않는다.
   - 이미지·음악을 직접 만들지 않는다. 필요하면 대기열에 줄을 넣는다.
   - 특정 작가의 고유 명칭을 쓰지 않는다.
   - 힘을 파는 결제는 넣지 않는다.
4. docs/feedback.md 를 읽고, docs/backlog.md 에서 맡은 줄기의 항목을 찾는다.

## 나눠 갖기
- `docs/session_lock.md` 는 잡지 않는다. 대신 맡은 항목 줄 끝에 "(하는 중: 클라우드 11-04 <줄기>)" 를 붙여 먼저 커밋·푸시한다. 예약 세션과 다른 대화 세션은 '하는 중' 항목을 건너뛴다.
- 맡은 줄기 밖의 항목은 건드리지 않는다. 다른 줄기와 겹치는 파일(backlog.md, daily, gdd.md)은 고칠 때마다 `git pull --rebase` 하고 바로 올린다.

## 일하는 법
- 위에서부터 한다. 항목 하나를 끝낼 때마다 아래 명령으로 테스트를 돌린다. dotnet 이 없으면 CLAUDE.md 의 설치 명령을 쓴다.

       export PATH=~/.dotnet:$PATH; dotnet test core/BloodRain.sln

- 테스트가 통과하면 커밋하고 `git pull --rebase origin claude/dev && git push origin HEAD:claude/dev` 로 올린다.
  세션 여럿이 같이 올려서 푸시가 자주 거절된다(테스트를 도는 30초 사이 다른 세션이 먼저 올림). 거절되면 다시 pull --rebase 하고 푸시하기를 열 번까지 되풀이한다. 그래도 안 되면 몇 분 쉬었다 한다.
- 끝낸 항목은 `[x] 날짜 — 한 줄 결과` 로 적는다. 시간이 모자라면 "(진행: 어디까지)" 로 바꿔 둔다.
- 기획서에 없는 새 기획·새 숫자가 필요하면 지어내지 않는다. '사람 결정 대기' 항목으로 적고 추천안 하나를 붙인다.
- 줄기를 다 끝냈고 시간이 남으면, 같은 줄기의 다음 단계를 로드맵(기획서 17장)에서 쪼개 넣고 이어서 한다.

## 끝
- docs/daily/YYYY-MM-DD.md(한국 시간 날짜)에 "### HH:MM 클라우드 세션(<줄기>)" 으로 한 일·정한 것·막힌 것을 덧붙여 올린다.
- 마지막 메시지는 한 일, 정할 것, 막힌 것을 짧게 한국어로 쓴다.
