# 뱀서류 UI/UX 벤치마크 — 혈우강호 12장과 비교 (2026-11-05)

2026-11-05 09:01 사람: "UI가 다른 뱀서류에 비해서 좀 불편하게 느껴지는 감이 있는데, 다른 데서 아이디어를 좀 얻어보자." 그때 대화 세션이 조사한 것이다. 아래 (c)의 개선안 가운데 무엇을 기획서 12장에 넣을지는 사람이 정한다(backlog UI-01, `decisions.md`). 개선안은 웹을 하나로 합친 판(WP-10)에 넣는다.

조사한 게임: Vampire Survivors(VS), Brotato, Halls of Torment(HoT), HoloCure, 20 Minutes Till Dawn(20MTD), Soulstone Survivors, Deep Rock Galactic: Survivor(DRG:S), Death Must Die(DMD), Magic Survival, Survivor.io.
출처에서 확인한 것만 사실로 적었고, 내가 미루어 짐작한 것은 **(추정)** 으로 표시했다. (a)의 '왜 먹히나' 칸과 '메모' 칸의 평가 문장("장르의 기본 축", "준비할 시간을 준다" 같은 것)은 내 해석이라 추정으로 읽으면 된다.
무협·동양풍 뱀서류는 잘 팔렸고 UI 자료까지 남은 게임을 찾지 못했다. Chronicle of Wuxia & Xianxia Survivors는 아직 출시 전(2026년 6월 넥스트 페스트)이고, 天外武林(Traveler of Wuxia)은 덱빌딩이라 뺐다.

---

## (a) 화면별 핵심 패턴

### 1. 레벨업 선택 화면

| 패턴 | 하는 게임 | 왜 먹히나 |
| --- | --- | --- |
| 선택지 3~4장. 행운이 높으면 4장째 | VS(3~4장, 행운으로 4장째), HoloCure·20MTD·Brotato(4장), Magic Survival(3~4장), Survivor.io(3장) | 장르 표준. 4장을 넘는 게임은 못 찾았다 |
| 다시 고르기·건너뛰기·제외를 메타 성장으로 연다 | VS(Reroll·Skip·Banish PowerUp, Preserve는 쓴 횟수를 확률로 돌려줌), HoloCure(상점에서 사서 Reroll·Eliminate 최대 10, Hold 최대 5) | 처음엔 단순하게, 판을 거듭하면 운을 다스리는 도구가 생긴다 |
| **고정(Hold/Lock)**: 이 카드를 다음에도 띄운다 | HoloCure Hold(다음 레벨업에 그 선택지 보장), Brotato 상점 잠금(무료, 다시 굴려도 남고 다음 웨이브까지 감) | "지금은 안 되지만 다음엔 꼭" 하는 계획을 받아 준다 |
| 판 시작 전에 봉인 | VS Seal: 도감(Collection)에서 아이템을 레벨업 후보에서 미리 뺌. 봉인 칸은 PowerUp 4종으로 늘리고, 넷 다 끝까지 올리면 무제한 | 매 판 같은 걸 버리는 반복 작업이 줄어든다 |
| 버튼 위치 | HoloCure: 오른쪽 목록에 4장, 그 아래에 Reroll | 카드 바로 밑이라 시선이 덜 움직인다 |
| **조합 힌트를 레벨업 화면에 띄움** | HoloCure: 발견한 콜라보 조합 목록을 레벨업 화면 오른쪽과 일시정지 화면에 보여 줌 | 고르는 순간 무엇이 가까워지는지 알 수 있다 |
| 카드에 레벨 증가량·등급 | DRG:S: 대상 무기와 무기 레벨 증가량 표기, 5단계 등급. Brotato: 효과를 숫자로 적음 | 레벨 얼마에서 얼마로 가는지 바로 읽힌다 |
| 고른 뒤 잠깐 무적 | VS: 선택 뒤 재개하면서 짧은 무적 | 창이 닫히자마자 맞는 억울함이 없다 |
| 레벨업을 몰아서 처리 | Brotato: 웨이브가 끝난 뒤 그 웨이브에서 오른 레벨만큼 한꺼번에 고름 | 전투가 끊기지 않는다 |
| 자동 고르기 | VS: 위키에 따르면 스테이지 선택 메뉴에 Random LevelUp 토글이 있음 | 반복해서 돌리는 사람의 피로를 덜어 준다 |

### 2. 전투 HUD

