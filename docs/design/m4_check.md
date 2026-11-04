# M4 점검표(2026-11-04)

기획서 17장 M4 얼리 액세스 준비(6/1~8/15): "화산·곤륜·십만대산과 종장, 문파 9, 엔딩 세 갈래, 메타 전체, 최적화, Steam 업적·클라우드, QA."
이 가운데 세 지역(기믹·고유 적·정예·땅의 주인), 종장, 문파 9의 마지막 셋(화산·곤륜·마교), 엔딩 세 갈래와 회귀·모드를 기획서 ↔ 데이터 ↔ Core ↔ 웹 ↔ Unity 로 맞대 본 것이다. 메타 전체·최적화·Steam·QA 는 뺐다. 칸 읽는 법은 `m3_check.md` 와 같다(웹은 1장 프로토타입 `web/prototype/template.html` 과 천하 시안 `web/lookdev/cheonha.html` 을 따로, Unity 는 `unity_tasks.md` 에 줄이 있는지만).

## 문파 셋(화산·곤륜·마교)

| 항목 | 기획서 | 데이터 | Core | 웹 프로토타입 | 천하 시안 | Unity |
|---|---|---|---|---|---|---|
| 몸(기혈·걸음·음양 시작·시작 무공·오의·해금) | 5장 '한눈에 보기' | cheonha.json sects `huashan`·`kunlun`·`demonic`, 마교 대표 인물 백야는 characters.json `baekya`. 같음(`CheonhaDataTests.문파_표가_데이터와_같다`) | `Rebirth`, 해금은 `Soul` 의 `boss:<지역>` | 없음(진소운만) | 아홉 문파 몸, 땅의 주인 첫 처치로 열림 | TU-11 |
| 고유 능력 | 5장(화산 치명 +10%, 곤륜 3m 안 적 10% 느려짐, 마교 기혈이 낮을수록 공격속도·회복 -50%) | traitEffect 셋(R-04b). 마교 곡선만 없음(R-04c 사람 결정 대기) | `SectTrait`(`EnemySpeedMultiplier`·`HealingMultiplier`) | 없음 | 없음 | R-04U |
| 마교 혈공·마공 | 5장 '마교 몸의 성장: 혈공'·마공 표 | cheonha.json sects demonic `bloodArt`, insights.json `demonicInsights` 넷 | `Progression/BloodArt.cs`, `InsightEffects`(`BloodArtTests`·`BloodArtSimTests`·`InsightEffectsTests`) | 없음 | 없음 | 줄 없음 |
| 오의(일검·천설부·천마해체) | 5장 '오의 상세' | characters.json ultimates. 천설부·천마해체 params 있음(R-05), 일검은 숫자가 배율·관통 무한뿐이라 params 없음 | `Ultimates.HitDamage`(피해만) | 일검만 | 없음 | C-09(일검만) |
| 진전 | 20장 '진전 9문파' | synergies.json schools 아홉(`SynergyDataTests.기획서_20장_진전_표가_schools_와_같다`) | `Synergy`·`SynergyEffects` | 아홉 문파 인장 | 없음 | S-04 |
| 시작 무공(낙화검우·뇌부·혈조) | 9장 | weapons.json. 낙화검우·혈조는 shot·knockback 없음(9장 '손으로 맞춘 뒤 채운다', m3_check 와 같은 사정) | `WeaponGrowth` | 뇌부만 | 없음 | W-01(뇌부). 낙화검우·혈조 줄 없음 |

## 세 지역

