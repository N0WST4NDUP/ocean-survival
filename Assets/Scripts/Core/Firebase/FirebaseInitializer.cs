#if UNITY_WEBGL

using Cysharp.Threading.Tasks;
using UnityEngine;

// WebGL 폴백: Firebase Unity SDK는 WebGL 네이티브 바이너리를 제공하지 않으므로
// (Assets/Firebase/Plugins/*.dll.meta 가 Web: enabled 0) 초기화를 즉시 성공 처리하고,
// 실제 데이터는 LocalStore가 담당한다.
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

using Cysharp.Threading.Tasks;
using Firebase;
using Firebase.Auth;
using Firebase.Database;
using UnityEngine;

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

    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
            DontDestroyOnLoad(gameObject);

            Debug.Log("[Firebase] Initializer 싱글톤 생성.");
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        InitializeFirebaseAsync().Forget();
    }

    public enum InitState
    {
        Pending,
        Ready,
        Failed,
    }

    public InitState State { get; private set; } = InitState.Pending;
    public bool IsReady => State == InitState.Ready;
    public string LastError { get; private set; }

    public FirebaseApp App { get; private set; }
    public FirebaseDatabase Database { get; private set; }
    public FirebaseAuth Auth { get; private set; }

    private async UniTaskVoid InitializeFirebaseAsync()
    {
        Debug.Log("[Firebase] 초기화 시작...");

        try
        {
            DependencyStatus status = await FirebaseApp.CheckAndFixDependenciesAsync().AsUniTask();

            if (status != DependencyStatus.Available)
            {
                Fail($"의존성 오류: {status}");
                return;
            }

            App = FirebaseApp.DefaultInstance;

            Database = GetDatabase(App);
            Auth = FirebaseAuth.GetAuth(App);

            State = InitState.Ready;
            Debug.Log($"[Firebase] 초기화 성공 (app={App.Name})");
        }
        catch (System.Exception ex)
        {
            Fail(ex.Message);
        }
    }

    private FirebaseDatabase GetDatabase(FirebaseApp app)
    {
        FirebaseConfig config = Resources.Load<FirebaseConfig>("FirebaseConfig");
        if (config != null && !string.IsNullOrEmpty(config.databaseUrl))
        {
            return FirebaseDatabase.GetInstance(app, config.databaseUrl);
        }
        return FirebaseDatabase.GetInstance(app);
    }

    private void Fail(string error)
    {
        LastError = error;
        State = InitState.Failed;
        Debug.LogError($"[Firebase] 초기화 실패: {error}");
    }

    public async UniTask<bool> WaitForInitializationAsync()
    {
        await UniTask.WaitUntil(() => State != InitState.Pending);
        return State == InitState.Ready;
    }

    private void OnDestroy()
    {
        if (_instance == this)
        {
            _instance = null;
        }
    }
}

#endif
