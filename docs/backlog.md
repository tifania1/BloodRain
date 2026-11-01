# 작업 목록

한 항목은 예약 세션 하나(15~30분)에 끝낼 크기다. 위에서부터 한다. 끝내면 `[x] 날짜 — 한 줄 결과`.
Unity 에디터가 필요한 항목은 `[U]` 표시이고 `docs/unity_tasks.md` 로 옮겨 PC 루프가 맡는다.

## M0 프리프로덕션 (2026-11-02 ~ 11-29)

- [x] 2026-10-28 — 저장소 골격, CLAUDE.md, 데이터 JSON 6종, 코어(경험치·경지·음양·적 증가·스폰 곡선·로드아웃·절기 판정), 테스트 42개
- [x] B-01 2026-10-28 — 기획서 '15. 기술 설계' 폴더 구조와 '18. 자동화' 일정을 실제와 맞춤(원본 문서·gdd.md 둘 다)
- [x] B-02 2026-10-28 — `Core/Build/Stats.cs`: StatKind(14종 + 심법 부가 8 + 연무장 전용 6), 심법·연무장 합산, 쾌속 60% 상한, 연무장 비용 공식. 연무장 표는 balance.json `training` 으로 옮기고 기획서 표와 같은지 테스트
- [x] B-03 2026-10-29 — `RunXp.cs`: 흡수 → 레벨업 횟수·돌파 목록(AbsorbResult), 심득 배율, 남은 진기 이월. 녹색 구슬로 Lv60까지 돌파 4번 테스트
- [x] B-04 2026-10-29 — `UltimateCharge.cs`: 처치·피격·정예에 (1+연무장)×태극1.25 배율, 정진 20(balance.json `perSkip`)·역류 끊기는 배율 없이. yinyang.harmony 를 BalanceDef로 읽게 함
- [x] B-05 2026-10-29 — `Backlash.cs`: 극성 60초마다 3초 역류(이동 -50%), 버티면 최대 기혈 5%, 1초 안 경공으로 끊으면 오의 +50. 연달아 끊은 수는 비전 판정용으로 센다
- [x] B-06 2026-10-29 — `insights.json` 7종과 `Insights.cs` 3택1 추첨(가진 것 빼고, 진소운 4개). 문장·숫자를 기획서 경지표에서 읽어 맞춰 봄
- [x] B-07 2026-10-29 — `core/BloodRain.Sim` 시뮬레이터와 보고서 `docs/design/sim/level-curve.md`. 지금 데이터(프로토 처치 진기)면 5·10·15·20분 Lv16·38·64·89로 목표보다 높다(천하 이관 뒤 다시 잼). 처치 진기를 enemies.json `xp` 로 낮추는 안이 가장 작은 변경(P-02와 같이 결정)
- [x] B-08 2026-10-29 — `chapters.json` 서장·1장: 시간대별 가중치표, 정예 5·10·15분, 돌진·무리·포위, 보부상 12분, 금두꺼비 10~12분 창, 은빛 등롱
- [x] B-09 2026-10-29 — `WaveTable.cs`: 분에 맞는 적 뽑기, 한 판 사건 일정(창·확률은 시드로), 장별 생성 중지·보스 시각을 끼운 SpawnCurve
- [x] B-10 `[U]` 2026-10-30 — PC Unity 세션: 패키지 넷 확인, Boot·Run·Inn 씬(Bootstrap 메뉴), Sim·Platform·Presentation·Editor asmdef, 입력 처리 Both
- [x] B-11 `[U]` 2026-10-30 — PC Unity 세션: `Presentation/DataBoot.cs`, Boot 씬 플레이 때 파일 12개·무공 16종 확인. 뒤따라 Prologue·Life·FinalFight 의 CS8632 경고 6개를 `#nullable enable` 로 없애고 CI에 nullable 끈 빌드 단계를 넣음
- [x] B-13 2026-10-28 — 인트로 컷 이미지 12장을 기획서 원본 19장 '컷 이미지'에 컷 순서대로 붙임
- [ ] B-12 아트 시험 이미지 반영 확인(art_queue의 M0 항목 6개가 imported 되면 Sprite 설정 점검)
- [x] P-01 2026-10-28 — 웹 프로토타입(`web/prototype/`, 1장 숭산 소림 + 원각). 데이터 JSON을 그대로 읽는다. 공개 주소 https://bloodrain-proto.pages.dev
- [x] V-00 2026-10-28 — 무공 비주얼 시안(`web/lookdev/samjae.html`, 삼재검법 1단~절기)과 단계 규칙 `docs/design/vfx.md`
- [x] S-00 2026-10-28 — 시너지 설계(기획서 20장)와 `synergies.json`(연환 16·진전 9문파·비전 8·기연 8), 무결성 테스트 5개
- [x] S-01 2026-10-29 — `Synergy.cs`: 켜진 연환, 문파별 진전 개수·단계·남은 수, 기본의 극(삼재검법 Lv8), 바깥 보너스, 카드 미리보기
- [x] S-02 2026-10-29 — `Secrets.cs`: 판 기록(RunRecord)과 비전 8종 판정. 조건을 synergies.json `secrets[].require` 로 옮김
- [x] S-03 2026-10-29 — `Encounters.cs`: 사건마다 판에 한 번, 기본 확률 + 기연×0.005, 장·시간 제한
- [ ] S-04 `[U]` 레벨업 카드 연환 띠·문파 인장, HUD 진전 인장, 발견 연출(탁본 뒤집기 + 秘傳 낙관), 장경각 도감 화면
- [x] S-05 2026-10-30 — `core/BloodRain.Sim/SynergyCheck.cs`, 보고서 `docs/design/sim/synergy.md`. 무공 6칸 8,008조합 Lv8 보스 화력: 늘 켜진 효과만으로 최대 1.62배(1.3배 넘는 조합 1,184개), 조건부까지 1.92배. 큰 몫은 점혈·쌍검합벽·개방 3단계. 셋을 낮춰 봐도 1.50배
- [x] S-06 2026-11-01 — 비급 해금. 쌍검합벽·점혈창·취중매화·개방 3·곤륜 3 을 synergies.json `tier: secret` 로, 복원 값 `secretManuals`(봉인석 2·3·4·5·5개 지닌 채, 윤회의 기 2,000·3,000·4,500·6,000·8,000). Core `Synergy.Compute(restored)`·`LockedLinks`·`LockedTiers`·`LinkHint.Locked`, `LifeLog.NoteSynergy` → `Soul` 찢긴 비급·`TryRestore`·저장. 시뮬레이터 기본 층 1.29배, 전부 연 뒤 1.90배(`sim/synergy.md`). 기획서 20장 '비급 층과 해금'·10장 장경각. Unity 표시·장경각 화면은 unity_tasks S-04 줄에. 웹 프로토타입은 그대로(비급까지 다 켜진 상태)
- [x] S-07 2026-11-01 — 무명검이 열린다. 뜬 레벨업 카드가 모두 새 무공이면 은자·음식 카드를 한 장 더 붙인다(balance.json levelUp.fallbackWhenAllNewWeapons, Core `Loadout.RollLevelUp`, 웹 프로토타입, 기획서 8장). secrets.md 무명검 0/200 → 200/200(가운데 12.2분). 카드가 한 장 늘어 시뮬레이터 난수가 밀려 보고서 11개 숫자가 조금씩 바뀜(prologue 이류 2:46·일류 6:11·음양 처음 기욺 3:24)
- [x] J-00 2026-10-28 — 절기 그릇 규칙(절정1·초절정2·화경3·현경4, 짝 심법 Lv3, 돌파·보갑 계기)을 Core `Loadout` 과 테스트에, 각성 연출을 웹 프로토타입에(기획서 8장, `docs/design/vfx.md`)
- [x] M-00 2026-10-28 — 강호 지도·회혼과 산공·회귀 설계(기획서 21장)와 `worldmap.json`, 무결성 테스트 9개(2026-10-29 천하 이관 때 worldmap.json 은 지우고 `cheonha.json` 으로 옮김)
- [x] J-01 2026-10-29 — `Awakening.cs`: 긴 3.5초·짧은 1.5초 장면 신호, 파동 판정(잡몹 처치·정예 15%·보스 4%·탄 제거), 여운 8초 1.5배, 생성 2.5초 멈춤, 무적. balance.json `awakening`
- [x] M-01 2026-10-29 — 천하 구조로 바뀌어 하지 않음. 지역·문파 규칙은 T-03~T-08로 옮김
- [x] M-02 2026-10-29 — 천하 구조로 바뀌어 하지 않음(정화도는 없앰). 한 번 깨친 길·회귀는 T-08로 옮김
- [x] M-03 2026-10-29 — 천하 구조로 바뀌어 침방의 중원 지도(unity_tasks TU-12)로 옮김
- [x] M-04 2026-10-30 — `Core/Run/Prologue.cs`: 장삼전 기혈 바닥 → 천지인 삼검 고정 긴 각성 → 처치 → 컷 두 장 → 첫 환혼(소림·개방·남궁). 장삼 전 쓰러지면 처음부터, 각성 전 장삼 기혈 10% 바닥, 각성 뒤 기혈 1. 값은 cheonha.json prologueBody, 테스트 7개. 회혼침 컷 프롬프트는 T-23에서 이미 고쳐져 있었음
- [x] D-01 2026-10-29 — 사람이 천하 구조로 정함(`docs/feedback.md`). 기획서 7·10·21장과 `cheonha.json` 으로 옮기고, 할 일은 아래 T 항목으로 나눔
- [ ] P-02 **사람 결정 대기** — 프로토타입에서만 쓴 값 5개(맞은 뒤 무적, 강시 뛰는 비율, 처치 진기, 원각 체력, 보스전 잡몹 수)를 데이터·기획서로 옮길지. `docs/feedback.md` 에 결정이 적히기 전엔 건너뛴다. 정해지면 JSON·gdd.md·테스트를 같이 고치고 `python tools/build_web_prototype.py` (2026-10-30 사람: 실제로 해 봐야 알 것 같아 그대로 둠. 플레이 결과가 나온 뒤에 정한다. 참고 표: `docs/design/sim/cluster-xp.md`, T-40·T-41 과 묶은 표 `docs/design/sim/decisions.md`)