| 항목 | 기획서 | 데이터 | Core | 웹 프로토타입 | 천하 시안 | Unity |
|---|---|---|---|---|---|---|
| 자리·농도·봉인석·땅의 주인 | 7장 '지역과 혈운 농도' | cheonha.json regions 화산 2.8·곤륜 설원 3.5·십만대산 4.3(`CheonhaDataTests.지역표가_데이터와_같다`) | `Regions`, `RegionSpawns` | 없음 | 셋 다 있고 땅의 주인이 깨어남 | TU-09 |
| 화산 낙석·매화 더미 | 7장 '지형 기믹 수치' | gimmicks.json `fallingRocks`·`plumHeaps`(`Gimmicks2Tests.수치_표가_데이터와_같다`) | `FallingRocksGimmick`·`PlumHeapGimmick` | 없음 | 없음 | 줄 없음 |
| 화산 좁은 잔도 | 지역 표에 이름만. 수치 문단 '잔도는 아직 수치가 없습니다' | regions 에 `cliff_path` 만 | 없음 | 없음 | 없음 | 줄 없음 |
| 곤륜 눈보라·빙판·모닥불 | 7장 | gimmicks.json `blizzard`·`iceFloor`·`campfire`(같은 테스트) | `WeatherGimmick`·`IceFloorGimmick`·`CampfireGimmick` | 없음 | 없음 | 줄 없음 |
| 십만대산 혈제단·붉은 달 | 7장 | gimmicks.json `bloodAltars`·`redMoon`(같은 테스트, `붉은_달은_180초부터_3분마다_30초`) | `BloodAltarGimmick`, 붉은 달은 `WeatherGimmick` | 없음 | 없음 | 줄 없음 |
| 기믹을 어디에 몇 개 | 혈제단만 '네 개'. 낙석은 플레이어 둘레라 자리가 필요 없음. 매화 더미·모닥불 수와 자리는 없음 | - | 놓는 함수는 탑림 `PagodaForest.Layout` 뿐 | - | - | - |
| 고유 적 일곱(매화검귀 / 빙강시·설랑 떼·설인 / 마교도·혈영 분신·혈사자) | 6장 '지역별 고유 적'과 아래 문단 | enemies.json chapter5~7(`EliteDataTests.지역별_고유_적_표의_모든_줄에_데이터가_있고_수치가_같다`, `EnemyTableTests.지역_고유_적_행동_숫자_표가_데이터와_같다`) | `EnemyDef`, 혈영 분신은 `Mimicry`(`MimicryTests`) | 없음 | 없음 | C-02b 에 일곱 행동 줄 없음 |
| 정예 아홉 기혈·속도·피해 | 6장 '정예 수치' | enemies.json elites chapter5~7(`EliteDataTests.정예_수치_표가_데이터와_같다`) | `RegionSpawns` 차례 | 없음 | 큰 일반 적 | 줄 없음 |
| 정예 아홉 패턴 | 6장 '정예 패턴 수치'가 '화산·곤륜 설원·십만대산 아홉은 아직' | bosspatterns.json elites 에 없음(열셋만) | - | - | - | - |
| 땅의 주인 셋 시간표·수치 | 6장 땅의 주인 표·'땅의 주인 패턴 수치' | enemies.json bosses, bosspatterns.json bosses(`BossPatternsMoreTests.패턴_수치_표가_데이터와_같다`·`검혼_일섬_뒤_2초_경직이_다음_패턴_전에_끝난다`·`설무향_싸움터는_모닥불_곁에서_천천히_줄고_혁련휘_혈월은_모든_적을_강화한다`) | `BossPatterns` 시간표·2막, 혁련휘 분신은 `Mimicry`(`혈영회주_분신_값이_기획서와_같다`) | 원각만 | 싸움은 되지만 일섬(`line`)·붉은 손바닥(`circles`)·소환만 나옴. 나선·방사·눈보라·빙분신·얼음 가시·축소 원·순간이동·혈월은 모양을 몰라 건너뜀 | 줄 없음 |
| 땅의 주인 셋 모양 판정 | 같은 표(나선 세 갈래 초당 120°·0.1초, 스물네 갈래 파마다 3.75°, 빙분신 넷 6m·진짜만 그림자·한 대에 깨짐·8초, 얼음 가시 여덟 0.12초·12m, 축소 원 20m→8m 초당 0.25m·모닥불 곁 절반, 순간이동 5m 안 네 번 0.4초, 혈월 1.3·1.2배) | 칸은 다 있음(`spiral_bullets`·`radial_burst`·`view_shrink`·`clones`·`chain_circles`·`shrinking_arena`·`blink_slam`·`empower`) | R4-02(11-04) 로 판정 함수: `SpiralShotDegrees`·`RadialBurstDegrees`·`ViewScaleAt`·`BossClones`·`ChainCircleOffset`·`InChainCircle`·`ArenaRadiusStep`·`OutsideArena`·`BlinkOffsets`·`BlinkSlamSeconds`·`EmpowerDamage`(`M4BossRulesTests`) | - | - | - |

## 종장(봉마곡 혈지)

