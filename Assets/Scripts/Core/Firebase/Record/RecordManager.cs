#if UNITY_WEBGL

using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

// WebGL 폴백: 클리어 타임을 LocalStore(PlayerPrefs)에 저장한다.
// best는 최소값(빠를수록 상위), history는 최신순 LocalStore.HistoryCapacity개.
public class RecordManager : MonoBehaviour
{
    // --- Singleton ------------------------------
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

    private void OnDestroy()
    {
        if (_instance == this)
        {
            _instance = null;
        }
    }

    // --- Record ------------------------------
    private long _cachedBestMs = LocalStore.NoRecord;
    public long CachedBestMs => _cachedBestMs;
    public bool HasBest => _cachedBestMs >= 0;

    public bool IsInitialized { get; private set; } = false;

    public UniTask<bool> WaitForInitializationAsync()
    {
        return UniTask.FromResult(true);
    }

    // 클리어 기록 저장: history 누적 + 더 빠르면 best 갱신.
    public UniTask<(bool success, bool isNewBest, long bestMs)> SaveClearTimeAsync(float clearTimeSeconds)
    {
        long ms = (long)Math.Round(clearTimeSeconds * 1000.0);

        LocalStore.AppendHistory(ms);

        bool isNewBest = !HasBest || ms < _cachedBestMs;
        if (isNewBest)
        {
            _cachedBestMs = ms;
            LocalStore.SaveBestMs(ms);

            // Firebase 경로와 동일하게 신기록을 리더보드에도 알린다.
            // 로컬 리더보드는 히스토리에서 파생되므로 실제로는 갱신 통지만 한다.
            LeaderboardManager.Instance.SaveToLeaderboardAsync(ms).Forget();
        }

        Debug.Log($"[Record] WebGL 로컬 저장 (신기록: {isNewBest}, best: {_cachedBestMs}ms)");
        return UniTask.FromResult((true, isNewBest, _cachedBestMs));
    }

    // 베스트(최소 ms) 로드. 기록 없으면 -1.
    public UniTask<long> LoadBestMsAsync()
    {
        _cachedBestMs = LocalStore.LoadBestMs();
        return UniTask.FromResult(_cachedBestMs);
    }

    // 최근 클리어 기록 N개 (최신순).
    public UniTask<List<ClearTimeRecord>> LoadHistoryAsync(int limit = 10)
    {
        List<ClearTimeRecord> all = LocalStore.LoadHistory();
        if (all.Count > limit) all.RemoveRange(limit, all.Count - limit);
        return UniTask.FromResult(all);
    }
}

#else

using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Firebase.Database;
using UnityEngine;

// 클리어 타임 기록을 RTDB에 저장·조회.
//   records/{uid}/best            = 최소 클리어 타임(ms, 낮을수록 빠름)
//   records/{uid}/history/{push}  = { clearTimeMs, timestamp }
public class RecordManager : MonoBehaviour
{
    // --- Singleton ------------------------------
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

    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
            DontDestroyOnLoad(gameObject);

            Debug.Log("[Record] RecordManager 싱글톤 생성.");
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    private void OnDestroy()
    {
        if (_instance == this)
        {
            _instance = null;
        }
    }

    // --- Record ------------------------------
    private DatabaseReference _recordsRef;

    // 캐시된 최고(최소) 클리어 타임(ms). 기록 없으면 -1.
    private long _cachedBestMs = -1;
    public long CachedBestMs => _cachedBestMs;
    public bool HasBest => _cachedBestMs >= 0;

    public bool IsInitialized { get; private set; } = false;

    private async UniTaskVoid Start()
    {
        bool ready = await FirebaseInitializer.Instance.WaitForInitializationAsync();
        if (!ready)
        {
            Debug.LogError("[Record] Firebase 초기화 실패 → Record 초기화 불가...");
            return;
        }

        _recordsRef = FirebaseInitializer.Instance.Database.RootReference.Child("records");

        IsInitialized = true;
        Debug.Log("[Record] 초기화 완료.");

        // 로그인 상태면 베스트 미리 로드
        if (await AuthManager.Instance.WaitForInitializationAsync() && AuthManager.Instance.IsLoggedIn)
        {
            await LoadBestMsAsync();
        }
    }

