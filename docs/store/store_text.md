# 스팀 상점 문구 초안 (ST-01)

M2 가 끝나면(2027-02 말) 스토어 페이지를 연다(기획서 16·17장). 이 파일은 그때 Steamworks 에 붙일 글의 초안이다. 한국어가 원문이고 영어·간체·번체는 같은 뜻으로 옮겼다(간체·번체는 ST-06, 2026-11-04).

- 용어는 용어집(`docs/design/glossary.md`)을 따른다. 영어는 2026-11-04 에 용어집에 맞춰 고쳤다(Lightness Skill → Lightfoot, Secret Art → Pinnacle Art, cultivation method → Heart Method, innate trait → Birth Trait, Profound → Mystic Realm, Tang → Dang, seal shard → Seal Stone). 간체는 용어집 간체 칸, 번체는 용어집 '번체(상점 글)' 표.
- 간체·번체 글에는 진소운 이름을 쓰지 않고 '客栈的跑堂少年'으로 둔다. 간체 사람 이름 한자가 아직 사람 결정 대기(backlog L-04b)라서다. 정해지면 넣을지 다시 본다.
- 간체·번체는 원어민 검수 전이다. 번체는 간체를 OpenCC(s2twp, 대만 낱말)로 바꾼 뒤 손으로 고쳤다(影像·控制器·「」 따옴표).
- 다른 게임 이름은 쓰지 않는다. 장르는 Steam 태그 이름(Bullet Heaven·Roguelite)으로 말한다.
- 링크는 넣지 않는다. 2024년 9월부터 Valve 가 소개 글 칸(짧은 소개·긴 소개·특별 공지·수상)에 바깥 사이트와 다른 Steam 상품 링크를 막았다(기사 출처는 `docs/store/README.md`).
- 이미지에 글자를 굽지 않는다는 규칙(CLAUDE.md)은 긴 소개의 움짤에도 같다. 소제목은 Steam 서식 글자로 쓴다.
- 숫자는 얼리 액세스 목표 볼륨(1장 목표)이고 아래 '숫자와 출처' 표의 데이터와 같아야 한다. 테스트(`StoreCopyTests`)가 확인한다. 페이지를 여는 M2 시점엔 아직 다 들어 있지 않으니 긴 소개 끝에 '얼리 액세스 기준' 한 줄을 둔다.

## 숫자와 출처

| 항목 | 숫자 | 데이터 |
| --- | --- | --- |
| 문파 | 9 | cheonha.json sects |
| 공격 무공 | 16 | weapons.json |
| 심법 | 16 | passives.json |
| 절기 | 16 | evolutions.json |
| 지역 | 11 | cheonha.json regions |
| 땅의 주인 | 7 | cheonha.json regions 의 boss(봉마곡 혈마 빼고) |
| 경지 | 7 | cheonha.json realmScale |
| 타고난 성질 | 11 | cheonha.json birthTraits.list |
| 봉인석 | 7 | cheonha.json bosses.finalRequires.sealStones |
| 축척 | 2.25 | 기획서 7장(경지마다 세상이 2.25배) |

## 짧은 소개 (Short Description)

검색 결과와 페이지 맨 위에 나오는 글. Steamworks 문서는 정확한 글자 수 없이 '몇백 자로 제한'(limited to a few hundred characters), 글은 꾸밈 없는 평문만이라고 적는다. 300자는 업계 안내서들의 공통 숫자라 그 안으로 맞춘다(README, 2026-11-07 확인). 테스트가 두 언어 모두 300자 이하인지 본다.

### 한국어

<!-- store:short:ko -->
혈마가 깨어나 피의 비가 내리는 중원. 당신의 혼은 죽을 때마다 다른 문파 제자의 몸으로 다시 태어납니다. 밀려오는 강시 떼를 버티며 경지를 쌓으세요. 경지가 오를 때마다 세상이 작아져, 문파 마당에서 시작한 싸움이 끝내 중원 전체로 번집니다. 공격은 알아서 나가고, 당신은 걷고 피하고 오의를 터뜨립니다.
<!-- /store:short -->

### English

<!-- store:short:en -->
Blood rain falls and the dead rise as jiangshi. Each time you die, your soul is reborn as another sect's disciple. Survive the horde and break through Realm after Realm as the world shrinks, from one courtyard to the whole land. Attacks are automatic. You move, dodge and unleash your Ultimate Art.
<!-- /store:short -->

