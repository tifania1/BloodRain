# 밸런스 시뮬레이터 (B-07)

화면 없이 1장 한 판(20분)을 돌려서, 레벨·경지·절기가 기획서 목표(11장 '밸런스 기본값', 8장 '절기 그릇')대로 나오는지 본다.
코드는 `core/BloodRain.Sim/`, 결과 보고서는 [level-curve.md](level-curve.md). 천하 한 생(지역 이동·땅의 주인·윤회의 기)은 [cheonha.md](cheonha.md)(`CheonhaSim.cs`, Core `Life` 를 그대로 돌린다), 생을 이은 메타 곡선은 [meta.md](meta.md)(`MetaSim.cs`), 천하에서 처치 진기 조정 시험은 [cluster-xp.md](cluster-xp.md), 연환·진전 화력 상한은 [synergy.md](synergy.md)(`SynergyCheck.cs`), 천하 한 생의 기연 사건 출현율과 도감 채우기는 [encounters.md](encounters.md)(`EncounterSim.cs`). 맞는 쪽(한 번 피해, 둘러싸여 버티는 초, 회복)은 [contact.md](contact.md)(`ContactSim.cs`). 음양 게이지가 한 판 동안 머무는 구간과 기혈역류 횟수는 [yinyang.md](yinyang.md)(`YinYangSim.cs`). 마교 몸의 혈공(땅의 적 기혈 배율 + 진기 × 농도 + 심마·천마해체)은 [blood-art.md](blood-art.md)(`BloodArtSim.cs`). 침방 환생 후보 분포와 구파윤회까지 걸리는 환혼 수는 [rebirth.md](rebirth.md)(`RebirthSim.cs`). 지역 기믹(우물·술독·매화 더미·모닥불·독안개·낙석·혈제단·날씨)이 기혈을 얼마나 채우고 깎는지는 [gimmicks.md](gimmicks.md)(`GimmickSim.cs`). 돌파 깨달음 일곱이 후보에 보일 확률, 고르는 시각, 일검파만·살기충천 값어치는 [insights.md](insights.md)(`InsightSim.cs`). 사람 결정 셋(처치 진기 P-02·땅의 주인 T-40·혈공 T-41)을 같은 시드로 나란히 놓은 표는 [decisions.md](decisions.md)(`DecisionSim.cs`). 비전의 무공 구성이 한 판에서 언제 갖춰지는지, 무극·무명검이 실제로 열리는지는 [secrets.md](secrets.md)(`SecretSim.cs`). 캐릭터 여덟 명의 절기 각성 시각은 [awakenings.md](awakenings.md)(`AwakenSim.cs`, level-curve.md 는 진소운 한 사람). 땅의 주인 패턴을 예고 시간 안에 걸어서·경공으로 피할 수 있는지, 무적으로 버틸 수 있는지는 [dodge.md](dodge.md)(`DodgeSim.cs`). 현경 뒤 혈마의 그림자에 언제 붙잡히고 그림자만으로 언제 쓰러지는지, 상한을 올리거나 순간이동을 넣으면 어떻게 바뀌는지(T-42)는 [shadow.md](shadow.md)(`ShadowSim.cs`).

게임 규칙은 Core 것을 그대로 쓴다. 경험치 곡선(`XpCurve`), 진기 흡수와 돌파(`RunXp`·`Realms`), 적 생성(`SpawnCurve`), 적 기혈 증가(`EnemyScaling`),
레벨업 추첨·절기 그릇·각성(`Loadout`), 능력치 합산(`Stats`), 음양(`YinYangGauge`), 오의 게이지(`UltimateCharge`), 무공 피해 보간(`WeaponMath`).
데이터(JSON)에 아직 없는 값만 `core/BloodRain.Sim/Assumptions.cs` 한 곳에 모았다.

## 돌리는 법

저장소 루트에서. dotnet이 없으면 CLAUDE.md의 설치 줄을 먼저 돌린다.

