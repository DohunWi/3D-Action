using UnityEngine;

public class MemoryAltar : MonoBehaviour, IInteractable
{
    [Header("UI Connection")]
    public StatUpgradeUI upgradeUI; // ★ 인스펙터에서 StatMenu 패널을 여기에 연결하세요!

    [Header("Settings")]
    public string interactMessage = "Altar of Memeory\n<size=80%>[R] Pray</size>";
    public AudioClip interactSound; // (선택) 상호작용 시 날 소리

    // 인터페이스 구현 1: 상호작용 시 실행될 로직
    public void Interact(GameObject player)
    {
        Debug.Log("제단 앞에서 기도합니다...");

        if (upgradeUI != null)
        {
            // 1. UI 열기
            upgradeUI.Open();

            // 2. (선택) 체력/포션 회복 로직 추가 가능
            // var stats = player.GetComponent<PlayerStats>();
            // if (stats != null) stats.HealFull(); 

            // 3. 사운드 재생
            if (interactSound != null) 
            {
                AudioSource.PlayClipAtPoint(interactSound, transform.position);
            }
        }
        else
        {
            Debug.LogError("❌ 오류: 'StatUpgradeUI'가 연결되지 않았습니다! 인스펙터를 확인해주세요.");
        }
    }

    // 인터페이스 구현 2: 안내 문구 (PlayerInteraction에서 가져감)
    public string GetInteractPrompt()
    {
        return interactMessage;
    }
}