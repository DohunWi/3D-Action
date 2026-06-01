using UnityEngine;

public class AreaNameTrigger : MonoBehaviour
{
    public string areaName = "Area Name";
    private bool _triggered = false;

    private void OnTriggerEnter(Collider other)
    {
        if (_triggered) return;
        if (!other.CompareTag("Player")) return;

        _triggered = true;
        AreaNameUI.Instance?.Show(areaName);
    }
}
