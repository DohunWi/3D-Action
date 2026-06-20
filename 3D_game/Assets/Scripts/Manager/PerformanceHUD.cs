using UnityEngine;
using TMPro;
using UnityEngine.Profiling;

/// <summary>
/// 우상단 성능 오버레이 HUD
/// — 개발 빌드 + 에디터에서만 활성화 (Debug.isDebugBuild)
/// — Inspector에서 씬에 빈 GameObject를 만들고 이 컴포넌트를 붙이면 됨
///   (Canvas/Text를 직접 연결하거나, _autoCreate = true로 런타임 자동 생성)
/// </summary>
public class PerformanceHUD : MonoBehaviour
{
    public static PerformanceHUD Instance { get; private set; }

    [Header("Auto Create (Inspector 연결 없을 때)")]
    [Tooltip("true면 Canvas/Text를 코드로 자동 생성합니다.")]
    public bool autoCreate = true;

    [Header("Manual Reference (autoCreate = false일 때)")]
    public TextMeshProUGUI hudText;

    [Header("Settings")]
    [Tooltip("HUD 갱신 주기 (초)")]
    public float updateInterval = 0.5f;

    // --- 내부 상태 ---
    private float _timer;
    private int   _frameCount;
    private float _fps;

    // Draw Calls는 에디터 전용 API라 조건부 컴파일
#if UNITY_EDITOR
    private int _drawCalls;
#endif

    private void Awake()
    {
        // 씬 전환 시 중복 인스턴스 제거
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // 개발 빌드 / 에디터가 아니면 비활성화
        if (!Debug.isDebugBuild && !Application.isEditor)
        {
            gameObject.SetActive(false);
            return;
        }

        // DontDestroyOnLoad는 루트 오브젝트에만 동작
        transform.SetParent(null);
        DontDestroyOnLoad(gameObject);

        if (autoCreate && hudText == null)
            hudText = createOverlayText();
    }

    private void Update()
    {
        _frameCount++;
        _timer += Time.unscaledDeltaTime;

        if (_timer < updateInterval) return;

        _fps = _frameCount / _timer;
        _frameCount = 0;
        _timer = 0f;

#if UNITY_EDITOR
        _drawCalls = UnityEditor.UnityStats.drawCalls;
#endif

        refreshText();
    }

    private void refreshText()
    {
        if (hudText == null) return;

        float frameMs   = 1000f / Mathf.Max(_fps, 0.001f);
        float heapMB    = Profiler.GetTotalAllocatedMemoryLong() / 1048576f;
        float gcAllocKB = Profiler.GetMonoUsedSizeLong()        / 1024f;

#if UNITY_EDITOR
        hudText.text =
            $"FPS       {_fps:F0}\n" +
            $"Frame     {frameMs:F1} ms\n" +
            $"Draw      {_drawCalls}\n" +
            $"Heap      {heapMB:F1} MB\n" +
            $"GC Mono   {gcAllocKB:F0} KB";
#else
        hudText.text =
            $"FPS       {_fps:F0}\n" +
            $"Frame     {frameMs:F1} ms\n" +
            $"Heap      {heapMB:F1} MB\n" +
            $"GC Mono   {gcAllocKB:F0} KB";
#endif
    }

    // --- 런타임 Canvas/Text 자동 생성 ---
    private TextMeshProUGUI createOverlayText()
    {
        // Canvas — 이 GO의 자식으로 생성해 DontDestroyOnLoad를 부모가 일괄 관리
        var canvasGO = new GameObject("[PerfHUD Canvas]");
        canvasGO.transform.SetParent(transform);

        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 999;

        canvasGO.AddComponent<UnityEngine.UI.CanvasScaler>();
        canvasGO.AddComponent<UnityEngine.UI.GraphicRaycaster>();

        // Text (우상단 고정)
        var textGO = new GameObject("[PerfHUD Text]");
        textGO.transform.SetParent(canvasGO.transform, false);

        var rt = textGO.AddComponent<RectTransform>();
        rt.anchorMin  = new Vector2(1f, 1f);
        rt.anchorMax  = new Vector2(1f, 1f);
        rt.pivot      = new Vector2(1f, 1f);
        rt.anchoredPosition = new Vector2(-12f, -12f);
        rt.sizeDelta  = new Vector2(200f, 130f);

        var tmp = textGO.AddComponent<TextMeshProUGUI>();

        // 프로젝트 내 TMP 폰트를 명시 지정 (런타임 생성 시 기본 폰트를 못 찾는 경우 방지)
        var font = Resources.Load<TMPro.TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
        if (font != null) tmp.font = font;

        tmp.fontSize       = 14f;
        tmp.alignment      = TextAlignmentOptions.TopRight;
        tmp.color          = new Color(0.2f, 1f, 0.4f, 0.9f);
        tmp.fontStyle      = FontStyles.Bold;
        tmp.raycastTarget  = false;

        return tmp;
    }
}
