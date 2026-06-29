using UnityEngine;

public class EnemyStats : CharacterStats
{
    [Header("Rewards (Drop)")]
    public int expReward = 50;       // 주는 경험치
    // 메모리(기억의 파편)는 직접 지급하지 않고 사망 시 드랍되는 Memory Fragment 픽업으로만 획득한다.
    // 이 값은 그 드랍 파편의 금액으로 주입돼 적별로 보상을 튜닝할 수 있다 (직접 지급 아님 → 이중 지급 없음).
    public int memoryReward = 500;

    [Header("Loot Settings")]
    [Range(0f, 1f)] public float dropChance = 0.1f; // 아이템 드랍 확률 (10%)
    public GameObject dropItemPrefab; // 드랍할 아이템 (포션 등)

    [Header("References")]
    public Animator animator; 
    public Collider myCollider; // 죽으면 꺼버릴 콜라이더

    private bool _isDead = false;

    private static readonly int AnimID_DoDie = Animator.StringToHash("doDie");

    public override void Start()
    {
        base.Start(); // 부모의 Start (체력 초기화 등) 실행

        // 컴포넌트 자동 할당
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (myCollider == null) myCollider = GetComponent<Collider>();
    }

    // ★ 부모의 Die를 덮어씀 (Override)
    protected override void Die(Transform attacker)
    {
        if (_isDead) return; // 중복 사망 방지
        _isDead = true;

        // 1. 부모의 기본 사망 로직 실행 (사운드 재생, OnDeath 이벤트 호출)
        base.Die(attacker);

        // 2. 보상 지급 로직 (공격자가 플레이어일 때만)
        if (attacker != null && attacker.CompareTag("Player"))
        {
            GiveRewards(attacker);
        }

        // 3. 사망 연출 (애니메이션)
        if (animator != null)
        {
            animator.SetTrigger(AnimID_DoDie);
        }

        // 4. 물리 충돌 끄기 (시체가 길 막지 않게)
        if (myCollider != null) myCollider.enabled = false;

        // 5. 시체 청소 (5초 뒤 삭제)
        Destroy(gameObject, 5.0f);
    }

    private void GiveRewards(Transform player)
    {
        // 경험치 지급 (메모리는 드랍되는 Memory Fragment 픽업으로만 획득)
        PlayerStats pStats = player.GetComponent<PlayerStats>();
        if (pStats != null)
        {
            pStats.AddExperience(expReward);
        }

        // // D. 소울 흡수 이펙트 (Juice)
        // if (GameFeelManager.Instance != null)
        // {
        //     // 적 가슴 높이에서 플레이어 쪽으로
        //     GameFeelManager.Instance.SpawnSoulEffect(transform.position + Vector3.up, player);
        // }

        // E. 아이템 드랍 (Pickable)
        if (dropItemPrefab != null && Random.value <= dropChance)
        {
            GameObject drop = Instantiate(dropItemPrefab, transform.position + Vector3.up * 0.5f, Quaternion.identity);
            // 떨어진 것이 기억 파편이면 적별 보상값을 주입 → 픽업 시 memoryReward만큼 획득
            if (drop.TryGetComponent(out MemoryPickup memoryPickup))
                memoryPickup.memoryAmount = memoryReward;
        }
    }
}