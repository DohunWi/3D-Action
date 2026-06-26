using UnityEngine;
using System.Collections;

public class NightmareSpike : MonoBehaviour, IDamageable
{
    [Header("--- Settings ---")]
    public float launchSpeed = 40f;
    public float damage = 100f;
    public float composureDamage = 150f; // 공중 격추를 위해 높은 강인도 데미지 설정

    [Header("--- SFX ---")]
    public AudioClip impactClip; // 보스에 박힐 때 효과음

    public BossHurtbox targetHurtbox;
    private bool _isLaunched = false;
    private bool _isReady = false;

    public void Setup(BossHurtbox target)
    {
        targetHurtbox = target;
        StartCoroutine(ReadyRoutine());
    }

    private IEnumerator ReadyRoutine()
    {
        // 바닥에 생성된 후 잠시 대기 (연출용)
        yield return new WaitForSeconds(1.0f);
        _isReady = true;
        // 여기서 반짝이는 이펙트를 켜서 "나를 쳐라"라는 신호를 줄 수 있습니다.
    }

    // IDamageable 구현: 플레이어가 이 쐐기를 때리면 드래곤에게 발사됨
    public void TakeDamage(float damage, float composureDamage, Transform attacker)
    {
        if (!_isReady || _isLaunched) return;
        Launch();
    }

    public void Launch()
    {
        _isLaunched = true;
        StopAllCoroutines();
        StartCoroutine(MoveToBoss());
    }

    private IEnumerator MoveToBoss()
    {
        // 타겟 Hurtbox가 파괴되거나 없어질 경우를 대비해 null 체크
        while (targetHurtbox != null)
        {
            Vector3 targetPos = targetHurtbox.transform.position;
            transform.position = Vector3.MoveTowards(transform.position, targetPos, launchSpeed * Time.deltaTime);
            transform.LookAt(targetPos);

            if (Vector3.Distance(transform.position, targetPos) < 0.5f)
            {
                HitBoss();
                yield break;
            }
            yield return null;
        }
        // 만약 타겟이 사라졌다면 풀에 반환
        ReturnToPool();
    }

    private void HitBoss()
    {
        if (targetHurtbox != null)
        {
            targetHurtbox.TakeDamage(damage, composureDamage, null);
            SoundManager.Instance?.PlaySFX(impactClip, transform.position);
        }
        ReturnToPool();
    }

    private void ReturnToPool()
    {
        if (NightmareSpikePool.Instance != null)
            NightmareSpikePool.Instance.Return(this);
        else
            Destroy(gameObject);
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        _isLaunched = false;
        _isReady = false;
        targetHurtbox = null;
    }
}