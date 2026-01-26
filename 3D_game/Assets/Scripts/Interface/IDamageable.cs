using UnityEngine;

// 인터페이스는 interface로 선언.
public interface IDamageable
{
    // "이 인터페이스를 쓰는 놈은 무조건 TakeDamage 기능을 가지고 있어야 해!" 라는 약속
    void TakeDamage(float damage);
}