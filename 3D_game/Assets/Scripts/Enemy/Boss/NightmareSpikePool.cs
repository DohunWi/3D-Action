using UnityEngine;
using System.Collections.Generic;

public class NightmareSpikePool : MonoBehaviour
{
    public static NightmareSpikePool Instance { get; private set; }

    private readonly Queue<NightmareSpike> _pool = new();
    private Transform _poolRoot;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        transform.SetParent(null);
        DontDestroyOnLoad(gameObject);

        _poolRoot = new GameObject("[NightmareSpike Pool]").transform;
        _poolRoot.SetParent(transform);
    }

    public NightmareSpike Get(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        NightmareSpike spike;
        if (_pool.Count > 0)
            spike = _pool.Dequeue();
        else
        {
            GameObject obj = Instantiate(prefab, _poolRoot);
            spike = obj.GetComponent<NightmareSpike>();
        }

        spike.transform.SetPositionAndRotation(position, rotation);
        spike.gameObject.SetActive(true);
        return spike;
    }

    public void Return(NightmareSpike spike)
    {
        spike.gameObject.SetActive(false);
        spike.transform.SetParent(_poolRoot);
        _pool.Enqueue(spike);
    }

    public void WarmUp(GameObject prefab, int count)
    {
        for (int i = 0; i < count; i++)
        {
            GameObject obj = Instantiate(prefab, _poolRoot);
            obj.SetActive(false);
            _pool.Enqueue(obj.GetComponent<NightmareSpike>());
        }
    }
}
