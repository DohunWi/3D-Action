using UnityEngine;
using UnityEngine.Profiling;      // GetTotalAllocatedMemoryLong / GetMonoUsedSizeLong
using Unity.Profiling;            // ProfilerRecorder (프레임당 GC 할당 측정)
using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

/// <summary>
/// 성능 데이터를 수집해 CSV로 저장하는 로거 (정확도 개선판)
///
/// ── 측정 방식 ──
/// • FrameTime: 매 프레임 누적 → 구간 평균/최대/p95 산출 (단일 프레임 스냅샷 ❌)
/// • GC Alloc : ProfilerRecorder "GC Allocated In Frame" → 프레임당 실제 할당 바이트
///              ("GC 0B" 목표를 직접 검증 가능. Mono 힙 점유 스냅샷과는 다름)
/// • Memory   : TotalMem(네이티브+매니지드 총 할당) / MonoHeap(매니지드 점유)
///
/// ── 저장 경로 ──
/// • 에디터 : Assets/PerformanceData/<timestamp>/  (Unity에서 바로 확인)
/// • 빌드   : Application.persistentDataPath/<timestamp>/
///
/// ── 활성 조건 ──
/// 에디터 / 개발 빌드(DEVELOPMENT_BUILD) / ENABLE_PERF_LOG 정의 시에만 컴파일·동작.
/// 릴리즈 빌드에서 측정하려면 Player Settings > Scripting Define Symbols 에
/// ENABLE_PERF_LOG 추가. (개발 빌드는 프로파일러 오버헤드로 fps가 낮게 나옴)
/// </summary>
public class PerformanceLogger : MonoBehaviour
{
    public static PerformanceLogger Instance { get; private set; }

    [Header("Settings")]
    [Tooltip("데이터 기록 주기 (초). 이 구간 동안 매 프레임을 누적해 통계 산출")]
    public float sampleInterval = 1f;

    [Tooltip("최대 샘플 수 (0 = 무제한)")]
    public int maxSamples = 0;

    [Tooltip("측정용으로 VSync/프레임 캡을 강제 해제 (캡에 묶인 fps 방지)")]
    public bool uncapFrameRateForTest = true;

    // --- 출력 ---
    private StringBuilder _sb;
    private string        _filePath;
    private bool          _started;
    private int           _sampleCount;

    private const string CSV_HEADER =
        "Timestamp,AvgFPS,AvgFrameMs,MaxFrameMs,P95FrameMs,GCAvgPerFrame_B,GCMaxPerFrame_B,MonoHeapMB,TotalMemMB,EnemyCount";

    // --- 프레임 누적 버퍼 (구간마다 Clear, capacity 유지로 재할당 최소화) ---
    private readonly List<float> _frameMs = new List<float>(512);
    private float _intervalElapsed;
    private float _frameMsMax;
    private long  _gcSumInterval;
    private long  _gcMaxFrame;

    // 프레임당 GC 할당 바이트 측정 레코더
    private ProfilerRecorder _gcRecorder;

    private void Awake()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD || ENABLE_PERF_LOG
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        transform.SetParent(null);          // DontDestroyOnLoad는 루트에만 동작
        DontDestroyOnLoad(gameObject);

        if (uncapFrameRateForTest)
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;
        }

        initSession();
#else
        // 릴리즈 빌드(정의 없음): 완전 무동작
        enabled = false;
        Destroy(gameObject);
#endif
    }

    private void OnEnable()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD || ENABLE_PERF_LOG
        // "GC Allocated In Frame" — Memory 카테고리, 프레임당 GC 할당 바이트
        _gcRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
#endif
    }

    private void OnDisable()
    {
        if (_gcRecorder.Valid) _gcRecorder.Dispose();
        stopAndSave();
    }

    private void OnApplicationQuit() => stopAndSave();

    private void Update()
    {
        if (!_started) return;

        // 1) 매 프레임 누적
        float dtMs = Time.unscaledDeltaTime * 1000f;
        _frameMs.Add(dtMs);
        if (dtMs > _frameMsMax) _frameMsMax = dtMs;

        if (_gcRecorder.Valid)
        {
            long gc = _gcRecorder.LastValue;          // 직전 프레임 GC 할당 바이트
            _gcSumInterval += gc;
            if (gc > _gcMaxFrame) _gcMaxFrame = gc;
        }

        _intervalElapsed += Time.unscaledDeltaTime;

        // 2) 구간 경과 시 기록
        if (_intervalElapsed >= sampleInterval)
        {
            recordSample();
            resetInterval();

            _sampleCount++;
            if (maxSamples > 0 && _sampleCount >= maxSamples) stopAndSave();
        }
    }

    // --- Public API ---
    /// <summary>씬 전환 등 임의 시점에 강제 저장</summary>
    public void ForceSave() => writeCSV();

    // --- 내부 로직 ---
    private void initSession()
    {
        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        // 에디터: Assets/PerformanceData/ (Unity에서 바로 확인 가능)
        // 빌드:  persistentDataPath (Mac .app 내부는 읽기 전용이라 dataPath 사용 불가)
        string baseDir = Application.isEditor
            ? Path.Combine(Application.dataPath, "PerformanceData")
            : Application.persistentDataPath;
        string dir = Path.Combine(baseDir, timestamp);
        Directory.CreateDirectory(dir);

        _filePath = Path.Combine(dir, $"perf_{timestamp}.csv");
        _sb = new StringBuilder();
        _sb.AppendLine(CSV_HEADER);

        resetInterval();
        _started = true;
        Debug.Log($"[PerformanceLogger] 세션 시작 (VSync={QualitySettings.vSyncCount}, " +
                  $"target={Application.targetFrameRate}) → {_filePath}");
    }

    private void recordSample()
    {
        int n = _frameMs.Count;
        if (n == 0) return;

        // 평균 FrameTime / FPS
        float sum = 0f;
        for (int i = 0; i < n; i++) sum += _frameMs[i];
        float avgMs  = sum / n;
        float avgFps = 1000f / Mathf.Max(avgMs, 0.0001f);

        // p95 FrameTime (in-place 정렬 — backing 배열 재사용으로 추가 할당 없음)
        _frameMs.Sort();
        int p95i = Mathf.Clamp(Mathf.CeilToInt(0.95f * n) - 1, 0, n - 1);
        float p95Ms = _frameMs[p95i];

        long  gcAvgPerFrame = _gcSumInterval / n;            // 프레임당 평균 GC 할당 바이트
        float monoHeapMB    = Profiler.GetMonoUsedSizeLong()        / 1048576f;
        float totalMemMB    = Profiler.GetTotalAllocatedMemoryLong() / 1048576f;
        int   enemies       = countActiveEnemies();

        _sb.Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")).Append(',')
           .Append(avgFps.ToString("F1")).Append(',')
           .Append(avgMs.ToString("F2")).Append(',')
           .Append(_frameMsMax.ToString("F2")).Append(',')
           .Append(p95Ms.ToString("F2")).Append(',')
           .Append(gcAvgPerFrame).Append(',')
           .Append(_gcMaxFrame).Append(',')
           .Append(monoHeapMB.ToString("F2")).Append(',')
           .Append(totalMemMB.ToString("F2")).Append(',')
           .Append(enemies).Append('\n');
    }

    private void resetInterval()
    {
        _frameMs.Clear();          // capacity 유지
        _intervalElapsed = 0f;
        _frameMsMax      = 0f;
        _gcSumInterval   = 0;
        _gcMaxFrame      = 0;
    }

    private void stopAndSave()
    {
        if (!_started) return;
        _started = false;
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