    public async UniTask<bool> WaitForInitializationAsync()
    {
        if (IsInitialized) return true;

        bool firebaseReady = await FirebaseInitializer.Instance.WaitForInitializationAsync();
        if (!firebaseReady) return false;

        await UniTask.WaitUntil(() => IsInitialized);
        return true;
    }

    // 클리어 기록 저장: history 누적 + 더 빠르면 best 갱신.
    public async UniTask<(bool success, bool isNewBest, long bestMs)> SaveClearTimeAsync(float clearTimeSeconds)
    {
        if (!await EnsureReadyAsync()) return (false, false, _cachedBestMs);
        if (!AuthManager.Instance.IsLoggedIn) return (false, false, _cachedBestMs);

        long ms = (long)System.Math.Round(clearTimeSeconds * 1000.0);
        string userId = AuthManager.Instance.CurrentUser.UserId;

        try
        {
            Debug.Log($"[Record] 클리어 기록 저장 시도: {ms}ms");

            // 히스토리 누적 (서버 타임스탬프)
            var historyRef = _recordsRef.Child(userId).Child("history").Push();
            await historyRef.UpdateChildrenAsync(new Dictionary<string, object>
            {
                { "clearTimeMs", ms },
                { "timestamp", ServerValue.Timestamp },
            });

            // 최고(최소) 기록 갱신
            bool isNewBest = !HasBest || ms < _cachedBestMs;
            if (isNewBest)
            {
                await _recordsRef.Child(userId).Child("best").SetValueAsync(ms);
                _cachedBestMs = ms;

                // 신기록을 전체 리더보드에도 반영
                await LeaderboardManager.Instance.SaveToLeaderboardAsync(ms);
            }

            Debug.Log($"[Record] 저장 성공 (신기록: {isNewBest}, best: {_cachedBestMs}ms)");
            return (true, isNewBest, _cachedBestMs);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[Record] 저장 실패: {ex.Message}");
            return (false, false, _cachedBestMs);
        }
    }

    // 베스트(최소 ms) 로드. 기록 없으면 -1.
    public async UniTask<long> LoadBestMsAsync()
    {
        if (!await EnsureReadyAsync()) return -1;
        if (!AuthManager.Instance.IsLoggedIn) return -1;

        string userId = AuthManager.Instance.CurrentUser.UserId;

        try
        {
            var snapshot = await _recordsRef.Child(userId).Child("best").GetValueAsync();
            _cachedBestMs = snapshot.Exists ? Convert.ToInt64(snapshot.Value) : -1;

            Debug.Log($"[Record] 베스트 로드: {_cachedBestMs}ms");
            return _cachedBestMs;
        }
        catch (Exception ex)
        {
            // 내부 예외까지 출력 — "Permission denied"(규칙) / DB URL·지역 문제 등 실제 원인 확인용
            Debug.LogError($"[Record] 베스트 로드 실패: {ex.Message} | inner: {ex.InnerException?.Message}\n{ex}");
            return -1;
        }
    }

    // 최근 클리어 기록 N개 (최신순).
    public async UniTask<List<ClearTimeRecord>> LoadHistoryAsync(int limit = 10)
    {
        if (!await EnsureReadyAsync() || !AuthManager.Instance.IsLoggedIn) return new();

        string userId = AuthManager.Instance.CurrentUser.UserId;

        try
        {
            var query = _recordsRef.Child(userId).Child("history").OrderByChild("timestamp").LimitToLast(limit);
            var snapshot = await query.GetValueAsync();

            var list = new List<ClearTimeRecord>();
            if (snapshot.Exists)
            {
                foreach (var child in snapshot.Children)
                {
                    list.Add(ClearTimeRecord.FromJson(child.GetRawJsonValue()));
                }
                list.Reverse();
            }

            Debug.Log($"[Record] 히스토리 로드: {list.Count}개");
            return list;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[Record] 히스토리 로드 실패: {ex.Message}");
            return new();
        }
    }

    // Record(_recordsRef) + Auth(CurrentUser) 양쪽 준비 보장 — IsLoggedIn 접근 전 NRE 방지.
    private async UniTask<bool> EnsureReadyAsync()
    {
        bool recordReady = await WaitForInitializationAsync();
        bool authReady = await AuthManager.Instance.WaitForInitializationAsync();
        return recordReady && authReady;
    }
}

#endif
