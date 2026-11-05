# 용어집(현지화)

영어·간체 문구를 옮길 때 이 표를 먼저 본다. 한 번 정한 표기는 바꾸지 않는다(바꿀 때는 이 파일과 `strings/<lang>.json` 을 한 커밋에서 같이 고친다).
문구 파일과 키 규칙은 `unity/BloodRain/Assets/Scripts/Core/Data/Strings.cs` 맨 위, 언어 순위는 기획서 16장 '현지화 우선순위'.

## 원칙

1. **게임 규칙에 붙은 말은 뜻으로 옮긴다.** 경지 → Realm, 진기 → Inner Energy, 오의 → Ultimate Art(기획서 16장). 영어 사용자가 처음 봐도 무엇을 하는 칸인지 알아야 한다. 음역(jingi, ohui)은 쓰지 않는다.
2. **영어권에 이미 굳은 이름은 그대로 쓴다.** 소림 Shaolin, 무당 Wudang, 화산 Mount Hua, 곤륜 Kunlun, 개방 Beggars' Sect, 장강 Yangtze, 강시 jiangshi.
3. **사람 이름과 세가 이름은 한국어 로마자 표기(국어의 로마자 표기법)를 쓰고 데이터 id 와 맞춘다.** 진소운 Jin Sowoon, 남궁현 Namgung Hyeon, 당연화 Dang Yeonhwa, 남궁세가 Namgung Clan, 당가 Dang Clan. 한국에서 만든 무협이라는 색을 남긴다. 법명(혜공·원각)도 같다.
4. **무공·심법·절기·오의 이름은 뜻을 옮긴 짧은 이름(2~4단어, 제목 대문자)으로.** 한자 뜻을 따르되 영어로 읽어 그림이 떠오르게 한다. 삼재검법 Three Powers Sword, 나한권 Arhat Fist. 한자 붓글씨(데이터의 `hanja` 칸)는 언어와 상관없이 그대로 얹는다.
5. **간체는 원래 한자를 간체로 바꿔 쓴다.** 중국어는 무협 용어가 원래 말이라 옮기지 않는다(기획서 16장). 한자가 없는 한국어 고유어(한 생, 넷째 몸, 다시 고르기 등)만 뜻으로 옮긴다. 사람 이름도 한자로 쓰되, 데이터·기획서에 한자가 없으면 지어 붙이지 말고 '사람 결정 대기'로 묻는다.
6. **특정 작가 고유 명칭 금지**: 김용·고룡 등 특정 작가가 만든 고유 무공·문파·인물 이름(항룡십팔장, 독고구검 등)은 어느 언어로도 쓰지 않는다. 그 이름의 공식 영어·중국어 번역도 마찬가지다. 장르 공통어나 새로 지은 이름만 쓴다(CLAUDE.md).
7. **문체**: 영어는 짧고 담백하게, 고어(thee, thou, -eth)는 쓰지 않는다. 기술 설명은 현재형 평서문("Slashes front and back"). 숫자·%·m·초는 원문 그대로 두고 자리만 바꾼다. `{0}` 자리는 원문과 같은 번호를 쓴다(테스트가 본다).
8. **길이**: 레벨업 카드에 나오는 무공·심법 이름은 영어 24자, 간체 6자 안쪽(테스트 `GlossaryTests` 가 본다). 넘으면 뜻을 살린 짧은 이름을 따로 짓는다.

## 체계 용어

