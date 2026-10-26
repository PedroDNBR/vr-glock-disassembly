using Oculus.Interaction.HandGrab;
using Oculus.Interaction.Input;
using UnityEngine;

/// <summary>
/// Retém do ferrolho. Levantado, segura o ferrolho aberto quando ele vem de trás.
///
/// - Controle: segurar B/Y (numa mão que esteja pegando a arma ou o ferrolho) levanta o retém; se
///   o ferrolho for até o ponto de trava, ele fica aberto. Com o ferrolho travado, B/Y abaixa o
///   retém e o ferrolho vai para a frente.
/// - Puxar o ferrolho travado um pouco mais para trás deixa o retém cair (ao soltar, o ferrolho
///   fecha), a não ser que haja um carregador vazio: o transportador segura o retém em cima.
/// - Com carregador vazio inserido, o ferrolho trava aberto sozinho ao ir para trás.
///
/// No hand tracking, encostar a ponta do polegar ou do indicador no retém (qualquer lado) faz o
/// papel do B/Y: encostado é "segurando", e o toque que começa é "apertou".
/// </summary>
[DisallowMultipleComponent]
public class GlockSlideStop : MonoBehaviour
{
    [SerializeField]
    private GlockGripController _grip;

    [SerializeField]
    private GlockSlide _slide;

    [SerializeField]
    private Transform _leverPivot;

    [Tooltip("Curso do ferrolho (0–1) em que o entalhe encontra o retém.")]
    [SerializeField, Range(0f, 1f)]
    private float _lockTravel = 0.93f;

    [Tooltip("Curso (0–1) que, com o ferrolho travado e seguro, tira o peso do retém e o deixa cair.")]
    [SerializeField, Range(0f, 1f)]
    private float _releaseTravel = 0.98f;

    [Tooltip("Rotação do retém levantado, em graus, no X local do pivô (positivo sobe a aba de trás).")]
    [SerializeField]
    private float _raisedAngle = 4f;

    [Tooltip("Velocidade do retém, em cursos por segundo.")]
    [SerializeField, Min(0.1f)]
    private float _leverSpeed = 20f;

    [Tooltip("Hand tracking: caixa de toque no retém do lado +X, no espaço da arma. O lado −X é espelhado.")]
    [SerializeField]
    private Bounds _touchBox = new Bounds(new Vector3(0.021f, 0.11f, -0.037f), new Vector3(0.018f, 0.02f, 0.024f));

    [Tooltip("Carregador vazio inserido: o transportador levanta o retém. O GlockMagazine controla isso.")]
    [SerializeField]
    private bool _emptyMagazineInserted = true;

    /// <summary>O retém está segurando o ferrolho aberto.</summary>
    public bool IsLocked { get; private set; }

    /// <summary>Curso (0–1) em que o ferrolho fica parado quando travado.</summary>
    public float LockTravel => _lockTravel;

    public bool EmptyMagazineInserted
    {
        get => _emptyMagazineInserted;
        set => _emptyMagazineInserted = value;
    }

    private readonly bool[] _buttonWasDown = new bool[2];
    private bool _touchWasDown;
    private Quaternion _leverRest;
    private float _lever;
    private bool _armed = true;

    protected virtual void Awake()
    {
        _leverRest = _leverPivot.localRotation;
    }

    protected virtual void Update()
    {
        ReadButtons(out bool held, out bool pressed);
        float travel = _slide.Travel01;

        // Só trava quando o ferrolho chega ao entalhe vindo da frente.
        if (travel < _lockTravel - 0.05f)
        {
            _armed = true;
        }

        if (IsLocked)
        {
            bool slingshot = _slide.IsHeld && travel >= _releaseTravel && !_emptyMagazineInserted;
            if (pressed || slingshot)
            {
                IsLocked = false;
                _armed = false;
            }
        }
        else if (_armed && travel >= _lockTravel && (held || _emptyMagazineInserted))
        {
            IsLocked = true;
            _armed = false;
        }

        _lever = Mathf.MoveTowards(_lever, IsLocked ? 1f : 0f, _leverSpeed * Time.deltaTime);
        _leverPivot.localRotation = _leverRest * Quaternion.Euler(_lever * _raisedAngle, 0f, 0f);
    }

    /// <summary>
    /// B/Y de cada controle cuja mão esteja pegando a arma ou o ferrolho: <paramref name="held"/> se
    /// algum está apertado, <paramref name="pressed"/> se algum foi apertado neste quadro.
    /// </summary>
    private void ReadButtons(out bool held, out bool pressed)
    {
        held = false;
        pressed = false;
        for (int i = 0; i < 2; i++)
        {
            Handedness hand = (Handedness)i;
            HandGrabInteractor interactor = _grip.GetMode(hand) != GlockGripMode.None ? _grip.GetInteractor(hand) : null;
            var controller = interactor != null ? interactor.GetComponent<IController>() : null;
            bool down = controller != null && controller.ControllerInput.SecondaryButton;

            held |= down;
            pressed |= down && !_buttonWasDown[i];
            _buttonWasDown[i] = down;
        }

        // Hand tracking: dedo no retém, de qualquer lado, com a arma na mão.
        Bounds mirrored = new Bounds(Vector3.Scale(_touchBox.center, new Vector3(-1f, 1f, 1f)), _touchBox.size);
        bool touching = _grip.IsGunHeld && (_grip.IsFingertipIn(_touchBox) || _grip.IsFingertipIn(mirrored));
        held |= touching;
        pressed |= touching && !_touchWasDown;
        _touchWasDown = touching;
    }
}
