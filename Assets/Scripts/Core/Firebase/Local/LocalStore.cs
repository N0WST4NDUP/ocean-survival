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

    // PlayerPrefs는 long을 담지 못하므로 문자열로 저장한다.
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

    // JsonUtility는 최상위 배열을 다루지 못하므로 래퍼로 감싼다.
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
