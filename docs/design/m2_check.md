# M2 점검표(2026-11-04)

기획서 17장 M2 버티컬 슬라이스(1/11~2/28): "서장과 숭산 일대(삼류–절정, 원각)를 아트·사운드까지 완성. 문파 3(소림·개방·남궁), 축척 전환, 침방과 연무장, 정산 화면."
이 줄을 기획서 ↔ 데이터 ↔ Core ↔ 웹 ↔ Unity 로 맞대 본 것이다. 아트·음악은 `art_list.md`·`audio_queue` 가, Unity 일은 `unity_tasks.md` 가 맡으니 여기선 있는지만 적는다.
빈 곳은 backlog RM-02~RM-09, 두 번째 훑기(2026-11-04 저녁)에서 RM-12~RM-15 로 넣었다.

| M2 줄 | 기획서 | 데이터 | Core | 웹 | Unity |
|---|---|---|---|---|---|
| 서장 | 4장 서장 상세 흐름, 19장 | chapters.json `prologue`, cheonha.json `prologueBody` | `Run/Prologue.cs`, `Spawning/PrologueDirector.cs`, 첫 레벨업 한 장(T-43) | 프로토타입 서장 → 끝 컷 → 그 판 침방 첫 환혼 셋 → 첫 생(WP-14e) | C-06b(첫 레벨업 카드 한 장) |
| 숭산 일대 삼류–절정 | 6·7장 | cheonha.json `regions.songshan`, chapters.json `life`, gimmicks.json 범종·탑림(우물은 청운진), enemies.json 숭산 정예 셋·고유 적 | `RegionSpawns`·`LifeDirector`·`Gimmicks`·`Regions` | 프로토타입 한 생 판: 범종, 시간표(chapters.json life), 숭산 정예·원각(WP-13·14), 탑림(WP-13b, 시안 `pagodaPatchAt`·`pushOutOfPagodas` 글자 그대로, 무더기 간격은 시안 놀이판 값). 범종은 시안과 같은 30m 격자(WP-13b, `?scale=0` 만 1장 마당 종 셋) | TU 축척·지역 HUD 끝 |
| 원각 | 6장 땅의 주인 패턴 수치 | bosspatterns.json `wongak` | `BossPatterns`·`BossAwakening`(기혈 곡선 T-40) | 둘 다 패턴 다 나옴 | 보스 패턴 러너 |
| 문파 3 | 5·21장 | cheonha.json sects 소림·개방·남궁(`unlock: start`), 시작 무공, 대표 인물 | `Rebirth`(후보·몸·성별), 진전 `Synergy` | 프로토타입 한 생 판: 몸·시작 무공·고유 능력(WP-14a·20), 대표 인물 마당 줄 | TU-11 후보 카드 |
| 축척 전환 | 7장 | cheonha.json `realmScale`·`breakthrough`·`cluster`·`crush` | `Scale`·`Breakthrough` | 프로토타입 기본(WP-12·15, `?scale=0` 이 옛 시야) | TU-01 축척 카메라 |
| 침방 | 10장 환혼 강화, 21장 | cheonha.json `rebirth`·환혼 강화 | `Soul.TryBuyUpgrade`·`CandidateCount`·`RerollsPerRebirth` | 프로토타입 침방(WP-14d): 후보 셋·넷째 몸·다시 고르기·목 노인 줄 | TU-11·12 |
| 연무장 | 10장 표 20항목 | balance.json `training` 20 | `Stats`·`Soul.TryBuyTraining`·`RefundTraining` | 프로토타입 연무장: training 스무 항목 다(WP-14d·21) | TU-14 |
| 정산 화면 | 10장 윤회의 기, 12장 정산 줄 | cheonha.json `rebirthQi` | `RebirthQi`(줄별 내역), `Soul.EndLife` | 프로토타입 정산(WP-14c): 윤회의 기 줄·은자 줄·혈정·새 무공 | TU-13. 생존 시간·처치·무공별 피해·혈정은 전투 쪽 `SettlementStats` |

## 걸리는 것

- ~~**혈정이 혼에 안 쌓인다.** 땅의 주인 처치가 혈정 5를 계산하지만(`BossKillInfo.BloodCrystal`) `Soul` 저장에 혈정 칸이 없다. 정산 화면은 전투 쪽이 센 수를 보여 주기만 한다. 15장 저장 목록에 혈정이 있다 → RM-02~~ → RM-02(2026-11-04) 혈정 잔고가 일지·Soul 에 쌓임(2026-11-07 확인)
- ~~**객잔 시설 해금 표가 데이터·Core 에 없다.** 10장 표 일곱 줄. M2 에서 쓰는 건 본채·연무장(서장 끝)·장경각(봉인석 1)이지만 판정은 한 곳에 두는 게 낫다 → RM-03. 장경각이 M2 에서 어떻게 보일지는 A-07(사람 결정 대기)~~ → RM-03(2026-11-04) cheonha.json `inn.facilities` 일곱, A-07(11-05) 장경각 `underRepair`(2026-11-07 확인)
- ~~**보부상 품목 값이 기획서에 없다.** 7장에 '회복약, 다시 고르기 권, 취기부'만 있다 → RM-04(사람 결정 대기), RM-05~~ → RM-04(11-05 사람이 추천대로)·RM-05 balance.json `merchant`·Core `Merchant`(2026-11-07 확인)
- ~~**은자 수입이 목표에 맞는지 잰 적이 없다.** 10장 '20분 600~900' → RM-06~~ → RM-06(2026-11-04) `sim/silver.md`(2026-11-07 확인)
- 웹 천하 시안이 M2 의 손맛을 보려면 깨달음·숭산 기믹·침방 강화가 있어야 한다 → RM-07~RM-09(끝)
- 천하 시안 정산 화면에 12장 줄 가운데 밟은 땅·무공별 피해 막대·혈정이 없다 → RM-13(끝)
- 천하 시안에 7장 '한 생의 시간대별 흐름' 사건(혈인 돌진·목인나한 무리·동갑시 포위)이 없다. 레벨과 정예만 시간표를 따른다 → RM-14(끝)
- 천하 시안의 문파 몸은 시작 무공을 안 쓴다(무공은 시안 셋) → RM-15 사람 결정 대기. 합친 프로토타입 한 생 판은 시작 무공을 씀(WP-14a)
- (WP-24, 2026-11-06) 웹 칸을 합친 프로토타입 기준으로 고침. 보부상(WP-22)도 이제 웹에 있음
- (WP-02, 2026-11-04) 웹 한 판 이어 보기: 서장 → 첫 환혼 → 숭산 삼류–절정 → 원각이 이어진다. 서장 생이 시안 기록에 안 남던 것과 장삼 뒤 컷이 다른 화면에 막히던 것을 붙였다. 결과는 `web/prototype/README.md` 'M2 한 판 이어 보기'

## 이미 맞는 것

- 경지 문턱·진기 곡선·축척 수치는 `BalanceTests`·`CheonhaDataTests` 가 기획서 표와 맞춰 본다
- 원각 기혈은 T-40 곡선(절정에 바로 덤비면 약 1.5배), 처치 시간 보고서는 `sim/cheonha.md`·`decisions.md`
- 서장 시각(첫 레벨업 0:15, 이류 2:45, 일류 6:10)은 `sim/prologue.md`
