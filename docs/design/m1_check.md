# M1 점검표(2026-11-04)

기획서 17장 M1 코어 프로토타입(11/30~1/10): "이동·자동 공격·적 300마리·레벨업·음양·경공·오의. 무공 8, 심법 8, 절기 4. 서장 끝까지 플레이 가능."
이 줄을 기획서 ↔ 데이터 ↔ Core ↔ 웹 ↔ Unity 로 맞대 본 것이다. Unity 일은 `unity_tasks.md` 가 맡으니 여기선 있는지만 적는다.
빈 곳은 backlog M1-02~M1-07 로 넣었다.

| M1 줄 | 기획서 | 데이터 | Core | 웹(1장 프로토타입 `template.html`) | Unity |
|---|---|---|---|---|---|
| 이동·자동 공격 | 8장 | weapons.json `behavior` | `Combat/WeaponGrowth`, `Loadout` | 있음(시작 무공 9종 동작) | C-01·C-04 끝 |
| 적 300마리 | 8·11장 | balance.json `spawnTarget` | `SpawnCurve` | 적 220마리 56fps(README) | C-02·C-03 끝(300마리 60fps) |
| 레벨업 | 8장 | balance.json `levelUp`·`xpCurve` | `RunXp`, `Loadout.RollLevelUp`·`RollFirstLevelUp`, `LevelUpCharges` | 있음(3~4택, 다시 고르기·정진). 서장 첫 레벨업 한 장은 없음 | C-06 진행, C-06b 끝 |
| 음양 | 8장 | balance.json `yinyang` | `YinYangGauge`, `Backlash` | 있음 | C-08 |
| 경공 | 8장 | balance.json `dash` | `Combat/Dash` | 있음 | C-01 끝 |
| 오의 | 8장 | characters.json `ultimates` | `UltimateCharge`, `Ultimates` | 있음(일검) | C-09 |
| 무공 8·심법 8·절기 4 | 9장 | 무공·심법·절기 16씩(데이터는 M3 몫까지 이미 있음) | 전부 | 무공 9·심법 16·절기 그릇 | C-04 동작 8종 끝, 절기 4종은 C-09 |
| 서장 끝까지 | 4장 '서장 상세 흐름', 21장 '서장의 절기 맛보기와 첫 환혼' | chapters.json `prologue`, cheonha.json `prologueBody`, bosspatterns.json `jang_sam`·elites `gangsi_axeman`, dialogue.json `prologue` | 조각만 있음: `Run/Prologue`(단계), `Spawning/PrologueDirector`(생성), `BossPatterns`. 한데 묶는 것이 없다 → M1-02. 안내·말풍선 차례 → M1-03 | 없음. 숭산부터 시작하고 장삼이 없다 → M1-04~M1-06 | C-10(먼저: C-01~C-09), DL-03b(3) |

## 걸리는 것

- **서장 한 판을 묶는 Core 가 없다.** 한 생은 `Life` 가 진기·돌파·각성·역류·생성·땅의 주인을 묶어 주는데, 서장은 `Prologue`·`PrologueDirector`·`BossPatterns` 를 Unity 가 따로 엮어야 한다. 서장 몸(`Rebirth.Prologue`)으로 `Life` 를 만들면 생성이 서장 표가 아니라 한 생 표(`LifeDirector`)로 돈다. 장삼 전에 쓰러져 처음부터 할 때 일지를 어떻게 하는지(생 횟수에 안 들어가야 함, 21장)도 정해진 곳이 없다 → M1-02
- **서장 안내가 언제 뜨는지 정한 곳이 없다.** 4장 표의 '오의를 써라'(게이지가 처음 참), 음양 설명(처음 기울 때), 경공(3:30 돌진)과 dialogue.json `prologue.lines` 의 `when`(start·first_levelup·ultimate_ready·realm:iryu·first_charge·elite_spawn…)을 사건에서 한 번씩 고르는 규칙이 Core 에 없다. Unity DL-03b(3)·웹이 같은 규칙을 따로 짜게 된다. 안내 문구도 '오의를 써라' 한 줄뿐이다 → M1-03
- **웹에서 서장을 못 해 본다.** 1장 프로토타입은 숭산부터, 천하 시안은 첫 환혼부터다. 4장 표의 시각(0:15·2:45·3:20·6:10·9:00)을 사람이 손으로 해 볼 곳이 없다 → M1-04(판), M1-05(장삼·고정 각성), M1-06(말풍선·끝 컷·첫 환혼), M1-07(자동 판 확인)
- 무공 8·심법 8·절기 4 는 데이터가 16씩이라 모자라지 않다. 어느 여덟을 M1 에 넣는지는 Unity C-04 가 고른 동작 8종(front_arc·homing_dagger·orbit_slash·aura … 처음 8종)이 곧 그 답이라 따로 항목을 만들지 않았다.
