using UnityEngine;
using UnityEngine.Profiling;
using System;
using System.Collections;
using System.Text;
using System.IO;

/// <summary>
/// 성능 데이터를 매 초 수집해 CSV로 저장하는 로거
/// — 씬 언로드 또는 앱 종료 시 자동으로 CSV 파일을 기록
/// — 저장 경로: Application.persistentDataPath/perf_YYYYMMDD_HHmmss.csv
/// — 개발 빌드 + 에디터에서만 동작 (릴리즈 빌드 오버헤드 없음)
/// </summary>
public class PerformanceLogger : MonoBehaviour
{
    public static PerformanceLogger Instance { get; private set; }

    [Header("Settings")]
    [Tooltip("데이터 수집 주기 (초)")]
    public float sampleInterval = 1f;

    [Tooltip("최대 샘플 수 (0 = 무제한)")]
    public int maxSamples = 0;

    // --- 내부 ---
    private StringBuilder _sb;
    private string        _filePath;
    private bool          _isRunning;
    private Coroutine     _collectCoroutine;

    private const string CSV_HEADER = "Timestamp,FPS,FrameTime_ms,GC_AllocBytes,ActiveEnemyCount,HeapMB";

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        // DontDestroyOnLoad는 루트 오브젝트에만 동작
        transform.SetParent(null);
        DontDestroyOnLoad(gameObject);

        // 개발 빌드 / 에디터가 아니면 아무것도 안 함
        if (!Debug.isDebugBuild && !Application.isEditor)
        {
            enabled = false;
            return;
        }

        initSession();
    }

    private void OnEnable()
    {
        if (_isRunning || (_sb == null)) return;
        _collectCoroutine = StartCoroutine(collectLoop());
        _isRunning = true;
    }

    private void OnDisable()
    {
        stopAndSave();
    }

    private void OnApplicationQuit()
    {
        stopAndSave();
    }

    // --- Public API ---

    /// <summary>씬 전환 등 임의 시점에 강제 저장</summary>
    public void ForceSave()
    {
        writeCSV();
        Debug.Log($"[PerformanceLogger] 저장 완료: {_filePath}");
    }

    // --- 내부 로직 ---

    private void initSession()
    {
        // 에디터/개발 빌드: Assets/PerformanceData/<timestamp>/ 에 저장
        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string dir = Path.Combine(Application.dataPath, "PerformanceData", timestamp);
        Directory.CreateDirectory(dir);

        _filePath = Path.Combine(dir, $"perf_{timestamp}.csv");
        _sb = new StringBuilder();
        _sb.AppendLine(CSV_HEADER);
        Debug.Log($"[PerformanceLogger] 세션 시작 → {_filePath}");
    }

    private IEnumerator collectLoop()
    {
        var wait = new WaitForSecondsRealtime(sampleInterval);
        int sampleCount = 0;

        while (true)
        {
            yield return wait;

            recordSample();
            sampleCount++;

            if (maxSamples > 0 && sampleCount >= maxSamples)
            {
                stopAndSave();
                yield break;
            }
        }
    }

    private void recordSample()
    {
        float fps       = 1f / Mathf.Max(Time.unscaledDeltaTime, 0.0001f);
        float frameMs   = Time.unscaledDeltaTime * 1000f;
        long  gcBytes   = Profiler.GetMonoUsedSizeLong();
        float heapMB    = Profiler.GetTotalAllocatedMemoryLong() / 1048576f;
        int   enemies   = countActiveEnemies();

        _sb.AppendLine(
            $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}," +
            $"{fps:F1}," +
            $"{frameMs:F2}," +
            $"{gcBytes}," +
            $"{enemies}," +
            $"{heapMB:F2}"
        );
    }

    private void stopAndSave()
    {
        if (!_isRunning) return;
        _isRunning = false;

        if (_collectCoroutine != null)
        {
            StopCoroutine(_collectCoroutine);
            _collectCoroutine = null;
        }

        writeCSV();
    }

    private void writeCSV()
    {
        if (_sb == null || _sb.Length == 0) return;
        try
        {
            File.WriteAllText(_filePath, _sb.ToString());
            Debug.Log($"[PerformanceLogger] CSV 저장 완료 ({_filePath})");
        }
        catch (Exception e)
        {
            Debug.LogError($"[PerformanceLogger] CSV 저장 실패: {e.Message}");
        }
    }

    private static int countActiveEnemies()
    {
        // FindObjectsByType은 캐싱 불가 (동적 스폰) → 주기 1초이므로 허용
        return FindObjectsByType<Enemy>(FindObjectsInactive.Exclude).Length;
    }
}