| 항목 | 기획서 | 데이터 | Core | 웹 프로토타입 | 천하 시안 | Unity |
|---|---|---|---|---|---|---|
| 혈영 일곱 → 세 단계 | 6장 '종장 보스' | bosspatterns.json final(`shadowHealthRatio` 0.4, shadows 일곱, stages 셋), enemies.json `danmok_hyeon` phases(`FinalStagesTests.단계_셋의_기혈이_enemies_json과_기획서와_같다`) | `Combat/FinalFight.cs`(차례·기혈·단계 시간표·진엔딩 컷 `CheckTrueEnding`, `FinalFightTests`) | 없음 | 봉인석 일곱이면 봉마곡이 열리고 단목현 하나와 싸움(혈영·단계 없음) | 줄 없음 |
| 단계 패턴 수치 | 6장 '단계 패턴 수치' | final stages[].set(`FinalStagesTests.단계_패턴_표가_기획서와_같다`) | 시간표는 `BossPatterns`. 피바다 섬 크기는 테스트(`피바다_섬은_90초쯤에_바닥에_닿는다`)만 셈 | - | - | - |
| 단계 모양 판정 | 같은 표(맥 짚는 손 고리 9m→1.5m 초당 3.75m·폭 1.2m, 역병 구름 셋 10m 안·속도 25·12초, 섬 다섯 7m→2.5m 약 90초, 혈우 탄막 12m 안 14개 × 5파·0.6초, 혈영 솟음 10%·20초) | 칸은 다 있음(`closing_ring`·`drifting_clouds`·`shrinking_islands`·`rain_drops`·`summon_shadow`) | 고리 반경은 `ClosingRadius` 로 셈이 되고, 혈영 솟음 차례·기혈은 `FinalFight.RisingShadow`·`RisingShadowHealth`. 섬 자리·반경과 섬 위 판정, 구름 떠다님, 핏방울 자리는 없음 → R4-03 | - | - | - |
| 차오르는 피(기믹) | 7장 지역 표 '피가 차올라 안전 지대가 줄어든다', 수치 문단은 '아직 수치가 없습니다' | regions 봉마곡 `rising_blood`. 수치는 종장 혈해 `rising_blood_sea` 에 있음 | - | - | - | - |

## 엔딩 세 갈래·회귀·모드

| 항목 | 기획서 | 데이터 | Core | 웹 프로토타입 | 천하 시안 | Unity |
|---|---|---|---|---|---|---|
| 회귀(봉인 엔딩 뒤) | 21장 '회귀(혈월 천하)' | cheonha.json cycle(`resets` 봉인석, `keeps`) | `Soul.Regress`·`Cycle`·`IsBloodMoon`(`SoulTests`) | 없음 | 봉하면 생이 끝남(회귀 없음) | 줄 없음 |
| 혈월 천하 배율 | 7장 '모드'·21장 | cycle.bloodmoon(적 기혈 1.5·속도 1.15·보상 1.5) | `LifeLog.RewardMultiplier`(`BloodmoonLifeTests.기획서_21장_회차표와_같다`) | 없음 | 없음 | 줄 없음 |
| 혈월 천하 땅의 주인 새 패턴 | 7장 '모드' 칸에 말만. 17장 '밀릴 때 줄일 것'에 '대사 한 줄만' | 없음 | 없음 | - | - | - |
| 회차마다 고를 수 있는 엔딩 | 4장 '엔딩 세 갈래'(1회차 봉인만, 2회차 셋) | cycle `endingsFirst`·`endingsSecond` | `Soul.Endings` 목록만. 고른 엔딩을 혼에 옮기는 곳이 없음(봉인 → 회귀, 계승 → 혈마 모드 열림, 진엔딩 → 환혼·회귀 끝) → R4-04 | 없음 | 없음 | 줄 없음 |
| 진엔딩 조건 | 4장(의서 일곱 권, 목 노인 대화 끝까지, 서로 다른 문파 4곳 이상으로 종장), 10장 화폐 표 '7권' | `trueEndingDistinctSects` 4 는 있고 의서 권 수(7)는 없음 | `Soul.TrueEndingSectsMet`·`Manuscripts` 는 있음. 셋을 묶는 판정이 없음 → R4-04. 목 노인 대화 줄기는 `docs/story/mok.md` 끝에 '이 풀에 없다' | - | - | - |
| 의서가 나오는 확률 | 4장·7장·10장 '낮은 확률' | chapters.json 은빛 등롱(15~17분 하나, 17~19분 0.5). 의서 확률 칸 없음 | `LifeLog.FindManuscript`(혈월 천하에서만 셈) | - | - | - |
| 일일 도전·보스 연전 열림 | 7장 '모드'(일일 도전 봉인석 3개, 보스 연전 봉인 엔딩) | 없음 | 없음(같은 시드면 같은 후보만 `Rebirth`·`Insights`) → R4-05 | - | - | - |

