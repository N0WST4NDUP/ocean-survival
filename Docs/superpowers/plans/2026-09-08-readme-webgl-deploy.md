# README + WebGL 배포/임베드 구현 계획

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** WebGL 빌드를 GitHub Pages에 배포해 브라우저에서 바로 플레이하게 하고, 그 링크와 GIF를 담은 README를 작성한다.

**Architecture:** Firebase Unity SDK가 WebGL을 지원하지 않으므로 매니저 5개를 `#if UNITY_WEBGL` 로 분기해 동일 public API의 로컬 폴백(PlayerPrefs 기반)을 제공한다. 빌드는 로컬에서 돌리고 orphan `gh-pages` 브랜치에 force push한다. README는 `main`에 두고 미디어는 LFS 예외로 일반 git 객체로 저장한다.

**Tech Stack:** Unity 6000.3.15f1 (URP 17.3.0), UniTask (`Assets/Plugins/UniTask`), Firebase Unity SDK 13.16.0 (Auth + Realtime Database), IL2CPP/WebGL, ffmpeg, GitHub Pages

**Spec:** [Docs/superpowers/specs/2026-09-08-readme-webgl-deploy-design.md](../specs/2026-09-08-readme-webgl-deploy-design.md)

## Global Constraints

- 대상 저장소: `N0WST4NDUP/ocean-survival`. Pages URL: `https://n0wst4ndup.github.io/ocean-survival/`
- 작업 브랜치: `docs/readme-webgl-deploy`. `main` 직접 푸시 금지 (CLAUDE.md §4)
- 커밋 Prefix: `Feat` / `Fix` / `Chore` / `Docs` / `Refactor` / `Test` (CLAUDE.md §4)
- C# 네이밍: `_camelCase` = private 필드, `PascalCase` = public 멤버·메서드·`const` (CLAUDE.md §6)
- 매직 넘버 금지 — `const` 또는 ScriptableObject로 (CLAUDE.md §3)
- 호출부 7개 중 수정 허용 대상은 `Assets/Scripts/UI/Leaderboard/LeaderboardUI.cs` **1줄뿐**. 다른 호출부를 고쳐야 한다면 폴백 설계가 틀린 것이므로 멈추고 보고할 것
- WebGL 폴백에서 온라인 기능(계정 연동·전체 리더보드)은 제공하지 않는다. 실패 시 사용자에게 보이는 문구는 `"웹 데모에서는 계정 기능을 사용할 수 없습니다."` 로 통일
- PlayerPrefs 키 접두사: `local.`
- 미디어 총량 10MB 이하, `Docs/images/` 에 저장
- 빌드 산출물은 저장소 밖에 출력한다. 이 문서에서 `<스크래치>` 는 **저장소 밖의 임시 작업 디렉터리**를 뜻하며, 실행자가 시작 시 한 번 정해 모든 태스크에서 같은 경로를 쓴다 (예: 세션 스크래치 폴더). 저장소 안의 경로를 쓰지 말 것 — `.gitignore` 가 `**/Build/` 를 제외하므로 산출물이 조용히 사라진 것처럼 보인다
- 게임 내용을 서술하는 모든 문구(README·랜딩 페이지)는 [Docs/GDD.md](../../GDD.md) 를 근거로 한다. GDD가 SSoT다 (CLAUDE.md §3)

## 테스트 전략에 대한 참고

이 프로젝트에는 assembly definition이 하나도 없고 모든 코드가 predefined assembly(`Assembly-CSharp`)에 있다. Unity Test Framework용 asmdef는 `Assembly-CSharp`을 참조할 수 없으므로, 자동화 테스트를 붙이려면 154개 스크립트를 asmdef 구조로 재편해야 한다. 이는 spec §11에서 명시적으로 범위 밖이다.

따라서 각 태스크는 **검증 우선(verification-first)** 루프를 따른다: 검증 방법을 먼저 정하고 → 지금 실패하는지 확인하고 → 구현하고 → 통과를 확인하고 → 커밋한다. 검증 수단은 컴파일 결과와 Play 모드 관찰이다. TDD의 순서를 유지하되 테스트 러너 대신 컴파일러와 에디터를 사용한다.

**에러가 늘었다가 줄어드는 것이 정상이다.** Task 1에서 확인했듯 시작 시점의 에디터
컴파일 에러는 0이다. Task 3에서 `FirebaseInitializer` 를 가드하면 그 WebGL 분기에는
`App` / `Database` / `Auth` 프로퍼티가 없으므로, 이를 참조하는 나머지 매니저 4개에서
에러가 **새로 발생한다**. 이것은 실수가 아니라 이 계획이 만들어내는 실패 신호다.
Task 4~7이 그 에러를 하나씩 지우고, Task 7 종료 시 다시 0이 된다.

| 시점 | 에디터 컴파일 에러 |
| --- | --- |
| Task 1~2 종료 | 0 |
| Task 3 종료 | Auth / Profile / Record / Leaderboard 4개 파일 |
| Task 4 종료 | Profile / Record / Leaderboard 3개 파일 |
| Task 5 종료 | Record / Leaderboard 2개 파일 |
| Task 6 종료 | Leaderboard 1개 파일 |
| Task 7 종료 | **0** + Play 모드에서 폴백 로그 확인 |

---

### Task 1: 베이스라인 확정 (완료)

**실행 결과 (2026-09-08):** Web 프로필로 전환했으나 에디터 Console에 컴파일
에러가 **0개**였다. 계획 초안이 예상한 `CS0246` 은 나오지 않았다.

원인은 플러그인 플랫폼 설정이다. `Assets/Firebase/Plugins/*.dll.meta` 는 다음과 같다.

```yaml
Any:    enabled: 0
Editor: enabled: 1     # 에디터용 어셈블리는 이 DLL을 링크한다
Web:    enabled: 0     # Web 플레이어 빌드에서는 제외된다
```

에디터가 조용한 이유는 에디터 변형이 `Editor: enabled: 1` 인 DLL로 `using Firebase` 를
해결하기 때문이다. 실제 Web 빌드는 이 DLL 없이 `Assembly-CSharp` 을 다시 컴파일하므로
`CS0246` 이 **빌드 시점에** 발생한다. 실패가 사라진 것이 아니라 뒤로 밀려 있다.

이 `.meta` 설정 자체가 "Firebase는 Web에서 쓸 수 없다"는 결정적 증거이므로, 베이스라인
확보를 위해 실패하는 빌드를 따로 돌리지 않는다.

**검증 신호 변경:** 현재 활성 타깃이 Web이라 에디터에도 `UNITY_WEBGL` 이 정의되어 있다.
따라서 가드를 넣는 즉시 에디터 Play 모드가 폴백 분기를 실행한다. Task 3~7의 검증은
**Play 모드 Console 로그**로 한다 — 태스크마다 빌드를 돌릴 필요가 없다.

| 확인할 로그 | 의미 |
| --- | --- |
| `[Firebase] WebGL 폴백 모드 — 로컬 저장소를 사용합니다.` | 가드가 살아 있다 |
| `[Firebase] 초기화 시작...` | 가드가 안 먹었다. 활성 타깃이 Web인지 다시 볼 것 |

**무시해도 되는 경고 2개** (이번 작업과 무관, 기존부터 존재):

- `LoginUI.cs(48,30) CS1998` — `UpdateUI()` 가 `async UniTaskVoid` 인데 `await` 가 없다
- `Could not locate google-services.json` — Firebase Editor 도구가 Android/iOS 설정 파일을
  찾는 경고. 이 프로젝트는 `FirebaseConfig` ScriptableObject의 `databaseUrl` 을 쓴다

---

### Task 2: 로컬 저장 계층 `LocalStore` 추가

플랫폼 무관 순수 C#. WebGL 폴백들이 공유하는 PlayerPrefs 백엔드다. 이 태스크는 Windows 타깃에서도 컴파일되므로 단독으로 검증할 수 있다.

**Files:**
- Create: `Assets/Scripts/Core/Firebase/Local/LocalStore.cs`