| 한국어 | 영어 | 간체 | 게임에서 |
| --- | --- | --- | --- |
| 무공(武功) | Martial Art (공격 칸은 Technique) | 武功 | 자동 공격 |
| 심법(心法) | Heart Method | 心法 | 패시브 |
| 절기(絶技) | Pinnacle Art | 绝技 | 진화 |
| 오의(奧義) | Ultimate Art | 奥义 | 캐릭터 필살기 |
| 경지(境地) | Realm | 境界 | 레벨 구간 |
| 돌파 | Breakthrough | 突破 | 경지가 오를 때 |
| 깨달음 | Insight | 顿悟 | 돌파 보상 |
| 진기(眞氣) | Inner Energy | 真气 | 경험치(구슬) |
| 진기 구슬 | Qi Orb | 真气珠 | 떨어지는 경험치 |
| 단전 | Dantian | 丹田 | 오의 게이지 |
| 내공 | Inner Power | 内功 | 내공 능력치(기획서 쓰는 곳에 따라) |
| 공력 | Might | 功力 | 피해 배율 능력치 |
| 기혈(氣血) | Vitality (HP) | 气血 | 체력 |
| 호신 | Guard | 护体 | 받는 피해 고정 감소 |
| 경공(輕功) | Lightfoot (Dash) | 轻功 | 대시 |
| 기연(奇緣) | Fortune (사건은 Fated Encounter) | 奇缘 | 행운·기연 사건 |
| 비전(秘傳) | Secret Legacy | 秘传 | 숨은 조건 보상 |
| 비급(秘笈) | Manual | 秘笈 | 레벨업 카드 하나 |
| 찢긴 비급 | Torn Manual | 残页秘笈 | 잠긴 연환 |
| 연환 | Combo Link | 连环 | 무공 짝 시너지 |
| 문파 공명 | Sect Resonance | 门派共鸣 | 같은 문파 시너지 |
| 음양 / 양·음·중 | Yin-Yang / Yang·Yin·Neutral | 阴阳 / 阳·阴·中 | 태극 게이지 |
| 태극 | Taiji | 太极 | 조화 구간 |
| 주화입마 | Qi Deviation | 走火入魔 | 극성 위험 |
| 기혈역류 | Backlash | 气血逆流 | 극성 60초마다 |
| 정진 | Train (건너뛰기) | 精进 | 레벨업 건너뛰기 |
| 파기 | Discard | 废弃 | 선택지 빼기 |
| 다시 고르기 | Reroll | 重选 | 선택지 새로 |
| 행낭 | Satchel | 行囊 | 대체 카드(은자·음식) |
| 은자 | Silver | 银两 | 판 안 화폐 |
| 회복약 | Healing Draught | 回复药 | 보부상 품목(기혈 50%) |
| 다시 고르기 권 | Reroll Token | 重选券 | 보부상 품목(다시 고르기 +1) |
| 혈정 | Blood Crystal | 血晶 | 메타 화폐 |
| 보갑(寶匣) | Treasure Chest | 宝匣 | 상자 |
| 절기 그릇 | Pinnacle Slot | 绝技槽 | 진화 가능 수 |
| 정예 | Elite | 精英 | 정예 적 |
| 땅의 주인 | Lord of the Land | 一方之主 | 지역 보스 |
| 혈운(血雲) / 혈운 농도 | Blood Cloud / Blood Cloud Density | 血云 / 血云浓度 | 지역 난이도 |
| 혈우(血雨) 장막 | Blood Rain Veil | 血雨帷幕 | 맵 경계 |
| 혈마(血魔) | Blood Demon | 血魔 | 최종 보스 |
| 차시환혼(借屍還魂) | Borrowed Body Rebirth | 借尸还魂 | 죽으면 다른 몸으로 |
| 환혼 / 환생 | Rebirth | 还魂 | 새 몸 고르기 |
| 한 생 | A Life | 一生 | 몸 하나로 버티는 판 |
| 윤회의 기 | Samsara Qi | 轮回之气 | 메타 성장 화폐 |
| 타고난 성질 | Birth Trait | 天赋 | 몸의 장단점 |
| 봉인석 | Seal Stone | 封印石 | 땅의 주인 보상, 시설 열기 |
| 천하 | All Under Heaven | 天下 | 게임 구조 이름 |
| 중원 | the Central Plains | 中原 | 맵 |
| 문파 | Sect | 门派 | 몸의 소속 |
| 세가 | Clan | 世家 | 남궁·당가 |
| 마교 | Demonic Cult | 魔教 | 문파 하나 |
| 혈공 | Blood Art | 血功 | 마교 몸 성장 |
| 사형 / 사매 | Senior Brother / Junior Sister | 师兄 / 师妹 | 호칭 |

`땅의 주인` 간체: 地主 는 '지주(땅 가진 부자)'로 읽혀 쓰지 않고 `一方之主` 로 한다.

심법 이름에는 Heart 를 빼고 Method·Art 로 끝낸다(Clear Mind Method). 갈래 이름(카드 위 '새 심법')만 Heart Method.

## 전투·능력치 말

