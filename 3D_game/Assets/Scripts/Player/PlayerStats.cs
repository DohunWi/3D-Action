using UnityEngine;

// CharacterStats를 상속받음
public class PlayerStats : CharacterStats
{
    [Header("Player Specific")]
    public PlayerController playerController; // 상태 확인용 연결

    // 부모의 TakeDamage를 덮어씀 (Override)
    public override void TakeDamage(float damage)
    {
        // 1. 무적 판정 로직 추가
        if (playerController != null && playerController.currentState == PlayerState.Roll)
        {
            Debug.Log("구르기 무적(i-frame)으로 데미지를 씹었습니다!");
            return; // 데미지 적용 안 하고 종료
        }

        // 2. 무적이 아니면 부모의 원래 기능(체력 깎기) 실행
        base.TakeDamage(damage);
    }
}