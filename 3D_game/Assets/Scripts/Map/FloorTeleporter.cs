using UnityEngine;
using System.Collections;

public class FloorTeleporter : MonoBehaviour, IInteractable
{
    [Header("이동 설정")]
    public Transform destination;
    public bool isLocked = true;
    public string displayName = "Tower Gate";

    [Header("잠긴 상태 대사")]
    [TextArea(2, 5)]
    public string[] lockedLines =
    {
        "The gate does not yield.\nAn oppressive weight hangs in the air —\nsomething powerful still roams the castle.",
        "Defeat the guardian that lurks within these walls.\nOnly then will the Tower open its path to you."
    };

    public string GetInteractPrompt()
    {
        if (isLocked) return $"{displayName}\n<size=80%>[E] Examine</size>";
        return $"{displayName}\n<size=80%>[E] Enter</size>";
    }

    public void Interact(GameObject player)
    {
        if (isLocked)
        {
            if (DialogueUI.Instance == null) return;
            var controller = player.GetComponent<PlayerController>();
            controller?.ChangeState(PlayerState.Interact);
            DialogueUI.Instance.Open(displayName, lockedLines, controller);
            return;
        }

        StartCoroutine(TeleportSequence(player));
    }
    private IEnumerator TeleportSequence(GameObject player)
    {
        // 텔레포트하면 트리거를 빠져나가도 OnTriggerExit가 안 불리므로 튜토리얼 패널을 강제로 닫음
        TutorialUI.Instance?.Close();

        CharacterController cc = player.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;

        yield return new WaitForSeconds(0.1f);

        player.transform.position = destination.position;
        player.transform.rotation = destination.rotation;

        yield return new WaitForSeconds(0.1f);

        if (cc != null) cc.enabled = true;
    }
}