**Interfaces:**
- Consumes: `ClearTimeRecord`, `LeaderboardEntry`, `UserProfileData`, `TimeUtil` (모두 기존 타입, Firebase 의존 없음)
- Produces:
  - `LocalStore.UserId` → `string` (없으면 생성 후 영속)
  - `LocalStore.Nickname` → `string` (get/set, 기본값 `"Guest"`)
  - `LocalStore.Email` → `string` (get/set, 기본값 `"local@webdemo"`)
  - `LocalStore.CreatedAtMillis` → `long`
  - `LocalStore.LoadBestMs()` → `long` (없으면 `-1`)
  - `LocalStore.SaveBestMs(long ms)` → `void`
  - `LocalStore.LoadHistory()` → `List<ClearTimeRecord>` (최신순)
  - `LocalStore.AppendHistory(long clearTimeMs)` → `void`
  - `LocalStore.LoadLeaderboard(int limit)` → `List<LeaderboardEntry>` (빠른 순)
  - `LocalStore.HistoryCapacity` → `const int` = `20`

- [ ] **Step 1: 검증 기준 정의 (실패 확인)**

```bash
grep -r "LocalStore" Assets/Scripts --include=*.cs
```

기대: 결과 없음. 아직 존재하지 않는다.

- [ ] **Step 2: `LocalStore.cs` 작성**

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

// WebGL 폴백용 로컬 저장소. PlayerPrefs를 백엔드로 사용한다.
// Firebase를 쓸 수 없는 플랫폼에서 계정·기록·리더보드를 기기 로컬에 유지하기 위한 것.
// 플랫폼 무관하게 컴파일되며, 실제 사용은 #if UNITY_WEBGL 분기에서만 한다.
public static class LocalStore
{
    private const string KeyUserId = "local.auth.userId";
    private const string KeyNickname = "local.profile.nickname";
    private const string KeyEmail = "local.profile.email";
    private const string KeyCreatedAt = "local.profile.createdAt";
    private const string KeyBestMs = "local.record.bestMs";
    private const string KeyHistory = "local.record.history";

    private const string DefaultNickname = "Guest";
    private const string DefaultEmail = "local@webdemo";

    // 히스토리 보관 개수. PlayerPrefs 문자열 길이를 제한하기 위한 상한.
    public const int HistoryCapacity = 20;

    // 기록 없음을 나타내는 값. RecordManager의 계약과 동일하게 -1.
    public const long NoRecord = -1;

    // --- 계정 ------------------------------

    // 최초 호출 시 GUID를 만들어 영속화한다. 이후 같은 브라우저/기기에서 동일하다.
    public static string UserId
    {
        get
        {
            string id = PlayerPrefs.GetString(KeyUserId, string.Empty);
            if (string.IsNullOrEmpty(id))
            {
                id = "local-" + Guid.NewGuid().ToString("N").Substring(0, 12);
                PlayerPrefs.SetString(KeyUserId, id);
                PlayerPrefs.Save();
            }
            return id;
        }
    }

    public static string Nickname
    {
        get => PlayerPrefs.GetString(KeyNickname, DefaultNickname);
        set
        {
            PlayerPrefs.SetString(KeyNickname, string.IsNullOrEmpty(value) ? DefaultNickname : value);
            PlayerPrefs.Save();
        }
    }

    public static string Email
    {
        get => PlayerPrefs.GetString(KeyEmail, DefaultEmail);
        set
        {
            PlayerPrefs.SetString(KeyEmail, string.IsNullOrEmpty(value) ? DefaultEmail : value);
            PlayerPrefs.Save();
        }
    }

    public static long CreatedAtMillis
    {
        get
        {
            string raw = PlayerPrefs.GetString(KeyCreatedAt, string.Empty);
            if (long.TryParse(raw, out long millis)) return millis;

            long now = TimeUtil.NowUnixMillis();
            PlayerPrefs.SetString(KeyCreatedAt, now.ToString());
            PlayerPrefs.Save();
            return now;
        }
    }

    // --- 베스트 기록 ------------------------------

    public static long LoadBestMs()
    {
        string raw = PlayerPrefs.GetString(KeyBestMs, string.Empty);
        return long.TryParse(raw, out long ms) ? ms : NoRecord;
    }

    public static void SaveBestMs(long ms)
    {
        PlayerPrefs.SetString(KeyBestMs, ms.ToString());
        PlayerPrefs.Save();
    }

    // --- 히스토리 ------------------------------

    // PlayerPrefs는 배열을 담지 못하므로 JsonUtility가 다룰 수 있는 래퍼로 감싼다.
    [Serializable]
    private class HistoryBox
    {
        public List<ClearTimeRecord> items = new();
    }

    // 최신순으로 반환한다 (RecordManager.LoadHistoryAsync 계약과 동일).
    public static List<ClearTimeRecord> LoadHistory()
    {
        string json = PlayerPrefs.GetString(KeyHistory, string.Empty);
        if (string.IsNullOrEmpty(json)) return new List<ClearTimeRecord>();

        HistoryBox box = JsonUtility.FromJson<HistoryBox>(json);
        return box?.items ?? new List<ClearTimeRecord>();
    }

    public static void AppendHistory(long clearTimeMs)
    {
        List<ClearTimeRecord> items = LoadHistory();
        items.Insert(0, new ClearTimeRecord(clearTimeMs, TimeUtil.NowUnixMillis()));

        if (items.Count > HistoryCapacity)
        {
            items.RemoveRange(HistoryCapacity, items.Count - HistoryCapacity);
        }

        PlayerPrefs.SetString(KeyHistory, JsonUtility.ToJson(new HistoryBox { items = items }));
        PlayerPrefs.Save();
    }

    // --- 리더보드 ------------------------------

    // 로컬 히스토리에서 빠른 순 상위 limit개를 리더보드 형태로 만든다.
    // 이 기기의 기록만 담기므로 전체 랭킹이 아니다.
    public static List<LeaderboardEntry> LoadLeaderboard(int limit)
    {
        List<ClearTimeRecord> history = LoadHistory();
        history.Sort((a, b) => a.clearTimeMs.CompareTo(b.clearTimeMs));

        string userId = UserId;
        string nickname = Nickname;

        var entries = new List<LeaderboardEntry>();
        int count = Math.Min(limit, history.Count);
        for (int i = 0; i < count; i++)
        {
            entries.Add(new LeaderboardEntry(userId, nickname, history[i].clearTimeMs, history[i].timestamp));
        }
        return entries;
    }
}
```

- [ ] **Step 3: 컴파일 확인**

에디터로 돌아가 컴파일이 끝날 때까지 기다린다. Console에 `LocalStore` 관련 에러가 없어야 한다.

에디터 컴파일 에러는 여전히 0개다 (이 태스크는 매니저를 건드리지 않았다).

- [ ] **Step 4: 커밋**

```bash
git add Assets/Scripts/Core/Firebase/Local/
git commit -m "Feat: WebGL 폴백용 로컬 저장 계층 LocalStore 추가

Firebase를 쓸 수 없는 플랫폼에서 계정·기록·리더보드를 PlayerPrefs에
유지하기 위한 공용 계층. 플랫폼 무관하게 컴파일되며 사용은
#if UNITY_WEBGL 분기에서만 한다.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

### Task 3: `FirebaseInitializer` WebGL 분기

**Files:**
- Modify: `Assets/Scripts/Core/Firebase/FirebaseInitializer.cs`

**Interfaces:**
- Consumes: 없음
- Produces (양쪽 분기에서 동일):
  - `FirebaseInitializer.Instance`
  - `FirebaseInitializer.InitState` (`Pending` / `Ready` / `Failed`)
  - `State`, `IsReady`, `LastError`
  - `UniTask<bool> WaitForInitializationAsync()`
  - `App` / `Database` / `Auth` 는 **비WebGL 분기에만** 존재한다 (Firebase 타입이므로). 사용처는 형제 매니저뿐이고 그쪽도 함께 분기되므로 문제없다.

- [ ] **Step 1: 실패 확인**

```bash
grep -c "UNITY_WEBGL" Assets/Scripts/Core/Firebase/FirebaseInitializer.cs
```

기대: `0`

- [ ] **Step 2: 파일 전체를 분기 구조로 교체**

기존 내용 전체를 `#else` 블록으로 옮기고, `#if UNITY_WEBGL` 블록에 폴백을 넣는다. 파일 첫 줄이 `#if UNITY_WEBGL`, 마지막 줄이 `#endif` 가 되어야 한다.

WebGL 분기 내용:

```csharp
#if UNITY_WEBGL

using Cysharp.Threading.Tasks;
using UnityEngine;

// WebGL 폴백: Firebase Unity SDK는 WebGL 네이티브 바이너리를 제공하지 않으므로
// 초기화를 즉시 성공 처리하고, 실제 데이터는 LocalStore가 담당한다.
public class FirebaseInitializer : MonoBehaviour
{
    private static FirebaseInitializer _instance;
    public static FirebaseInitializer Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindFirstObjectByType<FirebaseInitializer>();

                if (_instance == null)
                {
                    var singletonObject = new GameObject();
                    _instance = singletonObject.AddComponent<FirebaseInitializer>();
                    singletonObject.name = typeof(FirebaseInitializer).ToString() + " (Singleton)";
                }
            }
            return _instance;
        }
    }

    public enum InitState
    {
        Pending,
        Ready,
        Failed,
    }

    public InitState State { get; private set; } = InitState.Ready;
    public bool IsReady => State == InitState.Ready;
    public string LastError { get; private set; }

    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
            DontDestroyOnLoad(gameObject);

            State = InitState.Ready;
            Debug.Log("[Firebase] WebGL 폴백 모드 — 로컬 저장소를 사용합니다.");
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public UniTask<bool> WaitForInitializationAsync()
    {
        return UniTask.FromResult(true);
    }

    private void OnDestroy()
    {
        if (_instance == this)
        {
            _instance = null;
        }
    }
}

#else

// (기존 Firebase 구현 전체를 여기에 그대로 둔다 — using 문 포함)

#endif
```

기존 구현의 `using Cysharp.Threading.Tasks; using Firebase; using Firebase.Auth; using Firebase.Database; using UnityEngine;` 다섯 줄도 `#else` 블록 **안**으로 들어가야 한다. `using Firebase;` 가 `#if` 밖에 남으면 WebGL 컴파일이 실패한다.

- [ ] **Step 3: 컴파일 확인**

`FirebaseInitializer.cs` 자체에는 에러가 없어야 한다.

동시에 나머지 매니저 4개(`AuthManager`, `ProfileManager`, `RecordManager`,
`LeaderboardManager`)에서 `FirebaseInitializer.Instance.Auth` / `.Database` 를 찾을 수 없다는
에러가 **새로 나타난다**. 이것이 기대한 결과다 — WebGL 분기에는 그 프로퍼티가 없기 때문이다.
Task 4~7이 이 에러들을 지운다.

4개가 아닌 다른 파일에서 에러가 나면 멈추고 보고할 것.

- [ ] **Step 4: 커밋**

```bash
git add Assets/Scripts/Core/Firebase/FirebaseInitializer.cs
git commit -m "Feat: FirebaseInitializer WebGL 폴백 분기

Firebase Unity SDK는 WebGL 네이티브 바이너리를 제공하지 않아
초기화 자체가 불가능하다. WebGL에서는 즉시 Ready로 두고
데이터는 LocalStore가 담당한다.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

### Task 4: `AuthManager` WebGL 분기 + `CurrentUserId` 노출

**Files:**
- Modify: `Assets/Scripts/Core/Firebase/Auth/AuthManager.cs`
- Modify: `Assets/Scripts/UI/Leaderboard/LeaderboardUI.cs:68`

**Interfaces:**
- Consumes: `LocalStore.UserId`, `LocalStore.Nickname` (Task 2)
- Produces (양쪽 분기에서 동일):
  - `AuthManager.Instance`
  - `bool IsInitialized`, `bool IsLoggedIn`
  - `string CurrentUserId` — **신규**. 로그인 상태가 아니면 `null`
  - `string CurrentDisplayName` — **신규**. 없으면 `null`
  - `event Action<bool> LoginStateChanged`
  - `UniTask<bool> WaitForInitializationAsync()`
  - `UniTask<(bool success, string error)> SignInAnonymouslyAsync()`
  - `UniTask<(bool success, string error)> CreateUserWithEmailAsync(string email, string passwd, string nickname = "Anonymous")`
  - `UniTask<(bool success, string error)> SignInUserWithEmailAsync(string email, string passwd)`
  - `UniTask<(bool success, string error)> LinkWithEmailAsync(string email, string passwd)`
  - `void SignOut()`
  - `Auth` / `CurrentUser` 는 **비WebGL 분기에만** 존재한다

- [ ] **Step 1: 실패 확인 — 외부 호출부가 Firebase 타입을 만지는 지점**

```bash
grep -n "CurrentUser" Assets/Scripts/UI/Leaderboard/LeaderboardUI.cs
```

기대: `68:            ? AuthManager.Instance.CurrentUser.UserId`

이 한 줄이 spec §5.3의 예외다. 다른 호출부에서 `CurrentUser` 가 나오면 멈추고 보고할 것.

```bash
grep -rn "\.CurrentUser" Assets/Scripts --include=*.cs | grep -v "Core/Firebase"
```

기대: 위 한 줄만 나온다.

- [ ] **Step 2: 비WebGL 분기에 `CurrentUserId` / `CurrentDisplayName` 추가**

기존 `CurrentUser` 프로퍼티 바로 아래에 넣는다.

```csharp
    public FirebaseUser CurrentUser => Auth.CurrentUser;

    // Firebase 타입을 UI로 새어나가지 않게 하는 얇은 래퍼.
    // WebGL 폴백에서도 같은 이름으로 제공되므로 호출부가 플랫폼을 몰라도 된다.
    public string CurrentUserId => Auth?.CurrentUser?.UserId;
    public string CurrentDisplayName => Auth?.CurrentUser?.DisplayName;
```

`ResolveNickname()`(LeaderboardManager)과 `SaveProfileAsync()`(ProfileManager)는 각자의 WebGL 분기에서 처리되므로 지금 건드리지 않는다.

- [ ] **Step 3: `LeaderboardUI.cs:68` 을 래퍼로 교체**

변경 전:

```csharp
        string myUid = (AuthManager.Instance.IsInitialized && AuthManager.Instance.IsLoggedIn)
            ? AuthManager.Instance.CurrentUser.UserId
            : null;
```

변경 후:

```csharp
        string myUid = (AuthManager.Instance.IsInitialized && AuthManager.Instance.IsLoggedIn)
            ? AuthManager.Instance.CurrentUserId
            : null;
```

- [ ] **Step 4: 파일 전체를 분기 구조로 교체**

Task 3과 같은 방식이다. `using System; using Cysharp.Threading.Tasks; using Firebase.Auth; using UnityEngine;` 를 포함한 기존 구현 전체가 `#else` 블록 안으로 들어간다.

WebGL 분기 내용:

```csharp
#if UNITY_WEBGL

using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

// WebGL 폴백: 게스트 계정으로 자동 로그인한다.
// 계정 식별자와 닉네임은 LocalStore(PlayerPrefs)에 저장되며 브라우저 로컬에만 존재한다.
public class AuthManager : MonoBehaviour
{
    // 웹 데모에서 계정 기능 호출 시 UI에 표시할 문구.
    private const string WebDemoNotice = "웹 데모에서는 계정 기능을 사용할 수 없습니다.";

    private static AuthManager _instance;
    public static AuthManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindFirstObjectByType<AuthManager>();

                if (_instance == null)
                {
                    var singletonObject = new GameObject();
                    _instance = singletonObject.AddComponent<AuthManager>();
                    singletonObject.name = typeof(AuthManager).ToString() + " (Singleton)";
                }
            }
            return _instance;
        }
    }

    public bool IsInitialized { get; private set; } = false;

    // 웹 데모는 항상 게스트로 로그인된 상태로 취급한다.
    public bool IsLoggedIn => true;

    public string CurrentUserId => LocalStore.UserId;
    public string CurrentDisplayName => LocalStore.Nickname;

    public event Action<bool> LoginStateChanged;

    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
            DontDestroyOnLoad(gameObject);

            IsInitialized = true;
            Debug.Log($"[Auth] WebGL 게스트 로그인: {LocalStore.UserId}");
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        // 구독자가 붙을 시간을 준 뒤 로그인 상태를 한 번 알린다.
        LoginStateChanged?.Invoke(true);
    }

    public UniTask<bool> WaitForInitializationAsync()
    {
        return UniTask.FromResult(true);
    }

    public UniTask<(bool success, string error)> SignInAnonymouslyAsync()
    {
        return UniTask.FromResult<(bool, string)>((true, null));
    }

    public UniTask<(bool success, string error)> CreateUserWithEmailAsync(string email, string passwd, string nickname = "Anonymous")
    {
        LocalStore.Nickname = nickname;
        return UniTask.FromResult<(bool, string)>((false, WebDemoNotice));
    }

    public UniTask<(bool success, string error)> SignInUserWithEmailAsync(string email, string passwd)
    {
        return UniTask.FromResult<(bool, string)>((false, WebDemoNotice));
    }

    public UniTask<(bool success, string error)> LinkWithEmailAsync(string email, string passwd)
    {
        return UniTask.FromResult<(bool, string)>((false, WebDemoNotice));
    }

    // 웹 데모에는 로그아웃 개념이 없다. 게스트 상태가 유지된다.
    public void SignOut()
    {
        Debug.Log("[Auth] WebGL 게스트 모드 — 로그아웃하지 않습니다.");
    }

    private void OnDestroy()
    {
        if (_instance == this)
        {
            _instance = null;
        }
    }
}

#else

// (기존 Firebase 구현 전체 — Step 2에서 추가한 CurrentUserId / CurrentDisplayName 포함)

#endif
```

