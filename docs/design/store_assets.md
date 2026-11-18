# 에셋 스토어에서 산 것

2026-11-17 사람: "내 에셋들을 확인해 보고 이 게임에 쓸 만한 것들을 설치해서 적극 활용해 줘. DOTween Pro, sprite shader, Text Animator 는 꼭."

## 규칙
- **산 에셋은 커밋하지 않는다.** 이 저장소는 공개이고, 에셋 스토어 약관은 산 에셋을 남이 받아 갈 수 있는 곳에 올리는 것을 금한다. 들여온 폴더는 `.gitignore` 에 적고 PC(`E:\Unity\BloodRain`)에만 둔다.
- 그래서 클라우드에는 이 에셋이 없다. 에셋을 쓰는 코드는 PC Unity 에서만 컴파일되니, 그런 코드를 고친 세션은 unity_tasks 에 '컴파일 확인'을 남긴다.
- PC 클론을 새로 받았다면 Unity 의 Package Manager → My Assets 에서 아래 표의 것을 다시 들여온다(버전은 표대로).
- Core(`Assets/Scripts/Core`)는 에셋을 참조하지 않는다. 규칙과 테스트는 에셋 없이 돈다.

## 들여온 것 (2026-11-17 밤)

PC 의 에셋 스토어 내려받기 폴더(`%APPDATA%\Unity\Asset Store-5.x`)에 있던 것을 `AssetDatabase.ImportPackage` 로 넣었다. 버전은 그 폴더에 받아 둔 그대로.

| 에셋 | 폴더(.gitignore) | 어셈블리 | 지금 쓰는 곳 |
|---|---|---|---|
| DOTween Pro | `Assets/Plugins/Demigiant` | `DOTween.Modules`(asmdef 를 손으로 만들었다 — 아래) | `Hud/RegionBanner.cs` 띠가 펼쳐지고 걷히는 움직임 |
| Text Animator for Unity 2.X | `Assets/Plugins/Febucci` | `Febucci.TextAnimator.Runtime`·`.TMP.Runtime` | `Hud/RegionBanner.cs` 한자 이름이 한 글자씩 뜸 |
| All In 1 Sprite Shader | `Assets/Plugins/AllIn1SpriteShader` | 코드 참조 없음(재질로만) | `Resources/Fx/PlayerAllIn1.mat` — 플레이어 몸 테두리·피격 번쩍임(`PlayerLook`) |
| Damage Numbers Pro | `Assets/DamageNumbersPro` | `DamageNumbersPro` | Presentation·Editor asmdef 가 참조. `DamageNumberLayer.usePro`(지금 꺼짐 — 켜면 에디터가 죽어서, unity_tasks DN-01U)와 프리팹 짓기 메뉴. 지금 피해 숫자는 `Fx/DamageNumberLayer.cs` 의 TextMeshPro 풀 |

- `BloodRain.Presentation.asmdef` 가 위 어셈블리 셋을 참조한다. 에셋이 없는 곳에서는 `RegionBanner.cs` 가 컴파일되지 않는다.
- Actions 의 WebGL 빌드(`webgl.yml`)는 Unity 라이선스 비밀값이 없어 지금도 건너뛴다. 비밀값을 넣어 켜면 에셋이 없어 빌드가 깨지니, 켜려면 먼저 산 에셋을 쓰는 코드를 따로 떼어야 한다.
- DOTween: 설정 창을 쓰지 않고 `DOTween/Modules/DOTween.Modules.asmdef`, `DOTweenPro/DOTweenPro.Scripts.asmdef`, `DOTweenPro/Editor/DOTweenPro.EditorScripts.asmdef` 를 손으로 만들었다. **`Assets/Resources/DOTweenSettings.asset` 의 `createASMDEF` 가 1 이어야 한다** — 0 이면 DOTween 이 다음 도메인 리로드 때 asmdef 셋을 지워 `RegionBanner.cs` 가 `DOFade`·`DOAnchorPosY` 를 못 찾는다(2026-11-18 아침에 그렇게 깨져 있었다. 설정 에셋의 그 줄을 1 로 고치고 asmdef 셋을 다시 만들면 된다). `ProjectSettings` 의 `DOTWEEN` 정의는 DOTween 이 넣은 것이고 커밋돼 있다. TextMeshPro 모듈(`DOTWEEN_TEXTMESHPRO`)은 켜지 않았다.
- 들여올 때 "New Version of DOTween Imported" 대화창이 에디터를 멈춘다(unityMCP 가 답이 없으면 이 창을 닫는다).
- All In 1 재질은 셰이더가 없으면 `PlayerLook` 이 옛 재질을 그대로 쓴다(셰이더 이름을 보고 고른다).

## 내 에셋에 있는 것 가운데 이 게임에 쓸 만한 것 (2026-11-17 밤, 에셋 스토어 '내 에셋' 412개를 훑음)

사람이 고르면 넣는다. 그림체가 수묵이라 3D·만화풍 묶음은 뺐다.

- 소리: Punch Sound Pack, Ultimate Sound FX Bundle, Monster Sounds Pack, Footsteps Sound Pack, Total Music Collection — 지금 게임에 타격·휘두름 소리가 하나도 없다. 에셋 스토어 음원은 게임에 넣어 팔 수 있다(Artlist 스톡과 다름). 다만 2026-11-05 에 "효과음은 직접 만든다"고 정했으니 사람이 바꾸라고 해야 쓴다
- 손맛: Feel(More Mountains) — 흔들림·멈춤·튐을 한데 묶은 것. 지금은 `HitFeel` 을 직접 짰다
- 화면 글자·UI: UI Particle Image, 6000 Fantasy Icons / 5000 Fantasy Icons(그림체가 맞는지 봐야 함)
- 이펙트: Magic Slashes FX, Magic swords VFX, Blood Effects Pack, Blood Gush, Procedural Lightning, Stylized Shoot & Hit Vol.2, Cartoon FX 4 Remaster, RPG VFX Bundle — 2026-11-17 에 "이펙트는 셰이더로만"이라고 정한 까닭이 재료를 뽑을 곳이 없어서였으니, 사 둔 묶음을 재료로 쓸지는 사람이 정할 일
- 그 밖: Ink Painter(먹 번짐), kcisa Korean Traditional Pattern(전통 무늬), Smart Lighting 2D, InteliMap AI Tilemap Generator, Seamless Texture Generator, Easy Save, I2 Localization(저장·현지화는 이미 직접 짠 것이 있음), Dialogue System for Unity, TopDown Engine
