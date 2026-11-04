# M3 점검표(2026-11-04)

기획서 17장 M3 콘텐츠 확장(3/1~5/31): "당가타·장강·무당산 지역과 땅의 주인 셋, 문파 6, 무공 16·절기 16, 데모 빌드, 현지화 1차(한·영·간체)."
이 가운데 문파·무공·절기·세 지역(기믹·고유 적·정예·땅의 주인)을 기획서 ↔ 데이터 ↔ Core ↔ 웹 ↔ Unity 로 맞대 본 것이다. 데모 빌드·현지화는 뺐다. 웹은 1장 프로토타입(`web/prototype/template.html`, 숭산·진소운만)과 천하 시안(`web/lookdev/cheonha.html`)을 따로 본다. Unity 칸은 `unity_tasks.md` 에 줄이 있는지만 적는다.

## 문파 6

| 항목 | 기획서 | 데이터 | Core | 웹 프로토타입 | 천하 시안 | Unity |
|---|---|---|---|---|---|---|
| 몸(기혈·걸음·음양 시작·시작 무공·오의) | 5장 '한눈에 보기' | cheonha.json sects 여섯, characters.json 당연화·하연·운화. 같음(`CheonhaDataTests.문파_표가_데이터와_같다`·`대표_인물이_있는_문파는_그_인물의_몸과_같다`) | `Rebirth`(후보·몸), 해금은 `Soul` 의 `boss:<지역>` 토큰 | 없음(진소운만) | 아홉 문파 몸의 기혈·걸음, 땅의 주인 첫 처치로 문파가 열림. 음양 시작은 카드에 흐리게만 | TU-11 |
| 고유 능력 | 5장 칸(소림 피해 -2, 개방 은자 +30%·음식 +50%, 남궁 기혈 50% 이상 공력 +25%, 당가 독 무효·중독된 적 +20%, 수로채 움직이는 동안 회피 15%·물 감속 무시, 무당 태극 ±40·조화 보너스 2배) | sects `traitEffect`(아홉 문파, R-04·R-04b 2026-11-04). 마교 '기혈이 낮을수록 공격속도' 곡선만 없음(R-04c 사람 결정 대기). 무당 태극 구간은 `harmonyRange` 40 | `Build/SectTrait.cs`(능력치 덧셈·독 무효·물 무시·중독 피해 배율·조화 보너스 배수), `RebirthBody.SectTrait`·`ComputeStats(…, healthRatio, moving)`, `Life.CurrentStats`·`Life.HarmonyCooldownReduction`, `UltimateCharge.HarmonyBonusMultiplier`, `StatKind.Evade`·`Evade.Roll`(`SectTraitTests` 16). 시뮬레이터 소림 호신도 데이터에서 읽음 | 없음 | 없음 | R-04U |
| 오의(만천화우·역류·태극무량, 나머지 셋 포함) | 5장 '오의 상세' | characters.json ultimates: `mightMultiplier` 와 `params`(R-05 2026-11-04: 시간·반경·곳·방향·연타·천마해체 비율, `UltimateParamsTests` 가 5장 칸과 맞춤). 일검(숫자 없음)·역류(R-05b 사람 결정 대기)만 params 없음 | `Ultimates.HitDamage`(피해), `UltimateCast`(박자·다섯 오의 모양, R-15)·`UltimateEffects`(천설부·천마해체, R4-10). 빈 숫자는 R-15b | 일검만(`useUlt`) | 없음 | C-09(진소운 일검), R-15U(다섯), R4-13(천설부·천마해체) |
| 진전 | 20장 '진전 9문파' | synergies.json schools. 여섯 문파 단계 글·인원이 표와 같음(`SynergyDataTests.기획서_20장_진전_표가_schools_와_같다`, R-06). 소림 4·개방 4 는 effect `arhat_ring`·`beggar_allies`(R-07) | `Synergy.Compute`, `SynergyEffects`(effect 13), 판 상태는 `SchoolSummons`. 수로채 4 는 `Chest` 의 `chestUpgrades` | 아홉 문파 인장, proto 단계 효과 11 다 씀. 태극검·쇄겸술·이화창이 없어 무당·수로채는 심법 둘(+기본의 극)까지만 | 없음 | S-04(표시), 몸에 얹기는 C-06 '남은 것' |

## 무공 16·절기 16

