#if UNITY_WEBGL

using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

// WebGL 폴백: 이 기기의 로컬 기록만으로 리더보드를 구성한다.
// 전체 유저 랭킹이 아니라는 점은 랜딩 페이지 안내가 담당한다.
public class LeaderboardManager : MonoBehaviour
{
    // 로컬 리더보드에 표시할 최대 항목 수 (spec §5.2).
    private const int LocalLeaderboardLimit = 10;

    // --- Singleton ------------------------------
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

    private void OnDestroy()
    {
        StopRealtimeListener();
        if (_instance == this)
        {
            _instance = null;
        }
    }

    // --- Leaderboard ------------------------------
    private bool _isListenerActive;

    public bool IsInitialized { get; private set; } = false;

    // 갱신 콜백 (UI가 구독). 메인스레드에서 호출됨.
    public event Action<List<LeaderboardEntry>> OnLeaderboardUpdated;

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

    // 가장 빠른 N개 1회 조회 (오름차순).
    public UniTask<List<LeaderboardEntry>> LoadLeaderboardAsync(int limit = 10)
    {
        return UniTask.FromResult(LocalStore.LoadLeaderboard(limit));
    }

    // --- 실시간 동기화 ------------------------------
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
        // 호출 직후 구독하는 UI가 놓치지 않도록 한 프레임 미룬다.
        await UniTask.Yield();
        OnLeaderboardUpdated?.Invoke(LocalStore.LoadLeaderboard(limit));
    }

    public void StopRealtimeListener()
    {
        _isListenerActive = false;
    }
}

#else

using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Firebase.Database;
using UnityEngine;

// 전체 유저 클리어 타임 랭킹. leaderboard/{uid} = { userId, nickname, clearTimeMs, timestamp }.
// clearTimeMs 가 낮을수록 상위(빠른 클리어). 유저당 1개(본인 best)만 유지.
public class LeaderboardManager : MonoBehaviour
{
    // --- Singleton ------------------------------
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

    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
            DontDestroyOnLoad(gameObject);

            Debug.Log("[Leaderboard] LeaderboardManager 싱글톤 생성.");
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    private void OnDestroy()
    {
        StopRealtimeListener();
        if (_instance == this)
        {
            _instance = null;
        }
    }

    // --- Leaderboard ------------------------------
    private DatabaseReference _leaderboardRef;
    private Query _listenerQuery;
    private bool _isListenerActive;

    public bool IsInitialized { get; private set; } = false;

    // 실시간 갱신 콜백 (UI가 구독). 메인스레드에서 호출됨.
    public event Action<List<LeaderboardEntry>> OnLeaderboardUpdated;

    private async UniTaskVoid Start()
    {
        bool ready = await FirebaseInitializer.Instance.WaitForInitializationAsync();
        if (!ready)
        {
            Debug.LogError("[Leaderboard] Firebase 초기화 실패 → Leaderboard 초기화 불가...");
            return;
        }

        _leaderboardRef = FirebaseInitializer.Instance.Database.RootReference.Child("leaderboard");

        IsInitialized = true;
        Debug.Log("[Leaderboard] 초기화 완료.");
    }

    public async UniTask<bool> WaitForInitializationAsync()
    {
        if (IsInitialized) return true;

        bool firebaseReady = await FirebaseInitializer.Instance.WaitForInitializationAsync();
        if (!firebaseReady) return false;

        await UniTask.WaitUntil(() => IsInitialized);
        return true;
    }

    // 본인 best 클리어 타임을 리더보드에 반영 (RecordManager가 신기록 시 호출).
    public async UniTask<(bool success, string error)> SaveToLeaderboardAsync(long clearTimeMs)
    {
        if (!await WaitForInitializationAsync()) return (false, "초기화에 실패했습니다.");
        if (!AuthManager.Instance.IsLoggedIn) return (false, "로그인이 필요합니다.");

        string userId = AuthManager.Instance.CurrentUser.UserId;
        string nickname = ResolveNickname();

        try
        {
            var entry = new Dictionary<string, object>
            {
                { "userId", userId },
                { "nickname", nickname },
                { "clearTimeMs", clearTimeMs },
                { "timestamp", ServerValue.Timestamp },
            };
            await _leaderboardRef.Child(userId).UpdateChildrenAsync(entry);

            Debug.Log($"[Leaderboard] 리더보드 반영: {nickname} {clearTimeMs}ms");
            return (true, null);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[Leaderboard] 저장 실패: {ex.Message}");
            return (false, ex.Message);
        }
    }

    // 가장 빠른 N개 1회 조회 (오름차순).
    public async UniTask<List<LeaderboardEntry>> LoadLeaderboardAsync(int limit = 10)
    {
        if (!await WaitForInitializationAsync()) return new();

        try
        {
            var query = _leaderboardRef.OrderByChild("clearTimeMs").LimitToFirst(limit);
            var snapshot = await query.GetValueAsync();
            return ParseEntries(snapshot);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[Leaderboard] 불러오기 실패: {ex.Message}");
            return new();
        }
    }

    // --- 실시간 동기화 ------------------------------
    // UI에서 호출 전 await WaitForInitializationAsync() 로 준비를 보장할 것.
    public void StartRealtimeListener(int limit = 10)
    {
        if (_isListenerActive || _leaderboardRef == null) return;

        _listenerQuery = _leaderboardRef.OrderByChild("clearTimeMs").LimitToFirst(limit);
        _listenerQuery.ValueChanged += OnValueChanged;
        _isListenerActive = true;
        Debug.Log("[Leaderboard] 실시간 리스너 시작");
    }

    public void StopRealtimeListener()
    {
        if (_isListenerActive && _listenerQuery != null)
        {
            _listenerQuery.ValueChanged -= OnValueChanged;
            _listenerQuery = null;
            _isListenerActive = false;
            Debug.Log("[Leaderboard] 실시간 리스너 중지");
        }
    }

    private void OnValueChanged(object sender, ValueChangedEventArgs args)
    {
        if (args.DatabaseError != null)
        {
            Debug.LogError($"[Leaderboard] 리스너 오류: {args.DatabaseError.Message}");
            return;
        }

        var leaderboard = ParseEntries(args.Snapshot);
        DispatchUpdateAsync(leaderboard).Forget();
    }

    // ValueChanged가 백그라운드 스레드에서 올 수 있으므로 메인스레드로 전환 후 이벤트 발행.
    private async UniTaskVoid DispatchUpdateAsync(List<LeaderboardEntry> leaderboard)
    {
        await UniTask.SwitchToMainThread();
        OnLeaderboardUpdated?.Invoke(leaderboard);
    }

    private List<LeaderboardEntry> ParseEntries(DataSnapshot snapshot)
    {
        var list = new List<LeaderboardEntry>();
        if (snapshot != null && snapshot.Exists)
        {
            foreach (var child in snapshot.Children)
            {
                list.Add(LeaderboardEntry.FromJson(child.GetRawJsonValue()));
            }
        }

        // 빠른 순(오름차순) 정렬 — 쿼리 순서가 흐트러질 경우 대비.
        list.Sort((a, b) => a.clearTimeMs.CompareTo(b.clearTimeMs));
        return list;
    }

    // 리더보드 표시용 닉네임: 프로필 > Auth DisplayName > "익명".
    private string ResolveNickname()
    {
        var profile = ProfileManager.Instance.CachedProfile;
        if (profile != null && !string.IsNullOrEmpty(profile.nickname)) return profile.nickname;

        string displayName = AuthManager.Instance.CurrentUser?.DisplayName;
        return string.IsNullOrEmpty(displayName) ? "익명" : displayName;
    }
}

#endif