| 패턴 | 하는 게임 | 메모 |
| --- | --- | --- |
| 생존 타이머를 화면 위에 | VS | 장르의 기본 축 |
| 화면 밖 대상 안내는 기본으로 넣지 않음 | VS: Milky Way Map 유물을 얻어야 일시정지 지도와 아이템 방향 화살표('Show Guides')가 생김 | 신규가 화면 밖 보스를 못 보고 죽었다며 알림을 요청한 스레드가 있다 |
| 보스 체력바는 기본으로 넣지 않음 | HoloCure: 체력바 없이 일부 보스가 50%에서 빛나기만 함 | 보스 체력바를 켜고 끄는 옵션 요청이 나왔고, 보스 여럿이면 화면이 복잡해진다는 반론도 있었다 |
| 큰 체력 표시 | HoT: 체력·마나를 큰 구슬로 표시 | PC Gamer가 좋게 언급 |
| 내 체력바는 맨 위에 | Brotato: 플레이어 체력바가 다른 것 위에 안 그려지던 버그를 고침 | 내 상태가 어떤 이펙트에도 가려지면 안 된다 |
| 다음 위험 예고 | Brotato: 위험도 2 이상이면 상점에서 정예·무리가 나오는 웨이브를 보여 줌 | 준비할 시간을 준다 |
| 미니맵 | DRG:S | 채굴하는 맵이라 필요함 |

### 3. 빌드 확인·일시정지

| 패턴 | 하는 게임 |
| --- | --- |
| 일시정지 화면의 조합식. 가진 재료는 강조하고, 조합식을 다 갖추면 식 전체를 강조하고, 이번 판에 더 못 맞추는 건 흐리게 | VS Grim Grimoire(0.7.3 추가, 한 번 본 진화만 보임) |
| 일시정지 중에 무기를 누르면 피해·연사·오버클럭 상세 | DRG:S(Update 02) |
| 지난 웨이브의 무기별 피해, 보조 능력치는 따로 탭, 캐릭터 레벨 표시 | Brotato(0.6.1.6) |
| 일시정지에서 스탬프(장비) 정리 | HoloCure |

### 4. 가독성 설정

| 설정 | 하는 게임 |
| --- | --- |
| 피해 숫자 끄기, 깜빡임 효과 끄기, 피 끄기, 무기 화면 흔들림 끄기, 픽셀 글꼴 | VS 옵션 |
| 특수 효과 보이기 슬라이더(반투명~안 보이게), 판 중에 단축키로 올리고 내리기 | Soulstone Survivors |
| 무기·VFX 불투명도 낮추기, 피해 숫자·효과 끄기 | HoloCure |
| 폭발 투명도를 패치로 낮춤 | Brotato 0.6.1.6 |
| 무기 이펙트 투명도 설정 **없음** → 후반에 화면이 덮인다는 불만 | VS |

### 5. 입력 편의

| 패턴 | 하는 게임 |
| --- | --- |
| 자동 조준·자동 공격 켜고 끄기 | HoT |
| PC는 Space로 수동/자동 조준 전환, 모바일은 오른쪽 스틱을 잡는 동안만 수동이고 놓으면 자동 | 20MTD |
| 마우스 조준을 왼클릭으로 켜고 끔, 패드는 오른쪽 스틱으로 조준 | HoloCure |
| 모바일 기본은 세로 화면, 터치 스틱 종류 선택(Legacy/Default) | VS |
| 이동 키 대체(IJKL) | Brotato |

### 6. 메타 화면

| 패턴 | 하는 게임 |
| --- | --- |
| 결과 화면: 무기별 레벨·피해·쓴 시간·초당 피해, 피해 1위와 초당 피해 1위를 노랑으로 강조 | VS Results |
| 상점에 들어갈 때 자동 저장, 끄고 켜면 그 상점부터 | DRG:S |
| 장비 비교 때 능력치 차이를 숫자로 | DRG:S |
| 화폐를 한꺼번에 쓰는 버튼 요청 | HoT(Torment Shards) |

---

## (b) 장르에서 자주 나오는 UI 불만과 해결법

1. **후반에 내 이펙트가 적 공격을 덮는다.** Soulstone 스레드: 효과가 겹치면 "25%가 100%가 된다", 붉은 계열 빌드가 특히 어렵다, 적 공격 표시를 최종 화면 위에 그리자는 제안이 나왔다. 대부분 투명도를 거의 0까지 내린다는 말도 있다. VS는 투명도 설정이 없어서 불만이 이어지고, 무기별 투사체 수 제한이나 특정 무기 이펙트 숨기기 같은 요청이 나왔다.
   → 해결: 투명도 슬라이더(0까지), 판 중 단축키(Soulstone), 적 표시를 위 레이어에.
