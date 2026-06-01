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
        _wallet = FindFirstObjectByType<PlayerWallet>();
        if (_wallet != null)
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

        TutorialUI.Instance?.Show(tutorialTitle, tutorialContent, displayDuration);
    }
}
