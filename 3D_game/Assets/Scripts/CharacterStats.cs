using UnityEngine;
using UnityEngine.Events; // 이벤트 쓰려고 추가

public class CharacterStats : MonoBehaviour, IDamageable
{
    [Header("Stats")]
    public float maxHealth = 100f;
    public float currentHealth { get; protected set; } // 자식도 수정 가능하게 protected로 변경

    [Header("Events")]
    // 죽었을 때 다른 스크립트들에게 "나 죽었어!"라고 방송하는 이벤트
    public UnityEvent OnDeath; 
    public UnityEvent OnTakeDamage;
    public virtual void Start()
    {
        currentHealth = maxHealth;
    }

    public virtual void TakeDamage(float damage, Transform attacker = null)
    {
        if (currentHealth <= 0) return; // 이미 죽었으면 무시

        currentHealth -= damage;
        Debug.Log($"[{gameObject.name}][Stats] 남은 체력: {currentHealth}");
        
        OnTakeDamage?.Invoke(); // 맞았다고 알림 (나중에 피 튀기는 효과 등에 사용)

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    private void Die()
    {
        Debug.Log($"<color=red>{gameObject.name} 사망!</color>");
        OnDeath?.Invoke(); // "나 죽었어!" 방송 송출
    }
}