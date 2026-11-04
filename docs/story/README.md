# 대사 풀

대사 원문과 쓰는 법(언제, 몇 마디, 어떤 순서)은 이 폴더의 문서이고, 게임이 읽는 사본은 `unity/BloodRain/Assets/Resources/Data/dialogue.json` 이다. 문장을 바꾸면 두 곳을 같이 고친다. 테스트(`DialogueDataTests`, `StoryDocTests`)가 JSON 문장이 문서에 그대로 있는지, 이름·id 가 데이터와 맞는지 본다.

| 파일 | 무엇 | dialogue.json |
| --- | --- | --- |
| `intro.md` | 인트로 컷신 「혈우」 12컷 | `intro` |
| `prologue.md` | 서장 판 안 말풍선, 끝 컷 두 장 | `prologue` |
| `mok.md` | 목 노인 귀환 대사, 첫 환혼, 문파 첫 환생 | `mok`, `bedroom` |
| `unnang.md` | 운낭 귀환 대사, 연무장 | `unnang`, `training` |
| `inn_screens.md` | 침방 줄과 언제 하는지, 정산 자막 | `bedroom`, `settlement` |
| `leaders.md` | 대표 인물 마당·합류·객잔 줄, 사매판 | `leaders` |
| `boss_cuts.md` | 땅의 주인 만남·처치 컷, 싸움 중 한 줄 | `lords` |
| `final.md` | 종장 봉마곡 입장 컷, 단목현 단계 한 줄 | `final` |
| `endings.md` | 엔딩 세 갈래 컷과 고르는 화면 | `endings` |
| `sects.md` | 침방 후보 카드의 문파 소개와 몸 묘사 | `cards` |

조건(`when`)의 뜻은 dialogue.json 묶음마다 `_note` 에 있다. 현지화 키는 `dialogue.<묶음>.<…>` 로 저절로 잡힌다(`Core/Data/Strings.cs`). 고르는 코드는 `Core/Meta/InnDialogue.cs`(귀환 `OnReturn`, 마당 `YardLine`, 연무장 `Training`, 침방 `Bedroom` → `BedroomTalk`, 정산 `Settlement`, 땅의 주인 `LordMeet`·`LordKill`), 인트로·종장·엔딩은 `Core/Meta/FinalDialogue.cs`(`Intro`, `FinalEnter`·`FinalOpponent`, `EndingChoices`·`EndingCuts`).