| 한국어 | 영어 | 간체 |
| --- | --- | --- |
| 진전(眞傳) | Lineage (step) | 真传 |
| 쾌속 | Haste | 快速 |
| 흡혈 | Lifesteal | 吸血 |
| 넉백 | Knockback | 击退 |
| 관통 | Pierce | 贯穿 |
| 치명타 / 치명 확률 | Critical Hit / Critical Chance | 暴击 / 暴击率 |
| 기절 | Stun | 眩晕 |
| 둔화 | Slow | 减速 |
| 중독 | Poison | 中毒 |
| 감전 | Shock | 感电 |
| 화상 | Burn | 灼烧 |
| 지속 피해 | Damage over Time | 持续伤害 |
| 정신부 | Stilling Talisman | 定身符 |
| 취기부 | Qi-Gathering Talisman | 聚气符 |
| 의서 | Medical Book | 医书 |
| 은빛 등롱 | Silver Lantern | 银灯笼 |
| 혈월 | Blood Moon | 血月 |

연무장·성질 카드의 능력치 이름(`strings/*.json` 의 `ui.stat.<이름>`, 2026-11-04 L-05). 위 표에 있는 공력·기혈·호신·쾌속·기연·정진·파기는 그 표기 그대로.

| 한국어 | 영어 | 간체 | 코드 이름 |
| --- | --- | --- | --- |
| 회복 | Recovery | 回复 | Recovery |
| 기세 | Area | 气势 | Area |
| 탄속 | Projectile Speed | 弹速 | ProjectileSpeed |
| 지속 | Duration | 持续 | Duration |
| 분광 | Amount | 分光 | Amount |
| 신법 | Footwork | 身法 | MoveSpeed |
| 흡기 | Qi Pull | 吸气 | Magnet |
| 심득 | Growth | 心得 | Growth |
| 탐욕 | Greed | 贪欲 | Greed |
| 진기 충전 | Dantian Charge | 丹田充能 | UltimateCharge |
| 경공 충전 | Lightfoot Charges | 轻功次数 | DashCharges |
| 재기 | Revival | 再起 | Revival |
| 재고 | Rerolls | 重选 | Rerolls |
| 음식 회복 | Food Healing | 食物回复 | FoodHeal |

## 세력·곳·이야기

| 한국어 | 영어 | 간체 |
| --- | --- | --- |
| 혈영회 | Blood Shadow Society | 血影会 |
| 하오문 | Lower Five Gates | 下五门 |
| 혈겁 | Blood Calamity | 血劫 |
| 계승 엔딩 | Inheritance Ending | 继承结局 |
| 객잔 | Inn | 客栈 |
| 침방 | Sleeping Quarters | 寝房 |
| 연무장 | Training Ground | 练武场 |
| 장경각 | Sutra Library | 藏经阁 |
| 정산 | Reckoning | 结算 |
| 목 노인 | Elder Mok | 사람 결정 대기 |
| 운낭 | Unnang | 사람 결정 대기 |

## 경지

| 한국어 | 영어 | 간체 |
| --- | --- | --- |
| 삼류 | Third-Rate | 三流 |
| 이류 | Second-Rate | 二流 |
| 일류 | First-Rate | 一流 |
| 절정 | Peak | 绝顶 |
| 초절정 | Beyond Peak | 超绝顶 |
| 화경 | Harmony Realm | 化境 |
| 현경 | Mystic Realm | 玄境 |

## 문파·세가

| 한국어 | 영어 | 간체 |
| --- | --- | --- |
| 소림 | Shaolin | 少林 |
| 무당 | Wudang | 武当 |
| 화산 | Mount Hua | 华山 |
| 곤륜 | Kunlun | 昆仑 |
| 개방 | Beggars' Sect | 丐帮 |
| 남궁 / 남궁세가 | Namgung / Namgung Clan | 南宫 / 南宫世家 |
| 당가 | Dang Clan | 唐门 |
| 수로채 | Waterway Fort | 水路寨 |
| 마교 | Demonic Cult | 魔教 |

당가: 영어는 원칙 3(사람 이름과 같은 Dang), 간체는 중국어 장르 관용 그대로 唐门.

## 지역

| 한국어 | 영어 | 간체 |
| --- | --- | --- |
| 청운진 | Cheongun Town | 青云镇 |
| 숭산 | Mount Song | 嵩山 |
| 개방 총타 | Beggars' Sect Headquarters | 丐帮总舵 |
| 남궁세가 | Namgung Clan Manor | 南宫世家 |
| 당가타 | Dang Clan Fort | 唐家堡 |
| 장강 수로 | Yangtze Waterway | 长江水路 |
| 무당산 | Mount Wudang | 武当山 |
| 화산 | Mount Hua | 华山 |
| 곤륜 설원 | Kunlun Snowfield | 昆仑雪原 |
| 십만대산 | The Hundred Thousand Mountains | 十万大山 |
| 봉마곡 혈지 | Demon-Sealing Valley Blood Pool | 封魔谷血池 |

