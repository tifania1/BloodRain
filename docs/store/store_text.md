# 스팀 상점 문구 초안 (ST-01)

M2 가 끝나면(2027-02 말) 스토어 페이지를 연다(기획서 16·17장). 이 파일은 그때 Steamworks 에 붙일 글의 초안이다. 한국어가 원문이고 영어는 같은 뜻으로 옮겼다. 간체·번체는 현지화 줄기(L-04)에서 이 글을 옮긴다.

- 영어 용어는 16장 예시(Inner Energy·Realm·Ultimate Art)를 따랐다. 용어집(docs/design 의 glossary.md, L-02)이 생기면 그쪽에 맞춰 고친다.
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

검색 결과와 페이지 맨 위에 나오는 글. 영어 300자 안팎이 한도라는 것이 업계 안내서들의 공통 숫자다(Steamworks 문서 확인 필요, README). 테스트가 두 언어 모두 300자 이하인지 본다.

### 한국어

<!-- store:short:ko -->
혈마가 깨어나 피의 비가 내리는 중원. 당신의 혼은 죽을 때마다 다른 문파 제자의 몸으로 다시 태어납니다. 밀려오는 강시 떼를 버티며 경지를 쌓으세요. 경지가 오를 때마다 세상이 작아져, 문파 마당에서 시작한 싸움이 끝내 중원 전체로 번집니다. 공격은 알아서 나가고, 당신은 걷고 피하고 오의를 터뜨립니다.
<!-- /store:short -->

### English

<!-- store:short:en -->
Blood rain falls and the dead rise as jiangshi. Each time you die, your soul is reborn as another sect's disciple. Survive the horde and break through Realm after Realm as the world shrinks, from one courtyard to the whole land. Attacks are automatic. You move, dodge and unleash your Ultimate Art.
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

On the first night of the blood rain, Jin Sowoon, an errand boy at a roadside inn, receives the last inner power of a dying master and falls. But his soul does not fade. Borrowing the body of a sect disciple who died that same night, he rises again to recover the seven shards of the seal and imprison the Blood Demon once more.

[GIF: Scene 3 - jiangshi horde]

## Just move. Your martial arts do the rest
Walk, and the arts your body knows strike on their own. Your job is to cut a path through the horde, slip through bullets with Lightness Skill, and unleash your sect's Ultimate Art when your Inner Energy is full.

## Break through, and the world shrinks
Reach the threshold of a new Realm and time stops as an ink-wash vision unfolds. Choose one Insight, and the world becomes 2.25 times smaller. The sect courtyard that filled your screen as a Third-Rate fighter is a single dot by the Peak Realm, and at the Profound Realm the whole land fits on one screen. Lesser foes are crushed underfoot, while greater armies wait beyond the Blood Rain Veil.

[GIF: Scene 5 - Realm breakthrough]

## Death is not the end
When you die, your soul returns to the ruined inn. Spend the Samsara Qi you gathered to train your soul, then choose your next body from three disciples who died that night. It may be the sturdy body of a Shaolin monk, or the quick hands of a Tang Clan poisoner. Every body comes with innate traits, so each life asks a different question: what were you reborn to do?

## Between Yin and Yang
Every art leans toward yin or yang, and what you grow tips your Taiji gauge. Stay balanced for steady strength, or push to one side for great power at the risk of Qi Deviation.

[GIF: Scene 7 - Secret Art awakening]

## What's inside (at Early Access)
- 9 sects, each with its own starting art, unique ability and Ultimate Art
- 16 attack arts, 16 cultivation methods, and 16 Secret Arts awakened when an art meets its method
- 7 Realms, from Third-Rate to Profound
- 11 regions, 7 Lords of the Land, and seven seal shards
- 11 innate traits
- The truth of the Blood Demon and three endings
- Full keyboard and gamepad support, Steam Deck as a target

## The only way to get stronger is to play
No loot boxes. No paid stats or arts. Buy the game once, and everything that makes your soul stronger is earned in play.
<!-- /store:long -->