```
dotnet run --project core/BloodRain.Sim -- --policy average --seed 1      # 한 판. 분당 표, 빌드, 절기·경지 시각
dotnet run --project core/BloodRain.Sim -- --policy greedy --seeds 30     # 30판 요약(5·10·15·20분 레벨, 경지, 절기)
dotnet run --project core/BloodRain.Sim -- --scenario kill-xp+pair2 --seeds 30   # 조정 시험을 얹어서
dotnet run --project core/BloodRain.Sim -c Release -- --report docs/design/sim/level-curve.md   # 보고서 전체를 다시 쓴다(10초쯤)
dotnet run --project core/BloodRain.Sim -- --cheonha docs/design/sim/cheonha.md --seeds 5     # 천하 한 생 30분, 문파 아홉(T-17)
dotnet run --project core/BloodRain.Sim -- --meta docs/design/sim/meta.md                     # 혼 하나로 생을 이어 메타 곡선(T-18, 10초쯤)
dotnet run --project core/BloodRain.Sim -- --cluster-xp docs/design/sim/cluster-xp.md         # 천하 30분에서 조정 시험별 레벨(T-19, P-02 표)
dotnet run --project core/BloodRain.Sim -- --synergy docs/design/sim/synergy.md               # 무공 6칸 조합 전부의 연환·진전 화력 배수(S-05, S-06 표)
dotnet run --project core/BloodRain.Sim -c Release -- --encounters docs/design/sim/encounters.md # 기연 사건 8종이 한 생에 나오는 비율, 도감 채우기(15초쯤)
dotnet run --project core/BloodRain.Sim -- --contact docs/design/sim/contact.md                   # 접촉 피해: 지역·분마다 한 번 피해, 6마리 포위 버티는 초, 회복(Core 피해 식 그대로)
dotnet run --project core/BloodRain.Sim -c Release -- --yinyang docs/design/sim/yinyang.md       # 캐릭터 8명 × 정책 셋, 음양 구간별 머문 시간·처음 기운 분·역류 횟수(20초쯤)
dotnet run --project core/BloodRain.Sim -c Release -- --blood-art docs/design/sim/blood-art.md   # 백야 혈공: 숭산 진소운과 경지 도달 시각, 심마·천마해체, 머무는 땅별(10초쯤)
dotnet run --project core/BloodRain.Sim -c Release -- --rebirth docs/design/sim/rebirth.md       # 환생 후보 분포(문파·성질·성별), 원하는 문파가 나올 확률, 구파윤회까지 환혼 수(20초쯤)
dotnet run --project core/BloodRain.Sim -- --gimmicks docs/design/sim/gimmicks.md                 # 지역 기믹 기혈 수지: 회복 기믹, 독안개·낙석, 혈제단 곁 피해, 날씨 켜진 몫
dotnet run --project core/BloodRain.Sim -- --dodge docs/design/sim/dodge.md                       # 땅의 주인 패턴: 빠져나갈 거리 대 예고 동안 걷는·경공 거리, 무적으로 버티기, 탄 틈
dotnet run --project core/BloodRain.Sim -c Release -- --shadow docs/design/sim/shadow.md     # 혈마의 그림자: 문파 몸 × 신법 × 방어별 붙잡히는 분, 그림자만으로 쓰러지는 분(10초쯤)
dotnet run --project core/BloodRain.Sim -c Release -- --insights docs/design/sim/insights.md     # 돌파 깨달음: 노리는 것이 보일 확률, 고르는 시각, 공격 깨달음 값어치(20초쯤)
dotnet run --project core/BloodRain.Sim -c Release -- --decisions docs/design/sim/decisions.md   # 사람 결정 묶음: 처치 진기를 줄이면 땅의 주인·혈공이 어떻게 움직이나(15초쯤)
dotnet run --project core/BloodRain.Sim -c Release -- --secrets docs/design/sim/secrets.md       # 비전: 무공 구성이 갖춰지는 분, 칸 막힘, 다시 고르기 효과, 무극·무명검이 열리는 비율(15초쯤)
dotnet run --project core/BloodRain.Sim -c Release -- --awakenings docs/design/sim/awakenings.md # 캐릭터 여덟 명의 첫~넷째 절기 시각, 시작 무공이 첫 절기인 비율, 보갑으로 깬 몫(10초쯤)
```

| 인자 | 뜻 |
|---|---|
| `--policy average\|greedy\|slow` | 레벨업 카드 고르는 방식(아래) |
| `--seed N` / `--seeds N` | 한 판의 시드 / 시드 1~N 여러 판 요약 |
| `--scenario <id>` | 데이터를 메모리에서만 바꿔 보는 조정 시험. id는 `Scenarios.cs`(baseline, refill, kill-xp, health, curve, kill-xp+pair2, kill-xp+health, refill+kill-xp) |
| `--crowd-radius <m>` `--arrival <초>` `--max-density <마리/㎡>` | 무리 가정 바꿔 보기 |
| `--minutes <분>` `--data <폴더>` | 판 길이, 데이터 폴더(기본은 위로 올라가며 `unity/BloodRain/Assets/Resources/Data` 를 찾는다) |