| 항목 | 기획서 | 데이터 | Core | 웹 프로토타입 | 천하 시안 | Unity |
|---|---|---|---|---|---|---|
| 무공 성장 | 9장 '공격 무공 16종' | weapons.json 16. 같음(`WeaponGrowthTests` 넷, `DataIntegrityTests.공격_무공_해금_칸이_데이터와_같다`) | `WeaponGrowth`·`WeaponMath`(성장 값). 맞히는 판정은 Unity·웹 몫 | behavior 아홉(`PROTO_BEHAVIORS`). 태극검·쇄겸술·혈조·음공·이화창·뇌부·낙화검우 없음 | 시안 무공 셋(검기·도는 검·먹 파동), 데이터 무공 아님 | WeaponRunner 아홉 갈래(처음 8 + 뇌부 W-01). 취선봉 `random_arc` 와 태극검·쇄겸술·혈조·음공·이화창·낙화검우는 줄 없음 |
| 날아가는 것·넉백 | 9장 표, 여섯 무공 비어 있음('손으로 맞춘 뒤 채운다') | 태극검·쇄겸술·혈조·음공·이화창·낙화검우에 shot·knockback 없음(`WeaponShotTests.프로토타입에_없는_무공_개수가_문장과_같다`) | - | - | - | - |
| 해금 조건 | 9장 표(음공 한 판 5,000 처치, 이화창 한 판 경공 300, 뇌부 정신부 누적 10, 태극검·쇄겸술 땅의 주인 첫 처치) | weapons.json `unlock` 같음 | `boss:` 는 `Soul` 이 판정. 도전 과제 셋은 `Soul.Unlock(token)` 로 받기만 하고, 세는 곳 없음 | 처음부터 쓰는 아홉만 | - | 줄 없음 |
| 심법 16 | 9장 '심법 16종' | passives.json 16. 같음(`GddTablesTests.심법_표가_passives_json_과_같다`) | `Stats` | 16 다 | 몸 능력치에 닿는 심법만 카드로 | C-06 '남은 것'(심법 능력치 얹기) |
| 절기 16 | 9장 '절기 16종'·'절기로 바뀐 뒤 날아가는 것' | evolutions.json 16, params 는 열다섯(프로토타입 아홉 + 오뢰정법 + R-09 다섯은 효과 칸 숫자만). 흑룡탄만 params 없음(`EvolutionParamsTests` 넷) | `Awakening`(문턱·그릇), `EvolutionDef.Param` | 아홉 | 없음 | J-01U(각성 연출) 끝, C-09(절기 4종), C-09c(셋 효과), W-01(오뢰정법). 나머지 줄 없음 |

## 세 지역

| 항목 | 기획서 | 데이터 | Core | 웹 프로토타입 | 천하 시안 | Unity |
|---|---|---|---|---|---|---|
| 자리·농도·봉인석·땅의 주인 | 7장 '지역과 혈운 농도' | cheonha.json regions 당가타 1.3·장강 1.7·무당산 2.2. 같음(`CheonhaDataTests.지역표가_데이터와_같다`) | `Regions`, `RegionSpawns` | 없음(숭산만) | 셋 다 있음, 땅의 주인이 깨어나고 싸움이 됨 | TU-09(지역 HUD) |
| 당가타 독안개 | 7장 지역 표·'지형 기믹 수치' | gimmicks.json `poisonFog`(id `poison_fog`). 같음(`GimmicksTests.지형_기믹_수치_표가_데이터와_같다`·`독안개는_90초마다_4초에_퍼져_20초_머물고_4초에_걷힌다`) | `PoisonFogGimmick` | 없음 | 없음 | 줄 없음 |
| 당가타 대나무 덤불 | 지역 표 '대나무 덤불', 수치 문단 '대숲'. 수치 없음 | regions 에 `bamboo_thicket` 만, gimmicks.json 에 없음 | 없음 | 없음 | 없음 | 줄 없음 |
| 장강 물 | 7장 | gimmicks.json `water`. 같음(`물에선_플레이어든_적이든_0점7배이고_나는_적은_그대로`·`경공을_쓰고_3초는_수상비로_물에서_느려지지_않는다`) | `WaterGimmick`·`WaterWalk` | 없음 | 없음 | 줄 없음 |
| 장강 떠내려오는 배·역류 파도 | 지역 표에 이름과 '2분마다 역류 파도'만. 수치 없음 | regions 에 `drifting_boats`·`reverse_wave` 만, gimmicks.json 에 없음 | 없음 | 없음 | 없음 | 줄 없음 |
| 무당산 태극 바닥 | 7장 | gimmicks.json `taijiFloor`. 같음(`태극_바닥은_게이지와_반대_속성_무공을_30초_동안_30퍼센트_올린다` 외 셋) | `TaijiFloorGimmick`·`TaijiBlessing` | 없음 | 없음 | 줄 없음 |
| 무당산 운해 | 지역 표에 이름만. 수치 없음 | regions 에 `cloud_sea` 만, gimmicks.json 에 없음 | 없음 | 없음 | 없음 | 줄 없음 |
| 고유 적(독두꺼비·금선지네·독인 / 수귀·수적 / 흑백 쌍강시) | 6장 '지역별 고유 적'과 아래 문단 | enemies.json chapter2·3·4. 같음(`EliteDataTests.지역별_고유_적_표의_모든_줄에_데이터가_있고_수치가_같다`, `EnemyTableTests.지역_고유_적_행동_숫자_표가_데이터와_같다`·`쌍강시_되살림_숫자가_기획서_문단과_같다`) | `EnemyDef`, `RegionSpawns`(5분 10%·15분 15%) | 없음 | 없음(모든 적이 한 모양) | C-02b: 독두꺼비 장판·지네·독인 안개·수적 갈고리·쌍강시 되살림은 붙음, 수귀만 '남은 것'(물 지형) |
| 정예 아홉 | 6장 '정예 수치'·'정예 패턴 수치' | enemies.json elites chapter2~4, bosspatterns.json elites 아홉. 같음(`EliteDataTests.정예_수치_표가_데이터와_같다`, `ElitePatternsTests.당가타_장강_무당산_정예_아홉_표가_데이터와_같다`·`장판_값은_같은_땅_적에서_왔다`) | `BossPatterns`(`CastSeconds`·`ClosingRadius`·`RingOffsets`·`InPool`), `RegionSpawns` 차례 | eliteAI 는 `jump_slam`·`projectile_fan`·`circles`(self) 셋만 알고 숭산 셋만 부름 | 데이터 정예가 아니라 24초마다 큰 일반 적 | R-01U(먼저 E-01) |
| 땅의 주인 셋(당소혜·마천패·혈허진인) | 6장 땅의 주인 표·'땅의 주인 패턴 수치' | enemies.json bosses, bosspatterns.json bosses. 같음(`BossPatternsMoreTests.패턴_수치_표가_데이터와_같다` 외 다섯) | `BossPatterns` 시간표·2막, `BossAwakening` 기혈 배율. R-11(11-04)로 판정 함수: `SplitFieldColor`·`SplitFieldDamageTaken`(흑백·경계 회전), `BossSplit`(분열 되살림), `BigDipperOffsets`, `InhalePull`·`InBurst`, `ChainReach`·`InChain`·`ChainPullOffset`, `HalfFieldFog`(`BossFieldRulesTests`) | 원각만(bossAI 가 원각 숫자를 코드에 둠) | 셋 다 싸움은 되지만 패턴은 shape 넷(`cone`·`summon_ring`·`circles`·`rotating_beams`)만 앎. 마천패 물기둥, 혈허진인 칠성검진(북두칠성 대신 흩뿌린 원)만 나오고 당소혜는 몸 박치기뿐. 2막 없음 | 줄 없음(C-10 은 장삼만, 원각도 줄 없음) |

