using UnityEngine;

public class MonologueTrigger : MonoBehaviour
{
    [Header("독백 설정")]
    public string triggerID = "monologue_001";  // ★ Inspector에서 지정 (예: monologue_tower, monologue_boss 등)
    public string speakerName = "...";

    [TextArea(2, 5)]
    public string[] lines =
    {
        "The top of the Tower...\nThe air itself feels wrong here.\nLike the nightmare is breathing.",
        "That fragment — a shard of pure darkness.\nIt pulses. Watches.\nThis is what poisoned the castle.",
        "Shatter it.\nEnd this."
    };

    private bool _triggered = false;

    private void Start()
    {
        // 저장된 진행 상태 확인
        if (GameManager.Instance != null)
            _triggered = GameManager.Instance.IsMonologueTriggered(triggerID);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_triggered) return;
        if (!other.CompareTag("Player")) return;
        if (DialogueUI.Instance == null) return;

        _triggered = true;

        // GameManager에 독백 완료 상태 저장
        if (GameManager.Instance != null)
            GameManager.Instance.MarkMonologueTriggered(triggerID);

        var controller = other.GetComponent<PlayerController>();
        controller?.ChangeState(PlayerState.Interact);
        DialogueUI.Instance.Open(speakerName, lines, controller);
    }
}