같은 시드면 결과가 늘 같다.

## 한 판이 도는 순서

0.1초마다 아래를 한 번씩 한다.

1. 사건: 혈인 돌진(3.5·8.5·9.5·15.5·16.6·17.7분), 정예(5·10·15분), 동갑시 포위(13.5분), 금두꺼비(10.3~11.6분). 웹 프로토타입 `makeEvents` 그대로.
2. 1초마다 적 생성: Core `SpawnCurve.SpawnThisSecond`(목표 − 지금 수의 30%, 한 생 곡선이라 19~20분에도 멈추지 않는다). 종류는 시간대별 비율표(프로토 `pickType`), 기혈은 Core `EnemyScaling`.
3. 무리 갱신: 생긴 지 7초가 지난 적만 플레이어 곁 '무리'에 들어와 맞을 수 있다. 정예·금두꺼비가 맨 앞, 나머지는 먼저 온 순서.
4. 무공 발동: 쿨다운(쾌속·태극 -10% 반영)마다 동작별 계산으로 몇 마리를 몇 번 때리는지 정한다(아래 표).
5. 오의: 게이지(Core `UltimateCharge`, 처치 0.4·정예 15, 태극이면 1.25배)가 차면 일검.
6. 처치 진기 흡수(Core `RunXp`) → 오른 레벨마다 카드 한 장(Core `Loadout.RollLevelUp` + 정책) → 경지 돌파(초절정 7칸째, `AwakenOnBreakthrough`) → 보갑(`AwakenOnChest`, 아니면 강화 1~3개).
7. 절기가 깨어나면 각성 파동: 무리 안 잡몹 전부 처치, 정예 기혈 15%, 생성 2.5초 멈춤, 그 절기 8초 1.5배.

## 무리 모형

적 좌표는 들고 있지 않다. 무리에 닿은 적은 반경 8m 원 안에 고르게 퍼져 있다고 보고,
밀도 = 무리 수 ÷ 원 넓이(1㎡에 1마리까지). 범위 무공은 '밀도 × 무공이 덮는 넓이'만큼 맞힌다(소수점은 확률로 반올림).
무공 사거리에는 적 몸 반지름 0.4m를 더한다. 이 셋(무리 반경, 최대 밀도, 도착 시간)은 재 볼 방법이 없어서
보고서 '가정 민감도'에서 크게 흔들어 보고 결론이 그대로인지 확인한다.

시작 무공 8종은 웹 프로토타입 `fireWeapon` 을 옮겼다. 잠긴 8종은 '사거리 반원 한 획'으로 대충 잡았다(1장 첫 판에는 안 나온다).

| 무공 | 덮는 넓이 / 맞히는 수 | 타수 | 단계·절기 |
|---|---|---|---|
| 삼재검법 | 반원(반경 2.2 × 기세) | Lv7부터 3연 베기 | Lv4 앞뒤(원 전체), 절기 세 방향·반경 1.6배·세 번째마다 관통 검기 |
| 나한권 | 원(1.8) | 3→5연타 | 절기 관통 권경 세 갈래 |
| 비도술 | 가까운 적 n자루 × (관통+1) | 1 | 자루 1→4 + 분광, Lv6 중독, 절기 +2자루·5번 튕김 |
| 창천검기 | 원(2.5) | Lv5부터 두 바퀴 | 절기 반경 1.3배 |
| 장풍 | 바라보는 쪽 띠(폭 1.1~1.8m × 무리 반경) × 0.6 | Lv6부터 2연발 | 절기 맞을 때마다 작은 폭발 |
| 섬광지 | 가까운 적 아무나 한 발(초당 4→9발) | 1 | 절기 25% 작은 폭발 |
| 벽력탄 | 폭탄 1→3개(+분광), 원(2→3m × 폭발 반경) | 1 + 화상 2초 | 절기 3연쇄 |
| 독무 | 발밑 원(2→3.5m) | 0.5초마다 초당 피해의 절반 | 절기 반경 2배, 7초 버틴 잡몹 즉사 |

모든 피해에 공력, 음양(편향 +15%·극성 +40%, 기운 쪽 속성만), 절기 2배, 치명(확률 × 2배의 기대값)을 곱한다.

## 정책