## 걸리는 것

- ~~**땅의 주인 셋의 모양 판정이 Core 에 없다.**~~ → 2026-11-04 R4-02 로 Core 에 넣음(웹 R4-02w, Unity R4-02U). 수치는 6장과 bosspatterns.json 에 다 있다. 꽃잎 나선 방향, 스물네 갈래 엇갈림, 빙분신 자리와 진짜 하나, 얼음 가시 차례 자리와 시각, 줄어드는 싸움터 반경(모닥불 곁 절반), 순간이동 네 자리, 혈월 배율. 당소혜·마천패·혈허진인 때(R-11)처럼 `Combat/BossPatterns.cs` 에 더하면 웹·Unity 가 같은 셈을 쓴다 → R4-02
- **종장 단계 모양 판정이 덜 있다.** 피바다 섬 다섯의 자리와 반경, 섬 위에 서 있는지, 역병 구름 위치, 혈우 핏방울 자리. 수치는 6장 표에 있다 → R4-03
- **엔딩을 고른 뒤가 없다.** `Soul.Endings` 는 고를 수 있는 목록만 준다. 봉인은 `Regress`, 계승은 혈마 모드 해금 기록, 진엔딩은 환혼·회귀를 닫는 기록이 필요하다. 진엔딩 조건 셋을 묶는 판정도 없고 의서 일곱 권은 데이터 칸이 없다 → R4-04. 목 노인 대화를 '끝까지' 본 것이 무엇인지는 대사 줄기가 없어 정하지 못한다(사람 결정 R4-08)
- **모드 열림 조건이 데이터에 없다.** 일일 도전(봉인석 3개)·보스 연전(봉인 엔딩) → R4-05. 일일 도전 시드 규칙·리더보드는 Steam 몫이라 뺀다
- **정예 아홉 패턴 수치가 없다.** 기획서가 '아직'이라 적었다. M3 아홉은 사람이 고른 R-01 이 '같은 차례 숭산 정예 기준'으로 정했다 → 사람 결정 R4-06
- **잔도 수치가 없다.** 7장이 '아직'이라 적었다. 매화 더미·모닥불을 몇 개 어디에 놓는지도 없다(M3 의 R-10 과 같은 사정) → 사람 결정 R4-07
- **의서 확률과 목 노인 진엔딩 대화가 없다** → 사람 결정 R4-08
- 혈월 천하 땅의 주인 새 패턴은 기획서에 말만 있다. 17장이 밀리면 '대사 한 줄만'으로 줄인다고 했으니 얼리 액세스 뒤로 미뤄도 된다 → 사람 결정 R4-09
- 7장 지형 기믹 수치 문단이 '차오르는 피는 아직 수치가 없다'고 적었는데, 종장 혈해의 차오르는 피바다(6장 단계 패턴 표)가 같은 것이다. 7장 문장을 고쳤다(2026-11-04)
- 웹 천하 시안은 화산·곤륜·십만대산의 기믹·고유 적·정예가 없고, 세 땅의 주인 패턴은 절반쯤 건너뛰며, 종장은 단목현 하나뿐이다. 1장 프로토타입은 숭산만이다. Core 판정(R4-02·03)이 생긴 뒤에 붙이는 게 순서다(R-11 → R-11w 처럼)
- Unity 는 세 지역 기믹, 고유 적 일곱 행동, M4 땅의 주인 셋, 종장, 엔딩·회귀 화면 모두 unity_tasks 에 줄이 없다. Core 가 생기는 항목마다 'U' 줄을 단다

## 이미 맞는 것

- 문파 셋 몸·해금·고유 능력(마교 곡선 빼고)·혈공·마공·진전, 지역 표, 기믹 일곱(낙석·매화 더미·눈보라·빙판·모닥불·혈제단·붉은 달) 수치, 고유 적 일곱, 정예 아홉 기혈·속도·피해, 땅의 주인 셋 시간표, 종장 차례·단계 기혈·단계 패턴 수치, 혈마의 그림자, 혈월 천하 배율은 위 칸에 적은 테스트가 기획서 표와 데이터를 맞춰 본다. 숫자가 서로 다른 곳은 찾지 못했다