### 이펙트(2026-11-01 사람: "이펙트는 엄청 필요할 텐데 지금부터 조금씩 해 놔야")
무공 이펙트는 이미지 생성이 아니라 코드로 만든다(`docs/design/vfx.md` '아트 애셋 나눠 맡기'). 그래서 클라우드 세션이 밤에 할 수 있다. 위에서부터 한다.
- [x] V-01 2026-11-01 — `tools/bake_brush.py`(samjae.html brushArc 를 numpy·Pillow 로, 시드 고정, `--check`). 삼재검법 1·2·3단 × 3장을 층별(stroke·wash·gold) 흰 획 + 알파 512px 19장으로 `Assets/Art/VFX/Brush/`, 크기는 `samjae.json` sizeMeters. 미리보기 `docs/vfx_preview/brush_samjae.png`, 시안 페이지 캡처와 견줘 획 모양·마른 붓 끊김이 같음. 테스트 2개
- [x] V-02 2026-11-01 — vfx.md '무공별 획 모양' 표(16종: 호·찌르기·원·점·번짐). bake_brush.py 에 찌르기·번짐을 더해 시작 무공 아홉 × 3단을 구움(삼재검법 3장, 나머지 2장씩, 모두 123장 + 무공별 json). 미리보기 `docs/vfx_preview/brush_start9.png`. 나머지 일곱은 표에 모양만
- [x] V-03 2026-11-01 — bake_brush.py 로 낙관 바탕·먹 튐 6·바닥 먹 자국 4·노이즈 2(이음매 없음)를 `Assets/Art/VFX/Common/` 에, `common.json`, 미리보기 `docs/vfx_preview/vfx_common.png`. 테스트 1개
- [ ] V-04 `[U]` 붓 획 셰이더와 이펙트 틀: `_Progress`(각도 마스크로 획이 그어짐)·`_Dissolve`(노이즈 문턱으로 흩어짐) URP 2D 셰이더, 담채 번짐(흐린 복사본이 커지며 옅어짐), 먹 튐 파티클, 바닥 먹 자국 데칼 풀. unity_tasks 로 옮긴다. 먼저: V-01·V-03 (2026-11-01 unity_tasks 로 옮김)
- [ ] V-05 `[U]` 시작 무공 9종에 이펙트 붙이기: WeaponRunner 가 때리는 자리에 V-04 틀 + V-02 텍스처, 1단 먹 → 2단 담채 → 3단 금빛·잔상(vfx.md 단계 규칙). 무공 하나 끝날 때마다 웹 빌드 화면 한 장. unity_tasks 로 옮긴다. 먼저: V-02·V-04
- [ ] V-06 나머지 무공 7종과 절기 16종 이펙트(V-05 와 같은 방식). 먼저: V-05
- [ ] V-07 이펙트 재료 목록: 무공 16종·절기 16종마다 코드로 안 되는 재료를 `art_queue` 에 `vfx_ink`·`vfx_glow` 줄로 넣는다(`docs/design/art_pipeline.md` '문구': progress·sizeM·use, 주홍 금지). chat 은 '이펙트 재료(먹)'·'이펙트 재료(빛)' 둘 중 하나로 두고 chat_url 은 시범 줄(fx_mist_ink·fx_lightning_glow)과 같게 — 이미지 세션은 chat_url 이 있는 줄만 뽑는다. 먼저 시작 무공 아홉 몫 20줄 안팎(장풍 손바닥 기운, 벽력탄 폭발 먹, 독무 안개 더 둘, 검기 빛, 태극 문양(글자 없이), 낙화검우 꽃잎, 음공 파문, 이화창 찌르기 빛, 뇌부 번개 더 둘). 시범 두 장(fx_mist_ink·fx_lightning_glow)은 2026-11-01 PC 에 받아 둠 (진행: 2026-11-01 대화 세션이 시작 무공 몫 첫 묶음 14줄을 todo 로 넣음. 다음은 나머지 무공 일곱과 절기)
- [ ] V-08 일렁임: V-04 셰이더에 노이즈(Common/noise_1)로 UV 를 흔드는 세기·빠르기를 더해 안개·불꽃이 살아 움직이게. 웹 갤러리(V-09)에 같은 식. 먼저: V-04
- [ ] V-09 이펙트 갤러리 `web/lookdev/vfx_gallery.html`: Brush·Common·AI 텍스처를 셰이더와 같은 식(층 색, 그어짐, 흩어짐, 담채 커지며 옅어짐, 빛 더하기, 일렁임)으로 캔버스에 돌려 무공·단계별로 보여 준다. 느리게 보기. 사람이 휴대폰으로 보는 검수 화면. 텍스처는 빌드 때 같이 싣는다(web/prototype README). 먼저: V-02
- [ ] V-10 bake_brush.py 다듬기: (1) 터럭 한 올을 선분마다 따로 칠하면 이음매가 두 번 겹쳐 구슬 무늬가 생긴다(시안도 같음). 한 올 안에서는 덮임의 최댓값만 남기고 다 그린 뒤 한 번에 얹으면 무늬가 없어진다(진하기는 1−(1−a)² 로 맞추면 시안과 비슷) (2) 512 정사각의 대부분이 비어 휴대폰 메모리·덧그리기가 아깝다. 획이 있는 쪽만 잘라 4의 배수로, json 에 피벗(몸 위치)을 적고 Unity 는 Tight 메시. 바꾸면 테스트·V-04 와 함께. 2026-11-01 대화 세션이 시험해 봄

