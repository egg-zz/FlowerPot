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
- 시작 코인 **15개** (새 게임은 최소 15코인 보장)
- 시작 보드: 모든 체인 중에서 **1~2단계 랜덤 7개**
- ADD ITEM: 랜덤 아이템(모든 체인)을 추가하고 **나온 단계만큼 코인을 낸다** (1단계 1코인, 2단계 2코인...). 가진 코인으로 낼 수 있는 단계까지만 나온다. 버튼에 코인 개수는 표시하지 않는다. **빈 칸이 없을 때만** 막고 "자리를 비워달라"고 안내 ("merge first" 규칙은 없앰)
- 스폰 단계는 그 판의 최고 단계에 따라 **단계적으로** 넓어진다: 처음 **1~2단계** → 4단계를 만들면 **1~3단계** → 5단계를 만들면 **1~4단계** (상한 4, `MergeBoard.earlySpawnMaxLevel / spawnMaxLevel`)
- 머지할 게 없고 보드 가득 → 게임 오버
- 머지할 게 없고 빈 칸은 있는데 코인이 모자람 → **OUT OF COINS 팝업**: 광고 보기(+15코인, 하루 5회) / 쥬얼리 5개 = 20코인 / HOME / RESTART

## 재화 (사용자가 정한 방향)
- **코인**: 머지·퀘스트·광고로 얻고, ADD ITEM에 쓴다
- **쥬얼리**(프리미엄): **현질** 또는 **3일마다 열리는 럭키 박스**(1~5개)로만 얻는다. 코인 교환(5=20, 12=60, 25=150), 추후 예쁜 코스튬 구매에 쓴다
- **광고는 코인만** 준다
- 아직 연결 안 됨: 광고 SDK(`AdService.cs`는 바로 보상을 주는 임시 구현), 결제(상점의 쥬얼리 팩은 SOON 버튼)
- 6초간 입력이 없으면 머지 가능한 두 아이템이 흔들리는 힌트

## 화면 구성 (구현됨)
1. **타이틀**: 둥근 섬 위에 꽃집 하나, "FLOWER / POT" 버블 글씨(Modak, 갈색 외곽선+그림자)가 글자별로 둥실둥실, 탭하면 홈으로
   - 둥근 꽃집: `Assets/Models/Shop/RoundShop.fbx` (원본 `D:\KSR_26.11.17_RoundShop_Title.max`의 `RoundShop`). 같은 파일의 `Title`(캡슐 글자)은 버블 폰트로 대체되어 안 씀
2. **홈**: 좌상단 코인·쥬얼리, 그 아래 QUESTS → SHOP → LUCKY BOX(남은 시간 표시), 우상단 COLLECTION → 설정, 화면 3/4 지점에 GAME START
   - 상점: 쥬얼리로 코인 팩 구매, 쥬얼리 팩(결제 미연결), 코스튬은 coming soon
3. 게임 카메라: 기울기 48도(`MergeBoard.cameraPitch`), 아이템은 칸을 거의 꽉 채움(`itemFootprint 0.95`)
   - 체인별 최고 단계는 도감 탭에, 일일 퀘스트는 QUESTS 버튼 팝업에 있다
- UI는 전부 둥글게(알약 버튼, 큰 모서리). 보드 타일도 둥근 모서리 메시(`RoundedMesh.cs`)
- 폰트(`Assets/PotMerge/Resources/`, 둘 다 SIL OFL, 라이선스 txt 같이 둠): `RoundFont.ttf` = Titan One(모든 UI 글자), `TitleFont.ttf` = Modak(타이틀)
3. **게임**: 보드, 코인, 홈·설정 버튼, ADD ITEM 버튼 (게임 오버 카드에 HOME / RESTART)
- 설정: 사운드 on/off, 진행 초기화(확인 팝업) / 도감: 체인 탭별 11칸, 미해금은 흐린 실루엣 + ???
- 일일 퀘스트: 날짜 기준으로 하루 3개, 완료하면 CLAIM으로 코인 보상
- **상점: 아직 "coming soon"** — 꽃집 사장(동물 캐릭터)과 코스튬을 코인으로 사는 기능 예정 (캐릭터 목록 제안 → Max 모델링 → OK 후 Unity)

## 꽃집 사장 (진행 중)
- **햄스터**로 결정 (볼 빵빵한 크림색, 2등신). 코스튬 붙일 자리: 모자 / 얼굴 / 목 / 몸 / 손
- 코스튬 예시 — 코인: 밀짚모자, 꽃무늬 앞치마, 동그란 안경, 리본 넥타이, 물뿌리개 / 쥬얼리: 꽃 화관, 딸기 모자, 요리사 모자, 아이스크림 헬멧, 벚꽃 기모노, 왕관

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
  - `MergeHUD.cs` — 게임 화면 HUD / `MenuUI.cs` — 타이틀·홈·팝업 / `UIKit.cs` — 공용 UI 스타일·벡터 아이콘
  - `GameFlow.cs` — 타이틀→홈→게임 전환, 카메라 넘기기 / `ShopBackdrop.cs` — 타이틀·홈 뒤 꽃집 디오라마
- `Assets/PotMerge/Data/ChainDatabase.asset` — 4체인 데이터 / `Assets/PotMerge/Icons/` — 아이템 아이콘 44개(에디터에서 렌더해 구움)
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
