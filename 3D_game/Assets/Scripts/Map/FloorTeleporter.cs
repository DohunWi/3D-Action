using UnityEngine;
using System.Collections;

// 기존 인터페이스를 상속받아 구현 [cite: 2026-01-20]
public class FloorTeleporter : MonoBehaviour, IInteractable 
{
    [Header("이동 설정")]
    public Transform destination; 
    public bool isLocked = true; 
    [Header("안내 문구")]
    public string interactMessage = "Go to 2-Floor\n<size=80%>[E] Go</size>";

    // 인터페이스 구현: 상호작용 안내 문구 [cite: 2026-01-20]
    public string GetInteractPrompt()
    {
        if (isLocked) return "Door is locked.";
        return interactMessage;
    }

    // 인터페이스 구현: 실제 상호작용 로직 [cite: 2026-01-20]
    public void Interact(GameObject player)
    {
        if (isLocked) 
        {
            // 상호작용 거부 사운드나 연출 추가 가능
            return;
        }

        StartCoroutine(TeleportSequence(player));
    }
    private IEnumerator TeleportSequence(GameObject player)
    {
            // Character Controller 컴포넌트를 가져옴
        CharacterController cc = player.GetComponent<CharacterController>();

        // 1. 이동 전 컨트롤러 비활성화 [cite: 2026-01-20]
        if (cc != null) cc.enabled = false;

        // 1. 페이드 아웃 (화면이 깜빡이는 연출 추가 지점)
        // FadeManager.Instance.FadeOut(); 

        yield return new WaitForSeconds(0.1f); // 물리 연산이 멈출 수 있도록 아주 짧게 대기

        // 2. 실제 위치와 회전값 변경 [cite: 2026-01-20]
        player.transform.position = destination.position;
        player.transform.rotation = destination.rotation;

        yield return new WaitForSeconds(0.1f); // 위치가 고정될 때까지 대기

        // 3. 이동 후 컨트롤러 다시 활성화 [cite: 2026-01-20]
        if (cc != null) cc.enabled = true;

        // 3. 페이드 인
        // FadeManager.Instance.FadeIn(); 
        
        Debug.Log("위치 이동 완료!"); 

        
    }
}