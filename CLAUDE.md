# FLOWER POT — 하이퍼캐주얼 머지 게임

Unity 프로젝트 + 3ds Max 모델링을 MCP로 연결해서 만드는 머지 게임이다. 이 파일은 새 PC나 새 Claude 세션에서 작업을 이어가기 위한 프로젝트 설명서다.

## 환경
- **Unity 6000.5.0f1**, Built-in Render Pipeline, UI Toolkit(UGUI/TextMeshPro 없음), 레거시 Input Manager
- **3ds Max** (단위: inch) — 모델링은 MAXScript로 프리미티브를 조합해서 만든다
- MCP 서버: `unityMCP`(MCP for Unity), `3dsmax-mcp` — 로컬 경로가 들어 있는 `.mcp.json`은 git에서 제외
- GitHub: `egg-zz/FlowerPot` — 커밋 작성자는 `egg-zz <203041175+egg-zz@users.noreply.github.com>`, **커밋에 Claude 이름이나 Co-authored-by를 넣지 않는다**. 푸시는 GitHub Desktop으로 한다(CLI에는 GitHub 인증이 없음).

## 앞으로 할 일
- 확장 기획(레벨 모드, 햄스터 주문, 박스·거미줄 기믹, 콤보, 메타 요소)과 작업 순서는 **`docs/ROADMAP.md`** 에 정리되어 있다. 다음 작업은 거기서 고른다.

## 게임 규칙 (사용자가 정한 것)
- 보드 **7 x 7**
- 머지 체인 **4종 x 11단계**: Flower, Cafe, Bakery, Ice Cream
- **같은 체인 + 같은 단계**끼리만 머지된다. 다른 아이템 위에 놓으면 자리가 바뀐다.
- 머지 보상: **N단계 두 개를 머지하면 +(N-1) 코인** (1단계 머지는 0, 2단계 +1, 3단계 +2, ...) — `MergeBoard.MergeCoins`
- 신규 플레이어 시작 코인 **15개**

### 레벨 모드 (2026-11-19부터, 무한 모드는 없앰)
- 레벨 데이터: `Assets/PotMerge/Data/LevelDatabase.asset` (`LevelDatabase.cs`, 기본값은 `DefaultLevels()` — 에셋 우클릭 메뉴 "Reset To Default Levels"로 코드 값을 다시 채움). 지금은 **Lv 1~9** (머지 목표만, **제한 시간 없음** = 튜토리얼 느낌)
- 레벨마다: **목표**(머지 N번 / 특정 체인 머지 N번 / 특정 아이템(또는 아무 체인 N단계) 만들기), **제한 시간**(0 = 없음), **디자인된 시작 보드**(`"F1 3 3; C2 0 6"` = 체인 글자 F/C/B/I + 단계, 칸 x y), **아이템 상자 위치**(`boxes`, 비우면 `"F 0 0; C 2 0; B 4 0; I 6 0"` = 맨 아래 줄)
- **아이템 상자 4개**(ADD ITEM 대신, 2026-11-19): 체인마다 하나씩 보드 칸 위에 놓인다(`Gen_Flower/Cafe/Bakery/IceCream`, `Assets/Models/Generators/`, `MergeBoard.boxModels`). **톡 누르면 에너지 ⚡1**을 쓰고 그 체인의 **1~3단계**(같은 확률, `boxMinLevel/boxMaxLevel`) 아이템이 가장 가까운 빈 칸으로 나온다. 끌면 자리 이동, 머지는 안 됨(`PotItem.IsGenerator` = Level 0). 빈 칸 없으면 막고 안내, 에너지 없으면 에너지 팝업. 상자에서 나온 아이템도 "만들기" 목표에 들어간다(그래서 만들기 목표는 4단계 이상)
- 목표 다 채우면 **클리어** → 남은 시간 50% 이상 별 3 / 25% 이상 별 2 / 그 외 별 1 (시간 연장했으면 별 1, 시간 없는 레벨은 항상 별 3). **처음 깰 때만** 별당 2코인, 10레벨마다 쥬얼리 3개
- **실패**: 시간 초과 → 광고 +30초 / 쥬얼리 5개 +30초 / HOME / RETRY. 보드 가득 + 머지 불가 → HOME / RETRY. **포기(HOME·RETRY)하면 하트 1개** 소모
- **에너지 ⚡**: 최대 100, 2분마다 1 회복, 기존 플레이어는 100으로 시작. 홈(하트 옆)·게임 상단 ⚡ 알약을 누르면 에너지 팝업: 광고 +20(하루 광고 횟수에 포함) / 쥬얼리 10개 = 가득. 저장: `PlayerData.energy / energyNextTicks`
- **하트 5개**, 30분마다 1개 회복, 0개면 레벨 시작 불가 → 광고 +1 / 쥬얼리 10개 = 가득. 홈 좌상단 하트 알약을 누르면 하트 팝업
- 진행 중인 레벨은 홈에 다녀와도 메모리에 남아 이어진다(버튼이 CONTINUE LV N). 앱을 끄면 레벨은 처음부터(하트 소모 없음). 진행 저장: `PlayerData.levelReached / levelStars / hearts`

