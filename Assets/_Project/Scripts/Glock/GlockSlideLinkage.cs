using UnityEngine;

/// <summary>
/// Cano e conjunto da mola recuperadora seguindo o curso do ferrolho, sem física.
///
/// Cano (sistema Browning, como na Glock): nos primeiros milímetros recua travado com o ferrolho;
/// depois a culatra desce no bloco de trava e o cano para enquanto o ferrolho segue. O cano sempre
/// passa pelo furo da frente do ferrolho, que recua junto com ele: quanto mais o ferrolho recua,
/// mais perto da culatra fica o furo e mais a boca aponta para cima (com o ferrolho todo atrás,
/// uns 3,5–4°, como na arma real). Mola: a guia fica parada, a luva externa (com a mola externa) acompanha o
/// ferrolho e a mola interna, da flange da guia até a frente da luva, comprime.
///
/// Roda depois do <see cref="GlockSlide"/>, que define o curso do quadro.
/// </summary>
[DefaultExecutionOrder(100)]
[DisallowMultipleComponent]
public class GlockSlideLinkage : MonoBehaviour
{
    [SerializeField]
    private GlockSlide _slide;

    [Tooltip("Pivô do cano (BarrelDriver), sem rotação na posição montada.")]
    [SerializeField]
    private Transform _barrel;

    [Tooltip("Luva externa da mola (OuterSleeve), que acompanha o ferrolho.")]
    [SerializeField]
    private Transform _outerSleeve;

    [Tooltip("Mola interna (InnerSpring), com a origem na ponta de trás; comprime com escala em Z.")]
    [SerializeField]
    private Transform _innerSpring;

    [Header("Cano")]
    [Tooltip("Quanto o cano recua travado com o ferrolho antes de começar a inclinar, em metros.")]
    [SerializeField, Min(0f)]
    private float _lockedTravel = 0.003f;

    [Tooltip("Recuo total do cano, em metros. Daí em diante só o ferrolho anda.")]
    [SerializeField, Min(0f)]
    private float _barrelTravel = 0.0055f;

    [Tooltip("Quanto a culatra desce no bloco de trava, em metros (medido no pivô do cano).")]
    [SerializeField, Min(0f)]
    private float _breechDrop = 0.0035f;

    [Tooltip("Z do furo da frente do ferrolho por onde o cano passa, com o ferrolho fechado, no espaço do pai do pivô do cano.")]
    [SerializeField]
    private float _slideFrontZ = 0.092f;

    [Header("Mola")]
    [Tooltip("Comprimento da mola interna montada, em metros (da flange da guia até a frente da luva).")]
    [SerializeField, Min(0.001f)]
    private float _innerSpringLength = 0.0819f;

    private bool _initialized;
    private Vector3 _barrelRest;
    private Quaternion _barrelRestRotation;
    private Vector3 _sleeveRest;
    private Vector3 _innerSpringRestScale;

    protected virtual void Awake()
    {
        Initialize();
    }

    protected virtual void LateUpdate()
    {
        Apply(_slide.Offset);
    }

    /// <summary>Posiciona cano e mola para um recuo do ferrolho, em metros.</summary>
    public void Apply(float offset)
    {
        Initialize();

        float back = Mathf.Min(offset, _barrelTravel);
        float drop = Mathf.InverseLerp(_lockedTravel, _barrelTravel, offset) * _breechDrop;

        // O pivô do cano desce e o cano gira para continuar passando pelo furo da frente do
        // ferrolho, na altura do eixo do cano. Rotação negativa em X sobe o que fica à frente.
        float span = Mathf.Max(0.01f, (_slideFrontZ - offset) - (_barrelRest.z - back));
        float tilt = Mathf.Atan2(drop, span) * Mathf.Rad2Deg;
        _barrel.localRotation = _barrelRestRotation * Quaternion.Euler(-tilt, 0f, 0f);
        _barrel.localPosition = _barrelRest + new Vector3(0f, -drop, -back);

        _outerSleeve.localPosition = _sleeveRest + Vector3.back * offset;

        Vector3 scale = _innerSpringRestScale;
        scale.z *= Mathf.Max(0.05f, (_innerSpringLength - offset) / _innerSpringLength);
        _innerSpring.localScale = scale;
    }

    private void Initialize()
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;
        _barrelRest = _barrel.localPosition;
        _barrelRestRotation = _barrel.localRotation;
        _sleeveRest = _outerSleeve.localPosition;
        _innerSpringRestScale = _innerSpring.localScale;
    }
}
