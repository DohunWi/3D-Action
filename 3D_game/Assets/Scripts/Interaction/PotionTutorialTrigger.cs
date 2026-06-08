using UnityEngine;

// 플레이어 체력이 처음 50% 이하로 떨어지면 포션 사용법 안내
public class PotionTutorialTrigger : MonoBehaviour
{
    [Header("Tutorial Content")]
    public string tutorialTitle = "Potion";

    [TextArea(4, 8)]
    public string tutorialContent =
        "Your Ego is fading.\n\n" +
        "[ R ]  Use Potion\n" +
        "Restores a portion of your Ego.\n" +
        "Refills when you rest at an Altar.";

    public float displayDuration = 6f;

    private PlayerStats _playerStats;
    private bool _triggered = false;

    private void Start()
    {
        // 저장된 진행 상태 확인
        if (GameManager.Instance != null)
            _triggered = GameManager.Instance.potionTutorialTriggered;

        _playerStats = FindAnyObjectByType<PlayerStats>();
        if (_playerStats != null && !_triggered)
            _playerStats.OnEgoChanged += onEgoChanged;
    }

    private void OnDestroy()
    {
        if (_playerStats != null)
            _playerStats.OnEgoChanged -= onEgoChanged;
    }

    private void onEgoChanged(float current, float max)
    {
        if (_triggered) return;
        if (current > max * 0.5f) return;

        _triggered = true;
        _playerStats.OnEgoChanged -= onEgoChanged;

        // GameManager에 튜토리얼 완료 상태 저장
        if (GameManager.Instance != null)
            GameManager.Instance.potionTutorialTriggered = true;

        TutorialUI.Instance?.Show(tutorialTitle, tutorialContent, displayDuration);
    }
}