## 특징 다섯 (상점 요약·보도 자료·영상 자막)

| | 한국어 | English |
| --- | --- | --- |
| 1 | **경지가 오르면 세상이 작아진다**: 돌파할 때마다 세상이 2.25배 작아져, 문파 마당에서 시작한 싸움이 중원 전체로 번진다 | **Break through, and the world shrinks**: every Realm makes the world 2.25 times smaller, until your fight spans the whole land |
| 2 | **죽을 때마다 다른 몸**: 문파 9곳의 제자 가운데 셋 중 하나로 환생. 문파와 타고난 성질이 생마다 다른 판을 만든다 | **A new body every death**: be reborn as one of three fallen disciples from 9 sects, each life shaped by sect and innate traits |
| 3 | **음양과 주화입마**: 무엇을 키우느냐에 따라 태극이 기운다. 균형의 안정이냐, 극성의 화력이냐 | **Yin, Yang and Qi Deviation**: your build tips the Taiji gauge. Steady balance, or raw power at the edge |
| 4 | **자동 공격, 손은 바쁘게**: 공격은 알아서, 경공 회피와 문파 오의는 직접 | **Auto attacks, busy hands**: arts strike on their own, while you dodge with Lightness Skill and fire your sect's Ultimate Art |
| 5 | **수묵으로 그린 무협**: 먹선과 담채로 그린 중원, 붓 획으로 터지는 무공. 힘을 파는 결제 없음 | **Wuxia in ink and wash**: a hand-painted land and brushstroke martial arts. No paid power, ever |

## 태그 후보 (무게 순)

개발자는 태그를 20개까지 붙이고, 위 다섯이 게임을 가장 잘 말해야 한다. 일부 필터는 앞 15개만 본다(Steamworks 'Steam Tags' 문서를 인용한 검색 결과, README). Bullet Heaven·Wuxia 는 2026년 5월 태그 개편에서 새로 생겼다.

| 순 | 태그 | 이유 |
| --- | --- | --- |
| 1 | Bullet Heaven | 장르 그 자체(자동 공격 + 떼 + 성장 선택) |
| 2 | Wuxia | 세계관. 문파·내공이 Valve 정의와 맞음 |
| 3 | Roguelite | 생이 끝나도 윤회의 기·해금이 남음 |
| 4 | Action Roguelike | 같은 장르 이용자가 많이 찾는 태그 |
| 5 | Top-Down | 시점 |
| 6 | Martial Arts | 무공(태그가 남아 있는지 확인 필요) |
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
| 운영체제 | Windows 10 64비트 | Windows 10·11 64비트 |
| 프로세서 | 측정 대기 | 측정 대기 |
| 메모리 | 측정 대기 | 측정 대기 |
| 그래픽 | DirectX 11 지원 | GTX 1060급(1920×1080 60fps, 적 600·투사체 1,500) |
| DirectX | 11 | 11 |
| 저장 공간 | 측정 대기 | 측정 대기 |
| 추가 사항 | 게임패드 권장 | Steam Deck: 1280×800 60fps(적 400) 목표, 인증(Verified) 목표 |

| Item | Minimum (draft, to be measured) | Recommended |
| --- | --- | --- |
| OS | Windows 10 64-bit | Windows 10/11 64-bit |
| Processor | TBD | TBD |
| Memory | TBD | TBD |
| Graphics | DirectX 11 capable | GTX 1060 class (1920x1080 at 60 fps) |
| DirectX | Version 11 | Version 11 |
| Storage | TBD | TBD |
| Additional Notes | Gamepad recommended | Steam Deck: 1280x800 at 60 fps target |

운영체제를 Windows 10 부터로 둔 것은 Unity 6 플레이어가 더 오래된 Windows 를 지원하지 않는다고 알고 있어서다. Unity 문서에서 확인 필요(README).

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

글 초안에 AI 를 쓴 것(대사·설명·기획서)을 어디까지 밝힐지, 배경음 줄을 넣을지는 사람이 정할 일이다(README '정할 것'). 초안은 넓게 밝히는 쪽으로 썼다.