2. **보스가 있는지, 얼마나 남았는지 모른다.** VS 신규 이용자 스레드, HoloCure 체력바 요청.
   → 해결: 등장 알림, 선택형 보스 체력바.
3. **효과 설명이 모자라다.** DMD: 효과 문구만으로는 뜻을 몰라 "게임 안에 백과가 있었으면" 했다. HoT 리뷰 요약: "업그레이드 효과가 불분명", "메커니즘 설명 부족".
   → 해결: VS Grimoire·HoloCure 콜라보 목록처럼 게임 안에서 조합을 보여 주고, Brotato처럼 효과를 숫자로 적는다.
4. **원하는 게 안 나와서 고르는 맛이 없다.** 장르 밖 사례지만, 생존 시뮬레이션 Judgment 개발사는 무작위 3장 방식에서 플레이어가 "정답이 없다"고 느낀다며 고정 트리로 바꿨다.
   → 해결: 다시 고르기·제외·고정·봉인을 단계적으로 열어 준다.
5. **조작 설정과 패드 지원.** VS: 키 설정이 없다는 불만, 패드로 Skip 버튼이 선택되지 않던 버그(2024-11). 20MTD: 왼클릭을 20분 누르고 있기 힘들다며 자동 사격 요청. HoloCure: 바쁜 화면에서 커서·방향 표시가 안 보인다며 크기·색 옵션 요청.
6. **메뉴가 잡일이 된다.** HoT: "스크롤이 많은 투박한 UI". DRG:S: 판이 끝날 때마다 드워프 12명 장비를 관리하는 게 잡일 같다(PC Gamer).

---

## (c) 우리 12장과 비교

**이미 장르보다 앞선 것**: 화면 밖 정예·보스 화살표(VS는 유물을 얻어야 생김), 정산의 무공별 피해 비중(VS 결과와 같은 축), 내 이펙트 투명도·피해 숫자·색각 보정 설정, 모바일 48dp.

**불편하게 느껴질 만한 곳**
- 레벨업 카드에 무엇을 적는지 12장에 규칙이 없다. 프로토타입 카드는 Lv a→b, 피해 a→b, 절기 짝, 진전, 연환을 한 장에 몇 줄씩 적는다. 3~4장을 한 번에 읽기엔 글이 많다 **(추정)**.
- 레벨업 화면에서 지금 무공·심법 칸이 안 보인다. HoloCure는 옆에 띄운다.
- 한 생에 레벨업 80번 넘게, 경지 돌파 6번, 보갑, 각성 연출까지 끊기는 일이 잦다 **(추정)**.
- 능력치 14개가 고유어다(분광=투사체 수, 흡기=흡수 반경, 심득=경험치, 기세=범위…). 처음 하는 사람은 카드만 보고 뜻을 모른다 **(추정)**. DMD·HoT에서 나온 불만과 같은 종류다.
- 내 이펙트 투명도가 50%까지만 내려간다. Soulstone 이용자는 거의 0까지 내린다고 한다. 우리는 적·혈우·혈우 장막 경고·저체력 연출까지 붉은색이 많다 **(추정 위험)**.
- 일시정지에 절기 조합표가 없다(장경각에만 있음).
- 땅의 주인 체력바가 12장 HUD 표에 없다. 프로토타입엔 `bossbar`가 있다(문서와 코드 불일치).
- 프로토타입: 설정이 제목 화면에만 있고, 레벨업 키는 1~4와 R뿐이다(정진 키 없음, 파기 버튼 없음). 일시정지 화면에는 무공 카드와 깨달음·연환·진전만 나오고 심법 레벨과 능력치는 안 나온다(12장에는 능력치가 있다).
- 판 중간 저장 이야기가 없다. 모바일에선 필요해 보인다 **(추정)**. DRG:S는 정식 출시 뒤에 넣었다.

**개선안 (효과 큰 순, 품은 상·중·하)**

