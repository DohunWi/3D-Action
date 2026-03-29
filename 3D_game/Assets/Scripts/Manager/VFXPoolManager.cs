using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// VFX 오브젝트 풀링 매니저
/// Instantiate 대신 PlayVFX()를 사용하면 파티클 재생 후 자동으로 풀에 반환됩니다.
/// ※ 풀링 효과를 위해 VFX 프리팹의 Stop Action은 None 또는 Disable로 설정하세요.
/// </summary>
public class VFXPoolManager : MonoBehaviour
{
    public static VFXPoolManager Instance { get; private set; }

    // prefab -> 재사용 가능한 인스턴스 풀
    private readonly Dictionary<GameObject, Queue<GameObject>> _pools = new();
    // 인스턴스 -> ParticleSystem 캐시 (GetComponent 반복 호출 방지)
    private readonly Dictionary<GameObject, ParticleSystem> _psCache = new();

    private Transform _poolRoot;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        GameObject root = new GameObject("[VFX Pool]");
        root.transform.SetParent(transform);
        _poolRoot = root.transform;
    }

    // --- Public API ---

    /// <summary>
    /// VFX를 풀에서 꺼내 재생합니다. 파티클이 끝나면 자동으로 풀에 반환됩니다.
    /// </summary>
    public void PlayVFX(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        if (prefab == null) return;

        GameObject vfx = GetFromPool(prefab);
        vfx.transform.SetPositionAndRotation(position, rotation);

        ParticleSystem ps = GetCachedPS(vfx);
        if (ps != null)
        {
            ps.Play(true);
            StartCoroutine(ReturnWhenFinished(prefab, vfx, ps));
        }
        else
        {
            StartCoroutine(ReturnAfterDelay(prefab, vfx, 2f));
        }
    }

    /// <summary>
    /// 씬 시작 시 풀을 미리 채워 첫 재생 스파이크를 없앱니다.
    /// </summary>
    public void WarmUp(GameObject prefab, int count)
    {
        if (!_pools.TryGetValue(prefab, out var queue))
        {
            queue = new Queue<GameObject>();
            _pools[prefab] = queue;
        }

        for (int i = 0; i < count; i++)
        {
            queue.Enqueue(CreateInstance(prefab));
        }
    }

    // --- Internal ---

    private GameObject GetFromPool(GameObject prefab)
    {
        if (!_pools.TryGetValue(prefab, out var queue))
        {
            queue = new Queue<GameObject>();
            _pools[prefab] = queue;
        }

        while (queue.Count > 0)
        {
            GameObject obj = queue.Dequeue();
            if (obj != null) return obj;
        }

        return CreateInstance(prefab);
    }

    // 인스턴스 생성 및 초기 정지 상태로 세팅
    private GameObject CreateInstance(GameObject prefab)
    {
        GameObject obj = Instantiate(prefab, _poolRoot);
        ParticleSystem ps = obj.GetComponentInChildren<ParticleSystem>();
        // SetActive 대신 파티클을 정지 상태로 초기화 (오브젝트는 항상 활성 유지)
        ps?.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        _psCache[obj] = ps;
        return obj;
    }

    private ParticleSystem GetCachedPS(GameObject instance)
    {
        if (!_psCache.TryGetValue(instance, out var ps))
        {
            ps = instance.GetComponentInChildren<ParticleSystem>();
            _psCache[instance] = ps;
        }
        return ps;
    }

    private void ReturnToPool(GameObject prefab, GameObject instance)
    {
        ParticleSystem ps = GetCachedPS(instance);
        ps?.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        if (!_pools.TryGetValue(prefab, out var queue))
        {
            queue = new Queue<GameObject>();
            _pools[prefab] = queue;
        }
        queue.Enqueue(instance);
    }

    private IEnumerator ReturnWhenFinished(GameObject prefab, GameObject instance, ParticleSystem ps)
    {
        yield return null;
        yield return new WaitUntil(() => instance == null || ps == null || !ps.IsAlive(true));

        if (instance != null)
            ReturnToPool(prefab, instance);
    }

    private IEnumerator ReturnAfterDelay(GameObject prefab, GameObject instance, float delay)
    {
        yield return new WaitForSeconds(delay);

        if (instance != null)
            ReturnToPool(prefab, instance);
    }
}