- [ ] **Step 5: 컴파일 확인**

`AuthManager.cs` 와 `LeaderboardUI.cs` 의 에러가 사라졌는지 Console에서 확인한다.

남아 있어야 할 에러: `ProfileManager`, `RecordManager`, `LeaderboardManager` 3개 파일.
이들은 `FirebaseInitializer.Instance.Database` 와 `AuthManager.Instance.CurrentUser` 를
참조하는데 둘 다 WebGL 분기에는 없다. Task 5~7에서 해소된다.

- [ ] **Step 6: 커밋**

```bash
git add Assets/Scripts/Core/Firebase/Auth/AuthManager.cs Assets/Scripts/UI/Leaderboard/LeaderboardUI.cs
git commit -m "Feat: AuthManager WebGL 게스트 폴백 + CurrentUserId 래퍼

LeaderboardUI가 FirebaseUser를 직접 만지고 있어 플랫폼 분기가
UI로 새어나갔다. CurrentUserId 래퍼를 양쪽 분기에 두어 호출부가
플랫폼을 모르게 했다.

WebGL에서는 LocalStore 기반 게스트 계정으로 자동 로그인하며,
이메일 계정 기능은 안내 문구와 함께 거부한다.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

### Task 5: `ProfileManager` WebGL 분기

**Files:**
- Modify: `Assets/Scripts/Core/Firebase/Profile/ProfileManager.cs`

**Interfaces:**
- Consumes: `LocalStore.Nickname`, `LocalStore.Email`, `LocalStore.CreatedAtMillis` (Task 2)
- Produces (양쪽 분기에서 동일):
  - `ProfileManager.Instance`
  - `UserProfileData CachedProfile`
  - `bool IsInitialized`
  - `UniTask<bool> WaitForInitializationAsync()`
  - `UniTask<(bool success, string error)> SaveProfileAsync(string nickname)`
  - `UniTask<(UserProfileData profile, string error)> LoadProfileAsync()`
  - `UniTask<(bool success, string error)> UpdateNicknameAsync(string nickname)`

- [ ] **Step 1: 실패 확인**

```bash
grep -c "UNITY_WEBGL" Assets/Scripts/Core/Firebase/Profile/ProfileManager.cs
```

기대: `0`

- [ ] **Step 2: 파일 전체를 분기 구조로 교체**

WebGL 분기 내용:

```csharp
#if UNITY_WEBGL

using Cysharp.Threading.Tasks;
using UnityEngine;

// WebGL 폴백: 프로필을 LocalStore(PlayerPrefs)에 저장한다.
public class ProfileManager : MonoBehaviour
{
    private static ProfileManager _instance;
    public static ProfileManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindFirstObjectByType<ProfileManager>();

                if (_instance == null)
                {
                    var singletonObject = new GameObject();
                    _instance = singletonObject.AddComponent<ProfileManager>();
                    singletonObject.name = typeof(ProfileManager).ToString() + " (Singleton)";
                }
            }
            return _instance;
        }
    }

    private UserProfileData _cachedProfile;
    public UserProfileData CachedProfile => _cachedProfile;

    public bool IsInitialized { get; private set; } = false;

    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
            DontDestroyOnLoad(gameObject);

            _cachedProfile = BuildLocalProfile();
            IsInitialized = true;
            Debug.Log($"[Profile] WebGL 로컬 프로필: {_cachedProfile.nickname}");
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // createdAt이 매번 갱신되지 않도록 LocalStore에 보관된 값을 쓴다.
    private UserProfileData BuildLocalProfile()
    {
        var profile = new UserProfileData(LocalStore.Nickname, LocalStore.Email);
        profile.createdAt = LocalStore.CreatedAtMillis;
        return profile;
    }

    public UniTask<bool> WaitForInitializationAsync()
    {
        return UniTask.FromResult(true);
    }

    public UniTask<(bool success, string error)> SaveProfileAsync(string nickname)
    {
        LocalStore.Nickname = nickname;
        _cachedProfile = BuildLocalProfile();
        return UniTask.FromResult<(bool, string)>((true, null));
    }

    public UniTask<(UserProfileData profile, string error)> LoadProfileAsync()
    {
        _cachedProfile = BuildLocalProfile();
        return UniTask.FromResult<(UserProfileData, string)>((_cachedProfile, null));
    }

    public UniTask<(bool success, string error)> UpdateNicknameAsync(string nickname)
    {
        LocalStore.Nickname = nickname;
        if (_cachedProfile != null) _cachedProfile.nickname = nickname;
        return UniTask.FromResult<(bool, string)>((true, null));
    }

    private void OnDestroy()
    {
        if (_instance == this)
        {
            _instance = null;
        }
    }
}

#else

// (기존 Firebase 구현 전체 — using 문 포함)

#endif
```

- [ ] **Step 3: 컴파일 확인**

`ProfileManager.cs` 의 에러가 사라졌는지 확인한다.

남아 있어야 할 에러: `RecordManager`, `LeaderboardManager` 2개 파일.

- [ ] **Step 4: 커밋**

```bash
git add Assets/Scripts/Core/Firebase/Profile/ProfileManager.cs
git commit -m "Feat: ProfileManager WebGL 로컬 폴백

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

### Task 6: `RecordManager` WebGL 분기

**Files:**
- Modify: `Assets/Scripts/Core/Firebase/Record/RecordManager.cs`

**Interfaces:**
- Consumes: `LocalStore.LoadBestMs()`, `SaveBestMs()`, `AppendHistory()`, `LoadHistory()`, `LocalStore.NoRecord` (Task 2)
- Produces (양쪽 분기에서 동일):
  - `RecordManager.Instance`
  - `long CachedBestMs`, `bool HasBest`, `bool IsInitialized`
  - `UniTask<bool> WaitForInitializationAsync()`
  - `UniTask<(bool success, bool isNewBest, long bestMs)> SaveClearTimeAsync(float clearTimeSeconds)`
  - `UniTask<long> LoadBestMsAsync()`
  - `UniTask<List<ClearTimeRecord>> LoadHistoryAsync(int limit = 10)`

- [ ] **Step 1: 실패 확인**

```bash
grep -c "UNITY_WEBGL" Assets/Scripts/Core/Firebase/Record/RecordManager.cs
```

기대: `0`

- [ ] **Step 2: 파일 전체를 분기 구조로 교체**

WebGL 분기 내용:

