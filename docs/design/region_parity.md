# 지역마다 숭산에 있고 여기엔 없는 것 (RG-01)

2026-11-24 밤 개발 세션이 저장소를 세어 채움(claude/dev 1d07d77 무렵). 숭산은 화면 집중(11-13~11-24)으로 맞춘 기준이다. 다음 차례 RG-02 가 이 표를 보고 unity_tasks RG-01U~05U 의 '쓸 것'·'끝'을 채운다.

세는 곳:
- 바닥 원화: `Resources/Art/Tiles`(게임이 읽는 곳) + `Fx/Biomes.cs` ArtOf 표. `Art/Tiles` 에만 있는 것은 게임이 아직 안 읽는다.
- 소품: `Art/Props/props_*.png`(시트) → `Art/Props/Parts/prop_*.png`(낱장). 게임은 낱장만 쓰고, `Fx/PropLayer.cs` ArtIds 는 **지역 구분 없이 한 벌**(소나무·집·전각·산 — 숭산·청운진 낱장)이다.
- 적·정예·땅의 주인: `Art/Enemies`·`Art/Bosses`, 데이터 `enemies.json`(chapter)·`bosspatterns.json`.
- 기믹: `gimmicks.json`(Core 값)과 `Fx/GimmickLayer.cs`(화면). 화면은 숭산 범종·탑림 둘만 깐다(GM-01U).
- 배경음: `audio_queue` 줄은 33곡 전부 generated(사람 귀 검수 대기, audio_pipeline 9번), 게임에 들어온 ogg 는 아직 없다(숭산 포함). 격해진 판(`_intense`)은 숭산만 뽑았다.
- 침방 카드·몸 그림: `Art/Cards/card_sect_*`, `Art/Characters/char_<문파>_<m|f>`.

## 표

○ 있음 · △ 있는데 게임이 아직 안 씀 · ✕ 없음 · — 그 땅에 해당 없음(기획서에 없음)

| 지역(문파) | 바닥 원화 | 지역 소품 | 고유 적 원화 | 정예 원화 | 땅의 주인 원화·패턴 | 기믹 화면 | 배경음 | 침방 카드·몸 |
|---|---|---|---|---|---|---|---|---|
| 숭산(소림) | ○ | ○(낱장으로 나뉨, PropLayer 기본 벌) | ○ 목인나한 | ○ 셋 | ○ 원각 1·2막, 패턴 셋 | ○ 범종·탑림 | △(뽑음, 두 벌) | ○ 카드, 몸은 남자만(소림 승려) |
| 개방 총타(개방) | ○ | ○ 낱장·지역 벌(RG-01e~g 낱장, 2026-11-25 20:37 PC RG-01U ② 가 PropLayer 에 붙임) | — 고유 적 없음(공통 적만) | — 정예 없음 | — 땅의 주인 없음 | ✕ 술독(`wine_jars`) | △ | ○ 남녀 |
| 남궁세가(남궁) | ○ | ○ 낱장·지역 벌(RG-01e~g 낱장, 2026-11-25 20:37 PC RG-01U ② 가 PropLayer 에 붙임) | — | — | — | ✕ 검 시렁(`sword_racks`) | △ | ○ 남녀 |
| 당가타(당가) | ○ | ○ 낱장·지역 벌(RG-01e~g 낱장, 2026-11-25 20:37 PC RG-01U ② 가 PropLayer 에 붙임) | ○ 셋 | ○ 셋 | ○ 당소혜, 패턴 셋(분신 그림 있음) | ○ 독안개·대숲(2026-11-26 RG-03U) | △ | ○ 남녀 |
| 장강 수로(수로채) | ○(+ 물 타일 `Resources/Art/Tiles/tile_changjiang_water` ○ 물길, 2026-11-26 RG-04U) | ○ 낱장·지역 벌(RG-01e~g 낱장, 2026-11-25 20:37 PC RG-01U ② 가 PropLayer 에 붙임) | ○ 둘 | ○ 셋 | ○ 마천패, 패턴 셋 | ○ 물·떠도는 배·역류(2026-11-26 RG-04U) | △ | ○ 남녀 |
| 무당산(무당) | ○ 2026-11-24 RG-01c 로 `tile_wudang_stone` 을 Resources 로 옮기고 ArtOf 한 줄(세기는 Unity 에서 눈으로). `tile_wudang_mountain` 은 그대로 Art/Tiles | ○ 낱장·지역 벌(RG-01e~g 낱장, 2026-11-25 20:37 PC RG-01U ② 가 PropLayer 에 붙임) | ○ 흑백 쌍강시 | ○ 셋 | ○ 혈허진인, 패턴 셋 | ✕ 태극 바닥·운해 | △ | ○ 남녀 |
| 화산(화산) | ○ | ○ 낱장·지역 벌(RG-01e~g 낱장, 2026-11-25 20:37 PC RG-01U ② 가 PropLayer 에 붙임) | △ 매화검귀 — art_queue generated(`docs/art_trial` 에 시안), 아직 안 들임(2026-11-25 그대로) | ○ 셋 | ○ 낙매검선의 검혼, 패턴 셋 | ✕ 벼랑길·낙석·매화 더미 | △ | ○ 남녀 |
| 곤륜 설원(곤륜) | ○ | ○ 낱장·지역 벌(RG-01e~g 낱장, 2026-11-25 20:37 PC RG-01U ② 가 PropLayer 에 붙임) | ○ 셋 | ○ 셋 | ○ 설무향, 패턴 셋 | ✕ 눈보라·얼음 바닥·모닥불 | △ | ○ 남녀 |
| 십만대산(마교) | ○ | ○ 낱장·지역 벌(RG-01e~g 낱장, 2026-11-25 20:37 PC RG-01U ② 가 PropLayer 에 붙임) | ○ 셋 | ○ 셋 | ○ 혁련휘, 패턴 셋 | ✕ 혈제단·붉은 달 | △ | ○ 남녀 |
| 봉마곡 혈지(종장) | ○ 2026-11-25 `tile_bongmagok_ground` 를 Resources 로 옮기고 ArtOf 한 줄(세기 0.4, Unity 에서 눈으로) | ○ 낱장·지역 벌(RG-01e~g 낱장, 2026-11-25 20:37 PC RG-01U ② 가 PropLayer 에 붙임) | — | — | ○ 단목현 원화, 패턴은 `bosspatterns.final`(혈영 차례) | ✕ 차오르는 피(`rising_blood`) | △ 종장 세 곡 | — |

