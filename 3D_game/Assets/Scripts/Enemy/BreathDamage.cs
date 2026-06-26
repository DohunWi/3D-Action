using UnityEngine;
using System.Collections.Generic;

public class BreathDamage : MonoBehaviour
{
    [Header("Damage Settings")]
    public int damagePerHit = 10;
    public float damageInterval = 0.5f; // 같은 놈은 0.5초에 한 번만 맞음 (연타 방지)
    public string targetTag = "Player";

    [Header("Sound Clip")]
    public AudioClip fireSound;

    // "누가(GameObject) 언제(Time) 맞았는지" 기록하는 장부
    private Dictionary<GameObject, float> _hitHistory = new Dictionary<GameObject, float>();
    private ParticleSystem _particleSystem;

    private void Awake()
    {
        _particleSystem = GetComponent<ParticleSystem>();
    }

    private void OnEnable()
    {
        _hitHistory.Clear();
        SoundManager.Instance?.PlaySFX(fireSound, transform.position, 1.0f);
    }

    // ★ 핵심: 파티클이 무언가에 닿으면 유니티가 이 함수를 호출해줌
    private void OnParticleCollision(GameObject other)
    {
        // 1. 태그 확인
        if (!other.CompareTag(targetTag)) return;

        // 2. 데미지 쿨타임 체크 (DoT 틱 관리)
        float lastHitTime = 0f;
        if (_hitHistory.TryGetValue(other, out lastHitTime))
        {
            // 아직 쿨타임 안 지났으면 무시
            if (Time.time < lastHitTime + damageInterval) return;
        }

        // 3. 데미지 적용
        ApplyDamage(other);

        // 4. 맞은 시간 갱신
        if (_hitHistory.ContainsKey(other))
        {
            _hitHistory[other] = Time.time;
        }
        else
        {
            _hitHistory.Add(other, Time.time);
        }
    }

    private void ApplyDamage(GameObject target)
    {
        CharacterStats stats = target.GetComponent<CharacterStats>();
        if (stats != null)
        {
            stats.TakeDamage(damagePerHit);
            // Debug.Log($"🔥 {target.name}에게 불 데미지! (파티클 적중)");
        }
    }
}