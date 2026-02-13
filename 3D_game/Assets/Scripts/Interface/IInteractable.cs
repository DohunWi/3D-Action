using UnityEngine;

// 모든 상호작용 가능한 물체(NPC, 상자, 레버, 제단)는 이 인터페이스를 상속받음.
public interface IInteractable
{
    // 상호작용 했을 때 일어날 일
    void Interact(GameObject player);

    // 가까이 갔을 때 띄울 안내 문구 (예: "[E] 기도하기", "[E] 대화하기")
    string GetInteractPrompt();
}