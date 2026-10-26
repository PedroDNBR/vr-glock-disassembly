using System;
using Oculus.Interaction.HandGrab;
using Oculus.Interaction.Input;
using UnityEngine;

/// <summary>
/// Gatilho da Glock e estado do percussor. Na empunhadura (modo <see cref="GlockGripMode.Handle"/>),
/// o gatilho segue a força do indicador da mão que segura: no controle é o gatilho analógico; no
/// hand tracking, a curvatura do indicador.
///
/// Como na arma real: a trava (a lâmina no meio do gatilho) afunda primeiro e só então o gatilho
/// inteiro gira. Perto do fim do curso o percussor solta. Depois disso:
/// - o gatilho fica atrás ("gatilho morto") até o ferrolho recuar ~1 cm, quando ele volta (reset);
/// - o percussor só é armado quando o ferrolho volta à bateria;
/// - fora da bateria o gatilho não dispara, e para disparar de novo ele precisa voltar até o
///   ponto de reset (quem segura o gatilho ao manobrar precisa soltá-lo um pouco).
/// </summary>
[DisallowMultipleComponent]
public class GlockTrigger : MonoBehaviour
{
    [SerializeField]
    private GlockGripController _grip;

    [SerializeField]
    private Transform _triggerPivot;

    [SerializeField]
    private Transform _safetyPivot;

    [Tooltip("Opcional: com o ferrolho fora da bateria o gatilho não dispara.")]
    [SerializeField]
    private GlockSlide _slide;

    [Header("Curso")]
    [Tooltip("Rotação do gatilho no fim do curso, em graus, no X local do pivô (positivo leva a sapata para trás).")]
    [SerializeField]
    private float _triggerAngle = 12f;

    [Tooltip("Rotação da trava do gatilho quando afundada, em graus.")]
    [SerializeField]
    private float _safetyAngle = 10f;

    [Tooltip("Parte inicial do curso em que só a trava afunda; depois o gatilho inteiro gira.")]
    [SerializeField, Range(0.01f, 0.99f)]
    private float _safetyTravel = 0.15f;

    [Tooltip("Controle: faixa do gatilho analógico que vira curso 0–1. Corta o ruído no começo e o fim do gatilho.")]
    [SerializeField]
    private Vector2 _inputRange = new Vector2(0.05f, 0.95f);

    [Tooltip("Hand tracking: faixa da curvatura do indicador que vira curso 0–1. Meio dedo dobrado já leva o gatilho ao fim.")]
    [SerializeField]
    private Vector2 _handInputRange = new Vector2(0.15f, 0.55f);

    [Tooltip("Velocidade do gatilho, em cursos por segundo. O retorno pela mola usa a mesma.")]
    [SerializeField, Min(0.1f)]
    private float _speed = 12f;

    [Header("Percussor")]
    [Tooltip("Ponto do curso em que o percussor solta.")]
    [SerializeField, Range(0f, 1f)]
    private float _breakPoint = 0.9f;

    [Tooltip("Ponto do curso até onde o gatilho precisa voltar (reset) antes de poder disparar de novo.")]
    [SerializeField, Range(0f, 1f)]
    private float _resetPoint = 0.6f;

    [SerializeField]
    private bool _startCocked = true;

    /// <summary>O percussor soltou (disparo a seco).</summary>
    public event Action WhenStrikerReleased = delegate { };

    /// <summary>O percussor foi armado pelo ferrolho.</summary>
    public event Action WhenStrikerCocked = delegate { };

    public bool IsCocked { get; private set; }

    /// <summary>Curso atual do gatilho, de 0 (solto) a 1 (fim do curso).</summary>
    public float Pull { get; private set; }

    private Quaternion _triggerRest;
    private Quaternion _safetyRest;
    private bool _dead;
    private bool _reset = true;

    protected virtual void Awake()
    {
        _triggerRest = _triggerPivot.localRotation;
        _safetyRest = _safetyPivot.localRotation;
        IsCocked = _startCocked;
        _dead = !_startCocked;
    }

    protected virtual void Update()
    {
        // Gatilho morto fica atrás até o ferrolho recuar o bastante (ResetTrigger).
        float target = _dead ? 1f : ReadInput();
        Pull = Mathf.MoveTowards(Pull, target, _speed * Time.deltaTime);
        if (Pull <= _resetPoint)
        {
            _reset = true;
        }

        bool inBattery = _slide == null || _slide.Travel01 <= 0.01f;
        if (IsCocked && _reset && inBattery && Pull >= _breakPoint)
        {
            IsCocked = false;
            _dead = true;
            _reset = false;
            WhenStrikerReleased();
        }

        float safety = Mathf.Clamp01(Pull / _safetyTravel);
        float trigger = Mathf.Clamp01((Pull - _safetyTravel) / (1f - _safetyTravel));
        _safetyPivot.localRotation = _safetyRest * Quaternion.Euler(safety * _safetyAngle, 0f, 0f);
        _triggerPivot.localRotation = _triggerRest * Quaternion.Euler(trigger * _triggerAngle, 0f, 0f);
    }

    /// <summary>
    /// Solta o gatilho morto para a frente. Chamado pelo <see cref="GlockSlide"/> quando o ferrolho
    /// recua o bastante; o percussor continua solto até o ferrolho voltar.
    /// </summary>
    public void ResetTrigger()
    {
        _dead = false;
    }

    /// <summary>Arma o percussor. Chamado pelo <see cref="GlockSlide"/> quando o ferrolho volta à bateria depois de manobrado.</summary>
    public void Cock()
    {
        if (IsCocked)
        {
            return;
        }

        IsCocked = true;
        WhenStrikerCocked();
    }

    private float ReadInput()
    {
        HandGrabInteractor interactor = HandleInteractor();
        if (interactor == null)
        {
            return 0f;
        }

        // No controle, a força do indicador é o gatilho analógico (ControllerPinchAPI do SDK); no
        // hand tracking, a curvatura do indicador, que precisa de uma faixa própria, mais curta.
        float strength = interactor.HandGrabApi.GetFingerPalmStrength(HandFinger.Index);
        Vector2 range = interactor.GetComponent<IController>() != null ? _inputRange : _handInputRange;
        return Mathf.InverseLerp(range.x, range.y, strength);
    }

    private HandGrabInteractor HandleInteractor()
    {
        if (_grip.GetMode(Handedness.Left) == GlockGripMode.Handle)
        {
            return _grip.GetInteractor(Handedness.Left);
        }

        if (_grip.GetMode(Handedness.Right) == GlockGripMode.Handle)
        {
            return _grip.GetInteractor(Handedness.Right);
        }

        return null;
    }
}
