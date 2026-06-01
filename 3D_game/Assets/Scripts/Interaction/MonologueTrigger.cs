using UnityEngine;

public class MonologueTrigger : MonoBehaviour
{
    [Header("독백 설정")]
    public string speakerName = "...";

    [TextArea(2, 5)]
    public string[] lines =
    {
        "The top of the Tower...\nThe air itself feels wrong here.\nLike the nightmare is breathing.",
        "That fragment — a shard of pure darkness.\nIt pulses. Watches.\nThis is what poisoned the castle.",
        "Shatter it.\nEnd this."
    };

    private bool _triggered = false;

    private void OnTriggerEnter(Collider other)
    {
        if (_triggered) return;
        if (!other.CompareTag("Player")) return;
        if (DialogueUI.Instance == null) return;

        _triggered = true;

        var controller = other.GetComponent<PlayerController>();
        controller?.ChangeState(PlayerState.Interact);
        DialogueUI.Instance.Open(speakerName, lines, controller);
    }
}