### 简体中文

<!-- store:short:zh-Hans -->
血魔苏醒，血雨笼罩中原。你的魂魄每死一次，便借另一名门派弟子的躯壳重生。在汹涌的僵尸群中活下来，一层层突破境界。境界每升一层，世界便缩小一圈，起于门派庭院的厮杀终将蔓延到整个中原。攻击自动施放，你只需走位、闪避，并释放奥义。
<!-- /store:short -->

### 繁體中文

<!-- store:short:zh-Hant -->
血魔甦醒，血雨籠罩中原。你的魂魄每死一次，便借另一名門派弟子的軀殼重生。在洶湧的殭屍群中活下來，一層層突破境界。境界每升一層，世界便縮小一圈，起於門派庭院的廝殺終將蔓延到整個中原。攻擊自動施放，你只需走位、閃避，並釋放奧義。
<!-- /store:short -->

## 긴 소개 (About This Game)

Steam 서식(BBCode)으로 옮겨 붙인다. `##` 은 `[h2]`, `-` 목록은 `[list][*]`. 움짤 자리는 ST-02 의 장면 번호로 적었다.

### 한국어

<!-- store:long:ko -->
삼백 년 전 정파·사파·마교가 함께 봉인한 혈마가 깨어났습니다. 그 피는 하늘로 올라 붉은 비가 되었고, 비를 맞은 시체는 강시로 일어납니다. 내공이 깊은 무인만이 이 비를 버틸 수 있습니다.

객잔 심부름꾼 진소운은 혈우의 첫 밤에 이름 모를 노고수의 내공을 받고 쓰러집니다. 그러나 혼은 사라지지 않습니다. 그날 밤 숨진 문파 제자의 몸을 빌려 다시 일어나, 일곱 봉인석 조각을 되찾아 혈마를 다시 가두어야 합니다.

[움짤: 장면 3 — 강시 떼]

## 걷기만 하세요, 무공은 알아서 나갑니다
움직이면 몸에 익힌 무공이 저절로 나갑니다. 당신이 할 일은 떼 사이로 길을 내고, 경공으로 탄막을 빠져나가고, 진기가 찼을 때 문파의 오의를 터뜨리는 것입니다.

## 경지가 오르면 세상이 작아집니다
레벨이 오르다 경지의 문턱에 닿으면 화면이 멈추고 수묵 심상이 펼쳐집니다. 깨달음 하나를 고르고 나면 세상이 2.25배 작아집니다. 삼류일 때 화면 가득하던 문파 마당은 절정에 이르면 점 하나가 되고, 현경에 오르면 중원 전체가 한 화면에 들어옵니다. 아래 경지의 적은 발밑에서 밟히고, 장막 너머에서는 더 큰 군세가 기다립니다.

[움짤: 장면 5 — 경지 돌파]

## 죽음은 끝이 아닙니다
죽으면 혼은 무너진 객잔으로 돌아옵니다. 쌓은 윤회의 기로 연무장에서 혼을 단련하고, 그날 밤 숨진 제자 셋 가운데 다음 몸을 고르세요. 소림 승려의 단단한 몸일 수도, 당가 독수의 날랜 몸일 수도 있습니다. 몸마다 타고난 성질이 붙어 '이번 생은 무엇을 하러 태어났나'가 달라집니다.

## 음과 양 사이에서
무공마다 음양이 있어 무엇을 키우느냐에 따라 태극이 기웁니다. 균형을 맞추면 안정된 힘을, 한쪽으로 몰면 큰 화력과 함께 주화입마의 위험을 얻습니다.

[움짤: 장면 7 — 절기 각성]

## 담긴 것 (얼리 액세스 기준)
- 문파 9곳, 저마다 다른 시작 무공·고유 능력·오의
- 공격 무공 16종, 심법 16종, 무공과 심법이 만나 깨어나는 절기 16종
- 경지 7단계: 삼류에서 현경까지
- 지역 11곳과 땅의 주인 7, 봉인석 일곱 조각
- 타고난 성질 11가지
- 혈마의 진실과 세 갈래 엔딩
- 키보드와 게임패드 완전 지원, Steam Deck 목표

## 강해지는 길은 플레이뿐입니다
확률형 뽑기도, 능력치나 무공을 파는 상품도 없습니다. 게임을 한 번 사면 혼을 단련하는 모든 것은 플레이로 얻습니다.
<!-- /store:long -->

### English