## 천하 구조 (2026-10-29 결정. 기획서 5·7·10·21장, `cheonha.json`)

위에서부터 한다. 서로 기대는 항목은 '먼저:' 로 적었다. 한 세션이 여러 항목을 해도 되지만 항목마다 커밋을 나눈다.

### 이관(먼저 해야 다른 것이 붙는다)
- [x] T-01 2026-10-29 — `chapters.json` chapter1 → `life`(30분, openEnded), 정예 nth 1~5·보부상 12·24분·돌진 8회, 지역 고유 적 localShare 10/15%, `Core/Spawning/RegionSpawns.cs`, spawnTarget 18분 뒤 400 유지. 11분 뒤 새 공통 적 가중치와 20~25분 돌진 수(36·40)는 스스로 정함
- [x] T-02 2026-10-29 — 현경 onlyIn 삭제(모든 생에서 Lv90 현경), 해금 토큰 boss:<지역>·start(처음 무공 9종), 9장 해금 칸과 데이터 맞춤 테스트

### Core 규칙
- [x] T-03 2026-10-29 — `Core/World/Scale.cs`·`Breakthrough.cs`: 배율·대각선·장막·밟힘·쓸림·덩이 사람 수, 돌파 장면 신호. 좌표는 System.Numerics.Vector2(세계 m)
- [x] T-04 2026-10-29 — `Core/World/Regions.cs`: 지도 비율↔m, 서 있는 지역, 두 지역 섞기 농도, 기믹·고유 적·정예 조회
- [x] T-05 2026-10-29 — `Core/World/BossAwakening.cs`: 깨어남·싸움 시작·기혈 식·첫 처치/혈영 보상·봉마곡, 계정 기록은 IBossRecord. 출생지 9곳 × 경지 표 테스트
- [x] T-06 2026-10-29 — `Core/Meta/Rebirth.cs`: 후보 추첨(5,000번 비율 확인), 다시 고르기, 몸 만들기(성질 → StatBonus, 진전 +1), 문파 몸 Loadout 어댑터
- [x] T-07 2026-10-29 — `Core/Meta/RebirthQi.cs`: 한 생 장부·정산·정산 화면 내역, 10장 목표를 XpCurve 로 확인
- [x] T-08 2026-10-29 — `Core/Meta/Soul.cs`: 혼(윤회의 기·연무장·환혼 강화·해금·봉인석·밟은 땅·한 번 깨친 길·회귀), 저장 JSON 왕복, 한 생 일지 LifeLog
- [x] T-09 2026-10-30 — `Core/Spawning/LifeDirector.cs`: 매초 생성(가중치표 + 지역 고유 적, 덩이 크기는 그 순간 경지), 보스전 0.4배, 돌파 1.4초·각성 2.5초 멈춤(긴 쪽), 사건은 멈춤 중에도 제때, 혈마의 그림자 단계(현경·봉인석 부족·30분부터 5분마다). 테스트 18개. 그림자 한 단계의 세기는 T-14 몫
- [x] T-10 2026-10-30 — `Core/Run/Life.cs`: 몸 → LifeLog·Loadout·RunXp·돌파·각성·기혈역류·LifeDirector·땅의 주인을 한데 묶음. Tick(실제 시간, 위치, 적 수)이 세상 시간·지역·싸움 시작·매초 생성을, Absorb 가 돌파와 보스 깨어남을, DefeatBoss·End 가 일지·정산 기록을 맡는다. 가짜 시계 30분 테스트(돌파 6번, 현경, 그림자 1단계) 포함 8개
- [x] T-11 2026-10-29 — `bosspatterns.json`(원각, 프로토 값 옮김)과 `Core/Combat/BossPatterns.cs`(Plan·Tick, 2막). 6장 '땅의 주인 패턴 수치' 새 절
- [x] T-12 2026-10-30 — bosspatterns.json 에 당소혜(독안개 좌우 번갈아·암기 3파·금선지네·2막 분열)와 마천패(갈고리 사슬·물기둥·수귀·2막 역류 파도). 새 칸(waves·pullSeconds·healthShare·rejoinSeconds·alternate 등), 6장 수치 절 둘, 테스트 15개. 숫자는 원각 기준으로 정함. 수귀는 T-33 대기
- [x] T-13 2026-10-30 — bosspatterns.json 에 혈허진인(흑백 구역 피해 2배·태극권·칠성검진 북두칠성·2막 경계 회전)과 낙매검선의 검혼(꽃잎 나선·일섬 뒤 2초 경직·매화검귀·2막 스물네 갈래). 새 칸 damageTakenMultiplier·burstRadius·arrangement·recoverySeconds, 6장 수치 절 둘, 테스트 14개 더. 매화검귀는 T-33 대기
- [x] T-14 2026-10-30 — 설무향(눈보라 시야 0.6배·빙분신 넷·얼음 가시 연쇄·2막 싸움터 축소, 모닥불 곁 절반)과 혁련휘(순간이동 네 번 뒤 내려찍기·붉은 손바닥·혈영 분신·2막 혈월 피해 1.3배)를 bosspatterns.json·6장에. 종장 뼈대 `final`(혈영 일곱 40%·혈의·혈해·진혈마, 진엔딩 컷 10%)과 `Core/Combat/FinalFight.cs`. 혈마 단계 패턴 수치·혈마의 그림자 세기는 아직(아래 T-39)
- [x] T-15 2026-10-29 — `gimmicks.json`·`Core/World/Gimmicks.cs`: 범종·탑림·우물·독안개·물·태극 바닥. 7장 '지형 기믹 수치' 새 절
- [x] T-16 2026-10-30 — gimmicks.json 둘째 묶음 아홉(술독·검각 시렁·낙석·매화 더미·눈보라·빙판·모닥불·혈제단·붉은 달)과 `Core/World/Gimmicks2.cs`, 7장 수치 표 아홉 줄, 테스트 10개. 숫자는 예약 세션이 정함. 대숲·운해·잔도·떠내려가는 배·역류 파도·차오르는 피·부술 수 있는 것은 아직 수치 없음

