using UnityEngine;

/// <summary>
/// Som de queda: toca quando o corpo deste objeto (Rigidbody) bate em algo com velocidade, com o
/// volume pela força do impacto. Na mesa (colisor com <see cref="ImpactSurface"/>) toca o som da mesa;
/// no resto (chão), o do chão, ou o da mesa um pouco mais grave se não houver um.
///
/// Fica no mesmo objeto do Rigidbody. Pode ficar num objeto que só ganha o Rigidbody depois (o ferrolho
/// ao sair da arma): sem corpo, não chegam colisões.
/// </summary>
[DisallowMultipleComponent]
public class ImpactSound : MonoBehaviour
{
    [SerializeField]
    private AudioSource _source;

    [SerializeField]
    private AudioClip _onTable;

    [Tooltip("Vazio = o som da mesa, um pouco mais grave.")]
    [SerializeField]
    private AudioClip _onFloor;

    [Tooltip("Velocidade relativa, em m/s, a partir da qual o impacto faz som.")]
    [SerializeField, Min(0f)]
    private float _minSpeed = 0.35f;

    [Tooltip("Velocidade relativa, em m/s, que dá o volume máximo.")]
    [SerializeField, Min(0.01f)]
    private float _fullSpeed = 2.5f;

    [SerializeField, Range(0f, 1f)]
    private float _volume = 1f;

    [Tooltip("Tempo mínimo, em segundos, entre dois sons (quiques seguidos não viram metralhadora).")]
    [SerializeField, Min(0f)]
    private float _cooldown = 0.12f;

    [Tooltip("Variação aleatória do pitch, para o mesmo som não cansar.")]
    [SerializeField, Range(0f, 0.3f)]
    private float _pitchJitter = 0.06f;

    private float _last = float.NegativeInfinity;
    private float _armedAt;

    protected virtual void OnEnable()
    {
        // As peças assentam na mesa logo que a cena começa: isso não é queda.
        _armedAt = Time.time + 0.5f;
    }

    protected virtual void OnCollisionEnter(Collision collision)
    {
        float speed = collision.relativeVelocity.magnitude;
        if (Time.time < _armedAt || speed < _minSpeed || Time.time - _last < _cooldown || _source == null)
        {
            return;
        }

        // Peças da própria arma batendo entre si não contam.
        if (collision.rigidbody != null && collision.rigidbody.transform.root == transform.root)
        {
            return;
        }

        bool table = collision.collider.GetComponentInParent<ImpactSurface>() != null;
        AudioClip clip = table || _onFloor == null ? _onTable : _onFloor;
        if (clip == null)
        {
            return;
        }

        _last = Time.time;
        float pitch = 1f + Random.Range(-_pitchJitter, _pitchJitter);
        _source.pitch = !table && _onFloor == null ? pitch * 0.85f : pitch;
        _source.PlayOneShot(clip, _volume * Mathf.Lerp(0.2f, 1f, Mathf.InverseLerp(_minSpeed, _fullSpeed, speed)));
    }
}
