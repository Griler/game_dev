# MathGame Online — Tiến Độ Dự Án

> **Dùng file này khi tiếp tục trên máy tính khác.**  
> Clone repo `griler/game_dev`, mở Unity 6.0 (6000.3.12f1), rồi đọc từ phần **Bước Tiếp Theo**.

---

## Tóm Tắt Dự Án

Game toán học online 1v1 real-time:
- Hai người chơi cùng rank đấu nhau trong giới hạn thời gian (60 giây)
- Mỗi câu hỏi có nhiều ô trống; chọn số từ pool tile ở dưới để điền từ trái qua phải
- Ai trả lời đúng nhiều câu hơn thắng → tính ELO
- Server-authoritative: server validate đáp án, tính ELO, client chỉ gửi `int[] filledValues`

**Stack:**
- Unity 6 (6000.3.12f1) · URP 17.3.0 · UGUI 2.0
- NGO (Netcode for GameObjects) 2.4.1 — dedicated server mode
- Unity Gaming Services: Auth (anonymous) + Lobby (matchmaking theo rank)
- Server: Oracle Cloud Free Tier ARM VM (4 CPU / 24 GB RAM, public IP), TCP/UDP port 7777
- TextMeshPro cho toàn bộ UI text (không dùng legacy Text)

---

## Trạng Thái Hiện Tại — TẤT CẢ ĐÃ MERGE VÀO MAIN

| PR | Branch | Nội dung |
|----|--------|----------|
| #1 | `claude/math-game-competitive-plan-p0KdU` | Core systems: câu hỏi, ELO, rank, UI components |
| #2 | `claude/scene-builder` | SceneBuilder Editor script (một click tạo full scene) |
| #3 | `claude/multiplayer-ngo` | NGO networking layer |
| #4 | `claude/multiplayer-bugfixes` | Bug fixes + refactor sang independent race + networking trong SceneBuilder |

`main` hiện tại ở commit `be80d63` — code hoàn chỉnh, chưa test trong Unity.

---

## Cấu Trúc File

```
Assets/_Project/Scripts/
├── Core/
│   ├── GameManager.cs          ← offline loop + network guard (_networked flag)
│   ├── GamePhase.cs            ← enum: Idle/Countdown/Playing/QuestionResult/RoundEnd/ShowResult
│   └── GameStateMachine.cs     ← TransitionTo(), OnPhaseChanged event
├── Player/
│   ├── PlayerData.cs           ← lưu ELO/games/wins vào PlayerPrefs
│   ├── PlayerRank.cs           ← enum: Bronze=0..Diamond=4
│   ├── RankSystem.cs           ← GetRank(elo), GetMatchRank(eloA,eloB)
│   └── EloCalculator.cs        ← CalculateFromScores(), K=40/20/10
├── Question/
│   ├── QuestionData.cs         ← expressionTemplate, correctAnswers[], poolNumbers[]
│   ├── QuestionGenerator.cs    ← pure C#, System.Random(seed), Generate(config)
│   ├── RankConfig.cs           ← ScriptableObject RankConfigData
│   ├── RankConfigProvider.cs   ← static GetDefault(rank), roundDurationSeconds=60
│   └── Operator.cs             ← enum phép tính
├── UI/
│   ├── GameHUD.cs              ← tất cả text là TextMeshProUGUI
│   ├── QuestionPanel.cs        ← DisplayQuestion(), ShowAnswerFeedback()
│   ├── NumberTilePool.cs       ← SetupPool(poolNumbers)
│   ├── NumberTile.cs           ← tile chạm để chọn số
│   ├── BlankSlot.cs            ← ô trống nhận số
│   ├── PlayerInfoCard.cs       ← Setup(name, rank, elo), SetScore()
│   ├── TimerBar.cs             ← SetDuration(), UpdateTime()
│   └── MatchmakingPanel.cs     ← StartMatchmaking(), Hide(), cancelButton
└── Network/
    ├── PlayerConnectionData.cs ← struct: playerName, eloRating, gamesPlayed; ToBytes()/FromBytes()
    ├── NetworkGameState.cs     ← NetworkBehaviour, MaxRaceQuestions=100, tất cả NetworkVariables
    ├── ServerGameLogic.cs      ← server-only, HandleAnswer(), AdvancePlayer(), EndMatch()
    ├── ServerMatchManager.cs   ← queue theo rank, StartMatch(), CheckObjectVisibility
    ├── ConnectionManager.cs    ← StartAsServer(), StartAsClient(), approval callback
    ├── MatchmakingService.cs   ← InitUGS(), RegisterServerLobbies(), FindServerLobby()
    ├── ClientGameProxy.cs      ← bridge NetworkGameState → GameHUD, TryShowMyQuestion()
    └── ServerBootstrap.cs      ← phát hiện -batchmode → auto StartServerFlow()

Assets/_Project/Editor/
└── SceneBuilder.cs             ← Tools→MathGame→Build Game Scene

Assets/_Project/Scripts/MathGame.asmdef
Assets/_Project/Editor/MathGame.Editor.asmdef
Packages/manifest.json
```

