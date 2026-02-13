using UnityEngine;

public class PlayerWeapon : Weapon
{
    [Header("Player Feel")]
    public AudioClip heavyHitSound; // 플레이어 전용 타격음
    public float cameraShakeAmount = 0.2f; // 카메라 흔들림 강도

    private PlayerController _player; // 플레이어 state 확인용
    private PlayerStats _playerStats; // 플레이어 stat 확인용

    // 부모의 Awake도 실행하고 내 것도 실행
    protected override void Awake()
    {
        base.Awake();
        ownerTag = "Player"; // 플레이어 무기라고 자동 설정
        _player = GetComponentInParent<PlayerController>();
        _playerStats = GetComponentInParent<PlayerStats>();
    }
    public override float damage
    {
        get
        {
            // 1. 데이터가 없으면 0
            if (weaponData == null) return 0f;

            // 2. PlayerStats가 있다면 -> 스탯 반영 계산식 사용
            if (_playerStats != null)
            {
                // (기본뎀 + 보정뎀) * 배율
                return _playerStats.CalculateTotalDamage(weaponData) * damageMultiplier;
            }

            // 3. PlayerStats가 없다면(혹시 모를 예외) -> 그냥 기본 데미지 사용
            return base.damage;
        }
    }

    // ★ 부모가 "때렸어!" 하고 알려주면 여기서 연출 실행
    protected override void OnAttackSuccess(Collider victim)
    {
        if (weaponData == null) return;

        // 1. [특수 상황] 반격 중일 때 base.OnAttackSuccess(victim); 을 부르지 않는다
        if (_player != null && _player.currentState == PlayerState.CounterAttack)
        {
            // A. 반격 소리 재생 (없으면 일반 소리라도 씀)
            AudioClip clip = weaponData.criticalHitSound != null ? weaponData.criticalHitSound : weaponData.hitSound;
            SoundManager.Instance.PlaySFX(clip, transform.position, 1.0f); 

            victim.GetComponent<Enemy>().KnockDown();
            // B. VFX 재생 (부모 코드를 안 부르니 여기서 직접 해줘야 함)
            if (weaponData.hitVFX != null)
            {
                // 충돌 지점 찾기 (없으면 적 위치)
                Vector3 hitPoint = victim.ClosestPoint(transform.position);
                Instantiate(weaponData.hitVFX, hitPoint, Quaternion.identity);
            }

            // C. 추가 연출 (카메라 쉐이크 등)
            // CameraShake.Instance.Shake(cameraShakeAmount * 2f); 

            Debug.Log("<color=red><b>카운터 어택 성공!</b></color>");
        }
        // 2. [일반 상황] 평소대로 공격할 때
        else
        {
            // 평소에는 부모의 '기본 로직'을 그대로 씀.
            base.OnAttackSuccess(victim);
        }
    }
}