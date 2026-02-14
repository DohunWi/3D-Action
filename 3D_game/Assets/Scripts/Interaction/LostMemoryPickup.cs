using UnityEngine;

public class LostMemoryPickup : MonoBehaviour
{
    private int memoryAmount;

    // 생성되자마자 게임매니저에게 "나 얼마짜리야?" 물어봄
    private void Start()
    {
        if (GameManager.Instance != null)
        {
            memoryAmount = GameManager.Instance.GetLostMemoryAmount();
            // (선택) 액수에 따라 이펙트 크기나 색깔 바꾸기 가능
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        // 플레이어가 닿으면 회수
        if (other.CompareTag("Player"))
        {
            PlayerWallet wallet = other.GetComponent<PlayerWallet>();
            if (wallet != null)
            {
                // 1. 지갑에 돈 복구
                wallet.AddMemory(memoryAmount);
                
                // 2. 매니저에게 "회수 완료" 알림 (데이터 삭제)
                GameManager.Instance.ClearLostMemory();
                
                // 3. 효과음 및 메시지
                Debug.Log($"<color=yellow>유실물 회수! (+{memoryAmount} Memory)</color>");
                // SoundManager.Instance.PlaySFX(...);

                // 4. 사라짐
                Destroy(gameObject);
            }
        }
    }
}