### 이어서(2026-10-30, 에이전트 보고에서 나온 것)
- [x] T-29 2026-10-30 — 결정 반영: BossAwakening.Tick(깨어날 때 원 안이면 5초 경고, 나가면 미룸, Life 가 씀), 첫 처치 혈정 5, 밟힌 아래 경지 정예 보갑(Scale.CrushedDropsChest), 혈월 보상 1.5배(정산 '혈월' 줄·혈정), 다시 오른 경지 돌파 0.7초(cheonha.json revisitSeconds, 생성 멈춤도 0.7초). 원 0.15배·1.4초 내내 0.05배·숨은 봉마곡 제외·구슬 주운 자리 농도는 이미 맞아 테스트만. 테스트 9개
- [x] T-30 2026-10-30 — '낙매검선의 피' 조건을 '화산 제자로 태어나'로: synergies.json `require.sect`(새 칸), Secrets 판정(몸 기록 id = 문파 id), 20장 표 문장, 테스트(서장 진소운은 안 열림)
- [x] T-31 2026-10-30 — 정예 표·고유 적 찾기를 `Regions.OwnElites`·`Regions.LocalEnemies` 한 곳으로. RegionSpawns 는 그걸 불러 흐름의 '대신 쓰는 땅'만 얹는다. 테스트 그대로 통과
- [x] T-32 2026-10-30 — enemies.json 에 정예 22종(서장 1 + 일곱 땅 × 3)의 기혈·속도·피해·행동 id, family elite, 넉백 0.85. 숭산 셋은 프로토타입 ELITES 값, 나머지는 숭산 차례별 값에 맞춰 정함(땅 세기는 혈운 농도로). 6장 '정예 수치' 표, 테스트 4개
- [x] T-33 2026-10-30 — 고유 적 10종(수귀·수적·흑백 쌍강시·매화검귀·빙강시·설랑·설인·마교도·혈영 분신·혈사자)을 enemies.json 에. 숭산·당가타 넷과 공통 적에 맞춰 정함. 빙강시는 family ice(모닥불 감속). 6장 표에 기혈·속도·피해 칸, 보스 소환 대기 목록 비움, 테스트 2개
- [x] T-34 2026-10-30 — 13장 '용도별 틀'에 환생 카드(세로 2:3)·화면 배경 틀 추가, 지도 틀을 3:2로 고침. art_queue card·background·map 줄이 틀의 고정 문장을 다 담는지 테스트(ArtFrameTests). 곤륜 카드의 붉은 진눈깨비는 틀의 칸 안 변형으로 둠
- [x] T-35 2026-10-30 — 지도 줄은 대화 세션이 끝냄. 남은 둘 추가: `char_mok_sheet`(목 노인 시트, chat '목 노인', 회혼침 컷의 노인과 같은 외형), `props_cheonha_terrain`(나무·집·탑·산·마을 다섯 소품 한 장, chat '지형 소품'). postprocess_art.py 에 props 종류(배경 제거)
- [x] T-36 2026-10-30 — template.html 에 취선봉 `random_arc`(무작위 방향 큰 원호, 휘두르기 1→3회, 범위 +30%, 넉백 7, 절기 팔선과해는 여덟 방향 + 2초 기절)를 넣고 시작 풀에 되돌림. Lv8 취선봉만 들고 90초 자동 판: 60번 휘둘러 피해 15,415, 콘솔 에러 0
- [x] T-37 2026-10-30 — cheonha.json `rebirth.femaleRatio` 0.5·`maleOnlySects` [shaolin], 후보·몸에 Female(성질 뒤에 굴려 난수 순서 유지), 테스트 4개. `leaders.md` 사매판(남녀 말이 갈리는 여섯 줄만), `sects.md` 여덟 문파 여자 몸 묘사 24줄. art_queue 여자 몸 카드 8줄은 넣지 않음: 산문 카드에 사람이 안 나와(No people) 같은 그림을 쓰면 된다고 판단, 사람 확인 필요
- [x] T-38 2026-10-30 — 혈공: cheonha.json `sects[demonic].bloodArt`, `Core/Progression/BloodArt.cs`(진기 × 농도, 더 받은 진기 300마다 음 -1, 천마해체로 시작값), Life 연결(윤회의 기는 구슬 값 그대로), insights.json `demonicInsights` 마공 넷(기획서 예시 둘 + 혈광폭주·심마환영)과 `Insights.RollWithDemonic`(셋 중 하나), 5장 마공 표, 테스트 8개. 마공 효과를 실제 능력치에 붙이는 일은 Unity 쪽 Sim
- [x] T-39 2026-10-30 — 혈마 세 단계 패턴 수치를 bosspatterns.json `final.stages[].set`(땅의 주인과 같은 틀)에: 혈의(혈침 탄막·맥 짚는 손·역병 구름), 혈해(시작부터 피바다 섬 다섯, 혈영 솟음은 shadows 차례로 기혈 10%), 진혈마(혈우 탄막, 30% 아래 간격 3초). 혈마의 그림자 세기는 cheonha.json `lifeEnd.shadow` 와 `World/BloodShadow.cs`(접촉 30 ×1.25, 속도 85 +8 → 125, 적 피해 +0.1배씩, 3단계부터 플레이어보다 빠름), `Life.Shadow`. 6·7장 표, 테스트 11개. 숫자는 예약 세션이 혁련휘 기준으로 정함