## 재화 (사용자가 정한 방향)
- **코인**: 머지·퀘스트·레벨 클리어로 얻고, 상점(코스튬)에 쓴다. 아이템 생성은 코인이 아니라 에너지
- **쥬얼리**(프리미엄): **현질** 또는 **하루에 한 번 열리는 럭키 박스**(1~5개)로만 얻는다. 코인 교환(5=20, 12=60, 25=150), 추후 예쁜 코스튬 구매에 쓴다
- **광고**는 하트 +1 · 에너지 +20 · 레벨 시간 +30초를 준다 (OUT OF COINS 팝업이 없어져서 코인 주는 광고는 지금 없음, `PlayerData.RewardAd`는 남아 있음)
- 아직 연결 안 됨: 광고 SDK(`AdService.cs`는 바로 보상을 주는 임시 구현), 결제(상점의 쥬얼리 팩은 SOON 버튼)
- 6초간 입력이 없으면 머지 가능한 두 아이템이 흔들리는 힌트

## 화면 구성 (구현됨)
1. **타이틀**: 둥근 섬 위에 꽃집 하나, "FLOWER / POT" 버블 글씨(Modak, 갈색 외곽선+그림자)가 글자별로 둥실둥실, 탭하면 홈으로
   - 둥근 꽃집: `Assets/Models/Shop/RoundShop.fbx` (원본 `D:\KSR_26.11.17_RoundShop_Title.max`의 `RoundShop`). 같은 파일의 `Title`(캡슐 글자)은 버블 폰트로 대체되어 안 씀
2. **홈**: 좌상단 코인·쥬얼리, 그 아래 하트(개수 + 다음 회복 시간)와 ⚡ 에너지, QUESTS → SHOP → LUCKY BOX(남은 시간 표시), 우상단 COLLECTION → 설정, 화면 3/4 지점에 **LEVEL N** 버튼(진행 중이면 CONTINUE LV N). 레벨 지도 화면은 아직 없음
   - 상점: 코스튬 구매·착용, 쥬얼리로 코인 팩 구매, 쥬얼리 팩(결제 미연결)
3. 게임 카메라: 기울기 48도(`MergeBoard.cameraPitch`), 아이템은 칸을 거의 꽉 채움(`itemFootprint 0.95`)
   - 체인별 최고 단계는 도감 탭에, 일일 퀘스트는 QUESTS 버튼 팝업에 있다
- UI는 전부 둥글게(알약 버튼, 큰 모서리). 보드 타일도 둥근 모서리 메시(`RoundedMesh.cs`)
- 폰트(`Assets/PotMerge/Resources/`, 둘 다 SIL OFL, 라이선스 txt 같이 둠): `RoundFont.ttf` = Titan One(모든 UI 글자), `TitleFont.ttf` = Modak(타이틀)
3. **게임**: 보드(아이템 상자 4개 포함), 코인·쥬얼리·⚡ 에너지, 홈·설정 버튼, 좌상단 목표 카드(GOALS), 우상단 LEVEL N + 타이머(시간 있는 레벨만, 10초 이하 빨갛게), 하단 "TAP A BOX FOR ITEMS ⚡1" 안내, 클리어/실패 카드
- 설정: 사운드 on/off, 진행 초기화(확인 팝업) / 도감: 체인 탭별 11칸, 미해금은 흐린 실루엣 + ???
- 일일 퀘스트: 날짜 기준으로 하루 3개, 완료하면 CLAIM으로 코인 보상
- **상점**: COSTUMES(햄스터 코스튬 25개) / COINS(쥬얼리→코인, 쥬얼리 팩은 결제 미연결) 두 탭

