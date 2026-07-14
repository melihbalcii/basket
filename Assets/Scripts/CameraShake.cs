using UnityEngine;

/// <summary>Sayı olunca kameraya kısa bir sarsıntı verir (geri bildirim).</summary>
public class CameraShake : MonoBehaviour
{
    Vector3 baseLocal;
    float t, dur, mag;

    void Awake() { baseLocal = transform.localPosition; }

    public void Shake(float duration = 0.18f, float magnitude = 0.12f)
    {
        dur = duration; mag = magnitude; t = duration;
    }

    void LateUpdate()
    {
        if (t > 0f)
        {
            t -= Time.deltaTime;
            float d = Mathf.Clamp01(t / Mathf.Max(dur, 0.0001f));
            Vector2 r = Random.insideUnitCircle * mag * d;
            transform.localPosition = baseLocal + new Vector3(r.x, r.y, 0f);
        }
        else
        {
            transform.localPosition = baseLocal;
        }
    }
}