---

## Kiến Trúc Network

```
Client A                   Dedicated Server              Client B
   │                            │                            │
   │── ConnectRequest ──────────►│                            │
   │   (PlayerConnectionData     │◄─── ConnectRequest ────────│
   │    JSON payload)            │     (PlayerConnectionData) │
   │                            │                            │
   │                   ConnectionManager                      │
   │                   OnClientConnected                      │
   │                   → ServerMatchManager.RegisterClient    │
   │                   → TryMatch(rank)                       │
   │                   → StartMatch(p1, p2)                   │
   │                            │                            │
   │              Spawn NetworkGameState                      │
   │              (CheckObjectVisibility = p1||p2)            │
   │                            │                            │
   │◄── NetworkGameState ───────►│◄── NetworkGameState ───────│
   │    (Spawn event)            │    (Spawn event)           │
   │                            │                            │
   │    ClientGameProxy         │                            │
   │    OnNetworkGameStateSpawned│                            │
   │    → SetNetworkedMode(true) │                            │
   │    → GenerateQuestions(seed)│                            │
   │                            │                            │
   │    CountdownClientRpc(3,2,1)│                            │
   │◄───────────────────────────►│◄───────────────────────────│
   │                            │                            │
   │    Phase → Playing          │                            │
   │    Player1QIndex = 1        │                            │
   │    Player2QIndex = 1        │                            │
   │                            │                            │
   │── SubmitAnswerServerRpc ───►│                            │
   │   int[] filledValues        │── SubmitAnswerServerRpc ───►
   │                            │   int[] filledValues        │
   │◄── ShowAnswerResultClientRpc│                            │
   │    correct/wrong            │                            │
   │    correctAnswers           │                            │
   │                            │                            │
   │    [correct] Player1QIndex++│                            │
   │    [wrong] retry same       │                            │
   │                            │                            │
   │    Time runs out → EndMatch │                            │
   │◄── MatchEndClientRpc ──────►│◄── MatchEndClientRpc ──────│
   │    winnerId, scores,        │    winnerId, scores,       │
   │    eloChange1, eloChange2   │    eloChange1, eloChange2  │
```

**Independent Race Model:**  
Mỗi player có `Player1QIndex`/`Player2QIndex` riêng. Đúng → tiến câu tiếp (sau 0.7s). Sai → retry câu đó (sau 1.2s). Không cần chờ đối thủ.

---

## NetworkGameState — Các NetworkVariable

```csharp
// Điểm số
NetworkVariable<int>   Player1Score    // R=Everyone, W=Server
NetworkVariable<int>   Player2Score

// Câu hỏi (independent race)
NetworkVariable<int>   QuestionSeed    // broadcast seed để client tự generate
NetworkVariable<int>   Player1QIndex   // 1-based, 0=chưa bắt đầu
NetworkVariable<int>   Player2QIndex

// Phase & Timer
NetworkVariable<int>   PhaseInt        // cast từ GamePhase enum
NetworkVariable<float> TimeRemaining   // đếm ngược từ 60f

// Thông tin player
NetworkVariable<ulong> Player1ClientId
NetworkVariable<ulong> Player2ClientId
NetworkVariable<FixedString64Bytes> Player1Name  // max 20 chars (clamp trong ToFixed())
NetworkVariable<FixedString64Bytes> Player2Name
NetworkVariable<int>   Player1Elo
NetworkVariable<int>   Player2Elo
NetworkVariable<int>   MatchRankInt    // cast từ PlayerRank enum
```

---

## ServerGameLogic — Flow

```
InitMatch(p1Id, p1Name, p1Elo, p1Games, p2Id, p2Name, p2Elo, p2Games)
  → write NetworkVariables (IDs, names, ELOs, rank, seed)
  → generate 100 câu từ seed vào _questions[]
  → StartCoroutine(RunCountdown())

RunCountdown()
  → CountdownClientRpc(3), wait 1s
  → CountdownClientRpc(2), wait 1s
  → CountdownClientRpc(1), wait 1s
  → Phase = Playing, Player1QIndex = 1, Player2QIndex = 1

HandleAnswer(clientId, filledValues)
  → check _p1Locked / _p2Locked (bỏ qua nếu locked)
  → check filledValues vs _questions[index].correctAnswers
  → ShowAnswerResultClientRpc(clientId, correct, correctAnswers)
  → correct: Score++, StartCoroutine(AdvancePlayer)  [0.7s → QIndex++]
  → wrong:   StartCoroutine(UnlockAfter, 1.2s)       [retry same]

Update()
  → Phase==Playing: TimeRemaining -= deltaTime
  → ≤ 0 → EndMatch()

EndMatch()
  → Phase = ShowResult
  → EloCalculator.CalculateFromScores(...)
  → MatchEndClientRpc(winnerId, p1Score, p2Score, eloChange1, eloChange2)
  → ServerMatchManager.OnMatchFinished(p1Id, p2Id)
```

