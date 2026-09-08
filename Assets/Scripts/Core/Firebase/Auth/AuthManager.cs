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

    // --- Singleton ------------------------------
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

    private void OnDestroy()
    {
        if (_instance == this)
        {
            _instance = null;
        }
    }

    // --- Auth ------------------------------
    public bool IsInitialized { get; private set; } = false;

    // 웹 데모는 항상 게스트로 로그인된 상태로 취급한다.
    public bool IsLoggedIn => true;

    public string CurrentUserId => LocalStore.UserId;
    public string CurrentDisplayName => LocalStore.Nickname;

    public event Action<bool> LoginStateChanged;

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
}

#else

using System;
using Cysharp.Threading.Tasks;
using Firebase.Auth;
using UnityEngine;

public class AuthManager : MonoBehaviour
{
    // --- Singleton ------------------------------
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

    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
            DontDestroyOnLoad(gameObject);

            Debug.Log("[Auth] AuthManager 싱글톤 생성.");
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

        if (Auth != null)
        {
            Auth.StateChanged -= OnAuthStateChanged;
        }
    }

    // --- Auth ------------------------------
    public FirebaseAuth Auth { get; private set; }
    public FirebaseUser CurrentUser => Auth.CurrentUser;

    // Firebase 타입이 UI로 새어나가지 않게 하는 얇은 래퍼.
    // WebGL 폴백에서도 같은 이름으로 제공되므로 호출부가 플랫폼을 몰라도 된다.
    public string CurrentUserId => Auth?.CurrentUser?.UserId;
    public string CurrentDisplayName => Auth?.CurrentUser?.DisplayName;

    private bool _lastNotifiedSignedIn = false;
    public bool IsInitialized { get; private set; } = false;
    public bool IsLoggedIn => CurrentUser != null;

    public event Action<bool> LoginStateChanged;

    private async UniTaskVoid Start()
    {
        bool ready = await FirebaseInitializer.Instance.WaitForInitializationAsync();
        if (!ready)
        {
            Debug.LogError("[Auth] 파이어 베이스 초기화 실패 Auth 초기화 불가...");
            return;
        }

        Auth = FirebaseInitializer.Instance.Auth;
        Auth.StateChanged += OnAuthStateChanged;

        IsInitialized = true;

        NotifyLoginState();
    }

    private void OnAuthStateChanged(object sender, EventArgs eventArgs)
    {
        NotifyLoginState();
    }

    private void NotifyLoginState()
    {
        bool signedIn = IsLoggedIn;
        if (signedIn == _lastNotifiedSignedIn) return;

        _lastNotifiedSignedIn = signedIn;
        Debug.Log(signedIn ? $"[Auth] 로그인 상태: {CurrentUser.UserId}" : "[Auth] 로그아웃 상태");
        LoginStateChanged?.Invoke(signedIn);
    }

    public async UniTask<bool> WaitForInitializationAsync()
    {
        if (IsInitialized) return true;

        bool firebaseReady = await FirebaseInitializer.Instance.WaitForInitializationAsync();
        if (!firebaseReady) return false;

        await UniTask.WaitUntil(() => IsInitialized);
        return true;
    }

    public async UniTask<(bool success, string error)> SignInAnonymouslyAsync()
    {
        if (!IsInitialized)
        {
            bool ready = await FirebaseInitializer.Instance.WaitForInitializationAsync();
            if (!ready) return (false, "초기화가 완료되지 않았습니다.");
        }

        try
        {
            Debug.Log("[Auth] 익명 로그인 시도...");
            await Auth.SignInAnonymouslyAsync();

            NotifyLoginState();

            Debug.Log($"[Auth] 익명 로그인 성공: {CurrentUser.UserId}");
            return (true, null);
        }
        catch (Exception ex)
        {
            Debug.Log($"[Auth] 익명 로그인 실패: {ex.Message}");
            return (false, ParseFirebaseError(ex.Message));
        }
    }

    public async UniTask<(bool success, string error)> CreateUserWithEmailAsync(string email, string passwd, string nickname = "Anonymous")
    {
        if (!IsInitialized)
        {
            bool ready = await FirebaseInitializer.Instance.WaitForInitializationAsync();
            if (!ready) return (false, "초기화가 완료되지 않았습니다.");
        }

        try
        {
            Debug.Log("[Auth] 회원 가입 시도...");
            await Auth.CreateUserWithEmailAndPasswordAsync(email, passwd);

            await CurrentUser.UpdateUserProfileAsync(new UserProfile { DisplayName = nickname });

            NotifyLoginState();

            Debug.Log($"[Auth] 회원 가입 성공: {CurrentUser.UserId} ({CurrentUser.DisplayName})");
            return (true, null);
        }
        catch (Exception ex)
        {
            Debug.Log($"[Auth] 회원 가입 실패: {ex.Message}");
            return (false, ParseFirebaseError(ex.Message));
        }
    }

    public async UniTask<(bool success, string error)> SignInUserWithEmailAsync(string email, string passwd)
    {
        if (!IsInitialized)
        {
            bool ready = await FirebaseInitializer.Instance.WaitForInitializationAsync();
            if (!ready) return (false, "초기화가 완료되지 않았습니다.");
        }

        try
        {
            Debug.Log("[Auth] 로그인 시도...");
            await Auth.SignInWithEmailAndPasswordAsync(email, passwd);

            NotifyLoginState();

            Debug.Log($"[Auth] 로그인 성공: {CurrentUser.UserId}");
            return (true, null);
        }
        catch (Exception ex)
        {
            Debug.Log($"[Auth] 로그인 실패: {ex.Message}");
            return (false, ParseFirebaseError(ex.Message));
        }
    }

    public async UniTask<(bool success, string error)> LinkWithEmailAsync(string email, string passwd)
    {
        if (!IsInitialized)
        {
            bool ready = await FirebaseInitializer.Instance.WaitForInitializationAsync();
            if (!ready) return (false, "초기화가 완료되지 않았습니다.");
        }

        if (CurrentUser == null) return (false, "로그인된 사용자가 없습니다.");
        if (!CurrentUser.IsAnonymous) return (false, "이미 이메일 계정에 연동된 사용자입니다.");

        try
        {
            Debug.Log("[Auth] 익명 → 이메일 계정 연동 시도...");
            Credential credential = EmailAuthProvider.GetCredential(email, passwd);
            await CurrentUser.LinkWithCredentialAsync(credential);

            NotifyLoginState();

            Debug.Log($"[Auth] 계정 연동 성공: {CurrentUser.UserId}");
            return (true, null);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[Auth] 계정 연동 실패: {ex.Message}");
            return (false, ParseFirebaseError(ex.Message));
        }
    }

    public void SignOut()
    {
        if (Auth != null && CurrentUser != null)
        {
            Debug.Log("[Auth] 로그아웃");
            Auth.SignOut();

            NotifyLoginState();
        }
    }

    private string ParseFirebaseError(string error)
    {
        Debug.LogWarning($"[Auth] Firebase 에러 원문: {error}");

        string lower = error.ToLowerInvariant();

        if (lower.Contains("already in use") || lower.Contains("email-already"))
        {
            return "이미 사용 중인 이메일입니다.";
        }
        if (lower.Contains("at least 6") || lower.Contains("weak") || lower.Contains("password is invalid"))
        {
            return "비밀번호는 6자 이상이어야 합니다.";
        }
        if (lower.Contains("badly formatted") || lower.Contains("invalid-email"))
        {
            return "이메일 형식이 올바르지 않습니다.";
        }
        if (lower.Contains("network"))
        {
            return "네트워크 연결을 확인해주세요.";
        }

        return "이메일 또는 비밀번호를 확인해주세요.";
    }
}

#endif
