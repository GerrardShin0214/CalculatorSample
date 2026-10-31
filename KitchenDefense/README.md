# 막타 키친 (주방 디펜스) · Unity 모바일 프로토타입

밤에는 몬스터를 막는 **타워 디펜스**, 낮에는 그 몬스터로 요리해 파는 **식당 경영**.
핵심은 **타워가 곧 조리기구라서, 무엇으로 막타를 쳤느냐가 내일의 메뉴를 정한다**는 것이다.

- 기획서: [주방 디펜스 — 게임 기획서 v0.1](https://claude.ai/artifact/DQ9BudzjnNZ5uYj5TNPtB9)
- 이 저장소는 기획서의 **"첫 프로토타입 범위"** 를 그대로 구현한 회색 박스(색 박스) 빌드다.
  목적은 하나, **막타를 노리는 게 재밌는지** 확인하는 것.
- 세로 화면 · 한 손 플레이 · Android/iOS 대상. 아트·사운드 없이 도형과 글자로만 그린다.

## 바로 실행하기

1. **Unity 2021.3.8f1 (LTS)** 로 연다. (`ProjectSettings/ProjectVersion.txt`, CalculatorSample과 같은 버전)
   - Unity Hub → Add → 이 폴더 선택. 더 새 버전(2022.3, Unity 6)으로 열어도 자동 업그레이드된다.
2. `Assets/KitchenDefense/Scenes/Main.unity` 를 연다. (메뉴 **Kitchen Defense → 메인 씬 열기**)
3. Game 뷰 해상도를 세로로 맞춘다. 예: `1080x1920 Portrait` 또는 `1080x2400`.
4. **Play**. 씬이 비어 있어도 `GameBootstrap`이 카메라·UI·보드를 전부 코드로 만든다.

> 에디터에서 처음부터 다시 하려면 메뉴 **Kitchen Defense → 세이브 데이터 지우기**.

## 휴대폰에서 실행하기

### Android
1. Unity Hub에서 해당 에디터에 **Android Build Support** (OpenJDK, Android SDK & NDK 포함) 모듈 추가.
2. File → Build Settings → **Android** → Switch Platform.
3. 휴대폰 개발자 옵션의 USB 디버깅을 켜고 연결 → **Build And Run**.

세로 고정, 패키지 이름 `com.gerrardshin.kitchendefense`, IL2CPP + ARMv7/ARM64, 최소 API 23이
`ProjectSettings`에 이미 들어 있다. 설정이 바뀌었으면 메뉴 **Kitchen Defense → 모바일 빌드 설정 적용**으로 되돌린다.

### iOS
Mac에서 **iOS Build Support** 모듈을 추가하고 iOS로 Switch Platform → Build → 나온 Xcode 프로젝트에서
서명(Team)을 고르고 실기기로 실행한다.

## 조작법

| 상황 | 조작 |
|---|---|
| 기구 설치 | 경로 옆 빈 칸을 탭 → 아래 바에서 기구 선택 (또는 기구를 먼저 고르고 초록 칸 탭) |
| 가마솥 | 경로 위 칸에만 설치 (함정) |
| 설치한 기구 | 탭하면 **약불**(피해 절반) / **강화**(화력, 오늘 밤만) / **이동** / **회수** |
| 웨이브 | 웨이브 사이에는 멈춘다. **웨이브 시작** 버튼으로 진행. 준비 시간에는 이동·회수가 무료 |
| 처치 예상 아이콘 | 몬스터 체력바 옆 원 = 지금 상태로 막타를 칠 기구. 예약에 필요한 몬스터는 크게 표시되고 테두리가 **초록**(원하는 재료가 나옴)/**빨강**(다른 재료가 나옴) |
| 체력바 | 빨간색(25% 이하) = 가마솥 확정 처치 구간 |
| 속도·일시정지 | 1배속/2배속, 오른쪽 위 `II`. 안드로이드 뒤로가기 = 일시정지 |
| 이어하기 | 앱을 나가는 순간 저장된다(웨이브 도중 몬스터 위치까지). 돌아오면 멈춘 상태로 이어짐 |

## 한 시즌의 흐름 (5일)

```
개업 전야(첫 예약 제안) → 0일 밤 → 1일 낮 → 1일 밤 → … → 4일 밤 → 5일 낮 → 시즌 평가
낮 = 메뉴 짜기(최대 3개) → 자동 영업 결과 → 상점(매일 무작위 4개 + 수리) → 마감(창고 정리, 내일 예약 수락/거절)
```

- **막타 = 조리법**: 식칼=손질(생고기), 그릴=구이, 냉동고=얼린 원재료, 가마솥=삶기. 표에 없는 조합은 잡재료(오늘의 백반).
- **얼린 원재료**: 보존 6일. 낮에 같은 몬스터의 다른 재료로 대체할 수 있지만 판매가 -30%.
- **예약**: 보상은 일반 판매의 2배 이상, 받고 못 지키면 평판 -1. 받은 예약이 그날 밤의 두 번째 목표가 된다.
- **누수**: 창고에 닿은 몬스터는 가장 비싼 재료를 먹고 내구도 -1. 내구도 10이 0이 되면 폐업.
- **평판**: 오르면 손님 수·판매가가 오르지만 몬스터도 강해진다.

## 기획서 대비 구현 범위

| 구분 | 이번 프로토타입에 포함 | 다음 단계로 미룸 (기획서 그대로) |
|---|---|---|
| 맵 | 주방 1개, 고정 경로 1개 (가로 7칸 x 10칸) | 맵 여러 개, 미로형 |
| 조리기구 | 식칼 거치대, 그릴, 냉동고, 가마솥 | 훈연기, 튀김기, 강화 트리 |
| 몬스터 | 젤리 슬라임(분열), 걷는 버섯(포자), 뿔멧돼지(돌진), 갑옷게(칼 저항·열 약점) | 맨드레이크, 보스(어미 버섯, 늙은 와이번) |
| 요리 | 레시피 8개 | 익힘 정도 시스템 |
| 낮 | 메뉴 짜기, 결과 화면, 상점(+수리) | 손님 유형, 아침 소식 카드, 영업 연출 |
| 진행 | 5일 시즌, 예약 하루 1건, 시즌 점수/등급 | 미끼, 도감, 메타 해금 |
| UI | 처치 예상 아이콘, 약불 모드, 세로 한 손 조작, 즉시 저장 | 아트, 사운드 |

기획서에 수치가 없던 부분(몬스터 체력, 기구 피해, 화력 등)은 프로토타입용 초기값이다.
전부 `GameData.cs`의 `Balance`와 데이터 표에 모여 있어 숫자만 바꿔 다시 테스트할 수 있다.

## 픽셀 이펙트 (pixelfx)

[pixelfx](https://github.com/patch-gg/pixelfx)(MIT)의 픽셀 아트 효과를 붙일 수 있게 해 두었다.
시트(PNG + JSON)를 `Assets/KitchenDefense/Resources/PixelFx/`에 넣으면 게임이 자동으로 쓰고,
없으면 지금처럼 도형 연출로 대신한다. **시트 파일은 아직 이 저장소에 들어 있지 않다.**

| 게임 연출 | pixelfx 효과 |
|---|---|
| 식칼 타격 | `impacts/hit-spark` |
| 몬스터 처치 | `spawn/poof` |
| 냉동 막타 | `ice/snowflake-burst` |
| 가마솥 삶기 | `water/splash` |
| 예약 재료 획득 | `pickups/collect-burst` |
| 창고가 털림 (내구도 -1) | `ui/heart-lost` |
| 화상 중인 몬스터 (반복) | `status/burning` |
| 감속 중인 몬스터 (반복) | `status/frozen` |

넣는 법 (Node.js 18 이상):

```
git clone https://github.com/patch-gg/pixelfx ../pixelfx
node Tools/pixelfx/import-pixelfx.mjs ../pixelfx
```

스크립트가 pixelfx의 `tools/render.mjs --sheet`로 위 8개를 뽑아 폴더에 복사하고, MIT 라이선스와 NOTICE를
`pixelfx-LICENSE.txt`로 같이 넣는다(게임과 함께 배포됨). Unity로 돌아가면 `Editor/PixelFxImport.cs`가
점 필터·무압축으로 임포트한다. 새로 생긴 `.meta` 파일도 함께 커밋할 것.
다른 효과로 바꾸려면 스크립트의 `EFFECTS`와 `PixelFxLibrary.cs`의 이름을 같이 바꾸면 된다.
pixelfx의 `gallery/fonts` 글꼴은 MIT가 아니므로 쓰지 않는다.

## 코드 구조

```
Assets/KitchenDefense/
  Scenes/Main.unity              GameBootstrap 하나만 있는 씬
  Scripts/Core/                  순수 C# (UnityEngine 참조 없음, asmdef noEngineReferences)
    GameData.cs                  기구·몬스터·재료·레시피·상점 데이터 표 + Balance 수치
    KitchenMap.cs                격자와 고정 경로
    NightSimulation.cs           밤 디펜스(막타 규칙, 화상, 감속, 가마솥, 분열, 포자, 돌진, 누수)
    KillPredictor.cs             처치 예상(같은 공식으로 몬스터 한 마리를 앞으로 굴려 봄)
    WaveGenerator.cs             웨이브 구성(예약에 필요한 몬스터는 반드시 섞음)
    DayLogic.cs                  보관·상함, 재료 배정(냉동 대체), 영업, 마감, 상점, 예약, 점수
    GameSession.cs               단계 전환(밤 ↔ 낮 ↔ 결과)
    States.cs                    저장되는 상태(JsonUtility)
  Scripts/Game/                  Unity 표시 계층(전부 코드로 생성, 프리팹 없음)
    GameController.cs            카메라·UI 생성, 고정 스텝(30Hz) 진행, 이벤트→연출, 저장
    BoardView.cs                 격자·기구·몬스터(눈, 체력바, 예상 아이콘)
    NightScreen.cs               밤 HUD와 조작
    CardScreens.cs / Card.cs     타이틀·낮 카드·결과·일시정지·게임 방법
    UIKit.cs, SpriteFactory.cs   코드 UI 도우미, 절차적 스프라이트
    SaveSystem.cs                세이브(JSON)·최고 점수
    PixelFxLibrary.cs            pixelfx 스프라이트 시트 읽기(없으면 도형 연출)
  Editor/KitchenDefenseMenu.cs   상단 메뉴 "Kitchen Defense"
  Editor/PixelFxImport.cs        pixelfx 시트 임포트 설정(점 필터, 무압축)
  Resources/Fonts/               주아체(배달의민족, SIL OFL 1.1)
  Tests/EditMode/                NUnit 테스트(Unity Test Runner와 dotnet 공용)
Tools/                           Unity 없이 Core를 빌드·테스트하는 dotnet 프로젝트, pixelfx 가져오기 스크립트
```

## 테스트

- **Unity**: Window → General → **Test Runner** → EditMode → Run All.
- **Unity 없이**: .NET 8 SDK가 있으면
  ```
  dotnet test Tools/CoreTests/CoreTests.csproj
  ```
  막타 규칙, 가마솥 25% 기준, 냉동 대체 -30%, 예약 성공/실패, 상함, 창고 초과, 상점,
  **처치 예상이 실제 막타와 정확히 일치하는지**, 그리고 봇이 시즌 전체를 플레이하는 테스트(결정론·난이도)가 들어 있다.
- 밸런스 리포트: `SeasonSimulationTests.BalanceReport`(Explicit)를 실행하면 기구 수별 실패율·누수·점수가 출력된다.
  현재 값 기준으로 기구 2개만 쓰면 절반쯤 폐업하고, 무난한 배치는 시즌당 누수 2회 정도로 버틴다.
  막타를 신경 쓰지 않는 봇의 예약 성공은 5건 중 2건 정도라서, 막타를 계획하면 점수가 크게 오른다.

## 플레이테스트에서 볼 것 (기획서)

1. 예약 재료를 얻으려고 배치를 바꾸는가, 아니면 딜만 올리는가?
2. 막타 조절이 퍼즐로 느껴지는가, 귀찮은 일로 느껴지는가?
3. 낮이 템포를 끊지 않는가? (낮 평균 1분 이내)
4. 5일을 마친 뒤 한 판 더 하고 싶어 하는가?
5. 한 손 세로 조작으로 배치와 약불 토글이 불편하지 않은가?

## 크레딧

- 글꼴: **주아체(Jua)** © 2018 The BM JUA Project Authors (배달의민족), [SIL Open Font License 1.1](Assets/KitchenDefense/Resources/Fonts/Jua-OFL.txt)
- 픽셀 이펙트(넣었을 때): **pixelfx** © 2026 Patch, Hugo Duprez, MIT License (일부는 Sprite Fusion, Hollow Crown의 MIT 코드를 바탕으로 함)