---

## ELO System

```csharp
// K-factor theo số game đã chơi
K = gamesPlayed < 30 ? 40      // người mới
  : eloRating >= 2400 ? 10     // top player
  : 20;                        // bình thường

// Score-based: tỉ lệ câu đúng
scoreA = correctA / (correctA + correctB)  // float [0..1]
expected = 1 / (1 + 10^((eloB - eloA)/400))
newEloA = eloA + K * (scoreA - expected)
```

---

## Rank System

| Rank | ELO Threshold |
|------|--------------|
| Bronze | 0 |
| Silver | 1000 |
| Gold | 1500 |
| Platinum | 2000 |
| Diamond | 2500 |

`GetMatchRank(eloA, eloB)` → rank của average ELO → độ khó câu hỏi.

---

## MatchmakingService — Lobby Flow

**Server:**
1. `InitUGS()` → UnityServices.InitializeAsync + SignInAnonymouslyAsync
2. `RegisterServerLobbies(publicIP, port)` → tạo 1 lobby cho mỗi rank (Bronze..Diamond)
   - Data: `ServerIP` (Member), `ServerPort` (Member), `Rank` (Public, index N1)

**Client:**
1. `InitUGS()` → init + sign in
2. `FindServerLobby(rank)` → query lobby by N1==rank, đọc ServerIP + ServerPort
   - Retry mỗi 5s, timeout 30s
3. `ConnectionManager.Instance.StartAsClient(ip, port, name, elo, gamesPlayed)`

---

## MatchmakingPanel

UI overlay "Tìm trận" hiển thị khi searching:
- Button "Tìm Trận" → `StartMatchmaking()` (async)
- Button "Hủy" → set `_cancelled = true`, hide panel
- `Hide()` → tắt overlay (gọi bởi `ClientGameProxy.OnNetworkGameStateSpawned`)

---

## SceneBuilder (Editor Only)

`Tools → MathGame → Build Game Scene` tạo toàn bộ scene:

1. **Camera + Lighting** (nếu chưa có)
2. **Canvas** (ScreenSpaceOverlay, TMP font)
3. **GameManager** GameObject + component
4. **GameHUD** với:
   - TimerBar, QuestionPanel, NumberTilePool
   - PlayerInfoCard[0] (local), PlayerInfoCard[1] (remote)
5. **Tạo prefab** `Assets/_Project/Prefabs/NetworkGameState.prefab`
   - Components: NetworkObject + NetworkGameState + ServerGameLogic
6. **NetworkManager** GameObject:
   - NetworkManager component (ConnectionApproval=true, EnableSceneManagement=false)
   - UnityTransport (127.0.0.1:7777 default)
   - Đăng ký NetworkGameState prefab vào Prefabs list
7. **Singletons** trên cùng 1 GameObject:
   - ConnectionManager, ServerMatchManager (với prefab wired), ClientGameProxy
   - MatchmakingService, ServerBootstrap
8. **MatchmakingPanel** (full-screen overlay TMP)
   - Button "Tìm Trận" → persistent listener `MatchmakingPanel.StartMatchmaking`
   - Button "Hủy" → persistent listener `MatchmakingPanel.OnCancel`
9. Save scene

---

## Bước Tiếp Theo (TODO)

### 1. Mở Unity + Verify Compile (ưu tiên cao nhất)

```
1. Mở Unity Hub → Open → chọn folder game_dev
2. Chờ import packages (lần đầu có thể 5–10 phút vì download NGO + UGS)
3. Kiểm tra Console: phải KHÔNG có compile error
   - Nếu có lỗi "Assembly 'Unity.Collections' not found": mở MathGame.asmdef,
     đảm bảo "Unity.Collections" có trong references array
4. Chạy Tools → MathGame → Build Game Scene
5. Mở scene vừa tạo, kiểm tra:
   - NetworkManager có UnityTransport chưa
   - Prefabs list có NetworkGameState chưa
   - ServerMatchManager.networkGameStatePrefab đã wired chưa
   - MatchmakingPanel buttons có persistent listener chưa
```

### 2. Test Offline (nhanh, không cần server)

```
1. Play mode trong Editor
2. Chơi 1–2 câu hỏi → đảm bảo tile pool hoạt động, ELO tính đúng
3. Offline flow: Idle → Playing → ShowResult
```

### 3. Build Server (Linux ARM64)