| 정책 | 고르는 법 |
|---|---|
| `average`(평균) | 가진 무공 강화를 먼저, 무공이 4개 안 되면 새 무공, 가진 무공의 짝 심법을 Lv3까지 서두르고, 짝 심법 자리를 남겨 두고 짝 없는 심법을 받는다. 약간 흔들림을 섞는다 |
| `greedy`(화력 욕심) | 지금 무리에 쏟는 초당 피해가 가장 많이 오르는 카드. 절기 짝을 따지지 않는다 |
| `slow`(아무거나) | 아무 카드나. 빌드를 계획하지 않는 처음 하는 사람 |

다시 고르기·파기·정진은 쓰지 않는다. 초절정 7칸째는 무공 쪽에 쓴다(`slow` 는 반반).

## 출력 열

| 열 | 뜻 |
|---|---|
| 화면 적 | 살아 있는 적 전부(걸어오는 중인 적 포함). 생성 디렉터가 세는 수와 같다 |
| 무리 안 | 그중 플레이어 곁에 닿아 맞을 수 있는 적 |
| 채움 | 화면 적 ÷ 그 시각 생성 목표 |
| 화력(단일) | 보스 한 마리를 칠 때의 초당 피해. 기획서 11장 '화력 목표'와 견주는 값 |
| 무리 피해/초 | 지난 1분 동안 실제로 깎은 기혈 ÷ 60(넘친 피해는 뺀다) |

## 테스트

`core/BloodRain.Core.Tests/SimulatorTests.cs`

- 평균 정책·시드 1의 5·10·15·20분 레벨. 목표 ±25% 띠가 기준인데, 지금 데이터는 10분부터 띠를 넘어서
  지금 잰 값 ±10%를 지키게 해 두었다(주석에 이유). 띠의 근거는 보고서 '가정 민감도'.
- 지금 데이터가 10분부터 띠 위쪽이라는 사실 자체. 데이터를 고쳐 이게 깨지면 잘 된 것이니, 위 검사를 목표 띠로 바꾼다.
- 추천안(`kill-xp+pair2`)을 얹으면 네 시점 모두 목표 띠 안.
- 같은 시드면 같은 결과, 레벨·처치가 줄지 않고 경지가 순서대로.

## 데이터나 가정을 바꿨을 때

1. `dotnet test core/BloodRain.sln` — 시뮬레이터 테스트가 깨지면 숫자가 움직였다는 뜻이다.
2. 보고서를 다시 만든다(`--report`). 문장 속 숫자도 계산해서 넣으므로 손으로 고칠 곳은 없다.
3. 테스트의 '지금 잰 범위'를 새 값에 맞춰 고친다.

나중에 데이터가 생기면 바꿀 곳:

| 생기는 것 | 바꿀 곳 |
|---|---|
| P-02 처치 진기 결정(`enemies.json` `xp`) | `Assumptions.KillXp` 대신 데이터를 읽게 |
| `chapters.json` 한 생 흐름(T-01, 들어옴) | `Assumptions.EnemyMix`·`Events` 대신 `WaveTable`(`life`)과 `RegionSpawns`(숭산 고유 적·정예)를 읽게. 아직 안 바꿈. `CheonhaSim` 도 이 시뮬레이터의 분당 진기를 빌려 쓰므로 바꾸면 천하 보고서들도 같이 움직인다(`ContactSim` 만 life 를 읽는다) |
| B-06 깨달음, J-01 각성 수치, S-01 연환·진전 | 데이터·Core 는 들어왔다(`insights.json`·`Insights`, balance.json `awakening`·`Awakening`, `synergies.json`·`Synergy`). 한 판 시뮬레이터는 아직 깨달음·연환을 안 얹고, 각성 파동은 Assumptions 값을 쓴다(데이터와 같은지는 `AssumptionsTests` 가 본다). 연환 화력은 `SynergyCheck` 가 따로 잰다 |

## 한계

- 플레이어가 죽지 않는다. 피격, 기혈역류 피해, 피격 오의 충전이 없다. 실제 판은 이보다 조금 느리다. 맞는 쪽은 [contact.md](contact.md), 역류 횟수는 [yinyang.md](yinyang.md) 가 따로 잰다.
- 구슬을 다 줍는다고 본다(낙관적인 쪽).
- 넉백·둔화·기절, 범종·등롱·부적, 보스전(20분부터)은 없다.
- 좌표가 없으니 '몰아서 한 번에 쓸기' 같은 사람의 손맛은 안 들어간다. 그 차이는 웹 프로토타입 자동 판(`__hyeolu.sim`)과 견줘 본다.