```csharp
#if UNITY_WEBGL

using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

// WebGL 폴백: 클리어 타임을 LocalStore(PlayerPrefs)에 저장한다.
// best는 최소값(빠를수록 상위), history는 최신순 LocalStore.HistoryCapacity개.
public class RecordManager : MonoBehaviour
{
    private static RecordManager _instance;
    public static RecordManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindFirstObjectByType<RecordManager>();

                if (_instance == null)
                {
                    var singletonObject = new GameObject();
                    _instance = singletonObject.AddComponent<RecordManager>();
                    singletonObject.name = typeof(RecordManager).ToString() + " (Singleton)";
                }
            }
            return _instance;
        }
    }

    private long _cachedBestMs = LocalStore.NoRecord;
    public long CachedBestMs => _cachedBestMs;
    public bool HasBest => _cachedBestMs >= 0;

    public bool IsInitialized { get; private set; } = false;

    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
            DontDestroyOnLoad(gameObject);

            _cachedBestMs = LocalStore.LoadBestMs();
            IsInitialized = true;
            Debug.Log($"[Record] WebGL 로컬 기록 로드: {_cachedBestMs}ms");
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public UniTask<bool> WaitForInitializationAsync()
    {
        return UniTask.FromResult(true);
    }

    public UniTask<(bool success, bool isNewBest, long bestMs)> SaveClearTimeAsync(float clearTimeSeconds)
    {
        long ms = (long)Math.Round(clearTimeSeconds * 1000.0);

        LocalStore.AppendHistory(ms);

        bool isNewBest = !HasBest || ms < _cachedBestMs;
        if (isNewBest)
        {
            _cachedBestMs = ms;
            LocalStore.SaveBestMs(ms);
        }

        Debug.Log($"[Record] WebGL 로컬 저장 (신기록: {isNewBest}, best: {_cachedBestMs}ms)");
        return UniTask.FromResult((true, isNewBest, _cachedBestMs));
    }

    public UniTask<long> LoadBestMsAsync()
    {
        _cachedBestMs = LocalStore.LoadBestMs();
        return UniTask.FromResult(_cachedBestMs);
    }

    public UniTask<List<ClearTimeRecord>> LoadHistoryAsync(int limit = 10)
    {
        List<ClearTimeRecord> all = LocalStore.LoadHistory();
        if (all.Count > limit) all.RemoveRange(limit, all.Count - limit);
        return UniTask.FromResult(all);
    }

    private void OnDestroy()
    {
        if (_instance == this)
        {
            _instance = null;
        }
    }
}

#else

// (기존 Firebase 구현 전체 — using 문 포함)

#endif
```

- [ ] **Step 3: 컴파일 확인**

`RecordManager.cs` 의 에러가 사라졌는지 확인한다.

남아 있어야 할 에러: `LeaderboardManager` 1개 파일.

- [ ] **Step 4: 커밋**

```bash
git add Assets/Scripts/Core/Firebase/Record/RecordManager.cs
git commit -m "Feat: RecordManager WebGL 로컬 폴백

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

### Task 7: `LeaderboardManager` WebGL 분기 + WebGL 컴파일 통과

이 태스크가 끝나면 WebGL 타깃에서 컴파일 에러가 0이 되어야 한다.

**Files:**
- Modify: `Assets/Scripts/Core/Firebase/Leaderboard/LeaderboardManager.cs`

**Interfaces:**
- Consumes: `LocalStore.LoadLeaderboard(int)` (Task 2)
- Produces (양쪽 분기에서 동일):
  - `LeaderboardManager.Instance`
  - `bool IsInitialized`
  - `event Action<List<LeaderboardEntry>> OnLeaderboardUpdated`
  - `UniTask<bool> WaitForInitializationAsync()`
  - `UniTask<(bool success, string error)> SaveToLeaderboardAsync(long clearTimeMs)`
  - `UniTask<List<LeaderboardEntry>> LoadLeaderboardAsync(int limit = 10)`
  - `void StartRealtimeListener(int limit = 10)`
  - `void StopRealtimeListener()`

- [ ] **Step 1: 실패 확인**

```bash
grep -c "UNITY_WEBGL" Assets/Scripts/Core/Firebase/Leaderboard/LeaderboardManager.cs
```

기대: `0`

- [ ] **Step 2: 파일 전체를 분기 구조로 교체**

WebGL 분기 내용:

```csharp
#if UNITY_WEBGL

using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

// WebGL 폴백: 이 기기의 로컬 기록만으로 리더보드를 구성한다.
// 전체 유저 랭킹이 아니므로 UI에 그 사실을 표시하는 것은 랜딩 페이지가 담당한다.
public class LeaderboardManager : MonoBehaviour
{
    // 로컬 리더보드에 표시할 최대 항목 수 (spec §5.2).
    private const int LocalLeaderboardLimit = 10;

    private static LeaderboardManager _instance;
    public static LeaderboardManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindFirstObjectByType<LeaderboardManager>();

                if (_instance == null)
                {
                    var singletonObject = new GameObject();
                    _instance = singletonObject.AddComponent<LeaderboardManager>();
                    singletonObject.name = typeof(LeaderboardManager).ToString() + " (Singleton)";
                }
            }
            return _instance;
        }
    }

    public bool IsInitialized { get; private set; } = false;

    public event Action<List<LeaderboardEntry>> OnLeaderboardUpdated;

    private bool _isListenerActive;

    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
            DontDestroyOnLoad(gameObject);

            IsInitialized = true;
            Debug.Log("[Leaderboard] WebGL 로컬 리더보드 모드.");
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public UniTask<bool> WaitForInitializationAsync()
    {
        return UniTask.FromResult(true);
    }

    // 로컬에서는 기록 저장 시점에 이미 LocalStore에 반영되므로 갱신 통지만 한다.
    public UniTask<(bool success, string error)> SaveToLeaderboardAsync(long clearTimeMs)
    {
        if (_isListenerActive)
        {
            OnLeaderboardUpdated?.Invoke(LocalStore.LoadLeaderboard(LocalLeaderboardLimit));
        }
        return UniTask.FromResult<(bool, string)>((true, null));
    }

    public UniTask<List<LeaderboardEntry>> LoadLeaderboardAsync(int limit = 10)
    {
        return UniTask.FromResult(LocalStore.LoadLeaderboard(limit));
    }

    // 로컬 저장소에는 외부 변경이 없으므로, 구독 시작 시 현재 값을 한 번 발행한다.
    public void StartRealtimeListener(int limit = 10)
    {
        if (_isListenerActive) return;

        _isListenerActive = true;
        DispatchOnceAsync(limit).Forget();
        Debug.Log("[Leaderboard] WebGL 로컬 리더보드 발행");
    }

    private async UniTaskVoid DispatchOnceAsync(int limit)
    {
        // 구독자가 붙기 전에 발행되지 않도록 한 프레임 미룬다.
        await UniTask.Yield();
        OnLeaderboardUpdated?.Invoke(LocalStore.LoadLeaderboard(limit));
    }

    public void StopRealtimeListener()
    {
        _isListenerActive = false;
    }

    private void OnDestroy()
    {
        StopRealtimeListener();
        if (_instance == this)
        {
            _instance = null;
        }
    }
}

#else

// (기존 Firebase 구현 전체 — using 문 포함)

#endif
```

- [ ] **Step 3: WebGL 컴파일 통과 확인**

에디터 Console을 Clear한 뒤 재컴파일한다 (`Assets > Refresh`, 단축키 `Ctrl+R`).

기대: 에러 0개. Task 3에서 새로 생겼던 에러가 전부 해소되었다.

에러가 남아 있으면 그 파일을 이 계획에 없는 새 항목으로 간주하고 멈춰서 보고할 것.

- [ ] **Step 4: WebGL Play 모드 동작 확인**

`Assets/Scenes/EntryPoint.unity` 를 열고 Play. 다음을 확인한다.

1. Console에 `[Firebase] WebGL 폴백 모드` / `[Auth] WebGL 게스트 로그인:` 로그가 뜬다
2. 로그인 화면을 거치지 않고 타이틀로 진입한다
3. 게임을 클리어하면 결과 화면에 클리어 타임이 표시되고 `[Record] WebGL 로컬 저장` 로그가 뜬다
4. 다시 클리어했을 때 이전 기록보다 빠르면 신기록으로 표시된다
5. 리더보드 화면에 자신의 기록이 나온다

- [ ] **Step 5: Windows 타깃 회귀 확인**

에디터: `File > Build Profiles > Windows > Switch Platform` (재임포트 5~15분)

`EntryPoint.unity` 에서 Play. Firebase 로그인·리더보드가 종전대로 동작하는지 확인한다. Console에 `[Firebase] 초기화 성공` 이 떠야 한다.

여기서 깨지면 `#else` 블록으로 옮기는 과정에서 기존 코드가 손상된 것이다. `git diff main -- Assets/Scripts/Core/Firebase/` 로 대조한다.

- [ ] **Step 6: 커밋**