<!-- store:long:en -->
Three hundred years ago, the righteous sects, the unorthodox factions and the Demonic Cult joined hands to seal away the Blood Demon. Now he has awoken. His blood has risen into the sky and falls again as crimson rain, and every corpse it touches rises as a jiangshi. Only martial artists with deep inner power can withstand the rain.

On the first night of the blood rain, Jin Sowoon, an errand boy at a roadside inn, receives the last inner power of a dying master and falls. But his soul does not fade. Borrowing the body of a sect disciple who died that same night, he rises again to recover the seven Seal Stones and imprison the Blood Demon once more.

[GIF: Scene 3 - jiangshi horde]

## Just move. Your martial arts do the rest
Walk, and the arts your body knows strike on their own. Your job is to cut a path through the horde, slip through bullets with Lightfoot, and unleash your sect's Ultimate Art when your Inner Energy is full.

## Break through, and the world shrinks
Reach the threshold of a new Realm and time stops as an ink-wash vision unfolds. Choose one Insight, and the world becomes 2.25 times smaller. The sect courtyard that filled your screen as a Third-Rate fighter is a single dot by the Peak Realm, and at the Mystic Realm the whole land fits on one screen. Lesser foes are crushed underfoot, while greater armies wait beyond the Blood Rain Veil.

[GIF: Scene 5 - Realm breakthrough]

## Death is not the end
When you die, your soul returns to the ruined inn. Spend the Samsara Qi you gathered to train your soul, then choose your next body from three disciples who died that night. It may be the sturdy body of a Shaolin monk, or the quick hands of a Dang Clan poisoner. Every body comes with Birth Traits, so each life asks a different question: what were you reborn to do?

## Between Yin and Yang
Every art leans toward yin or yang, and what you grow tips your Taiji gauge. Stay balanced for steady strength, or push to one side for great power at the risk of Qi Deviation.

[GIF: Scene 7 - Pinnacle Art awakening]

## What's inside (at Early Access)
- 9 sects, each with its own starting art, unique ability and Ultimate Art
- 16 attack arts, 16 Heart Methods, and 16 Pinnacle Arts awakened when an art meets its method
- 7 Realms, from Third-Rate to Mystic
- 11 regions, 7 Lords of the Land, and seven Seal Stones
- 11 Birth Traits
- The truth of the Blood Demon and three endings
- Full keyboard and gamepad support, Steam Deck as a target

## The only way to get stronger is to play
No loot boxes. No paid stats or arts. Buy the game once, and everything that makes your soul stronger is earned in play.
<!-- /store:long -->

### 简体中文

<!-- store:long:zh-Hans -->
三百年前，正派、邪派与魔教联手封印了血魔。如今血魔苏醒，他的血升上天空，化作猩红的雨落下，被雨淋到的尸体便化为僵尸起身。唯有内功深厚的武人才能抵挡这场雨。

血雨降临的第一夜，客栈的跑堂少年接下一位无名老高手临终传来的内功，随即倒下。但他的魂魄没有消散。他借那一夜死去的门派弟子之躯重新站起，要夺回七块封印石，再一次封印血魔。

[动图：场景 3 — 僵尸群]

## 只管走，武功自会出手
一迈步，身上练成的武功便自行施展。你要做的是在尸群中杀出一条路，用轻功穿过弹幕，在真气蓄满时释放本门奥义。

## 境界提升，世界缩小
等级升到境界的门槛时，画面会静止，展开一幅水墨心象。选定一项顿悟后，世界便缩小 2.25 倍。三流时铺满画面的门派庭院，到了绝顶只剩一个点；登上玄境，整个中原尽收于一屏。低境界的敌人在脚下被踏碎，血雨帷幕之外还有更庞大的大军在等待。

[动图：场景 5 — 境界突破]

## 死亡并非终点
死后，魂魄会回到残破的客栈。用积攒的轮回之气在练武场锤炼魂魄，再从那一夜死去的三名弟子中挑选下一副躯壳。也许是少林僧人结实的身躯，也许是唐门毒手灵巧的身手。每副躯壳都带着天赋，“这一生为何而来”也随之不同。

## 阴阳之间
每门武功都分阴阳，培养什么，太极就向哪边倾斜。保持平衡可得稳定之力；偏向一端则能得到强大火力，也要承担走火入魔的风险。

[动图：场景 7 — 绝技觉醒]

