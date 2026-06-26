using UnityEngine;
using System;

public class CharacterStats : MonoBehaviour, IDamageable
{
    [Header("Stats")]
    public float maxEgo = 100f;
    public float currentEgo { get; set; } 

    [Header("VFX")]
    public GameObject damagePopupPrefab; 

    [Header("Audio")]
    public AudioClip hitVoice;   // 맞았을 때 낼 소리 
    public AudioClip deathVoice; // 죽었을 때 낼 소리 

    [Header("Composure (Super Armor)")]
    public float maxComposure = 50f;
    public float composureRecoveryTime = 3.0f;
    public float composureRecoveryRate = 10f; // 초당 회복량
    private float _currentComposure;
    private float _lastDamageTime;

    // 다른 스크립트들에게 방송하는 이벤트
    public event Action OnDeath; 
    public event Action OnTakeDamage;    
    public event Action<float, float> OnEgoChanged;
    public event Action OnComposureBroken;
    public virtual void Start()
    {
        currentEgo = maxEgo;
        _currentComposure = maxComposure;
        OnEgoChanged?.Invoke(currentEgo, maxEgo);

        if (damagePopupPrefab != null && DamagePopupPool.Instance != null)
            DamagePopupPool.Instance.WarmUp(damagePopupPrefab, 5);
    }
    public virtual void Update()
    {
        // 강인도 자동 회복 로직 (여기서 통합 관리)
        if (Time.time > _lastDamageTime + composureRecoveryTime)
        {
            if (_currentComposure < maxComposure)
            {
                _currentComposure += composureRecoveryRate * Time.deltaTime;
                // (선택) UI 갱신이 필요하다면 이벤트 추가 가능
            }
        }
    }
    public virtual void TakeDamage(float damage, float composureDamage = 10f, Transform attacker = null)
    {
        // 1. 데미지 처리
        if (currentEgo <= 0) return; // 이미 죽었으면 무시

        currentEgo -= damage;

        // ★ 데미지 입을 때마다 UI 갱신 알림
        OnEgoChanged?.Invoke(currentEgo, maxEgo);
        OnTakeDamage?.Invoke(); // 맞았다고 알림 (나중에 피 튀기는 효과 등에 사용)

        // 2. 강인도 처리 
        _lastDamageTime = Time.time;
        _currentComposure -= composureDamage;

        if (_currentComposure <= 0)
        {
            // 강인도 파괴! -> Enemy에게 "너 기절해!" 라고 알림
            _currentComposure = maxComposure; // 초기화
            OnComposureBroken?.Invoke(); 
            // 피격 사운드 재생  
            if (hitVoice != null)
            {
                // Pitch를 살짝 랜덤하게 주면 훨씬 자연스러움
                SoundManager.Instance.PlaySFX(hitVoice, transform.position, UnityEngine.Random.Range(0.9f, 1.1f)); 
            }
        }
        else
        {
            // 강인도 버팀 -> Enemy에게 "너 맞긴 했는데 참아!" 라고 알림
            // OnTakeDamage?.Invoke();
        }
        // ====================================================

        // ★ 데미지 팝업 생성
        if (damagePopupPrefab != null && DamagePopupPool.Instance != null)
        {
            Vector3 spawnPos = transform.position + Vector3.up * 1.5f + Vector3.forward * 0.4f;
            spawnPos.x += UnityEngine.Random.Range(-0.5f, 0.5f);

            DamagePopup popup = DamagePopupPool.Instance.Get(damagePopupPrefab, spawnPos);
            popup.Setup(damage);
        }

        if (currentEgo <= 0)
        {
            Die(attacker);
        }
    }
    // 자식들이 이벤트를 부를 수 있게 해주는 '대리자 함수'
    protected void InvokeEgoChanged(float current, float max)
    {
        OnEgoChanged?.Invoke(current, max);
    }

    protected virtual void Die(Transform attacker)
    {
        OnDeath?.Invoke(); // "나 죽었어!" 방송 송출
        // ====================================================
        // 사망 사운드 재생
        // ====================================================
        if (deathVoice != null)
        {
            SoundManager.Instance.PlaySFX(deathVoice, transform.position, 1.1f);
        }
    }
}