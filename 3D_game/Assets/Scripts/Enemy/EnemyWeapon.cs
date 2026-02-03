using UnityEngine;

public class EnemyWeapon : Weapon
{
    protected override void Awake()
    {
        base.Awake();
        ownerTag = "Enemy"; // 적 무기라고 자동 설정
    }

    protected override void OnAttackSuccess(Collider victim)
    {
        // 1. 기본 소리/VFX 재생 (부모가 WeaponData에 있는 걸로 알아서 해줌)
        base.OnAttackSuccess(victim);

        // 2. 적 전용 추가 로직이 필요하다면 여기에 작성
        // 예: 적이 플레이어를 때리면 웃음소리 재생?
        // Debug.Log($"<color=red>크크크! {victim.name}을(를) 베었다!</color>");
    }
    
}