봉인석 일곱(천추·천선·천기·천권·옥형·개양·요광)은 북두칠성 별의 옛 이름이다. 영어는 한자 뜻으로 옮기고 서양 별 이름(Dubhe 등)은 쓰지 않는다: Heavenly Pivot, Heavenly Jade, Heavenly Pearl, Heavenly Authority, Jade Balance, Opening Yang, Flickering Light 뒤에 Stone. 간체는 天枢石·天璇石·天玑石·天权石·玉衡石·开阳石·摇光石.

## 사람(대표 인물)

| 한국어 | 영어 | 간체 |
| --- | --- | --- |
| 진소운 | Jin Sowoon | 秦昭云 |
| 남궁현 | Namgung Hyeon | 南宫玄 |
| 혜공 | Hyegong | 慧空 |
| 당연화 | Dang Yeonhwa | 唐莲华 |
| 하연 | Ha Yeon | 何燕 |
| 운화 | Unhwa | 云华 |
| 마칠 | Ma Chil | 马七 |
| 백야 | Baekya | 白夜 |

땅의 주인과 객잔 사람(이름만. 별호는 위 표·데이터 그대로 앞에 붙인다)

| 한국어 | 영어 | 간체 |
| --- | --- | --- |
| 장삼 | Jang Sam | 张三 |
| 원각 | Wongak | 圆觉 |
| 당소혜 | Dang Sohye | 唐昭惠 |
| 마천패 | Ma Cheonpae | 马天霸 |
| 혈허진인 | Hyeolheo the Perfected | 血虚真人 |
| 설무향 | Seol Muhyang | 雪无香 |
| 혁련휘 | Hyeokryeon Hwi | 赫连辉 |
| 단목현 | Danmok Hyeon | 端木玄 |
| 목 노인 | Elder Mok | 穆老人 |
| 운낭 | Unnang | 云娘 |
| 노고수 | Old Master | 老高手 |
| 청허진인 | Cheongheo the Perfected | 清虚真人 |
| 검혼(낙매검선의) | Sword Soul | 剑魂 |
| 천마 | Heavenly Demon | 天魔 |

간체 사람 이름은 2026-11-05 사람이 정한 한자다(backlog L-04b, 뜻과 장르 관습으로 고름). 노고수·청허진인·검혼·천마 넷은 대사 간체(L-07)를 옮기며 클라우드가 뜻대로 붙였다(검혼은 땅의 주인 이름 落梅剑仙之剑魂 의 끝, 천마는 오의 天魔解体 와 같은 글자). 번체는 같은 글자를 번체로(秦昭雲·南宮玄·唐蓮華·雲華·馬七 / 張三·圓覺·馬天霸·血虛真人·雪無香·赫連輝·穆老人·雲娘). 대사·story 에 한자를 쓸 때도 이 표.

## 대사(영어) — L-06 에서 정한 말 (2026-11-04)

`dialogue.json` 을 옮기며 정한 것. 말투(목 노인 하게체, 운낭 해요체, 혜공 하십시오체)는 영어에선 낱말 고르기로만 살린다: 목 노인 짧고 무겁게, 운낭·마칠 가볍게, 혜공 정중하게(benefactor), 백야 끊어서.