2026-11-25 19시 덧붙임: 지역 소품 시트 열한 장(개방은 PC 가 04시에)을 다 낱장으로 나눔(RG-01e·f·g) — 같은 날 20:37 PC 가 RG-01U ② 로 지역 벌을 붙여 표의 '지역 소품'을 ○ 로 고침(22시).  기획서에 값이 있는 지역 기믹은 이제 웹 합친 판에 다 있다(LG-01~05, 혈제단 WP-13b, 피바다 섬 WP-17) — 표의 '기믹 화면' ✕ 는 Unity 기준 그대로이고, Unity 줄은 RG-03U~05U·R-13U·R4-13b·R4-13c 에 있다. 놓는 자리가 없는 넷(우물·술독·시렁·수레와 장독)만 결정 대기 #67.

## 지역 소품 벌 추천(2026-11-25 19시 클라우드, RG-01U ② 에 붙여 쓸 표)

`PropLayer.ArtIds` 는 종류(나무·집·전각·산) × 벌 셋 = 열두 칸이다. 지역마다 아래 열두 장을 쓰고, 줄에 없는 지역(청운진·숭산)과 빈 칸은 지금 벌 그대로. 이름은 `Art/Props/Parts/prop_<이름>.png`. 눈으로 보고 바꿔도 된다(그림 고르기는 추천일 뿐).
기믹 그림으로 남겨 둘 낱장은 벌에 넣지 않았다: blood_altar·blood_altar_broken(혈제단), plum_petal_heap(매화 더미), campfire_ring(모닥불), ice_sheet(빙판), plank_walkway(잔도), poison_pond(독 웅덩이), wine_jar_pile(술독), sword_pavilion(검각 시렁 자리), roofed_well(우물 — 청운진 것이 따로 있으면 그쪽).

