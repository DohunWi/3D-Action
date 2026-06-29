using UnityEngine;
using System.Collections.Generic;

public class Weapon : MonoBehaviour
{
    [Header("Data Source")]
    public WeaponData weaponData; // ★ 여기에 SO를 드래그해서 넣음

    [Header("Runtime Settings")]
    public string ownerTag; 

    [Header("Hitbox")]
    public BoxCollider _collider;
    protected List<Collider> _alreadyHitList = new List<Collider>();
    [HideInInspector] public float damageMultiplier = 1.0f;

    // 이 무기를 소유한 캐릭터의 Transform. transform.root는 씬 정리용 컨테이너
    // (예: "--- Enemies ---")까지 올라가버리므로, 실제 캐릭터(CharacterStats)를 기준으로 잡는다.
    private Transform _ownerTransform;
    protected Transform AttackerTransform => _ownerTransform;
    protected virtual void Awake()
    {
        // 내 몸에 없으면 '자식 오브젝트(뼈)' 뒤져서라도 찾아라!
        if(_collider == null)
            _collider = GetComponentInChildren<BoxCollider>();

        if (_collider == null)
            Debug.LogError($"[Weapon] {gameObject.name}에 콜라이더가 없습니다! 인스펙터를 확인하세요.");

        // 무기를 소유한 캐릭터 본체를 캐싱 (없으면 안전하게 transform.root로 폴백)
        CharacterStats owner = GetComponentInParent<CharacterStats>();
        _ownerTransform = owner != null ? owner.transform : transform.root;
    }
    private void OnEnable()
    {
        // 시작할 땐 판정을 꺼둡니다.
        DisableHitbox();
    }

    private void Start()
    {
        // 자신의 WeaponData에 등록된 VFX를 씬 로드 시 미리 풀에 채워둠
        if (weaponData == null || VFXPoolManager.Instance == null) return;

        if (weaponData.impactVFX != null)
            VFXPoolManager.Instance.WarmUp(weaponData.impactVFX, 3);

        if (weaponData.hitVFX != null)
            VFXPoolManager.Instance.WarmUp(weaponData.hitVFX, 3);
    }
    public virtual float damage 
    {
        get { return (weaponData != null ? weaponData.damage : 0f) * damageMultiplier; }
    }
    // ★ 이제 데미지나 소리는 weaponData에서 꺼내 씁니다.
    protected virtual void OnTriggerEnter(Collider other)
    {
        if (weaponData == null) return;                      // 데이터 없으면 작동 안 함
        if (other.CompareTag(ownerTag)) return;              // 같은 진영(자기 무기 등)
        if (other.transform.root == transform.root) return;  // 자기 자신
        if (_alreadyHitList.Contains(other)) return;         // 이번 스윙에 이미 맞은 대상
        IDamageable target = other.GetComponent<IDamageable>();
        if (target != null)
        {
            // 데이터에서 데미지 가져오기
            target.TakeDamage(this.damage, weaponData.composureDamage, AttackerTransform);
            _alreadyHitList.Add(other);
            OnAttackSuccess(other);
        }
    }
    public void EnableHitbox()
    {
        if (_collider != null) 
        {
            _collider.enabled = true;
            _alreadyHitList.Clear();
             // 소리도 데이터에서 가져와 재생
            if (weaponData.swingSound != null)
            {
                SoundManager.Instance.PlaySFX(weaponData.swingSound, transform.position);
            }
            if (weaponData.impactVFX != null)
                VFXPoolManager.Instance.PlayVFX(weaponData.impactVFX, transform.position + transform.forward, Quaternion.identity);
        }
    }

    public void DisableHitbox()
    {
        if (_collider != null) 
            _collider.enabled = false;
    }
    protected virtual void OnAttackSuccess(Collider victim)
    {
        // 소리도 데이터에서 가져와 재생
        if (weaponData.hitSound != null)
        {
            SoundManager.Instance.PlaySFX(weaponData.hitSound, transform.position);
        }

        // 이펙트도 데이터에서
        if (weaponData.hitVFX != null)
        {
            // (VFX 생성 로직...)
        }
    }
}