## 内容（以抢先体验版为准）
- 9 个门派，各有不同的起始武功、独门能力与奥义
- 16 种攻击武功、16 种心法，以及武功与心法相遇时觉醒的 16 种绝技
- 7 层境界：从三流到玄境
- 11 个地区与 7 位一方之主，七块封印石
- 11 种天赋
- 血魔的真相与三种结局
- 完整支持键盘与手柄，以 Steam Deck 为目标

## 变强的唯一途径是游玩
没有抽卡开箱，也没有出售属性或武功的商品。游戏只需买断一次，锤炼魂魄所需的一切都在游玩中获得。
<!-- /store:long -->

### 繁體中文

<!-- store:long:zh-Hant -->
三百年前，正派、邪派與魔教聯手封印了血魔。如今血魔甦醒，他的血升上天空，化作猩紅的雨落下，被雨淋到的屍體便化為殭屍起身。唯有內功深厚的武人才能抵擋這場雨。

血雨降臨的第一夜，客棧的跑堂少年接下一位無名老高手臨終傳來的內功，隨即倒下。但他的魂魄沒有消散。他借那一夜死去的門派弟子之軀重新站起，要奪回七塊封印石，再一次封印血魔。

[動圖：場景 3 — 殭屍群]

## 只管走，武功自會出手
一邁步，身上練成的武功便自行施展。你要做的是在屍群中殺出一條路，用輕功穿過彈幕，在真氣蓄滿時釋放本門奧義。

## 境界提升，世界縮小
等級升到境界的門檻時，畫面會靜止，展開一幅水墨心象。選定一項頓悟後，世界便縮小 2.25 倍。三流時鋪滿畫面的門派庭院，到了絕頂只剩一個點；登上玄境，整個中原盡收於一屏。低境界的敵人在腳下被踏碎，血雨帷幕之外還有更龐大的大軍在等待。

[動圖：場景 5 — 境界突破]

## 死亡並非終點
死後，魂魄會回到殘破的客棧。用積攢的輪迴之氣在練武場錘鍊魂魄，再從那一夜死去的三名弟子中挑選下一副軀殼。也許是少林僧人結實的身軀，也許是唐門毒手靈巧的身手。每副軀殼都帶著天賦，「這一生為何而來」也隨之不同。

## 陰陽之間
每門武功都分陰陽，培養什麼，太極就向哪邊傾斜。保持平衡可得穩定之力；偏向一端則能得到強大火力，也要承擔走火入魔的風險。

[動圖：場景 7 — 絕技覺醒]

## 內容（以搶先體驗版為準）
- 9 個門派，各有不同的起始武功、獨門能力與奧義
- 16 種攻擊武功、16 種心法，以及武功與心法相遇時覺醒的 16 種絕技
- 7 層境界：從三流到玄境
- 11 個地區與 7 位一方之主，七塊封印石
- 11 種天賦
- 血魔的真相與三種結局
- 完整支援鍵盤與控制器，以 Steam Deck 為目標

## 變強的唯一途徑是遊玩
沒有抽卡開箱，也沒有出售屬性或武功的商品。遊戲只需買斷一次，錘鍊魂魄所需的一切都在遊玩中獲得。
<!-- /store:long -->

## 특징 다섯 (상점 요약·보도 자료·영상 자막)

| | 한국어 | English |
| --- | --- | --- |
| 1 | **경지가 오르면 세상이 작아진다**: 돌파할 때마다 세상이 2.25배 작아져, 문파 마당에서 시작한 싸움이 중원 전체로 번진다 | **Break through, and the world shrinks**: every Realm makes the world 2.25 times smaller, until your fight spans the whole land |
| 2 | **죽을 때마다 다른 몸**: 문파 9곳의 제자 가운데 셋 중 하나로 환생. 문파와 타고난 성질이 생마다 다른 판을 만든다 | **A new body every death**: be reborn as one of three fallen disciples from 9 sects, each life shaped by sect and Birth Traits |
| 3 | **음양과 주화입마**: 무엇을 키우느냐에 따라 태극이 기운다. 균형의 안정이냐, 극성의 화력이냐 | **Yin, Yang and Qi Deviation**: your build tips the Taiji gauge. Steady balance, or raw power at the edge |
| 4 | **자동 공격, 손은 바쁘게**: 공격은 알아서, 경공 회피와 문파 오의는 직접 | **Auto attacks, busy hands**: arts strike on their own, while you dodge with Lightfoot and fire your sect's Ultimate Art |
| 5 | **수묵으로 그린 무협**: 먹선과 담채로 그린 중원, 붓 획으로 터지는 무공. 힘을 파는 결제 없음 | **Wuxia in ink and wash**: a hand-painted land and brushstroke martial arts. No paid power, ever |

