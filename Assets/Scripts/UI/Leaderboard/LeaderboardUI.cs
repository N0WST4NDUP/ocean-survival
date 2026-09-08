using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

// 리더보드 패널에 붙인다. 패널이 SetActive(true)되면(OnEnable) 실시간 리스너를 구독·렌더하고,
// 닫히면(OnDisable) 리스너를 해제한다. 닫기 버튼은 패널을 SetActive(false).
public class LeaderboardUI : MonoBehaviour
{
    [SerializeField] private Transform _content;        // 행이 들어갈 부모(스크롤뷰 Content)
    [SerializeField] private LeaderboardRow _rowPrefab;
    [SerializeField] private Button _closeButton;
    [SerializeField] private GameObject _emptyText;     // 기록 없을 때 표시(선택)
    [SerializeField] private int _limit = 10;

    private readonly List<LeaderboardRow> _rows = new();

    private LeaderboardManager _manager;
    private bool _listening;

    private void Awake()
    {
        if (_closeButton != null) _closeButton.onClick.AddListener(Close);
    }

    private void OnDestroy()
    {
        if (_closeButton != null) _closeButton.onClick.RemoveListener(Close);
    }

    private void OnEnable() => SubscribeAsync().Forget();

    private async UniTaskVoid SubscribeAsync()
    {
        bool ready = await LeaderboardManager.Instance.WaitForInitializationAsync();

        // 대기 중 패널이 닫혔거나 파괴됐으면 중단
        if (this == null || !isActiveAndEnabled || !ready) return;

        _manager = LeaderboardManager.Instance;
        _manager.OnLeaderboardUpdated += Render;
        _manager.StartRealtimeListener(_limit);   // 구독 즉시 현재 데이터로 1회 콜백 → 초기 렌더
        _listening = true;
    }

    private void OnDisable()
    {
        if (_listening && _manager != null)
        {
            _manager.OnLeaderboardUpdated -= Render;
            _manager.StopRealtimeListener();
            _listening = false;
        }
    }

    private void Close() => gameObject.SetActive(false);

    private void Render(List<LeaderboardEntry> entries)
    {
        // 기존 행 제거
        foreach (var row in _rows)
        {
            if (row != null) Destroy(row.gameObject);
        }
        _rows.Clear();

        string myUid = (AuthManager.Instance.IsInitialized && AuthManager.Instance.IsLoggedIn)
            ? AuthManager.Instance.CurrentUserId
            : null;

        if (_content != null && _rowPrefab != null)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                var row = Instantiate(_rowPrefab, _content);
                row.Set(i + 1, entries[i], entries[i].userId == myUid);
                _rows.Add(row);
            }
        }

        if (_emptyText != null) _emptyText.SetActive(entries.Count == 0);
    }
}
