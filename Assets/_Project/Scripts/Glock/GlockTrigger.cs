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
/// inteiro gira. Perto do fim do curso o percussor solta. Depois disso o gatilho fica atrás
/// ("gatilho morto") até o ferrolho ser manobrado, o que arma o percussor de novo.
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

    [Tooltip("Ferrolho. Recuar além de Cock Travel arma o percussor (provisório até a D1).")]
    [SerializeField]
    private Transform _slide;

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

    [Tooltip("Faixa da força do indicador que vira curso 0–1. Corta o ruído do dedo esticado e o fim do gatilho do controle.")]
    [SerializeField]
    private Vector2 _inputRange = new Vector2(0.05f, 0.95f);

    [Tooltip("Velocidade do gatilho, em cursos por segundo. O retorno pela mola usa a mesma.")]
    [SerializeField, Min(0.1f)]
    private float _speed = 12f;

    [Header("Percussor")]
    [Tooltip("Ponto do curso em que o percussor solta.")]
    [SerializeField, Range(0f, 1f)]
    private float _breakPoint = 0.9f;

    [Tooltip("Recuo do ferrolho, em metros, que arma o percussor.")]
    [SerializeField, Min(0f)]
    private float _cockTravel = 0.02f;

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
    private float _slideRestZ;

    protected virtual void Awake()
    {
        _triggerRest = _triggerPivot.localRotation;
        _safetyRest = _safetyPivot.localRotation;
        if (_slide != null)
        {
            _slideRestZ = _slide.localPosition.z;
        }

        IsCocked = _startCocked;
    }

    protected virtual void Update()
    {
        if (_slide != null && _slideRestZ - _slide.localPosition.z >= _cockTravel)
        {
            Cock();
        }

        // Com o percussor solto, o gatilho fica atrás até o ferrolho ser manobrado.
        float target = IsCocked ? ReadInput() : 1f;
        Pull = Mathf.MoveTowards(Pull, target, _speed * Time.deltaTime);

        if (IsCocked && Pull >= _breakPoint)
        {
            IsCocked = false;
            WhenStrikerReleased();
        }

        float safety = Mathf.Clamp01(Pull / _safetyTravel);
        float trigger = Mathf.Clamp01((Pull - _safetyTravel) / (1f - _safetyTravel));
        _safetyPivot.localRotation = _safetyRest * Quaternion.Euler(safety * _safetyAngle, 0f, 0f);
        _triggerPivot.localRotation = _triggerRest * Quaternion.Euler(trigger * _triggerAngle, 0f, 0f);
    }

    /// <summary>Arma o percussor. Chamado quando o ferrolho é manobrado.</summary>
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

        // No controle, a força do indicador é o gatilho analógico (ControllerPinchAPI do SDK).
        float strength = interactor.HandGrabApi.GetFingerPalmStrength(HandFinger.Index);
        return Mathf.InverseLerp(_inputRange.x, _inputRange.y, strength);
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