간체:

1. **境界提升，世界缩小**：每次突破，世界缩小 2.25 倍，起于门派庭院的厮杀最终蔓延到整个中原
2. **每死一次，换一副躯壳**：从 9 个门派中三名死去的弟子里选一人重生，门派与天赋让每一生都不一样
3. **阴阳与走火入魔**：培养什么，太极就向哪边倾斜。要平衡的稳定，还是极端的火力
4. **自动攻击，双手不闲**：攻击自动施放，轻功闪避与本门奥义由你亲手掌控
5. **水墨武侠**：墨线与淡彩绘成的中原，笔触迸发的武功。绝无出售战力的付费

번체:

1. **境界提升，世界縮小**：每次突破，世界縮小 2.25 倍，起於門派庭院的廝殺最終蔓延到整個中原
2. **每死一次，換一副軀殼**：從 9 個門派中三名死去的弟子裡選一人重生，門派與天賦讓每一生都不一樣
3. **陰陽與走火入魔**：培養什麼，太極就向哪邊傾斜。要平衡的穩定，還是極端的火力
4. **自動攻擊，雙手不閒**：攻擊自動施放，輕功閃避與本門奧義由你親手掌控
5. **水墨武俠**：墨線與淡彩繪成的中原，筆觸迸發的武功。絕無出售戰力的付費

## 태그 후보 (무게 순)

개발자는 태그를 20개까지 붙이고, 위 다섯이 게임을 가장 잘 말해야 한다. 일부 필터는 앞 15개만 본다(Steamworks 'Steam Tags' 문서를 인용한 검색 결과, README). Bullet Heaven·Wuxia 는 2026년 5월 태그 개편에서 새로 생겼다.

| 순 | 태그 | 이유 |
| --- | --- | --- |
| 1 | Bullet Heaven | 장르 그 자체(자동 공격 + 떼 + 성장 선택) |
| 2 | Wuxia | 세계관. 문파·내공이 Valve 정의와 맞음 |
| 3 | Roguelite | 생이 끝나도 윤회의 기·해금이 남음 |
| 4 | Action Roguelike | 같은 장르 이용자가 많이 찾는 태그 |
| 5 | Top-Down | 시점 |
| 6 | Martial Arts | 무공(2026-05 개편 기사들의 지운·합친 목록에 없음 — 남아 있는 것으로 봄, 페이지 만들 때 태그 고르기 칸에서 확인) |
| 7 | Hand-drawn | 수묵 채색 |
| 8 | 2D | |
| 9 | Zombies | 강시 떼 |
| 10 | Dark Fantasy | 혈마·혈우 |
| 11 | Singleplayer | |
| 12 | Replay Value | 환생·성질·빌드 조합 |
| 13 | Stylized | |
| 14 | Atmospheric | |
| 15 | Controller | 게임패드 완전 지원(15장) |
| 16 | Indie | |
| 17 | Action | |
| 18 | Casual | 같은 장르 상위 게임들이 흔히 붙임 |
| 19 | Fantasy | |
| 20 | Arcade | |

넣지 않은 것: Xianxia(선술·수선이 아니라 무협), Bullet Hell(반대 뜻), Pixel Graphics(그림체가 다름), Multiplayer(없음).

## 시스템 사양 초안

권장은 기획서 15장 성능 목표에서 왔다. 최소는 아직 잴 빌드가 없어 **측정 대기**다. 아래 '최소(추천안)' 칸은 측정 전 자리만 잡은 값이고, M2 빌드를 낮은 PC 에서 잰 뒤 고친다(`docs/store/README.md` 의 할 일).

| 항목 | 최소(추천안, 측정 대기) | 권장 |
| --- | --- | --- |
| 운영체제 | Windows 10 64비트(21H1 이상) | Windows 10·11 64비트 |
| 프로세서 | 측정 대기 | 측정 대기 |
| 메모리 | 측정 대기 | 측정 대기 |
| 그래픽 | DirectX 11 지원 | GTX 1060급(1920×1080 60fps, 적 600·투사체 1,500) |
| DirectX | 11 | 11 |
| 저장 공간 | 측정 대기 | 측정 대기 |
| 추가 사항 | 게임패드 권장 | Steam Deck: 1280×800 60fps(적 400) 목표, 인증(Verified) 목표 |

