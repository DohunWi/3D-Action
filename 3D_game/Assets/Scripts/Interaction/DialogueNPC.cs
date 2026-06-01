using UnityEngine;

public class DialogueNPC : MonoBehaviour, IInteractable
{
    [Header("NPC Info")]
    public string speakerName = "The Last Dreamer";

    [Header("Dialogue — 첫 대화 (게임 시작)")]
    [TextArea(2, 5)]
    public string[] initialLines =
    {
        "You...\nYou're still lucid.\nI haven't seen eyes like that since the King fell asleep.",
        "This castle used to be full of light.\nNow the nightmare has swallowed everything —\nthe people, the knights... all of them, lost.",
        "If you want to fight back, you'll need strength.\nGather the Memory Fragments the lost souls carry.\nBring them to the altar. Let them anchor you.",
        "The Tower at the center — something dark festers at its peak.\nBut the gate won't open for just anyone.\nFind the ones that guard the inner wards.\nProve yourself first."
    };

    [Header("Dialogue — 엘리트 처치 후")]
    [TextArea(2, 5)]
    public string[] eliteDefeatedLines =
    {
        "You did it.\nThe wards are broken. The Tower gate... it's open.",
        "At the top, you'll find a Fragment of pure nightmare.\nShatter it.\nWhat waits beyond — end it."
    };

    public void Interact(GameObject player)
    {
        if (DialogueUI.Instance == null) return;

        var controller = player.GetComponent<PlayerController>();
        controller?.ChangeState(PlayerState.Interact);

        string[] lines = GetCurrentLines();
        DialogueUI.Instance.Open(speakerName, lines, controller);
    }

    public string GetInteractPrompt()
    {
        return $"{speakerName}\n<size=80%>[E] Talk</size>";
    }

    private string[] GetCurrentLines()
    {
        if (GameManager.Instance != null && GameManager.Instance.eliteDefeated)
            return eliteDefeatedLines;

        return initialLines;
    }
}
