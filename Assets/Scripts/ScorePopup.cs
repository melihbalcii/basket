using UnityEngine;

/// <summary>Sayı olunca havada beliren ve yukarı süzülerek kaybolan "+2" yazısı.</summary>
public class ScorePopup : MonoBehaviour
{
    float life = 0.8f;
    TextMesh tm;

    public static void Spawn(string text, Vector3 worldPos, Color color)
    {
        var go = new GameObject("ScorePopup");
        go.transform.position = worldPos;
        var tm = go.AddComponent<TextMesh>();
        tm.text = text;
        tm.fontStyle = FontStyle.Normal; // font zaten Fredoka Bold; ayrıca kalınlaştırma gerekmez
        tm.characterSize = 0.18f;
        tm.fontSize = 64;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.color = color;
        tm.font = Resources.Load<Font>("Fonts/Fredoka-Bold")
                  ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var mr = go.GetComponent<MeshRenderer>();
        mr.material = tm.font.material;
        mr.sortingOrder = 50;
        go.AddComponent<ScorePopup>();
    }

    void Awake() { tm = GetComponent<TextMesh>(); }

    void Update()
    {
        life -= Time.deltaTime;
        transform.position += Vector3.up * Time.deltaTime * 1.4f;
        if (tm != null)
        {
            var c = tm.color;
            c.a = Mathf.Clamp01(life / 0.8f);
            tm.color = c;
        }
        if (life <= 0f) Destroy(gameObject);
    }
}