```bash
git add Assets/Scripts/Core/Firebase/Leaderboard/LeaderboardManager.cs
git commit -m "Feat: LeaderboardManager WebGL 로컬 폴백

WebGL 타깃 컴파일 에러 0 달성. Firebase 매니저 5개 분기 완료.
Windows 타깃 회귀 없음을 Play 모드로 확인.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

### Task 8: WebGL 빌드 설정 + 첫 빌드

**Files:**
- Modify: `ProjectSettings/ProjectSettings.asset` (에디터가 갱신)

**Interfaces:**
- Consumes: Task 7의 컴파일 통과
- Produces: `<스크래치>/webgl-build/` — `index.html`, `Build/`, `TemplateData/`

- [ ] **Step 1: 현재 설정이 잘못되어 있음을 확인**

```bash
grep -E "webGLCompressionFormat|webGLDecompressionFallback" ProjectSettings/ProjectSettings.asset
```

기대: `webGLCompressionFormat: 0` (Brotli), `webGLDecompressionFallback: 0` (OFF)

이 조합은 GitHub Pages에서 로딩 실패한다 (spec §2.3).

- [ ] **Step 2: 에디터에서 설정 변경**

Unity 6은 플랫폼 표시명이 "WebGL"이 아니라 **"Web"** 이다 (스크립팅 심볼은 `UNITY_WEBGL`
그대로). 프로필은 `Web - Desktop - Release` 를 쓴다.

```
File > Build Profiles > Web - Desktop - Release > Switch Platform   (재임포트 10~20분)

같은 창의 Player Settings 섹션에서:
  Publishing Settings ▶
    Compression Format      = Brotli
    Decompression Fallback  = 체크 ON
  Other Settings ▶
    Strip Engine Code       = 체크 ON

Platform Settings (Web) 상단:
  Code Optimization       = Disk Size with LTO   (기본값. 그대로 두면 된다)
  Development Build       = 해제                  (배포용이므로)
```

Task 7에서 Windows로 회귀 확인을 했다면 여기서 Web으로 되돌아온다. 이미 Web이 활성이고
설정도 되어 있으면 Step 3으로 건너뛴다.

- [ ] **Step 3: 설정이 반영됐는지 확인**

```bash
grep -E "webGLCompressionFormat|webGLDecompressionFallback" ProjectSettings/ProjectSettings.asset
```

기대: `webGLCompressionFormat: 0`, `webGLDecompressionFallback: 1`

- [ ] **Step 4: 빌드**

```
File > Build Profiles > WebGL > Build
출력 경로: <스크래치 폴더>/webgl-build
```

첫 IL2CPP 빌드는 20~40분 걸린다. 이 시간에는 에디터를 건드리지 않는다. 대기 중 Task 10의 README 초안을 작성해도 된다.

- [ ] **Step 5: 로컬 서버로 빌드 검증**

Pages에 올리기 전에 로컬에서 확인한다. `file://` 로는 열리지 않는다 (CORS).

```bash
cd <스크래치>/webgl-build && python -m http.server 8080
```

브라우저에서 `http://localhost:8080` 을 연다. 확인 항목:

1. 로딩 바가 100%까지 도달한다
2. 개발자 도구 Console에 `Unable to parse` 오류가 없다
3. 타이틀 화면이 뜨고 조작에 반응한다

로딩이 멈추면 Step 2의 Decompression Fallback이 실제로 켜졌는지 다시 본다.

빌드 용량도 함께 확인한다.

```bash
du -sh <스크래치>/webgl-build
du -sh <스크래치>/webgl-build/Build
```

`Build/` 가 50MB를 넘으면 첫 로딩이 느려 데모로서 값어치가 떨어진다. 그 경우
`Assets/Imported/Epic Toon FX/Demo/` 의 데모 씬들이 빌드에 포함되고 있는지
확인한다 (Build Profiles의 씬 목록에는 `EntryPoint` 와 `InGame` 둘뿐이어야 한다).
그래도 크면 spec §12에 따라 데모 에셋 제외를 검토하되, 범위가 커지므로 멈추고 보고할 것.

- [ ] **Step 6: 커밋**

```bash
git add ProjectSettings/ProjectSettings.asset
git commit -m "Chore: WebGL 빌드 설정 — Decompression Fallback 활성화

GitHub Pages는 .br 파일에 Content-Encoding 헤더를 붙이지 않아
Brotli 압축 빌드가 로딩 실패한다. 로더에 JS 압축 해제기를 포함시켜
정적 호스팅에서도 동작하게 한다.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

### Task 9: `gh-pages` 배포 + 랜딩 페이지

**Files:**
- Create: `<스크래치>/pages-src/index.html` (작업용. `gh-pages` 브랜치 루트로 복사됨)

**Interfaces:**
- Consumes: Task 8의 `<스크래치>/webgl-build/`
- Produces: `https://n0wst4ndup.github.io/ocean-survival/` 및 `.../game/`

- [ ] **Step 1: 실패 확인**

```bash
git ls-remote --heads origin gh-pages
```

기대: 결과 없음 (브랜치가 아직 없다).

- [ ] **Step 2: 랜딩 페이지 작성**

`<스크래치>/pages-src/index.html` 로 저장한다.

```html
<!doctype html>
<html lang="ko">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>ShipSurivor — 브라우저에서 플레이</title>
<style>
  :root { color-scheme: light dark; --bg:#0d1117; --fg:#e6edf3; --muted:#8b949e; --line:#30363d; }
  * { box-sizing: border-box; }
  body { margin:0; padding:24px 16px 48px; background:var(--bg); color:var(--fg);
         font:16px/1.6 system-ui,-apple-system,"Segoe UI",sans-serif; }
  .wrap { max-width:960px; margin:0 auto; }
  h1 { font-size:1.75rem; margin:0 0 4px; }
  .sub { color:var(--muted); margin:0 0 24px; }
  .frame { width:100%; aspect-ratio:16/9; border:1px solid var(--line); border-radius:8px;
           overflow:hidden; background:#000; }
  .frame iframe { width:100%; height:100%; border:0; display:block; }
  table { border-collapse:collapse; margin:24px 0; }
  th, td { text-align:left; padding:6px 20px 6px 0; border-bottom:1px solid var(--line); }
  th { color:var(--muted); font-weight:600; }
  .note { color:var(--muted); font-size:0.9rem; }
  a { color:#58a6ff; }
</style>
</head>
<body>
<div class="wrap">
  <h1>ShipSurivor</h1>
  <p class="sub">Roguelite Bullet Heaven · Unity 6 URP ·
    <a href="https://github.com/N0WST4NDUP/ocean-survival">GitHub 저장소</a></p>

  <div class="frame">
    <iframe src="./game/index.html" title="ShipSurivor WebGL 빌드"
            allow="autoplay; fullscreen"></iframe>
  </div>

  <table>
    <tr><th>전진</th><td>W 홀드 — 뗄 때 감속</td></tr>
    <tr><th>브레이크</th><td>S / Left Ctrl</td></tr>
    <tr><th>선회</th><td>A · D / ← · →</td></tr>
    <tr><th>사격</th><td>자동</td></tr>
    <tr><th>일시정지</th><td>Esc</td></tr>
  </table>

  <p class="note">배는 관성으로 움직입니다. 제자리 회피가 아니라 궤적을 미리 그려 피하세요.</p>

  <p class="note">웹 데모는 게스트 모드로 동작합니다. 기록은 이 브라우저에만 저장되며,
    계정 로그인과 전체 유저 리더보드는 PC 빌드에서 지원합니다.</p>
</div>
</body>
</html>
```

조작은 GDD §6.4 기준이다. 실제 키가 다르면 `Assets/Scripts/Player/` 의 Input System 액션을 확인해 표와 GDD 양쪽을 맞출 것.

- [ ] **Step 3: 배포용 워크트리 생성**

`main` 워킹트리를 건드리지 않도록 격리한다.

```bash
git worktree add --detach <스크래치>/pages
cd <스크래치>/pages
git checkout --orphan gh-pages
git rm -rf .
```

`git rm -rf .` 은 **이 워크트리 안에서만** 동작한다. 실행 전 `pwd` 로 현재 위치가 `<스크래치>/pages` 인지 반드시 확인할 것.

- [ ] **Step 4: 산출물 배치**

```bash
cd <스크래치>/pages
cp <스크래치>/pages-src/index.html ./index.html
mkdir -p game
cp -r <스크래치>/webgl-build/* game/
touch .nojekyll
ls -a
```

`.nojekyll` 이 없으면 Jekyll이 밑줄로 시작하는 파일·폴더를 무시해 Unity 산출물 일부가 404가 난다.

- [ ] **Step 5: 푸시**

```bash
cd <스크래치>/pages
git add -A
git commit -m "Chore: WebGL 빌드 배포

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
git push -f origin gh-pages
```