## 꽃집 사장 햄스터 (구현됨)
- 볼 빵빵한 크림색 2등신 햄스터. 홈 화면 꽃집 옆에 서서 통통 튀고 눈을 깜빡인다 (`ShopOwner.cs`)
- 모델: `Assets/Models/Hamster/Hamster.fbx` — 몸·머리·볼이 **한 덩어리 매끈한 메시**, 흰 얼굴/분홍 볼/흰 배는 **정점 색**(셰이더 `PotMerge/VertexColorLit`, 재질 `HM_SkinVC`)
- 코스튬 FBX는 햄스터 원점 기준으로 내보내서 그대로 겹쳐 붙인다. **25개 전부 상점 등록됨** → `Assets/PotMerge/Data/CostumeDatabase.asset` (슬롯 순서로 정렬)
  - 원래 3개: `Assets/Models/Hamster/Costume_*.fbx` / 새 22개: `Assets/Models/Hamster/Costumes/<id>.fbx`, 아이콘 `Assets/PotMerge/Icons/Costumes/<id>.png`
  - 가격: 코인 30~60, 쥬얼리 10~20 (Flower Crown 10, Heart Shades 10, Ice Cream Hat 12, Star Wand 15, Sakura Kimono 18, Crown 20, Princess Dress 20)
  - 옷(앞치마·멜빵 바지·드레스 등)은 Max에서 햄스터 몸 단면 표를 따라 감싸는 메시로 만든다
- 슬롯: Hat / Face / Neck / Body / Hand — 같은 슬롯은 하나만 착용. 상점 COSTUMES 탭에서 구매(바로 착용)·WEAR/WEARING 토글 (상점은 COSTUMES / COINS 두 탭)
  - COSTUMES 탭: ALL / HAT / FACE / NECK / BODY / HAND 필터 칩 + 세로 스크롤 목록 (구매 후 다시 열려도 스크롤 위치 유지)
- **게임 화면 상단 중앙**에도 햄스터가 코스튬을 입고 서 있고, **머지할 때마다 점프+한 바퀴** (높은 단계일수록 크게). 보드 아래 숨은 무대(y=-300)를 작은 카메라로 렌더해 HUD에 띄운다 (`OwnerPortrait.cs`)
- Max 원본: `D:\KSR_26.11.17_Hamster.max` (`D:\KSR_26.11.17_RoundShop_Title.max`의 최신판)의 `Hamster_Base` / `Hamster_CoinLook` / `Hamster_JewelLook`, 코스튬 `Costume_*`(숨김, 내보내기용), 진열용 `Costume_Showcase/Show_*`
- 코스튬 목록(제작 완료): 모자 — 요리사 모자, 바리스타 베레모, 새싹 머리핀, 꿀벌 더듬이, 딸기 모자, 아이스크림 콘 모자, 왕관 / 얼굴 — 동그란 안경, 하트 볼스티커, 하트 선글라스 / 목 — 리본 넥타이, 체크 스카프, 꽃 레이 / 몸 — 바리스타 앞치마, 멜빵 바지, 벚꽃 기모노, 공주 드레스, 노란 비옷 / 손 — 물뿌리개, 꽃다발, 아이스크림 콘, 별 마법봉

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
  - `MergeBoard.cs` — 보드, 드래그, 머지, 스폰, 레벨 진행(목표·타이머·클리어/실패·하트)
  - `LevelDatabase.cs` — 레벨 데이터 ScriptableObject + 기본 레벨 1~9
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
- 게임 진행(코인, 레벨, 하트)은 PlayerPrefs라서 PC를 바꾸면 이어지지 않는다.

## 작업 방식 (사용자 선호)
- 답변은 한국어로.
- 새 콘텐츠는 먼저 목록을 제안 → 3ds Max로 모델링 → 렌더 시트 보고 → **사용자가 OK하면** Unity 작업.
