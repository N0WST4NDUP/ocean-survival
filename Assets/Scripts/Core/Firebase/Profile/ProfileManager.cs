#if UNITY_WEBGL

using Cysharp.Threading.Tasks;
using UnityEngine;

// WebGL 폴백: 프로필을 LocalStore(PlayerPrefs)에 저장한다.
public class ProfileManager : MonoBehaviour
{
    // --- Singleton ------------------------------
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

    private void OnDestroy()
    {
        if (_instance == this)
        {
            _instance = null;
        }
    }

    // --- Profile ------------------------------
    private UserProfileData _cachedProfile;
    public UserProfileData CachedProfile => _cachedProfile;

    public bool IsInitialized { get; private set; } = false;

    // UserProfileData 생성자가 createdAt을 현재 시각으로 채우므로,
    // 매번 갱신되지 않도록 LocalStore에 보관된 값으로 덮어쓴다.
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
}

#else

using System;
using Cysharp.Threading.Tasks;
using Firebase.Database;
using UnityEngine;

// 유저 프로필(닉네임/이메일/가입시각)을 RTDB users/{uid} 에 저장·조회하는 매니저.
public class ProfileManager : MonoBehaviour
{
    // --- Singleton ------------------------------
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

    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
            DontDestroyOnLoad(gameObject);

            Debug.Log("[Profile] ProfileManager 싱글톤 생성.");
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

    // --- Profile ------------------------------
    private DatabaseReference _usersRef;

    private UserProfileData _cachedProfile;
    public UserProfileData CachedProfile => _cachedProfile;

    public bool IsInitialized { get; private set; } = false;

    private async UniTaskVoid Start()
    {
        bool ready = await FirebaseInitializer.Instance.WaitForInitializationAsync();
        if (!ready)
        {
            Debug.LogError("[Profile] Firebase 초기화 실패 → Profile 초기화 불가...");
            return;
        }

        _usersRef = FirebaseInitializer.Instance.Database.RootReference.Child("users");

        IsInitialized = true;
        Debug.Log("[Profile] 초기화 완료.");

        // 세션 복원 등으로 이미 로그인 상태면 프로필을 미리 캐싱
        if (await AuthManager.Instance.WaitForInitializationAsync() && AuthManager.Instance.IsLoggedIn)
        {
            await LoadProfileAsync();
        }
    }

    // 외부 매니저(리더보드/히스토리 등)가 사용 전 초기화 완료를 기다린다.
    public async UniTask<bool> WaitForInitializationAsync()
    {
        if (IsInitialized) return true;

        bool firebaseReady = await FirebaseInitializer.Instance.WaitForInitializationAsync();
        if (!firebaseReady) return false;

        await UniTask.WaitUntil(() => IsInitialized);
        return true;
    }

    // 신규 프로필 생성/덮어쓰기 (가입 직후 호출). createdAt이 갱신되므로 수정엔 UpdateNicknameAsync 사용.
    public async UniTask<(bool success, string error)> SaveProfileAsync(string nickname)
    {
        if (!await EnsureReadyAsync()) return (false, "초기화에 실패했습니다.");
        if (!AuthManager.Instance.IsLoggedIn) return (false, "로그인이 필요합니다.");

        string userId = AuthManager.Instance.CurrentUser.UserId;
        string email = AuthManager.Instance.CurrentUser.Email;
        if (string.IsNullOrEmpty(email)) email = "익명";

        try
        {
            Debug.Log("[Profile] 프로필 저장 시도.");

            var profile = new UserProfileData(nickname, email);
            await _usersRef.Child(userId).SetRawJsonValueAsync(profile.ToJson());
            _cachedProfile = profile;

            Debug.Log("[Profile] 프로필 저장 성공.");
            return (true, null);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[Profile] 프로필 저장 실패: {ex.Message}");
            return (false, ex.Message);
        }
    }

    public async UniTask<(UserProfileData profile, string error)> LoadProfileAsync()
    {
        if (!await EnsureReadyAsync()) return (null, "초기화에 실패했습니다.");
        if (!AuthManager.Instance.IsLoggedIn) return (null, "로그인이 필요합니다.");

        string userId = AuthManager.Instance.CurrentUser.UserId;

        try
        {
            Debug.Log("[Profile] 프로필 불러오기 시도.");

            var snapshot = await _usersRef.Child(userId).GetValueAsync();
            if (!snapshot.Exists)
            {
                Debug.Log("[Profile] 프로필 없음.");
                _cachedProfile = null;
                return (null, "프로필이 존재하지 않습니다.");
            }

            var profile = UserProfileData.FromJson(snapshot.GetRawJsonValue());
            _cachedProfile = profile;

            Debug.Log($"[Profile] 프로필 불러오기 성공: {profile.nickname}");
            return (profile, null);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[Profile] 프로필 불러오기 실패: {ex.Message}");
            return (null, ex.Message);
        }
    }

    public async UniTask<(bool success, string error)> UpdateNicknameAsync(string nickname)
    {
        if (!await EnsureReadyAsync()) return (false, "초기화에 실패했습니다.");
        if (!AuthManager.Instance.IsLoggedIn) return (false, "로그인이 필요합니다.");

        string userId = AuthManager.Instance.CurrentUser.UserId;

        try
        {
            Debug.Log("[Profile] 닉네임 수정 시도.");

            await _usersRef.Child(userId).Child("nickname").SetValueAsync(nickname);
            if (_cachedProfile != null) _cachedProfile.nickname = nickname;

            Debug.Log("[Profile] 닉네임 수정 성공.");
            return (true, null);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[Profile] 닉네임 수정 실패: {ex.Message}");
            return (false, ex.Message);
        }
    }

    // Profile(_usersRef) + Auth(CurrentUser) 양쪽 준비 완료 보장 — IsLoggedIn 접근 전 NRE 방지.
    private async UniTask<bool> EnsureReadyAsync()
    {
        bool profileReady = await WaitForInitializationAsync();
        bool authReady = await AuthManager.Instance.WaitForInitializationAsync();
        return profileReady && authReady;
    }
}

#endif