| 지역 | 나무 셋 | 집 셋 | 전각 셋 | 산 셋 |
|---|---|---|---|---|
| gaebang_hq | pine_old · shrub · pine_gnarled | mat_lean_to · rag_awning · huts_thatched | shrine_bridge · cook_fire_pot · village_house | 지금 벌 |
| namgung_manor | pine_old · pine_gnarled · pine_cliff | white_wall · stone_lions · village_house | manor_gate · lotus_pavilion · temple_side_hall | 지금 벌 |
| danggata | bamboo_clump · bamboo_brush · bamboo_fallen | herb_store_stilts · poison_jar_row · bamboo_fence | tang_clan_hall · rock_violet_fungus · temple_side_hall | 지금 벌 |
| janggang | willow_water · reed_bed · reed_clump | covered_boat · cargo_raft · rope_oars | river_pier · river_watchtower · sunken_boat | river_stones · far_peak · peak_snow |
| wudangsan | pine_ledge · pine_cliff · pine_old | sitting_rock · mossy_boulders · incense_cauldron | taoist_hall · round_pavilion · blank_stele | misty_crag · far_peak · misty_crag |
| huashan | plum_tree · pine_cliff · pine_gnarled | granite_boulder · loose_rocks · boulder_mossy | 지금 벌 | 지금 벌 |
| kunlun | frosted_pines · pine_cliff · frosted_pines | snow_crate · snow_gate_pillar · huts_thatched | 지금 벌 | peak_snow · far_peak · peak_snow |
| sipmandaesan | rock_spire · pine_gnarled · rock_spire | spear_rack · black_watchtower · huts_thatched | black_watchtower · temple_side_hall · stone_lantern | 지금 벌 |
| bongmagok | dead_tree · pine_gnarled · dead_tree | broken_stele · dipper_stones · split_rock_gate | blood_pool · split_rock_gate · broken_stele | 지금 벌 |

## 읽은 것

1. **지역 소품이 가장 큰 구멍이다.** 시트는 일곱 지역 것이 다 들어와 있는데 낱장으로 나뉜 것은 숭산·청운진뿐이고, PropLayer 는 지역을 가리지 않고 같은 열두 장을 깐다. 그래서 어느 땅에 가도 숭산 전각·소나무가 선다. → RG-01a(코드, 지역마다 ArtIds 벌) + Unity 에서 시트를 낱장으로 나누는 일(PR-02U 와 같은 메뉴)
2. **기믹 화면은 숭산 둘뿐이다.** Core 값(`gimmicks.json`, R-10 표)은 열여덟 가지가 다 있다. 지역마다 Unity 몫.
3. **개방 총타·남궁세가는 고유 적·정예·땅의 주인이 기획서에 없다**(M2 '문파 셋의 땅'이라 문파 시작점으로만 쓰는 땅). 숭산 수준으로 올리려면 무엇을 넣을지부터 사람이 정해야 한다 → RG-01b 사람 결정 대기.
4. **무당산 바닥은 원화가 이미 있었다.** `Art/Tiles` 에서 `Resources/Art/Tiles` 로 옮기고 ArtOf 에 한 줄 → RG-01c(같은 날 끝).
5. 배경음은 지역 차이가 아니라 전부 사람 귀 검수 대기라 이 표에서는 같은 칸(△)이다. 격해진 판은 숭산만 있다(BGM-02b 가 '숭산으로 먼저 시험'으로 정함).
6. 땅의 주인 패턴 예고는 PatternRunner 가 모양 갈래(부채꼴·원·줄·소환·회전 빔·뛰어 내려찍기)로 그려 지역마다 따로 만들 것은 적다. 원각처럼 땅마다 하나 있는 '고유 그림'(범종 낙하)은 당가타 독안개 반쪽, 장강 물기둥, 무당 칠성검 낙하 같은 것이 숭산 수준의 몫이다 — 화면에서 볼 일이라 RG-xxU 에 적는다.

## 이어서 할 것
- RG-01a 지역마다 소품 벌: `PropLayer` ArtIds 를 지역 id → 열두 장 표로(없는 지역은 지금 벌), 이름은 시트를 나눌 때 정할 낱장 이름. 코드는 클라우드, 낱장 나누기·확인은 Unity.
- RG-01b 개방 총타·남궁세가에 무엇을 둘지(사람 결정 대기).
- RG-01c 무당산 바닥 원화 옮기기(파일 두 개 + ArtOf 한 줄, 세기는 Unity 에서 눈으로).
- 빠진 원화: 곤륜·십만대산·봉마곡 소품 시트, 봉마곡 바닥은 이미 art_queue todo 다. 매화검귀는 generated(이미지 세션이 들일 차례). 새로 넣을 줄은 없다.
