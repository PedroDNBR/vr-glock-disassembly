using UnityEngine;

/// <summary>
/// Sons da mecânica da Glock, a partir dos eventos e do curso das peças:
/// - percussor disparando (dry fire);
/// - ferrolho chegando ao fim do curso (puxar) e voltando à bateria (soltar, mais alto quanto mais rápido);
/// - carregador soltando;
/// - ferrolho saindo da arma; mola e cano saindo do ferrolho.
///
/// As quedas na mesa e no chão ficam com o <see cref="ImpactSound"/> de cada peça.
/// </summary>
[DisallowMultipleComponent]
public class GlockSounds : MonoBehaviour
{
    [Tooltip("Fonte na arma (ferrolho e carregador).")]
    [SerializeField]
    private AudioSource _source;

    [Tooltip("Fonte no ferrolho: depois que ele sai da arma, a mola e o cano saem dele, longe da armação.")]
    [SerializeField]
    private AudioSource _slideSource;

    [SerializeField]
    private GlockSlide _slide;

    [SerializeField]
    private GlockMagazine _magazine;

    [SerializeField]
    private GlockTrigger _trigger;

    [SerializeField]
    private GlockSlidePart _spring;

    [SerializeField]
    private GlockSlidePart _barrel;

    [Header("Sons")]
    [SerializeField]
    private AudioClip _dryFire;

    [SerializeField]
    private AudioClip _slidePull;

    [SerializeField]
    private AudioClip _slideRelease;

    [SerializeField]
    private AudioClip _magazineEject;

    [SerializeField]
    private AudioClip _slideRemoved;

    [SerializeField]
    private AudioClip _partRemoved;

    [Header("Ferrolho")]
    [Tooltip("Curso (0–1) em que toca o som de puxar.")]
    [SerializeField, Range(0f, 1f)]
    private float _pullAt = 0.9f;

    [Tooltip("Curso (0–1) em que toca o som de soltar, voltando da traseira.")]
    [SerializeField, Range(0f, 1f)]
    private float _releaseAt = 0.03f;

    [Tooltip("Velocidade de volta do ferrolho, em cursos por segundo, que dá o volume máximo do som de soltar.")]
    [SerializeField, Min(0.1f)]
    private float _releaseFullSpeed = 12f;

    [Tooltip("Pitch da mola saindo, em relação ao som do cano (o mesmo som, um pouco mais agudo).")]
    [SerializeField, Range(0.5f, 2f)]
    private float _springPitch = 1.2f;

    private bool _back;
    private float _lastTravel;

    protected virtual void OnEnable()
    {
        _trigger.WhenStrikerReleased += HandleStrikerReleased;
        _magazine.WhenReleased += HandleMagazineReleased;
        _slide.WhenRemoved += HandleSlideRemoved;
        _spring.WhenRemoved += HandleSpringRemoved;
        _barrel.WhenRemoved += HandleBarrelRemoved;
    }

    protected virtual void OnDisable()
    {
        _trigger.WhenStrikerReleased -= HandleStrikerReleased;
        _magazine.WhenReleased -= HandleMagazineReleased;
        _slide.WhenRemoved -= HandleSlideRemoved;
        _spring.WhenRemoved -= HandleSpringRemoved;
        _barrel.WhenRemoved -= HandleBarrelRemoved;
    }

    protected virtual void Update()
    {
        if (_slide.IsRemoved)
        {
            return;
        }

        float travel = _slide.Travel01;
        float speed = (_lastTravel - travel) / Mathf.Max(Time.deltaTime, 1e-4f);
        _lastTravel = travel;

        if (!_back && travel >= _pullAt)
        {
            _back = true;
            Play(_slidePull, 1f);
        }
        else if (_back && travel <= _releaseAt)
        {
            // Volta devagar com a mão = clique baixo; solta (mola ou retém) = batida forte.
            _back = false;
            Play(_slideRelease, Mathf.Lerp(0.35f, 1f, Mathf.Clamp01(speed / _releaseFullSpeed)));
        }
    }

    private void HandleStrikerReleased()
    {
        Play(_dryFire, 1f);
    }

    private void HandleMagazineReleased()
    {
        Play(_magazineEject, 1f);
    }

    private void HandleSlideRemoved()
    {
        Play(_slideRemoved, 1f);
    }

    private void HandleSpringRemoved()
    {
        Play(_partRemoved, 0.8f, _springPitch, _slideSource);
    }

    private void HandleBarrelRemoved()
    {
        Play(_partRemoved, 1f, 1f, _slideSource);
    }

    private void Play(AudioClip clip, float volume, float pitch = 1f, AudioSource source = null)
    {
        source = source != null ? source : _source;
        if (clip == null || source == null)
        {
            return;
        }

        source.pitch = pitch;
        source.PlayOneShot(clip, volume);
    }
}
