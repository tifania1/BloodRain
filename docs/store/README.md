# 스토어 (docs/store)

M2 끝(2027-02 말)에 Steam 스토어 페이지를 열고 위시리스트를 모은다(기획서 16·17장). 여기는 그 페이지에 들어갈 글과 영상·스크린샷 계획을 둔다. 그림(캡슐·라이브러리)은 `docs/design/art_list.md` 7절과 `art_queue`(A-06)에 있다.

| 파일 | 무엇 | backlog |
| --- | --- | --- |
| `store_text.md` | 짧은 소개·긴 소개(한·영), 특징 다섯, 태그 후보 20, 시스템 사양 초안, 언어 표, AI 생성 콘텐츠 공개 문구 | ST-01 |
| `trailer_screens.md` | 60초 트레일러 콘티, 스크린샷 10장 목록, 움짤 장면 | ST-02 |

`store_text.md` 의 숫자(문파 9, 무공 16 …)와 짧은 소개 길이는 `core/BloodRain.Core.Tests/StoreCopyTests.cs` 가 데이터와 견준다. 데이터 개수가 바뀌면 상점 글도 같이 고친다.

## 출처와 확인 못 한 것 (2026-11-04)

이 세션(클라우드)에서는 partner.steamgames.com·store.steampowered.com 이 네트워크에서 막혀 Steamworks 문서를 직접 읽지 못했다. 아래는 기사와 검색 결과로 확인한 것이다. 페이지를 열기 전에 사람이(또는 문서를 열 수 있는 세션이) Steamworks 문서로 다시 확인한다.

| 내용 | 출처 | 확인 |
| --- | --- | --- |
| 소개 글 칸(About This Game·Short Description·Special Announcements·Awards)에 바깥 링크와 다른 Steam 상품 링크 금지, 2024년 9월부터 | [Game Developer](https://gamedeveloper.com/marketing/valve-implementing-new-rules-to-refine-steam-store-page-written-descriptions), [PC Gamer](https://www.pcgamer.com/games/valve-issues-new-rules-outlawing-links-in-steam-page-descriptions-hopefully-bringing-an-end-to-the-plague-of-demos-pretending-theyre-prologue-games), [GamingOnLinux](https://gamingonlinux.com/2024/08/steam-store-pages-are-about-to-get-a-lot-cleaner-valve-puts-an-end-to-game-advert-spam) | 기사 |
| 짧은 소개 300자 안팎 | 업계 안내서(공식 숫자 아님) | **문서 확인 필요** |
| 태그는 20개까지, 위 다섯이 가장 중요, 일부 필터는 앞 15개 | [Steam Tags](https://partner.steamgames.com/doc/store/tags) 를 인용한 검색 결과 | 검색 요약 |
| Bullet Heaven·Wuxia 태그 신설(2026년 5월 개편) | [PC Gamer](https://pcgamer.com/gaming-industry/its-official-steam-decrees-bullet-heaven-the-name-of-the-vampire-survivors-genre), [GamingOnLinux](https://www.gamingonlinux.com/2026/05/valve-make-adjustments-to-steam-tags-and-they-settled-the-vampire-survivor-like-argument-with-bullet-heaven/page=1/), [Instant Gaming News](https://news.instant-gaming.com/en/articles/19609-steam-has-added-merged-and-removed-several-tags) | 기사 |
| Martial Arts 태그가 아직 있는지 | — | **확인 필요** |
| Unity 6 플레이어 최소 Windows 버전 | — | **Unity 문서 확인 필요** |
| 캡슐·스크린샷 규격 | `docs/design/art_list.md` 7절(Steamworks 문서, 2026-11-04 다른 세션이 확인) | 문서 |

## 정할 것 (사람)

- AI 생성 콘텐츠 공개 문구(`store_text.md` 끝): 글 초안에 AI 를 쓴 것까지 밝힐지, 배경음 줄(BGM-02 끝난 뒤). 추천: 초안처럼 넓게 밝힌다. 밝힌 것이 적어서 문제가 되는 일은 있어도 많아서 문제가 되는 일은 드물다.
- 짧은 소개에 '당신' 같은 2인칭을 쓸지. 추천: 쓴다. 한국 Steam 상점 글에서 흔하고 짧은 글에 힘이 있다.

## 스토어 페이지 공개까지 할 일 (로드맵 17장 M2 에서 쪼갬)

위에서부터. 클라우드가 할 수 있는 것과 사람·PC 가 할 것을 나눴다. backlog 'ST-' 항목과 같다.

1. ST-01 상점 문구 — 끝(이 폴더)
2. ST-02 트레일러 콘티·스크린샷 목록 — `trailer_screens.md`
3. ST-03 스크린샷·움짤 찍는 순서(PC Unity 세션): 장면을 다시 만드는 시험용 시작 상태(경지·무공·적 수)를 Core 시나리오로 만들어 두면 PC 세션이 같은 그림을 다시 찍을 수 있다
4. ST-04 시스템 사양 측정: M2 빌드를 낮은 PC·Steam Deck 에서 재고 최소 사양 칸을 채운다(PC Unity 세션 + 사람)
5. ST-05 Steamworks 준비(사람): 앱 등록비, 개발사·배급사 이름, 콘텐츠 설문(AI 공개), 연령 등급 설문, 출시일 칸('2027년 예정' 같은 표기), '곧 출시' 페이지 검토 요청. 검토에 며칠 걸리니 공개 목표일보다 2주 먼저 낸다(일정은 Valve 안내를 보고 사람이 확정)
6. ST-06 간체·번체 상점 글(L-04 와 같이)
