using UnityEngine;

// CharacterStats를 상속받음
public class PlayerStats : CharacterStats
{
    [Header("Player Specific")]
    public PlayerController playerController; // 상태 확인용 연결
    public float parryAngle = 90f; // 전방 90도 안에서 온 공격만 막기

    [Header("Stamina Settings")] // [NEW] 스태미나 설정
    public float maxStamina = 100f;
    public float currentStamina { get; private set; } // 읽기 전용 프로퍼티
    public float staminaRegenRate = 15f;   // 초당 회복량
    public float staminaRegenDelay = 2.0f; // 스태미나 쓴 후 회복 시작까지 딜레이
    private float _lastStaminaUseTime; // 마지막 사용 시간 기록용

    [Header("Mana Settings")] // [NEW] 마나 설정
    public float maxMana = 100f;
    public float currentMana { get; private set; }
    public float manaRegenRate = 5f; // 초당 5 회복 (스태미나보다 느리게)

    // 부모의 TakeDamage를 덮어씀 (Override)
    public override void Start()
    {
        base.Start();
        currentStamina = maxStamina; // 시작 시 풀 충전
        currentMana = maxMana;
    }
    private void Update()
    {
        // [NEW] 스태미나 자동 회복 로직
        // "마지막 사용 후 딜레이(2초)가 지났고" AND "스태미나가 꽉 차지 않았다면" -> 회복
        if (Time.time > _lastStaminaUseTime + staminaRegenDelay && currentStamina < maxStamina)
        {
            currentStamina += staminaRegenRate * Time.deltaTime;
            currentStamina = Mathf.Min(currentStamina, maxStamina); // 최대치 초과 방지
        }
        // 마나 자동 회복 (항상 천천히 회복)
        if (currentMana < maxMana)
        {
            currentMana += manaRegenRate * Time.deltaTime;
            currentMana = Mathf.Min(currentMana, maxMana);
        }

        // (테스트용) 스태미나 수치 확인
       // Debug.Log($"Stamina: {currentStamina:F1}");
    }

    // 마나 사용 함수
    public bool UseMana(float amount)
    {
        if (currentMana >= amount)
        {
            currentMana -= amount;
            Debug.Log($"현재 마나: {currentMana:F1}");
            return true;
        }
        Debug.Log("마나가 부족합니다!");
        return false;
    }
    // 스태미나 사용 함수 (성공하면 true, 실패하면 false 리턴)
    public bool UseStamina(float amount)
    {
        if (currentStamina >= amount)
        {
            currentStamina -= amount;
            _lastStaminaUseTime = Time.time; // 사용 시간 갱신 (회복 딜레이 리셋)
            Debug.Log($"현재 스태미나: {currentStamina:F1}");
            return true;
        }
        
        Debug.Log("스태미나 부족! (헥헥)");
        return false;
    }
    // 확인용 함수: 깎지는 않고 검사만 함
    public bool HasStamina(float amount)
    {
        return currentStamina >= amount;
    }    
    public override void TakeDamage(float damage, Transform attacker = null)
    {
        // 1. 무적 판정 로직 추가
        if (playerController != null && playerController.currentState == PlayerState.Roll)
        {
            Debug.Log("구르기 무적(i-frame)으로 데미지를 씹었습니다!");
            return; // 데미지 적용 안 하고 종료
        }
        // 2. 패링 시도
        if (playerController.currentState == PlayerState.Parry)
        {
            if (attacker != null)
            {
                // 각도 계산: (적 위치 - 내 위치) 와 (내 앞 방향) 사이의 각도
                Vector3 dirToAttacker = (attacker.position - transform.position).normalized;
                float angle = Vector3.Angle(transform.forward, dirToAttacker);

                // 내 전방(parryAngle / 2) 안에 적이 있는가?
                if (angle < parryAngle * 0.5f)
                {
                    Debug.Log($"<color=cyan>패링 성공! (Angle: {angle:F1})</color>");
                    
                    // A. 적에게 경직 주기
                    Enemy enemy = attacker.GetComponentInParent<Enemy>();
                    if (enemy != null) enemy.GetParried();

                    // B. 플레이어에게 반격 기회 부여
                    playerController.OnParrySuccess();

                    return; // 데미지 0
                }
            }
        }

        // 3. 무적이 아니면 부모의 원래 기능(체력 깎기) 실행
        base.TakeDamage(damage);
    }
    // 디버그용: 패링 각도를 눈으로 확인
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        // 내 위치에서 왼쪽 경계선
        Vector3 leftDir = Quaternion.Euler(0, -parryAngle * 0.5f, 0) * transform.forward;
        // 내 위치에서 오른쪽 경계선
        Vector3 rightDir = Quaternion.Euler(0, parryAngle * 0.5f, 0) * transform.forward;

        Gizmos.DrawRay(transform.position, leftDir * 2f);
        Gizmos.DrawRay(transform.position, rightDir * 2f);
    }
}