| # | 개선안 | 근거 게임 | 효과 | 품 |
| --- | --- | --- | --- | --- |
| 1 | 레벨업 카드 규칙을 12장에 박는다: 첫 줄 이름 + `Lv 3→4`(새 것은 '새'), 둘째 줄 바뀌는 수치 하나, 셋째 줄은 글 대신 배지(절기 짝·진전·연환 완성). 긴 설명은 마우스 올리기·길게 누르기로 | DRG:S, Brotato | 상 | 하 |
| 2 | 레벨업 화면 옆(세로 화면은 아래)에 지금 무공 6칸·심법 6칸을 띄우고, 이 카드를 고르면 켜지는 칸을 빛낸다 | HoloCure | 상 | 중 |
| 3 | 일시정지에 '절기 조합표' 탭: 발견한 것만, 가진 재료 강조, 이번 생에 더 못 맞추는 건 흐리게 | VS Grim Grimoire, HoloCure | 상 | 중 |
| 4 | 내 이펙트 투명도를 0~100%로 넓히고 PC 단축키로 판 중에 조절. 적 탄·패턴 예고는 내 이펙트보다 위 레이어에 그린다 | Soulstone, HoloCure, VS 불만 | 상 | 하~중 |
| 5 | 능력치 이름 옆에 쉬운 말과 아이콘(`분광 · 투사체 +1`), 처음 나올 때 한 번 풀이 | DMD·HoT 불만, Brotato | 상 | 하 |
| 6 | 레벨업 입력: 패드·방향키로 모든 버튼에 포커스가 가게 하고, 다시 고르기·파기·정진에 단축키를 붙여 버튼에 적는다. 열린 직후 입력 막기(프로토타입 0.35초)는 기획서에 남긴다. 닫힌 뒤 짧은 무적 | VS(Skip 패드 버그, 선택 후 무적) | 중 | 하 |
| 7 | 레벨업이 쌓이면 "남은 2"를 보여 주고 연달아 고르게 한다. 설정에 '자동 고르기'를 둔다 | Brotato(몰아서), VS(Random LevelUp) | 중 | 하 |
| 8 | 땅의 주인 체력바를 12장 HUD 표에 올리고 설정에서 끌 수 있게. 보스가 여럿이면 작게 쌓기 | HoloCure 요청 | 중 | 하 |
| 9 | 일시정지에 무공별 지금까지 피해·초당 피해, 무공을 누르면 상세 | DRG:S, Brotato | 중 | 하 |
| 10 | 설정을 일시정지에서도 열고, '깜빡임 끄기'와 '혈우 연출 세기'를 더한다 | VS 옵션(Flashing VFX, Disable blood) | 중 | 하 |
| 11 | 방향성 무공 조준 옵션: 가장 가까운 적 자동 조준, 패드 오른쪽 스틱, 모바일은 두 번째 손가락으로 끄는 동안만 수동 **(모바일 방식은 추정, 20MTD 오른쪽 스틱 방식을 옮김)**. 바라보는 방향 표시 크기 옵션 | HoT, 20MTD, HoloCure | 중 | 중 |
| 12 | 고정(Hold): 카드 하나를 다음 레벨업에 다시 띄우기 — 기획서에 없는 새 규칙이라 사람 결정 필요 | HoloCure Hold, Brotato 잠금 | 중 | 중 |
| 13 | 장경각에서 판 시작 전 봉인 — 사람 결정 필요 | VS Seal | 중 | 중 |
| 14 | 정산에 무공별 보유 시간·초당 피해와 1위 강조 | VS Results | 하 | 하 |
| 15 | 판 중간 자동 저장(경지 돌파 직후 등) — 모바일 이식 전에 결정 | DRG:S | 중 | 상 |

**덤으로 찾은 문서와 코드 불일치**: 8장은 정진이 '진기 게이지 20'을 준다고 적었는데 프로토타입은 `addUlt(20)`(오의 +20)이다. 8장 조작표는 일시정지 키를 Esc/Tab이라고 적었는데 프로토타입은 Esc/P다.

---

## (d) 출처

