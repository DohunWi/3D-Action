using UnityEngine;

// 처음 Memory Fragment를 획득하면 제단 강화 안내를 표시
public class MemoryTutorialTrigger : MonoBehaviour
{
    [Header("Tutorial Content")]
    public string tutorialTitle = "Memory Fragment";

    [TextArea(4, 8)]
    public string tutorialContent =
        "You've collected a Memory Fragment.\n\n" +
        "Bring them to the Altar.\n" +
        "Pray there to grow stronger —\n" +
        "and to anchor yourself to this world.";

    public float displayDuration = 7f;

    private PlayerWallet _wallet;
    private bool _triggered = false;

    private void Start()
    {
        // 저장된 진행 상태 확인
        if (GameManager.Instance != null)
            _triggered = GameManager.Instance.memoryTutorialTriggered;

        _wallet = FindAnyObjectByType<PlayerWallet>();
        if (_wallet != null && !_triggered)
            _wallet.OnMemoryChanged += onMemoryChanged;
    }

    private void OnDestroy()
    {
        if (_wallet != null)
            _wallet.OnMemoryChanged -= onMemoryChanged;
    }

    private void onMemoryChanged(int current)
    {
        if (_triggered) return;
        if (current <= 0) return;

        _triggered = true;
        _wallet.OnMemoryChanged -= onMemoryChanged;

        // GameManager에 튜토리얼 완료 상태 저장
        if (GameManager.Instance != null)
            GameManager.Instance.memoryTutorialTriggered = true;

        TutorialUI.Instance?.Show(tutorialTitle, tutorialContent, displayDuration);
    }
}