- [ ] **Step 6: Pages 활성화 (최초 1회)**

```bash
gh api -X POST repos/N0WST4NDUP/ocean-survival/pages \
  -f 'source[branch]=gh-pages' -f 'source[path]=/'
```

또는 웹에서: `Settings > Pages > Source = Deploy from a branch, Branch = gh-pages / (root)`

- [ ] **Step 7: 배포 확인**

첫 배포는 반영에 1~3분 걸린다.

```bash
curl -sI https://n0wst4ndup.github.io/ocean-survival/ | head -1        # 200 기대
curl -sI https://n0wst4ndup.github.io/ocean-survival/game/ | head -1   # 200 기대
```

브라우저에서 `https://n0wst4ndup.github.io/ocean-survival/` 를 열어 확인한다.

1. iframe 안의 게임이 로딩 100%까지 간다
2. 키 입력에 반응한다
3. Console에 오류가 없다

- [ ] **Step 8: 워크트리 정리**

```bash
cd <프로젝트 루트>
git worktree remove <스크래치>/pages
git worktree list
```

---

### Task 10: README 작성 + LFS 예외

미디어는 아직 없다. 이미지 참조는 미리 넣되 이 태스크에서는 `main`에 머지하지 않는다.

**Files:**
- Create: `README.md`
- Modify: `.gitattributes`

**Interfaces:**
- Consumes: Task 9의 Pages URL
- Produces: `Docs/images/` 경로 규약 — Task 11이 여기에 파일을 채운다

- [ ] **Step 1: 실패 확인**

```bash
ls README.md 2>/dev/null || echo "README 없음 (기대한 상태)"
git check-attr filter -- Docs/images/hero.gif
```

기대: `README 없음`, 그리고 `Docs/images/hero.gif: filter: lfs`

- [ ] **Step 2: `.gitattributes` 에 LFS 예외 추가**

파일 맨 끝에 붙인다.

```gitattributes

# README 미디어는 LFS 대역폭(무료 1GB/월)을 소모하지 않도록 일반 git 객체로 저장한다.
# 초과 시 README 이미지가 통째로 깨지므로 이 예외가 필요하다.
/Docs/images/**         filter= diff= merge= -text
```

- [ ] **Step 3: 예외가 먹었는지 확인**

```bash
git check-attr filter -- Docs/images/hero.gif
```

기대: `Docs/images/hero.gif: filter: unspecified`

`filter: lfs` 가 그대로면 규칙 위치나 경로 패턴이 틀린 것이다.

- [ ] **Step 4: `README.md` 작성**

```markdown
<div align="center">

# ShipSurivor

**Roguelite Bullet Heaven** · Unity 6 URP · 1인 개발

[![Play in browser](Docs/images/play-banner.png)](https://n0wst4ndup.github.io/ocean-survival/)

![gameplay](Docs/images/hero.gif)

</div>

배틀쉽을 조종해 바다를 항해하며, 네임드를 처치해 떨어뜨린 부품으로 자기 배를 강화하는
자동 사격 로그라이트. 한 판 약 10분, 보스를 잡으면 클리어다.

Vampire Survivors의 자동 사격, Time Wasters의 컴포넌트 빌드, Battleship의 선박 조작감을 합쳤다.

## 조작

| 입력 | 동작 |
| --- | --- |
| `W` 홀드 | 전진 가속 — 뗄 때 감속 |
| `S` / `Left Ctrl` | 브레이크 |
| `A` `D` / `←` `→` | 선회 |
| `Esc` | 일시정지 |
| — | 사격은 자동 |

배는 관성으로 움직인다. 제자리 회피가 아니라 **궤적 예측**으로 살아남는 게임이다.

## 주요 시스템

### 드롭과 흡수

네임드 적은 컴포넌트를 하나 장착한 채 등장한다. 처치하면 그 부품이 부위 표기가 붙은 채
필드에 떨어지고, 30초 뒤 바다 밑으로 가라앉는다.

떨어진 부품은 플레이어만의 것이 아니다. **다른 네임드도 같은 규칙으로 흡수할 수 있고,
흡수한 네임드는 그 자리에서 강해진다.** 먼저 닿은 쪽이 가져가는 선착순이다.

필요 없는 부위의 네임드는 싸우지 않고 피할 수도 있다 — 위험 회피와 전리품 포기의 맞교환이다.

![드롭과 흡수](Docs/images/component.gif)

> **구현 현황**: 흡수 규칙 자체는 동작하지만, 네임드가 드롭을 *향해 이동하는* 추격 AI는
> 3주 개발 기간 안에 넣지 못했다. 현재 네임드는 무작위 배회(`NamedWander`) 중 우연히
> 드롭에 닿았을 때만 흡수한다. 설계 의도였던 "드롭 경쟁"은 여기까지다 (GDD §5.5).

### 슬롯 & 컴포넌트

주포(Main) / 부포(Sub) / 후방(Rear) 슬롯이 각각 1칸. 같은 부위를 다시 얻으면 레벨업하거나
데코레이터로 진화한다 (레벨 상한 3).

`Cannon` 에 `Double` 과 `Triple` 이 겹치면 1틱에 2 × 3 = **6발**이 나간다.

### 레벨업 3택

일반 몹이 떨구는 EXP 젬을 모아 레벨업하면 화면이 멈추고 카드 3장이 뜬다.
스탯 강화 카드와 컴포넌트 슬롯 카드가 같은 풀에서 나온다.

![레벨업 3택](Docs/images/levelup.gif)

### 보스 — Pirate Lord

180초가 지나면 등장한다. leash가 없어 따돌릴 수 없고, 플레이어(10 m/s)보다 빠른
14 m/s로 따라붙는다. 체력 구간에 따라 3페이즈로 전환된다.

패턴 상세는 [Docs/Boss/PirateLord.md](Docs/Boss/PirateLord.md) 참고.

![보스전](Docs/images/boss.gif)

### 기록과 리더보드

클리어 타임을 측정해 개인 베스트를 갱신하고, 전체 유저 랭킹에 반영한다.

<p>
  <img src="Docs/images/leaderboard.png" width="45%" alt="리더보드">
  <img src="Docs/images/result.png" width="45%" alt="결과 화면">
</p>

## 기술 스택

| 영역 | 사용 |
| --- | --- |
| 엔진 | Unity 6000.3.15f1, URP 17.3.0 |
| 입력 | Input System 1.19.0 |
| 카메라 | Cinemachine 3.1.6 |
| 적 AI | Unity Behavior 1.0.15 (비헤이비어 트리) + 자체 FSM |
| 비동기 | UniTask |
| 백엔드 | Firebase Auth + Realtime Database 13.16.0 |

## 웹 데모 안내

브라우저 데모는 게스트 모드로 동작한다. Firebase Unity SDK가 WebGL 네이티브
바이너리를 제공하지 않기 때문에, 웹에서는 기록이 브라우저 로컬에만 저장된다.
계정 로그인과 전체 유저 리더보드는 PC 빌드에서 동작한다.

## 빌드

Unity 6000.3.15f1 (WebGL 모듈 포함)이 필요하다.

```
1. 프로젝트를 Unity Hub로 연다
2. File > Build Profiles 에서 대상 플랫폼 선택
3. 씬 목록: Assets/Scenes/EntryPoint.unity, Assets/Scenes/InGame.unity
4. Build
```

WebGL로 빌드할 경우 `Player > WebGL > Publishing Settings` 에서
`Decompression Fallback` 을 켜야 정적 호스팅에서 로딩된다.

## 문서

| 문서 | 내용 |
| --- | --- |
| [Docs/GDD.md](Docs/GDD.md) | 게임 디자인 문서. 코어 루프 / 메커닉 / 마일스톤 |
| [Docs/Balancing.md](Docs/Balancing.md) | 밸런싱 수치와 근거 |
| [Docs/Boss/PirateLord.md](Docs/Boss/PirateLord.md) | 해적왕 보스 패턴 명세 |
| [CLAUDE.md](CLAUDE.md) | 개발 프로세스 / 브랜치 / 이슈 규약 |
```

위 본문은 GDD에서 도출했다. 근거: 피치·차별점 §0/§2, 조작 §6.4, 드롭 경쟁 §5.5,
슬롯 §6.1, 레벨업 §5.4, 보스 §5.6. 문구를 고칠 때는 해당 절을 다시 읽고 맞출 것
(CLAUDE.md §3: GDD가 SSoT).