- VS 레벨업(팬덤 위키): https://vampire-survivors.fandom.com/wiki/Level_up
- VS 옵션: https://vampire.survivors.wiki/w/Options
- VS Seal: https://vampire.survivors.wiki/w/Seal
- VS Preserve: https://vampire.survivors.wiki/w/Preserve
- VS Grim Grimoire: https://vampire.survivors.wiki/w/Grim_Grimoire , https://destructoid.com/?p=331517
- VS Milky Way Map: https://vampire.survivors.wiki/w/Milky_Way_Map
- VS 결과 화면: https://vampire.survivors.wiki/w/Results
- VS 접근성: https://www.familygamingdatabase.com/accessibility/Vampire+Survivors
- VS 이펙트가 화면을 덮는 문제: https://steamcommunity.com/app/1794680/discussions/0/4631482569784862180
- VS 보스 알림 요청: https://steamcommunity.com/app/1794680/discussions/0/4634861089657535404
- VS Skip 패드 버그: https://steamcommunity.com/app/1794680/discussions/0/4631482569784919503
- VS 키 설정 불만: https://steamcommunity.com/app/1794680/discussions/0/3201496916522827766
- VS Skip 논쟁: https://steamcommunity.com/app/1794680/discussions/0/3276941718958957262
- Brotato 상점: https://brotato.wiki.spellsandguns.com/Shop
- Brotato 업그레이드: https://brotato.wiki.spellsandguns.com/Upgrades
- Brotato 웨이브: https://brotato.wiki.spellsandguns.com/Waves
- Brotato 밴: https://brotato.wiki.spellsandguns.com/Ban_System
- Brotato 0.6.1.6: https://brotato.wiki.spellsandguns.com/Patch_0.6.1.6 , https://www.player.one/brotato-update-0616-improves-stats-screen-implements-gladiator-changes-152839
- Brotato 분석(Adrian Hon): https://adrianhon.substack.com/p/brotato
- HoloCure Hold·Reroll·Eliminate: https://holocure.wiki.gg/wiki/Hold
- HoloCure 팁(조준·불투명도·콜라보 목록): https://kosgames.com/?p=44046
- HoloCure 위키피디아: https://en.wikipedia.org/wiki/HoloCure_%E2%80%93_Save_the_Fans!
- HoloCure 보스 체력바 요청: https://steamcommunity.com/app/2420510/discussions/0/3824173205609519362
- HoloCure 커서 가시성: https://steamcommunity.com/app/2420510/discussions/0/3810661765909387993
- Soulstone 가독성: https://steamcommunity.com/app/2066020/discussions/0/3591086730691827584 , https://steamcommunity.com/app/2066020/discussions/0/603018051014667712
- HoT 리뷰: https://pcgamer.com/games/action/halls-of-torment-review , https://rogueliker.com/halls-of-torment-review/
- HoT 리뷰 요약: https://vaporlens.app/app/2218750/halls_of_torment.md
- HoT 일괄 사용 요청: https://steamcommunity.com/app/2218750/discussions/0/758394165818106534
- 20MTD 위키피디아: https://en.wikipedia.org/wiki/20_Minutes_Till_Dawn
- 20MTD 조준 전환: https://steamcommunity.com/app/1966900/discussions/0/3598968030043419152 , https://steamcommunity.com/app/1966900/discussions/0/4632610585628917366
- 20MTD 자동 사격 요청: https://steamcommunity.com/app/1966900/discussions/0/3467225783137758960
- DRG:S 레벨업: https://deeprockgalactic.wiki.gg/wiki/Survivor:Mid-dive_Upgrades
- DRG:S Update 02: https://dotesports.com/deep-rock-galactic/news/deep-rock-galactic-survivor-patch-notes-for-update-02
- DRG:S 중간 저장: https://gamingonlinux.com/2025/12/deep-rock-galactic-survivor-gets-mid-game-saves-and-cpu-optimizations
- DRG:S Update 1: https://estnn.com/deep-rock-galactic-survivor-update-1-patch-notes
- DRG:S 리뷰: https://www.pcgamer.com/games/roguelike/deep-rock-galactic-survivor-review
- DMD 설명 부족: https://steamcommunity.com/app/2334730/discussions/0/6761669877516184058
- Magic Survival 리뷰: https://www.taptap.io/post/1741711
- Survivor.io 가이드: https://www.bluestacks.com/blog/game-guides/survivor-io/sio-skills-evolution-guide-en.html
- Judgment 레벨업 개편: https://suncrash.com/blog/judgment_update_15_1_on_steam_new_levelup_system
- 무협 뱀서류(출시 전): https://gamekeepr.net/en/game/chronicle-of-wuxia-and-xianxia-survivors
- 天外武林(덱빌딩이라 제외): https://vaporlens.app/app/2088160/天外武林_traveler_of_wuxia

조사 한계: vampire.survivors.wiki의 Level_up 문서는 접근이 막혀 팬덤 위키로 대신했다. VS 레벨업 카드에 정확히 무엇이 적히는지는 출처로 확인하지 못해 적지 않았다.