| 한국어 | 영어 | 쓰는 곳 |
| --- | --- | --- |
| 사제 / 사매(부를 때) | Junior Brother / Junior Sister | 혜공·운화·남궁현 마당 줄 |
| 시주 | benefactor | 혜공 |
| 법명 | dharma name | 혜공 |
| 스승님 / 사부님 | Master | 혜공·운화·원각·혈허진인 |
| 운낭 아주머니 | Madam Unnang | 혜공 |
| 주인장 | the innkeeper | 마칠·하연 |
| 고모 | Aunt | 당연화·당소혜 |
| 본가 / 방계 | the main house / branch family(branch child) | 당가 |
| 이놈 / 이 계집애(마칠) | the brat / the girl | 개방 몸 |
| 점소이 | errand boy | 남궁현 |
| 소가주 / 가주 | young master / clan head | 남궁 |
| 목탁 | wooden fish | 원각 컷 |
| 범종 | the great bell | 소림 |
| 혈독 / 혈기 | blood poison / blood qi | 땅의 주인 컷 |
| 고쳐라 | Heal him | 화산 검혼·혈허진인 컷 |
| 성별을 모르는 '그 사람'(목 노인) | that one | 목 노인 |
| 삼결 제자 | three-knot disciple | 개방 카드 |
| 오대세가 | the Five Great Clans | 남궁 카드 |
| 천마신교 | Heavenly Demon Cult | 마교 카드·혁련휘 컷 |
| 정파 | the orthodox sects | 혁련휘 컷 |
| 사파 | the unorthodox sects | 인트로 컷 2 |
| 대운 32년(연호) | Year 32 of Daeun | 인트로 컷 4 |
| 회혼침 | Soul-Recalling Needle | 엔딩 컷 |
| 의심(醫心, 진엔딩) | Physician's Heart | 엔딩 고르는 화면 |
| 백중의 밤 | the Ghost Festival night | 종장 혈월 줄 |

## 번체(繁體) — 상점 글과 화면에 쓴 말 (ST-06·L-08, 2026-11-04)

상점 글(`docs/store/store_text.md`)과 게임 화면 번체(`strings/zh-Hant.json`, L-08)가 같이 쓴다. 대만 표기를 따른다: `zh-Hans.json` 을 OpenCC s2twp 로 바꾼 뒤 아래 표와 그 밑 설명대로 고친다(`python3 tools/make_hant.py`, 고칠 말은 그 파일 FIXES). 간체에 문구를 더하면 이 도구로 번체도 같은 키로 더한다(테스트 `HantStringsTests` 가 키·글자 수·이 표 말·간체 글자 섞임·Noto Serif TC 글자를 본다).

| 한국어 | 간체 | 번체 |
| --- | --- | --- |
| 경지 | 境界 | 境界 |
| 진기 | 真气 | 真氣 |
| 오의 | 奥义 | 奧義 |
| 절기 | 绝技 | 絕技 |
| 심법 | 心法 | 心法 |
| 깨달음 | 顿悟 | 頓悟 |
| 경공 | 轻功 | 輕功 |
| 타고난 성질 | 天赋 | 天賦 |
| 윤회의 기 | 轮回之气 | 輪迴之氣 |
| 땅의 주인 | 一方之主 | 一方之主 |
| 봉인석 | 封印石 | 封印石 |
| 혈우 장막 | 血雨帷幕 | 血雨帷幕 |
| 주화입마 | 走火入魔 | 走火入魔 |
| 음양 / 태극 | 阴阳 / 太极 | 陰陽 / 太極 |
| 객잔 | 客栈 | 客棧 |
| 연무장 | 练武场 | 練武場 |
| 문파 | 门派 | 門派 |
| 당가 | 唐门 | 唐門 |
| 절정 | 绝顶 | 絕頂 |
| 현경 | 玄境 | 玄境 |
| 강시(떼를 두루 이를 때) | 僵尸 | 殭屍 |
| 얼리 액세스 | 抢先体验 | 搶先體驗 |
| 게임패드 | 手柄 | 控制器 |
| 회복(기혈·음식) | 回复 | 回復 |
| 종(범종·종루) | 钟 | 鐘 |
| 독후(당소혜 별호) | 毒后 | 毒后 |
| 확률 | 几率 | 機率 |
| 열다(상자·행낭) | 打开 | 打開 |
| 검 | 剑 | 劍 |
| 비급 | 秘笈 | 秘笈 |
| 수련 | 修炼 | 修煉 |

`강시` 는 적 하나의 이름(尸僵 등 데이터 값)이 아니라 떼를 두루 말할 때의 말이다.

OpenCC s2twp 가 뜻을 잘못 짚어 손으로 고친 것(L-08): 回复(회복)를 回覆(답장)로, 钟(종)을 鍾(성·술잔)으로, 毒后(독의 여왕)를 毒後로, '20发 지풍'의 发(발)를 髮(머리카락)로 바꾸므로 위 표대로 되돌린다. 상자·행낭을 '열다'는 開啟 대신 打開. 따옴표 “ ”·‘ ’ 는 대만식 「 」·『 』 로.