### 확인과 밸런스
- [x] T-40 땅의 주인 처치 시간 고치기(사람 결정 2026-11-01: 추천안). 보스 기혈을 '싸움을 건 시각의 기대 화력'(11장 화력 표·시뮬레이터 화력 곡선)에 맞춘다. 20분에 싸우면 지금 기준 체력 그대로, 절정에 깨자마자 싸우면 목표 처치 시간(60~80초)의 약 1.5배가 되게 기울기를 둔다('일찍 덤비면 조금 더 오래'). cheonha.json bosses 식과 7장 '땅의 주인이 깨어나는 규칙' 식, CheonhaDataTests, BossAwakening, 시뮬레이터 cheonha.md·decisions.md 다시 만들기. 처치 진기(P-02)와는 따로 맞춘다(P-02 는 플레이 뒤) — 2026-11-01 기혈 식을 cheonha.json `bosses.healthCurve`(5·10·15·20·30분 → 0.08·0.3·0.67·1·1.31, 11장 메타 초반 화력 ÷ 20분 × 서두름 1.5→1)로. 출생지 첫 싸움 가운데 8.3배 → 3.4배(decisions.md), 숭산 원각 5.3 → 2.2배(치는 시간 0.7 이라 기준 2.1배). 먼 땅의 주인은 기준 체력이 메타 중후반 열이라 6~8배로 남음(그 문파는 보스를 잡아야 열려 실제론 혼이 오른 뒤). 메타 봉인석 7 은 12.0 → 6.5시간(8분 가정, 목표 15~20시간)
- [x] T-41 혈공 세기 고치기(사람 결정 2026-11-01: 추천안). 진기 배율을 혈운 농도 → √농도로, 심마는 두 배 빠르게(더 받은 진기 300 → 150마다 음양 -1). 그 뒤 시뮬레이터로 십만대산 혈공 현경 도달이 기준(진소운·숭산)과 ±1분 안에 드는지 재고, 벗어나면 √ 대신 지수(농도^k)나 심마 값을 조정해 맞춘다. cheonha.json bloodArt, 5장 '마교 몸의 성장', blood-art.md·decisions.md 다시 만들기 — 2026-11-01 √농도로는 현경이 기준보다 3:58 빨라 ±1분 밖이라 지수를 재어 농도^0.25(십만대산 1.44배)로, 심마 150. cheonha.json `xpDensityExponent`·`extraXpPerStep`, Core BloodArt, 5장. 십만대산 현경 기준과 +0:05(decisions.md, 시드 3)·-0:16(blood-art.md, 시드 5). 처치 진기 줄이기 시험에선 -1:56 이라 P-02 뒤 다시 잰다. 지수 0.1·0.2·0.25·0.3 → +1:51·+0:22·+0:05·-1:59
- [x] T-42 혈마의 그림자 고치기(사람 결정 2026-11-01: 추천안). 속도 상한(maxSpeed 125)을 없애고, 5분 단계가 오를 때마다 몸 가까이로 짧게 순간이동(0.5초 예고)한다. 시뮬레이터 shadow.md 에 이 조합을 더해 운룡보 Lv5·'둘 다' 몸도 100분 안에 쓰러지는지 확인하고, 안 되면 단계별 속도 증가(+8)를 키운다. cheonha.json lifeEnd.shadow, 7장 '생이 끝나는 세 가지' 표, 테스트 — 2026-11-01 cheonha.json lifeEnd.shadow 에서 maxSpeed 를 빼고(0 = 상한 없음) `teleportWarningSeconds` 0.5·`teleportGap` 1m, Core `BloodShadow.TeleportsOn`. 시뮬레이터에 0.5초 예고를 넣어 보니 단계마다 +8 그대로도 운룡보 Lv5 66.8~88.0분·'둘 다' 72.5~95.4분에 다 쓰러져 속도 증가는 안 키움. 7장 표·문단, shadow.md, 테스트. Unity 그림자 구현은 unity_tasks C-12
- [x] T-43 서장 흐름 맞추기(사람 결정 2026-11-01: 추천안 (가) + 첫 레벨업 카드 한 장). (1) 기획서 4장 '서장 상세 흐름' 표 시각을 시뮬레이션에 맞춘다(`docs/design/sim/prologue.md`): 첫 레벨업 0:15, 이류 2:45, 일류 6:10, 음양 설명은 '처음 기울 때'(가운데 3:00쯤). 숫자(진기·생성 채움 비율)는 건드리지 않는다(P-02 는 플레이 뒤). (2) 6장 장삼 문장 '화력 300이면 18초쯤'을 '9분 화력 약 210이면 각성까지 35초쯤'으로. (3) 서장 첫 레벨업은 선택지가 무공 하나뿐이게 한다(표의 '고르기만 하면 됨'). Core `Loadout.RollLevelUp` 에 서장 첫 레벨업 규칙(chapters.json prologue 에 값), 웹 프로토타입, 테스트. Unity 레벨업 화면(C-06)은 카드 한 장도 그릴 수 있게 unity_tasks 에 한 줄 (2026-11-01 S-07 뒤 prologue.md 다시 잼: 첫 레벨업 0:15 그대로, 이류 2:46, 일류 6:11, 음양 처음 기욺 3:24) — 2026-11-01 4장 표 0:15·2:45·음양 3:20(처음 기울 때)·6:10, 7장 서장 문단, 6장 장삼 '9분 화력 약 210이면 35초쯤'. chapters.json prologue `firstLevelUpChoices` 1, Core `Loadout.RollFirstLevelUp`(새 무공 한 장, 은자 카드 없음), 시뮬레이터 서장 첫 레벨업. 다시 잰 prologue.md: 이류 2:42·일류 6:21·음양 3:02·장삼 36초. 웹 프로토타입엔 서장이 없어 손대지 않음. Unity 는 unity_tasks C-06b
- [ ] E-02 절기 효과 맞추기(사람 결정 2026-11-01: 추천안). 벽력천화 '5초짜리 불바다', 천뢰검진 '닿은 적에게 낙뢰 연쇄', 만천성지 '치명타 시 작은 폭발'은 기획서대로 evolutions.json params·Core·웹 프로토타입에 넣는다. 칠보단혼무 독안개 반경만 기획서 9장을 3배 → 2배(프로토타입 값)로 고친다. EvolutionParamsTests.기획서와_다른_곳 의 줄을 지운다
- [x] T-17 2026-10-30 — `core/BloodRain.Sim/CheonhaSim.cs`(`--cheonha`): RunSimulator 의 분당 진기·단일 화력으로 Core `Life` 를 0.1초씩 30분 돌림. 깨어난 가장 가까운 땅으로 걸어가 싸움(걸음은 화면 대각선에 비례), 처치 시간 = 기혈 ÷ (단일 화력 × 0.7). 보고서 `docs/design/sim/cheonha.md`: 현경 가운데 20:32(목표 30:00), 출생지 첫 보스전이 목표의 5.6~20배 → T-40. 테스트 2개
- [x] T-18 2026-10-30 — `core/BloodRain.Sim/MetaSim.cs`(`--meta`), 보고서 `docs/design/sim/meta.md`. 혼 하나로 생을 이어(CheonhaSim 한 생 → Soul.EndLife → 가장 싼 연무장부터 삼). 맞는 모형이 없어 생 길이는 '첫 생 버팀 + 연무장 레벨당 0.5분'으로 가정하고 6·8·12분으로 흔듦. 지금 데이터 8분이면 첫 절정 3생, 첫 봉인석 14생(목표 3~6), 봉인석 7은 38생 13.3시간. 곤륜 설무향이 늘 마지막. 테스트 1개
- [x] T-19 2026-10-30 — 덩이는 기혈·진기가 적 하나 값이라 1장 무리 모형 그대로 쓰고, 천하 30분(숭산 소림, Core Life)에서 조정 시험 다섯을 비교한 표 `docs/design/sim/cluster-xp.md`(`--cluster-xp`). 지금 데이터는 20분 Lv89·현경 20:06, '처치 진기 줄이기'(시강·혈인·혈강시·혈무사 1, 목인나한·동갑시 2)는 5·10·15·20·30분 Lv15·29·47·65·91, 현경 29:15로 목표에 가장 가깝다. P-02 결정용