GDD에 `❓` 로 표시된 미확정 값은 README에 쓰지 않는다.

**미구현 기능을 구현된 것처럼 쓰지 않는다.** GDD는 설계 의도와 구현 현황을 함께 담고 있어
그대로 옮기면 없는 기능을 광고하게 된다. 각 절의 "구현 현황", "추후", "v1.0" 표기를 확인할 것.
데모를 플레이한 사람이 바로 확인할 수 있는 항목이므로 과장은 즉시 드러난다.

알려진 예: 드롭 경쟁의 추격 AI는 미구현이다 (§5.5 — 네임드는 `NamedWander` 로 무작위
배회하며 우연 접촉 시에만 흡수). README는 이를 인용 블록으로 명시한다.

- [ ] **Step 5: 커밋**

```bash
git add README.md .gitattributes
git commit -m "Docs: README 작성 + Docs/images LFS 예외

미디어 파일은 아직 없다. LFS 예외를 먼저 넣어야 이후 추가되는
GIF가 LFS로 잡히지 않는다.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

### Task 11: 미디어 가공 + 머지

**Files:**
- Create: `Docs/images/hero.gif`, `component.gif`, `levelup.gif`, `boss.gif`, `play-banner.png`, `leaderboard.png`, `result.png`

**Interfaces:**
- Consumes: Task 10의 README 이미지 경로, 사용자가 지정한 타임스탬프
- Produces: 없음 (최종 산출물)

- [ ] **Step 1: 실패 확인**

```bash
ls Docs/images/ 2>/dev/null || echo "미디어 없음 (기대한 상태)"
```

GitHub에서 브랜치의 README를 열면 이미지 7개가 전부 깨져 보인다. 이것이 이 태스크의 시작 상태다.

- [ ] **Step 2: ffmpeg 설치**

```bash
winget install --id Gyan.FFmpeg -e
```

새 셸을 열고 확인한다.

```bash
ffmpeg -version | head -1
```

- [ ] **Step 3: 사용자에게 타임스탬프를 요청**

아래 표를 사용자에게 제시하고 빈칸을 채워받는다. 답이 오기 전까지 Step 4로 넘어가지 않는다.

| ID | 파일 | 담아야 할 내용 | 시작(mm:ss) | 길이(초) |
| --- | --- | --- | --- | --- |
| G1 | `hero.gif` | 다수의 적 한복판 전투 | | 6~8 |
| G2 | `component.gif` | 네임드 처치 → 부품 드롭 → 플레이어 흡수 → 화력 변화. 네임드가 우연히 먼저 먹는 장면이 잡혀 있으면 함께 넣으면 좋다 (추격 AI 미구현이므로 연출된 경쟁 장면은 만들지 말 것) | | 5~6 |
| G3 | `levelup.gif` | 레벨업 카드 3장 중 선택 | | 4~5 |
| G4 | `boss.gif` | Pirate Lord 페이즈 전환 | | 6~8 |
| S1 | `play-banner.png` | 배너용 인상적인 한 프레임 | | — |
| S2 | `leaderboard.png` | 리더보드 화면 | | — |
| S3 | `result.png` | 결과 화면 (점수 count-up 후) | | — |

원본 영상 경로도 함께 받는다.

- [ ] **Step 4: GIF 생성**

각 GIF마다 실행한다. `<시작>` 은 `mm:ss`, `<길이>` 는 초.

```bash
mkdir -p Docs/images
ffmpeg -ss <시작> -t <길이> -i "<원본.mp4>" \
  -vf "fps=12,scale=640:-1:flags=lanczos,split[s0][s1];[s0]palettegen=max_colors=128[p];[s1][p]paletteuse=dither=bayer:bayer_scale=3" \
  -loop 0 "Docs/images/<이름>.gif"
```

- [ ] **Step 5: 스크린샷 생성**

```bash
ffmpeg -ss <시각> -i "<원본.mp4>" -frames:v 1 "Docs/images/<이름>.png"
```

- [ ] **Step 6: 용량 확인**

```bash
du -b Docs/images/* | sort -n
du -ch Docs/images/ | tail -1
```

목표: `hero.gif` ≤ 2.5MB, `component.gif` ≤ 2MB, `levelup.gif` ≤ 1.5MB, `boss.gif` ≤ 2.5MB,
PNG 각 ≤ 300KB, **합계 10MB 이하**.

초과 시 순서대로 조정하고 Step 4를 다시 실행한다: `fps=12` → `fps=10`, `scale=640` → `scale=560`, 길이 단축.

세 단계를 다 해도 목표를 못 맞추는 슬롯이 있으면 spec §9.1의 대안을 쓴다: 해당 슬롯만
mp4로 만들어 GitHub 이슈 코멘트 작성 창에 드래그해 올리고, 거기서 얻은
`https://github.com/user-attachments/assets/...` URL을 README에 넣는다. GitHub이 인라인
플레이어로 렌더링한다. **단 자동재생되지 않고 클릭이 필요하므로 `hero.gif`(G1)에는 쓰지 않는다.**

- [ ] **Step 7: LFS를 타지 않았는지 확인**

```bash
git add Docs/images/
git diff --cached --stat
git lfs status
```

`git lfs status` 의 스테이징 목록에 `Docs/images/` 파일이 **나오지 않아야** 한다. 나온다면 Task 10 Step 2의 예외가 적용되지 않은 것이다.

- [ ] **Step 8: 커밋 및 푸시**

```bash
git commit -m "Docs: README 미디어 추가 (GIF 4, 스크린샷 3)

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
git push -u origin docs/readme-webgl-deploy
```

- [ ] **Step 9: README 렌더링 확인**

GitHub에서 `docs/readme-webgl-deploy` 브랜치의 README를 연다.

1. 이미지 7개가 모두 표시된다
2. `▶ PLAY` 배너를 누르면 `https://n0wst4ndup.github.io/ocean-survival/` 로 이동한다
3. 문서 링크 4개가 모두 열린다

- [ ] **Step 10: PR 생성 및 머지**

```bash
gh pr create --base main --head docs/readme-webgl-deploy \
  --title "Docs: README 작성 + WebGL 빌드 GitHub Pages 배포" \
  --body "$(cat <<'BODY'
## 변경 사항
- Firebase 매니저 5개에 `#if UNITY_WEBGL` 로컬 폴백 분기 추가
- 공용 로컬 저장 계층 `LocalStore` 추가
- `AuthManager.CurrentUserId` 래퍼 도입 — UI에서 Firebase 타입 제거
- WebGL 빌드 설정: Decompression Fallback 활성화
- `gh-pages` 브랜치에 WebGL 빌드 + 랜딩 페이지 배포
- README 작성, `Docs/images/` LFS 예외

## 관련 이슈
없음 (조직 이관으로 이슈 트래커가 초기화된 상태)

## 체크리스트
- [ ] GDD와 충돌하지 않음
- [ ] OUT OF SCOPE 위반 없음
- [ ] 풀링 필요 객체는 풀링 적용 (해당 없음)
- [ ] 로컬에서 60fps 유지 확인 (해당 없음)

## 씬 / 스크린샷
`Assets/Scenes/EntryPoint.unity`
플레이: https://n0wst4ndup.github.io/ocean-survival/

## 비고
- Firebase Unity SDK는 WebGL 네이티브 바이너리를 제공하지 않는다. 웹 데모는 게스트 모드로 동작하며 기록은 브라우저 로컬에만 저장된다.
- 설계 문서: `Docs/superpowers/specs/2026-09-08-readme-webgl-deploy-design.md`

🤖 Generated with [Claude Code](https://claude.com/claude-code)
BODY
)"
```

---

## 남은 이슈 (이 계획 밖)

- 이 저장소에는 이슈 트래커·Projects 보드·라벨·마일스톤이 없다 (조직 이관 시 따라오지 않음). CLAUDE.md §5의 이슈 메타데이터 규약을 계속 쓰려면 라벨·마일스톤·보드를 새로 만들어야 한다. 별도 작업으로 다룬다.
- `oldorigin` remote는 조직 삭제 후 404가 된다. `git remote remove oldorigin` 으로 정리한다.
- `d:/01_Application/00_Personal/_backup-ocean-survival/` 백업 폴더는 조직 삭제를 확인한 뒤 지운다.