```
Unity: File → Build Settings
- Platform: Dedicated Server (hoặc Linux 64-bit nếu không có Dedicated Server module)
- Architecture: ARM64 (Oracle Free Tier là Ampere ARM)
- Compression: LZ4
- Build

Upload lên Oracle VM (thay <IP> bằng public IP của VM):
scp -i ~/.ssh/oracle_key MathGameServer.x86_64 ubuntu@<IP>:~/mathgame/
scp -i ~/.ssh/oracle_key MathGameServer_Data ubuntu@<IP>:~/mathgame/ -r

Chạy server:
chmod +x ./MathGameServer.x86_64
./MathGameServer.x86_64 \
  -batchmode -nographics \
  -serverIP <PUBLIC_IP> \
  -serverPort 7777 \
  -logFile server.log &

Mở firewall Oracle (quan trọng!):
sudo iptables -I INPUT -p tcp --dport 7777 -j ACCEPT
sudo iptables -I INPUT -p udp --dport 7777 -j ACCEPT
sudo iptables-save | sudo tee /etc/iptables/rules.v4
# Cũng cần mở trong Oracle Console → VCN → Security List → Ingress Rules
```

### 4. Test Multiplayer End-to-End

```
1. Server đang chạy trên Oracle VM
2. Trong Unity Editor: MatchmakingPanel → Tìm Trận
   - Nhập IP server, port 7777, tên, ELO
3. Mở 2 instances client (hoặc build + chạy), cùng rank
4. Kiểm tra:
   - Countdown 3→2→1 hiện đúng
   - Mỗi player thấy câu hỏi riêng (independent race)
   - Điểm cập nhật real-time
   - Timer bar đếm ngược đúng
   - Kết thúc match: hiện win/lose + ELO change
   - ELO lưu vào PlayerPrefs
```

### 5. Cải Tiến Tùy Chọn (sau khi core hoạt động)

- [ ] UI nhập tên player (hiện tại mặc định là "Player" từ PlayerPrefs)
- [ ] Hiển thị rank badge (icon Bronze/Silver/...)
- [ ] Sound effects khi đúng/sai
- [ ] Reconnect flow khi mất mạng giữa chừng
- [ ] Server keep-alive heartbeat cho Lobby (Lobby API tự delete sau 30s không có host)
- [ ] Android/iOS build testing

---

## Lệnh Git Hữu Ích

```bash
# Clone về máy mới
git clone https://github.com/griler/game_dev.git
cd game_dev
git checkout main

# Xem lịch sử
git log --oneline

# Tạo branch mới nếu cần tiếp tục
git checkout -b claude/feature-name

# Push branch
git push -u origin claude/feature-name
```

**Lưu ý:** Trong môi trường Claude Code (web/remote), dùng MCP tools (`mcp__github__*`) thay vì `gh` CLI để tương tác GitHub.

---

## Cấu Hình Packages (manifest.json)

```json
{
  "dependencies": {
    "com.unity.netcode.gameobjects": "2.4.1",
    "com.unity.services.core": "1.13.0",
    "com.unity.services.authentication": "3.3.3",
    "com.unity.services.lobby": "1.5.1",
    "com.unity.textmeshpro": "3.0.9",
    "com.unity.inputsystem": "1.11.2",
    "com.unity.render-pipelines.universal": "17.3.0"
  }
}
```

---

## Các Bug Đã Fix (để tham khảo)

| Bug | Root Cause | Fix |
|-----|-----------|-----|
| GameManager crash khi networked | `HandlePhaseChange` gọi `_generator.Generate()` dù generator null | Thêm `if (_networked) return` |
| LocalClientId luôn = 0 | Cache trong `Start()` trước khi NGO connect | Đổi thành computed property |
| Timer bar đứng yên | Offline timer bị disabled, không update bar | Subscribe `TimeRemaining.OnValueChanged` |
| Race condition spawn | `RegisterClient` gọi trong approval callback, trước khi client kết nối xong | Defer sang `OnClientConnected` |
| Compile error FixedString | `Unity.Collections` thiếu trong asmdef | Thêm vào `MathGame.asmdef` |
| K-factor luôn 40 | `gamesPlayed` không có trong payload | Thêm field vào `PlayerConnectionData` |
| Player cards rỗng | `InitMatch` write NV sau Spawn, cards không nhận update | Subscribe `Player1/2ClientId.OnValueChanged` |
| FixedString overflow | Tên dài >61 bytes | `ToFixed()` clamp 20 chars |
| Lockstep gameplay | Spec yêu cầu independent race | Refactor sang `Player1QIndex`/`Player2QIndex` |
| Timer override | ClientGameProxy set duration trước `TransitionTo` | Set sau `TransitionTo` |

---

*File này được tạo ngày 2026-05-29. Xem thêm chi tiết trong CLAUDE.md nếu có.*
