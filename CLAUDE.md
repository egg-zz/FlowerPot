# FLOWER POT — 하이퍼캐주얼 머지 게임

Unity 프로젝트 + 3ds Max 모델링을 MCP로 연결해서 만드는 머지 게임이다. 이 파일은 새 PC나 새 Claude 세션에서 작업을 이어가기 위한 프로젝트 설명서다.

## 환경
- **Unity 6000.5.0f1**, Built-in Render Pipeline, UI Toolkit(UGUI/TextMeshPro 없음), 레거시 Input Manager
- **3ds Max** (단위: inch) — 모델링은 MAXScript로 프리미티브를 조합해서 만든다
- MCP 서버: `unityMCP`(MCP for Unity), `3dsmax-mcp` — 로컬 경로가 들어 있는 `.mcp.json`은 git에서 제외
- GitHub: `egg-zz/FlowerPot` — 커밋 작성자는 `egg-zz <203041175+egg-zz@users.noreply.github.com>`, **커밋에 Claude 이름이나 Co-authored-by를 넣지 않는다**. 푸시는 GitHub Desktop으로 한다(CLI에는 GitHub 인증이 없음).

## 게임 규칙 (사용자가 정한 것)
- 보드 **7 x 7**
- 머지 체인 **4종 x 11단계**: Flower, Cafe, Bakery, Ice Cream
- **같은 체인 + 같은 단계**끼리만 머지된다. 다른 아이템 위에 놓으면 자리가 바뀐다.
- 머지 보상: **N단계 두 개를 머지하면 +N 코인** (1단계 +1, 2단계 +2, ...)
- 시작 코인 **20개** (새 게임은 최소 20코인 보장)
- 시작 보드: 모든 체인 중에서 **1~4단계 랜덤 7개**
- **1코인**으로 랜덤 아이템(모든 체인, 1~4단계)을 추가. 코인이 있는 한 계속 추가 가능, **빈 칸이 없을 때만** 막고 "자리를 비워달라"고 안내 ("merge first" 규칙은 없앰)
- 머지할 게 없고 (코인 없음 또는 보드 가득) → 게임 오버
- 6초간 입력이 없으면 머지 가능한 두 아이템이 흔들리는 힌트

## 화면 구성 (진행 중)
1. **타이틀**: 꽃집 배경, "FLOWER / POT" 글씨가 둥실둥실 떠다님, 탭하면 홈으로
2. **홈**: 상단 코인 / 체인별 최고 단계 / 설정 버튼 / 도감 버튼, 좌측 일일 퀘스트, 화면 3/4 지점에 GAME START 버튼
3. **게임**: 보드, 코인, 홈 버튼, ADD ITEM 버튼
- 설정: 사운드 on/off, 진행 초기화 / 도감: 해금한 아이템 모아보기
- 일일 퀘스트: 날짜 기준으로 하루 3개, 완료하면 코인 보상

## 머지 체인 (단계 이름)
| 단계 | Flower | Cafe | Bakery | Ice Cream |
|---|---|---|---|---|
| 1 | Seed (화분 없음) | Ice Cubes | Wheat | Milk Drop |
| 2 | Empty Pot | Ice Cup (얼음 7개) | Flour Sack | Milk Carton |
| 3 | Sprout | Iced Americano | Dough (밀대 1개) | Cone |
| 4 | Leafy Plant | Mega Coffee | Bread Slice (식빵 한 장) | Soft Serve |
| 5 | Bud | Orange Juice | Croissant | Double Scoop |
| 6 | Bloom (원래 화분) | Bubble Tea (음료 안에 펄) | Donuts (작은 도넛 2개) | Sundae |
| 7 | Triple Bloom (금색 화분) | Strawberry Smoothie | Cupcake | Banana Split (자른 바나나) |
| 8 | Gift Flower (포장 한 송이) | Parfait | Strawberry Cake | Tower Sundae |
| 9 | Bouquet | Dessert Tower (3단 트레이) | Wedding Cake | Ice Cream Cake |
| 10 | Flower Stall | Coffee Cart (지붕 있음) | Bread Cart (바퀴+지붕) | Ice Cream Truck |
| 11 | Flower Shop | Cafe | Bakery | Ice Cream Shop |

## 폴더 / 파일
- `Assets/Scenes/PotMergeGame.unity` — 게임 씬 (빌드 첫 번째 씬, 세로 1080x1920)
- `Assets/Models/MergePots/Pot_L1~L11.fbx` — Flower 체인
- `Assets/Models/MergeCafe/Cafe_L*.fbx`, `MergeBakery/Bakery_L*.fbx`, `MergeIceCream/Ice_L*.fbx` — 새 체인
- `Assets/PotMerge/Scripts/`
  - `MergeBoard.cs` — 보드, 드래그, 머지, 스폰, 게임 오버, 보드 저장
  - `PotItem.cs` — 보드 위 아이템(체인, 단계, 애니메이션)
  - `ChainDatabase.cs` — 체인별 모델/이름/아이콘 ScriptableObject
  - `PlayerData.cs` — 코인 지갑, 체인별 최고 단계, 사운드 설정, 일일 퀘스트 (PlayerPrefs JSON)
  - `ModelUtil.cs` — 모델 스폰(180도 회전, 바닥 맞춤, 크기 정규화)
  - `SoundFx.cs` — 절차적으로 만든 효과음
  - `GameUI.cs` / 게임 흐름 스크립트 — 작성 중
- `Assets/PotMerge/UI/PotMergePanelSettings.asset` — UI Toolkit 패널 설정 (참조 해상도 1080x1920)

## 3ds Max 원본 (바탕화면, 레포 밖)
- `KSR_26.11.10.max` — 원래 화분 하나
- `KSR_26.11.10_MergeStages.max` — Flower 체인 11단계
- `KSR_26.11.10_NewChains.max` — Cafe / Bakery / Ice Cream 체인 33개

## 작업 시 주의할 점
- 3ds Max의 정면(-Y)은 Unity에서 +Z를 향해 들어온다 → 스폰할 때 180도 회전한다.
- FBX 루트의 축 변환 회전을 identity로 덮어쓰면 모델이 눕는다. 루트 회전은 건드리지 않는다.
- FBX 임포트 설정: `useFileScale = true`, `globalScale = 1` (Max에서 cm로 내보냄).
- 투명해야 하는 재질: `CupClear`, `Glass`, `MilkTeaClear` (컵 안 음료/버블이 보여야 함).
- MAXScript는 대소문자를 구분하지 않는다 (`R`과 `r`이 같은 변수).
- Max 뷰포트 캡처가 안 될 때는 Scanline으로 오프스크린 렌더해서 라인업 시트를 만든다(`Captures/` 폴더, git 제외).
- Play 모드 중에 스크립트를 수정하면 재컴파일되어 런타임 상태가 날아간다.
- 게임 진행(코인, 보드)은 PlayerPrefs라서 PC를 바꾸면 이어지지 않는다.

## 작업 방식 (사용자 선호)
- 답변은 한국어로.
- 새 콘텐츠는 먼저 목록을 제안 → 3ds Max로 모델링 → 렌더 시트 보고 → **사용자가 OK하면** Unity 작업.