### 웹 프로토타입
- [x] T-20 2026-10-29 — 천하 시안이 빌드 때 cheonha·characters·weapons·enemies·balance 를 받아 돈다: 문파 아홉·해금, 후보 카드(성질 비율 확인), 윤회의 기 정산 내역, 연무장 다섯 항목, 농도 빗줄기, 땅의 주인. 훅 늘림(unlockAll·fight·warp 등)
- [x] T-21 2026-10-30 — 천하 시안(`web/lookdev/cheonha.html`)에 레벨업 카드(레벨 셋마다 석 장: 시안 무공 셋의 단계 + passives.json 심법 일곱, 숫자키 1~3)와 땅의 주인 패턴(bosspatterns.json 시간표, 부채꼴·원 위 소환·동그라미·도는 빛줄기를 그림. 원각은 전부, 다른 보스는 이 모양에 드는 것만)을 붙임. 빌드가 bosspatterns·passives·levels 를 넣는다. 1366·360 스크린샷 확인, 콘솔 에러 0. 카드 간격 3레벨은 스스로 정함
- [x] T-22 2026-10-29 — 1366·360 화면 스크린샷으로 확인하고 HUD 바탕·알림 위치·지명 겹침·봉인석 두 번 세기·NaN 위치를 고침. 콘솔 에러 0

