using UnityEngine;
using System.Collections.Generic;

// 드래곤의 각 부위(머리, 가슴, 꼬리 등)에 부착하여
// 플레이어의 공격을 최상위 본체의 EnemyStats로 전달합니다.
public class BossHurtbox : MonoBehaviour, IDamageable
{
    [Header("--- Main Boss Stats ---")]
    // 부모 클래스인 CharacterStats로 선언해두면 EnemyStats도 자동으로 들어갑니다.
    public CharacterStats mainStats;

    [Header("--- Hitbox Settings ---")]
    public float damageMultiplier = 1.0f; // 부위 파괴나 약점 데미지 배율
    public float composureMultiplier = 1.0f; // 부위별 강인도 깎이는 배율
    public bool isWeakPoint = false;      // 머리 등 약점 여부

    [Header("--- Effects (Optional) ---")]
    public ParticleSystem weakPointHitVFX; // 약점 타격 시 터질 파티클 (옵션)

    // 같은 보스(mainStats 기준)에 대한 중복 타격 방지
    // 한 번의 스윙에서 여러 hurtbox가 맞아도 한 번만 피격 처리
    private static readonly Dictionary<CharacterStats, float> _lastHitTimes = new Dictionary<CharacterStats, float>();
    private const float HitCooldown = 0.3f;

    // 플레이어의 무기(Weapon) 스크립트가 이 함수를 호출합니다.
    // 인자들을 CharacterStats.TakeDamage 와 완벽히 동일하게 맞췄습니다.
    public void TakeDamage(float damage, float composureDamage = 10f, Transform attacker = null)
    {
        if (mainStats != null)
        {
            // 같은 스윙 내 중복 피격 차단
            if (_lastHitTimes.TryGetValue(mainStats, out float lastTime) && Time.time - lastTime < HitCooldown) return;
            _lastHitTimes[mainStats] = Time.time;
            // 부위별 배율 적용
            float finalDamage = damage * damageMultiplier;
            float finalComposureDamage = composureDamage * composureMultiplier;
            
            // 본체의 TakeDamage 호출 (이때 attacker를 꼭 넘겨줘야 사망 시 보상을 받습니다!)
            mainStats.TakeDamage(finalDamage, finalComposureDamage, attacker); 
            
            // 약점 타격 연출
            if (isWeakPoint)
            {
                // 특수 피격 사운드 재생 (일반 피격음보다 경쾌하거나 둔탁한 소리)
                // SoundManager.Instance.PlaySFX(weakPointSound, transform.position);

                if (weakPointHitVFX != null)
                {
                    weakPointHitVFX.transform.position = transform.position;
                    weakPointHitVFX.Play();
                }
            }
        }
    }
}