| Item | Minimum (draft, to be measured) | Recommended |
| --- | --- | --- |
| OS | Windows 10 64-bit (21H1 or later) | Windows 10/11 64-bit |
| Processor | TBD | TBD |
| Memory | TBD | TBD |
| Graphics | DirectX 11 capable | GTX 1060 class (1920x1080 at 60 fps) |
| DirectX | Version 11 | Version 11 |
| Storage | TBD | TBD |
| Additional Notes | Gamepad recommended | Steam Deck: 1280x800 at 60 fps target |

운영체제를 Windows 10 21H1 부터로 둔 것은 Unity 6 플레이어 최소 사양이 'Windows 10 version 21H1 (build 19043) or newer'이기 때문이다(Unity 6.3(6000.3) 문서, 2026-11-07 확인, README). 같은 표의 그래픽은 DX10·DX11·DX12·Vulkan, 프로세서는 SSE2 지원 x86·x64 또는 Arm64 라 최소 칸 'DirectX 11'은 그 안에 든다.

## 언어

| 언어 | 화면 | 음성 | 자막 | 언제 |
| --- | --- | --- | --- | --- |
| 한국어 | ✓ | 정하지 않음 | ✓ | 페이지 공개부터 |
| English | ✓ | | ✓ | M3 데모(현지화 1차) |
| 简体中文 | ✓ | | ✓ | M3 데모(현지화 1차) |
| 繁體中文 | ✓ | | ✓ | 16장 2순위. 1장은 출시 언어에 넣음 |
| 日本語 | ✓ | | ✓ | 얼리 액세스 뒤 |

Steam 언어 칸에는 그 시점에 실제로 들어 있는 언어만 체크한다. 페이지를 여는 M2 에 영어·간체가 아직 없으면 '출시 때 지원 예정'을 긴 소개에 한 줄로 적는다.

## AI 생성 콘텐츠 공개 문구 초안

Steam 은 AI 로 만든 콘텐츠를 콘텐츠 설문에 밝히게 한다(16장). 게임이 돌아가는 동안 AI 가 새로 만드는 것은 없다. 아래는 미리 만든 것(Pre-Generated)의 설명 초안이다. 배경음은 Gemini 이용 조건 확인(BGM-02)이 끝나야 확정한다.

- 한국어: 캐릭터·적·배경·UI 바탕·이펙트 재료의 원화 일부는 이미지 생성 AI 로 시안을 뽑은 뒤 사람이 고르고 다듬어 썼습니다. 스토어 캡슐 그림은 AI 시안을 바탕으로 사람이 손본 최종본입니다. 배경음 일부는 음악 생성 AI 로 만들었습니다. 대사·설명 글의 초안 일부도 AI 도구로 쓰고 사람이 고치고 검수했습니다. 게임 실행 중에 AI 가 콘텐츠를 만들지는 않습니다.
- English: Some character, enemy, background, UI and visual effect art began as drafts from an image generation AI, then was selected and edited by our team. The store capsules are final versions hand-finished from AI drafts. Some background music was made with a music generation AI. Some dialogue and description text was first drafted with AI tools, then rewritten and reviewed by our team. No content is generated by AI while the game is running.
- 简体中文: 角色、敌人、背景、UI 底图与特效素材的部分原画，先由图像生成 AI 产出草稿，再由团队挑选并修改后使用。商店胶囊图是以 AI 草稿为基础、经人工修饰的最终版本。部分背景音乐由音乐生成 AI 制作。部分对白与说明文字的初稿也用 AI 工具撰写，再由团队改写和审校。游戏运行期间不会由 AI 生成内容。
- 繁體中文: 角色、敵人、背景、UI 底圖與特效素材的部分原畫，先由影像生成 AI 產出草稿，再由團隊挑選並修改後使用。商店膠囊圖是以 AI 草稿為基礎、經人工修飾的最終版本。部分背景音樂由音樂生成 AI 製作。部分對白與說明文字的初稿也用 AI 工具撰寫，再由團隊改寫和審校。遊戲執行期間不會由 AI 生成內容。

글 초안에 AI 를 쓴 것(대사·설명·기획서)을 어디까지 밝힐지, 배경음 줄을 넣을지는 사람이 정할 일이다(README '정할 것'). 초안은 넓게 밝히는 쪽으로 썼다.