### 아트·이야기
- [x] T-23 2026-10-29 — art_queue 11줄 추가(문파 산문 9·침방 배경·서장 컷), 산공침 컷 → 회혼침, postprocess_art.py 에 card·background 종류
- [x] T-24 2026-10-29 — `docs/story/mok.md` 42줄
- [x] T-25 2026-10-29 — `docs/story/leaders.md` 59줄(마당 대사를 '모를 때·안 뒤' 둘로 나눔)
- [x] T-26 2026-10-29 — `docs/story/boss_cuts.md` 일곱 땅 × 만남 2 + 처치 뒤 4컷, 화산 두 벌
- [x] T-27 2026-10-29 — `docs/story/sects.md` 문파 아홉 소개와 몸 묘사 27개

### Unity(직접 하지 말고 unity_tasks.md 로)
- [x] T-28 2026-10-29 — `docs/unity_tasks.md` TU-01~TU-14 추가, 옛 M-03 닫음

## M1 코어 프로토타입 (11-30 ~ 2027-01-10)

- [x] C-01 `[U]` 2026-10-31 PC Unity 세션(unity_tasks 에 결과) — 플레이어 이동(WASD·패드), 카메라 추적, 경공 대시(0.25초 4m, 무적 0.2초, 2회 충전)
- [x] C-02 `[U]` 2026-11-01 PC Unity 세션(unity_tasks 에 결과) — 적 시뮬레이션: 구조체 배열 + Burst 잡(추적 + 밀어내기), 강시 콩콩 박자 이동, 오브젝트 풀, 적 300마리 60fps 확인
- [x] C-03 `[U]` 2026-11-01 PC Unity 세션(unity_tasks 에 결과) — 격자 공간 해시 충돌(원 대 원), 접촉 피해 0.35초 간격
- [x] C-04 `[U]` 2026-11-01 PC Unity 세션(unity_tasks 에 결과) — 무공 실행기: behavior별 컴포넌트(front_arc, homing_dagger, orbit_slash, aura …) 처음 8종
- [x] C-05 `[U]` 2026-11-01 PC Unity 세션(unity_tasks 에 결과) — 진기 구슬 드롭·흡수, 400개 넘으면 합치기
- [ ] C-06 `[U]` (unity_tasks 로 옮김) 레벨업 화면(비급 책장 카드 3~4장, 다시 고르기·파기·정진) (2026-11-01 Core 쪽 횟수 `LevelUpCharges` 준비함)
- [ ] C-07 `[U]` (unity_tasks 로 옮김) 경지 돌파 연출(흑백 심상 2초) + 깨달음 3택1 (2026-11-01 Core 쪽 돌파 보상 합 `RealmRewards` 준비함)
- [ ] C-08 `[U]` (unity_tasks 로 옮김) 음양 태극 문양 HUD, 기혈역류 연출
- [ ] C-09 `[U]` (unity_tasks 로 옮김) 오의 버튼(진소운 일검), 보갑, 절기 그릇과 각성 연출(J-01·vfx.md), 절기 4종 (2026-11-01 Core 쪽 보갑 `Chest`, 오의 피해 `Ultimates` 준비함)
- [ ] C-10 `[U]` (unity_tasks 로 옮김) 서장 10분 통과: 스폰 디렉터 + 강시 포쾌 장삼 보스 (2026-11-01 Core 쪽 서장 생성 `PrologueDirector` 준비함)
- [ ] C-11 WebGL 빌드 배포 확인(비밀값 5개가 저장소에 들어가 있어야 함 — 사람 몫) (진행: 2026-11-01 대화 세션이 PC Unity로 빌드해 Cloudflare Pages `bloodrain` 의 dev 에 올림 → https://dev.bloodrain.pages.dev , Run 씬부터 열림. 압축은 Brotli 로 바꿈(Gzip 은 Pages 가 두 번 감싸 로딩 실패). PC 명령은 `Assets/Editor/WebGLBuild.cs` 주석. CI 쪽은 Unity 비밀값 3개 대기)