## 걸리는 것

- **문파 고유 능력이 Core 에 없다.** 소림·개방·남궁·당가·수로채 다섯과 무당 '조화 보너스 2배'. 기획서 5장에 숫자가 있으니 cheonha.json sects 에 효과 칸을 더하고, Core 몸(`Meta/Rebirth.cs` 의 몸 능력치)과 기믹 인자(`poisonImmune`·`ignoresWater`)에 잇는다. 수로채 회피 15% 는 `Build/Stats.cs` `StatKind` 에 회피 칸이 없어 그것부터 필요하다. Unity 는 unity_tasks 에 줄 없음
- **오의 모양 수치가 글로만 있다.** characters.json ultimates 에 5장 '오의 상세' 숫자(만천화우 3초·중독, 태극무량 5초, 사자후 6m·3초, 천뢰낙 12곳·1초, 팔방풍우 8방향 3연타, 천설부 3초, 천마해체 8초·2%·2배·10%)를 칸으로. 역류는 '공력 x12' 말고 물살 폭·시간이 없다: 숫자 없음, 사람 결정 필요. Unity 는 C-09 가 일검만 적고 나머지 오의 줄 없음
- ~~진전 소림 4·개방 4 효과가 없다~~ → 2026-11-04 R-07: `arhat_ring`·`beggar_allies` + Core `SchoolSummons`. 허상이 밀쳐 내는 세기·둘레, 개방 제자의 피해·기혈은 숫자 없음, 사람 결정 R-07b
- ~~기획서 20장 진전 표 ↔ synergies.json schools 를 맞추는 테스트가 없다~~ → 2026-11-04 R-06: `SynergyDataTests.기획서_20장_진전_표가_schools_와_같다`
- **도전 과제 해금 셋을 세는 곳이 없다.** 음공(한 판 처치 5,000)·이화창(한 판 경공 300)·뇌부(정신부 누적 10)는 토큰만 있다. 한 판 기록(`Run/Life.cs` 쪽)과 혼에 쌓는 누적(`Meta/Soul.cs`)에서 세어 `Soul.Unlock` 을 부르게
- **무공 여섯의 날아가는 것·넉백 값이 없다**(태극검·쇄겸술·혈조·음공·이화창·낙화검우, 9장 표·weapons.json `shot`·`knockback`). 기획서는 프로토타입에서 손으로 맞춘 뒤 채운다고 했다 → WP-01. 그 전까지는 숫자 없음, 사람 결정 필요
- ~~**절기 여섯에 params 가 없다.**~~ → R-09(11-04) 다섯은 옮김, 흑룡탄은 숫자가 없어 그대로. 9장 효과 칸에 숫자가 있는 것은 evolutions.json params 로 옮기면 된다(태극양의검 검 8자루·태극 +30%, 혈마수 흡혈 4%·극음 범위 2배, 파천금음 최대 3배·기절 30%, 백화난무창 5방향, 매화만개 기혈 0.2). 흑룡탄과 여섯의 탄속·반경·수명은 숫자 없음, 사람 결정 필요. 넣으면 `EvolutionParamsTests.효과_칸의_숫자가_params_와_같다` 에 더한다
- Unity 무공 일곱(`random_arc`·`orbiting_blades`·`pull_chain`·`forward_claws`·`ring_wave`·`pierce_thrust`·`petal_scatter`)과 그 절기 모양은 unity_tasks 에 줄 없음(취선봉은 C-04b 에 '넣을 때' 한 마디뿐)
- **기믹 넷의 수치가 없다.** `bamboo_thicket`·`drifting_boats`·`reverse_wave`·`cloud_sea` 는 cheonha.json regions 에만 있고 gimmicks.json·Core 에 없다. 7장도 '아직 수치가 없다'(역류 파도는 '2분마다'만). 숫자 없음, 사람 결정 필요. 마천패 2막 역류 파도의 '배 위면 맞지 않음'이 떠내려오는 배에 걸려 있다. 이름도 지역 표는 '대나무 덤불', 수치 문단은 '대숲'
- **기믹을 지역 안 어디에 몇 개 놓는지가 없다.** 독안개 샘 수, 물 구역의 모양·넓이(수귀는 물에서만 솟는다), 태극 바닥 수. Core 에 놓는 함수는 탑림 `PagodaForest.Layout` 뿐이다(숭산 범종·우물도 같다). 숫자 없음, 사람 결정 필요
- Unity 지역 기믹은 unity_tasks 에 줄 없음(Presentation·Sim 에 `Gimmicks` 를 부르는 곳 없음). 수귀는 C-02b '남은 것'인데 물 지형이 먼저다
- ~~**땅의 주인 셋의 모양 판정이 Core 에 없다.**~~ → R-11(11-04) Core 에 넣고 웹 천하 시안도 같은 셈으로(R-11w). 흑백·흑백 경계(내 색, 경계 회전), 분열 되살림(`rejoinSeconds`), 칠성검진 `big_dipper` 자리, 태극권 끌어당김, 갈고리 사슬 끌기, 반쪽 독안개 쪽 고르기. `Combat/BossPatterns.cs` 에 `RingOffsets`·`InPool` 처럼 더하면 Unity·웹이 같은 셈을 쓴다. Unity 는 땅의 주인(원각 포함) 줄이 unity_tasks 에 없음
- 웹: 천하 시안에 세 지역 기믹·고유 적·데이터 정예·세 보스 패턴 대부분·2막이 없고, 1장 프로토타입은 숭산만이다. 1장 bossAI 는 원각 숫자를 코드에 박아 두어 다른 보스를 붙이려면 eliteAI 처럼 bosspatterns.json 을 읽게 해야 한다 → WP-01
- bosspatterns.json 마천패 `_note` 의 "수귀(water_ghost)는 아직 enemies.json 에 없다(T-33에서 넣는다)"는 옛말이다. 지금은 있다. 그 문장만 지우면 된다

## 이미 맞는 것

- 문파 여섯 몸·해금, 무공 16 성장·해금, 심법·절기 효과 칸, 지역 표, 기믹 셋(독안개·물·태극 바닥) 수치, 고유 적 여섯, 정예 아홉, 땅의 주인 셋 패턴은 위 칸에 적은 테스트가 기획서 표와 데이터를 맞춰 본다. 숫자가 서로 다른 곳은 찾지 못했다
- 정예 아홉을 걸어서 피할 수 있는지는 `docs/design/sim/dodge.md`(R-01)

## 그 뒤(2026-11-04 클라우드 M1·M3 이어서)

빈 칸 가운데 backlog 에 없던 것을 R-15~R-17 로 넣었다. 오의 박자·모양 Core 는 R-15(끝), 오의 빈 숫자는 R-15b, 웹 천하 시안 몸의 고유 능력·오의는 R-16, 무공 여섯 날아가는 것·넉백과 흑룡탄은 R-17(사람 결정 대기). 검혼 `_note` 의 '매화검귀가 아직 없다'도 옛말이라 고쳤다.
