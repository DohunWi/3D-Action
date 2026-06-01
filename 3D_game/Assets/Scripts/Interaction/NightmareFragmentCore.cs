using UnityEngine;

// Nightmare Fragment 본체. 콜라이더 1개로 공격을 받고, 한 대 맞을 때마다 조각을 하나씩 부순다.
// 모든 조각이 부서지면 별도의 포탈 오브젝트를 활성화한다 (상호작용으로 이동).
public class NightmareFragmentCore : MonoBehaviour, IDamageable
{
    [Header("Portal (전부 부서지면 활성화)")]
    public GameObject entrancePortal; // Interaction 레이어의 텔레포터 오브젝트 (처음엔 비활성)

    [Header("Narration")]
    [TextArea(1, 3)]
    public string shatterNarration = "The Heart of the Nightmare lies broken.\nBeyond the veil, the Dragon awaits.\nGo — end the dream.";

    [Header("FX")]
    public AudioClip hitSound;

    private NightmareFragmentPiece[] _pieces;
    private int _nextIndex = 0;
    private bool _broken = false;

    private void Start()
    {
        _pieces = GetComponentsInChildren<NightmareFragmentPiece>(true);
        if (entrancePortal != null) entrancePortal.SetActive(false);
    }

    public void TakeDamage(float damage, float poiseDamage, Transform attacker = null)
    {
        if (_broken) return;

        if (hitSound != null && SoundManager.Instance != null)
            SoundManager.Instance.PlaySFX(hitSound, transform.position);

        // 한 대당 조각 하나 파괴
        if (_nextIndex < _pieces.Length)
        {
            _pieces[_nextIndex]?.Break();
            _nextIndex++;
        }

        // 전부 부서지면 포탈 활성화
        if (_nextIndex >= _pieces.Length)
        {
            _broken = true;
            AreaNameUI.Instance?.Show(shatterNarration);
            if (entrancePortal != null) entrancePortal.SetActive(true);

            // 더 이상 타격 대상이 아니므로 콜라이더 제거
            Collider col = GetComponent<Collider>();
            if (col != null) col.enabled = false;
        }
    }
}
