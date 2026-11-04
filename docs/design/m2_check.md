# M2 점검표(2026-11-04)

기획서 17장 M2 버티컬 슬라이스(1/11~2/28): "서장과 숭산 일대(삼류–절정, 원각)를 아트·사운드까지 완성. 문파 3(소림·개방·남궁), 축척 전환, 침방과 연무장, 정산 화면."
이 줄을 기획서 ↔ 데이터 ↔ Core ↔ 웹 ↔ Unity 로 맞대 본 것이다. 아트·음악은 `art_list.md`·`audio_queue` 가, Unity 일은 `unity_tasks.md` 가 맡으니 여기선 있는지만 적는다.
빈 곳은 backlog RM-02~RM-09 로 넣었다.

| M2 줄 | 기획서 | 데이터 | Core | 웹 | Unity |
|---|---|---|---|---|---|
| 서장 | 4장 서장 상세 흐름, 19장 | chapters.json `prologue`, cheonha.json `prologueBody` | `Run/Prologue.cs`, `Spawning/PrologueDirector.cs`, 첫 레벨업 한 장(T-43) | 없음. 1장 프로토타입은 숭산부터, 천하 시안도 첫 환혼부터 | C-06b(첫 레벨업 카드 한 장) |
| 숭산 일대 삼류–절정 | 6·7장 | cheonha.json `regions.songshan`, chapters.json `life`, gimmicks.json 범종·탑림(우물은 청운진), enemies.json 숭산 정예 셋·고유 적 | `RegionSpawns`·`LifeDirector`·`Gimmicks`·`Regions` | 1장 프로토타입: 범종만. 천하 시안: 범종·탑림(RM-08, 원각이 떨어뜨린 종 포함) | TU 축척·지역 HUD 끝 |
| 원각 | 6장 땅의 주인 패턴 수치 | bosspatterns.json `wongak` | `BossPatterns`·`BossAwakening`(기혈 곡선 T-40) | 둘 다 패턴 다 나옴 | 보스 패턴 러너 |
| 문파 3 | 5·21장 | cheonha.json sects 소림·개방·남궁(`unlock: start`), 시작 무공, 대표 인물 | `Rebirth`(후보·몸·성별), 진전 `Synergy` | 천하 시안: 몸 기혈·걸음만, 무공은 시안 셋 | TU-11 후보 카드 |
| 축척 전환 | 7장 | cheonha.json `realmScale`·`breakthrough`·`cluster`·`crush` | `Scale`·`Breakthrough` | 천하 시안 그대로 | TU-01 축척 카메라 |
| 침방 | 10장 환혼 강화, 21장 | cheonha.json `rebirth`·환혼 강화 | `Soul.TryBuyUpgrade`·`CandidateCount`·`RerollsPerRebirth` | 천하 시안: 넷째 몸·다시 고르기(RM-09) | TU-11·12 |
| 연무장 | 10장 표 20항목 | balance.json `training` 20 | `Stats`·`Soul.TryBuyTraining`·`RefundTraining` | 천하 시안: 몸에 닿는 열 항목(RM-09) | TU-14 |
| 정산 화면 | 10장 윤회의 기, 12장 정산 줄 | cheonha.json `rebirthQi` | `RebirthQi`(줄별 내역), `Soul.EndLife` | 천하 시안: 은자 줄 없음(은자 자체가 없음) | TU-13. 생존 시간·처치·무공별 피해·혈정은 전투 쪽 `SettlementStats` |

## 걸리는 것

- **혈정이 혼에 안 쌓인다.** 땅의 주인 처치가 혈정 5를 계산하지만(`BossKillInfo.BloodCrystal`) `Soul` 저장에 혈정 칸이 없다. 정산 화면은 전투 쪽이 센 수를 보여 주기만 한다. 15장 저장 목록에 혈정이 있다 → RM-02
- **객잔 시설 해금 표가 데이터·Core 에 없다.** 10장 표 일곱 줄. M2 에서 쓰는 건 본채·연무장(서장 끝)·장경각(봉인석 1)이지만 판정은 한 곳에 두는 게 낫다 → RM-03. 장경각이 M2 에서 어떻게 보일지는 A-07(사람 결정 대기)
- **보부상 품목 값이 기획서에 없다.** 7장에 '회복약, 다시 고르기 권, 취기부'만 있다 → RM-04(사람 결정 대기), RM-05
- **은자 수입이 목표에 맞는지 잰 적이 없다.** 10장 '20분 600~900' → RM-06
- 웹 천하 시안이 M2 의 손맛을 보려면 깨달음·숭산 기믹·침방 강화가 있어야 한다 → RM-07~RM-09. 서장을 웹에 넣는 일은 M1 범위('서장 끝까지 플레이 가능')라 이번엔 넣지 않았다

## 이미 맞는 것

- 경지 문턱·진기 곡선·축척 수치는 `BalanceTests`·`CheonhaDataTests` 가 기획서 표와 맞춰 본다
- 원각 기혈은 T-40 곡선(절정에 바로 덤비면 약 1.5배), 처치 시간 보고서는 `sim/cheonha.md`·`decisions.md`
- 서장 시각(첫 레벨업 0:15, 이류 2:45, 일류 6:10)은 `sim/prologue.md`
