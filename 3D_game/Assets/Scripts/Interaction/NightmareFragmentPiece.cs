using UnityEngine;

// Nightmare Fragment의 조각 하나. Core가 한 대 맞을 때마다 하나씩 Break() 호출.
public class NightmareFragmentPiece : MonoBehaviour
{
    [Header("FX")]
    public GameObject breakVFX;
    public AudioClip breakSound;

    public void Break()
    {
        if (breakVFX != null)
        {
            if (VFXPoolManager.Instance != null)
                VFXPoolManager.Instance.PlayVFX(breakVFX, transform.position, Quaternion.identity);
            else
                Instantiate(breakVFX, transform.position, Quaternion.identity);
        }
        if (breakSound != null && SoundManager.Instance != null)
            SoundManager.Instance.PlaySFX(breakSound, transform.position);

        gameObject.SetActive(false);
    }
}
