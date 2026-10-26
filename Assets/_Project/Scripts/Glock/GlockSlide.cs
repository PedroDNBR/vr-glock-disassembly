using System;
using Oculus.Interaction;
using Oculus.Interaction.HandGrab;
using Oculus.Interaction.Input;
using UnityEngine;

/// <summary>
/// Ferrolho da Glock no trilho. Quem move o ferrolho é o Grabbable dele (arrasto com a outra
/// mão); aqui o curso é limitado e, solto, o ferrolho volta para a frente pela mola.
///
/// Ao voltar à bateria depois de ter ido quase até o fim, arma o percussor, como na Glock real:
/// o percussor é preso pela barra do gatilho na volta do ferrolho.
///
/// Na pegada de desmontagem (modo <see cref="GlockGripMode.Takedown"/>) a mão segura o ferrolho e
/// o polegar empurra a arma para a frente, alguns milímetros: no controle, pelo gatilho analógico
/// ou pelo analógico (eixo Y, para qualquer lado); no hand tracking, pelo movimento do polegar.
/// Por padrão quem anda é a arma, e o ferrolho fica parado na mão.
/// </summary>
[DisallowMultipleComponent]
public class GlockSlide : MonoBehaviour
{
    public enum TakedownControl
    {
        TriggerOrStick,
        Trigger,
        Stick,
    }

    [SerializeField]
    private GlockGripController _grip;

    [SerializeField]
    private Transform _slide;

    [Tooltip("Grabbable do ferrolho, com um OneGrabTranslateTransformer.")]
    [SerializeField]
    private Grabbable _grabbable;

    [SerializeField]
    private OneGrabTranslateTransformer _transformer;

    [Tooltip("Opcional: percussor armado ao manobrar o ferrolho.")]
    [SerializeField]
    private GlockTrigger _trigger;

    [Tooltip("Opcional: retém que segura o ferrolho aberto.")]
    [SerializeField]
    private GlockSlideStop _slideStop;

    [Tooltip("Curso para trás, em metros. Com 45 mm a culatra passa do cartucho de cima do carregador.")]
    [SerializeField, Min(0f)]
    private float _travel = 0.045f;

    [Tooltip("Aceleração da volta pela mola, em m/s². Com 25, o ferrolho volta do fim do curso em uns 60 ms.")]
    [SerializeField, Min(0f)]
    private float _springAcceleration = 25f;

    [Tooltip("Parte do curso que conta como manobrar o ferrolho.")]
    [SerializeField, Range(0f, 1f)]
    private float _rackThreshold = 0.8f;

    [Tooltip("Recuo, em metros, que solta o gatilho morto para a frente (reset). Maior que o recuo da desmontagem, como na arma real.")]
    [SerializeField, Min(0f)]
    private float _triggerResetTravel = 0.01f;

    [Tooltip("Distância da bateria, em metros, que já conta como ferrolho fechado.")]
    [SerializeField, Min(0f)]
    private float _batteryTolerance = 0.001f;

    [Header("Pegada de desmontagem")]
    [Tooltip("Quanto a pegada de desmontagem recua o ferrolho, em metros. Na arma real são poucos milímetros, o bastante para soltar a alavanca.")]
    [SerializeField, Min(0f)]
    private float _takedownTravel = 0.008f;

    [Tooltip("No controle: gatilho analógico, analógico (eixo Y) ou o maior dos dois.")]
    [SerializeField]
    private TakedownControl _takedownControl = TakedownControl.TriggerOrStick;

    [Tooltip("Faixa da entrada do controle (gatilho ou analógico) que vira recuo 0–1.")]
    [SerializeField]
    private Vector2 _takedownInputRange = new Vector2(0.05f, 0.95f);

    [Tooltip("Hand tracking: faixa do movimento do polegar (diferença da força dele desde a pegada, para qualquer lado) que vira recuo 0–1.")]
    [SerializeField]
    private Vector2 _thumbInputRange = new Vector2(0.02f, 0.15f);

    [Tooltip("Na pegada de desmontagem, anda a arma para a frente em vez do ferrolho para trás: o ferrolho fica parado na mão, como na pegada real.")]
    [SerializeField]
    private bool _takedownMovesGun = true;

    [Tooltip("Velocidade com que a mão recua o ferrolho, em m/s.")]
    [SerializeField, Min(0f)]
    private float _takedownSpeed = 0.25f;

    /// <summary>O ferrolho foi até perto do fim e voltou à bateria.</summary>
    public event Action WhenRacked = delegate { };

    /// <summary>Curso atual, de 0 (em bateria) a 1 (fim do curso).</summary>
    public float Travel01 => _travel > 0f ? _offset / _travel : 0f;

    /// <summary>Recuo atual do ferrolho, em metros.</summary>
    public float Offset => _offset;

    public bool IsHeld => _grabbable.SelectingPointsCount > 0;

    private Vector3 _rest;
    private float _offset;
    private float _velocity;
    private bool _rackStarted;

    private HandGrabInteractor _takedownHand;
    private float _thumbAtGrab;
    private Vector3 _gunPositionAfterShift;
    private float _gunShift;

    protected virtual void Awake()
    {
        _rest = _slide.localPosition;
        LimitTransformer();
    }

    protected virtual void LateUpdate()
    {
        // O Grabbable já moveu o ferrolho no Update dos interactors; aqui só limita, aplica a mola
        // e o recuo da pegada de desmontagem.
        float offset = _rest.z - _slide.localPosition.z;
        float locked = _slideStop != null && _slideStop.IsLocked ? _slideStop.LockTravel * _travel : 0f;
        float held = Mathf.Max(TakedownInput() * _takedownTravel, locked);
        if (IsHeld)
        {
            _velocity = 0f;
        }
        else if (offset > held)
        {
            // Volta pela mola até onde o retém ou a pegada de desmontagem segura.
            _velocity += _springAcceleration * Time.deltaTime;
            offset -= _velocity * Time.deltaTime;
            if (offset <= held)
            {
                offset = held;
                _velocity = 0f;
            }
        }
        else
        {
            _velocity = 0f;
            offset = Mathf.MoveTowards(offset, held, _takedownSpeed * Time.deltaTime);
        }

        // Com o retém em cima, o ferrolho não passa da trava para a frente.
        _offset = Mathf.Clamp(offset, locked, _travel);

        _slide.localPosition = new Vector3(_rest.x, _rest.y, _rest.z - _offset);
        if (_trigger != null && _offset >= _triggerResetTravel)
        {
            _trigger.ResetTrigger();
        }

        // Com o ferrolho travado aberto quem segura é o retém, não a mão: a arma não anda.
        ShiftGun(_takedownMovesGun && _takedownHand != null && !IsHeld && locked <= 0f ? _offset : 0f);
        UpdateRack();
    }

    /// <summary>
    /// Anda a arma para a frente em <paramref name="shift"/> metros. Como o ferrolho recua o mesmo
    /// tanto, ele fica parado no mundo, na mão. Se o Grabbable não reposicionou a arma neste
    /// quadro, aplica só a diferença, para não acumular.
    /// </summary>
    private void ShiftGun(float shift)
    {
        if (_grip == null)
        {
            return;
        }

        Transform gun = _grip.transform;
        bool repositioned = gun.position != _gunPositionAfterShift;
        float delta = repositioned ? shift : shift - _gunShift;
        if (delta != 0f)
        {
            gun.position += gun.forward * delta;
        }

        _gunShift = shift;
        _gunPositionAfterShift = gun.position;
    }

    private float TakedownInput()
    {
        HandGrabInteractor interactor = TakedownInteractor();
        bool newGrab = interactor != _takedownHand;
        _takedownHand = interactor;
        if (interactor == null)
        {
            return 0f;
        }

        var controller = interactor.GetComponent<IController>();
        if (controller == null)
        {
            // Hand tracking: o polegar, apoiado no beavertail, empurra a arma. Vale o quanto ele se
            // mexeu desde a pegada, para qualquer lado.
            float thumb = interactor.HandGrabApi.GetFingerPalmStrength(HandFinger.Thumb);
            if (newGrab)
            {
                _thumbAtGrab = thumb;
            }

            return Mathf.InverseLerp(_thumbInputRange.x, _thumbInputRange.y, Mathf.Abs(thumb - _thumbAtGrab));
        }

        // Controle: a força do indicador é o gatilho analógico (ControllerPinchAPI do SDK).
        float trigger = _takedownControl != TakedownControl.Stick
            ? interactor.HandGrabApi.GetFingerPalmStrength(HandFinger.Index)
            : 0f;
        float stick = _takedownControl != TakedownControl.Trigger
            ? Mathf.Abs(controller.ControllerInput.Primary2DAxis.y)
            : 0f;
        return Mathf.InverseLerp(_takedownInputRange.x, _takedownInputRange.y, Mathf.Max(trigger, stick));
    }

    private HandGrabInteractor TakedownInteractor()
    {
        if (_grip == null)
        {
            return null;
        }

        if (_grip.GetMode(Handedness.Left) == GlockGripMode.Takedown)
        {
            return _grip.GetInteractor(Handedness.Left);
        }

        if (_grip.GetMode(Handedness.Right) == GlockGripMode.Takedown)
        {
            return _grip.GetInteractor(Handedness.Right);
        }

        return null;
    }

    private void UpdateRack()
    {
        if (Travel01 >= _rackThreshold)
        {
            _rackStarted = true;
        }
        else if (_rackStarted && _offset <= _batteryTolerance)
        {
            _rackStarted = false;
            if (_trigger != null)
            {
                _trigger.Cock();
            }

            WhenRacked();
        }
    }

    /// <summary>
    /// Deixa o limite do transformer igual ao curso daqui, para o ferrolho não passar do fim
    /// nem no quadro em que é arrastado.
    /// </summary>
    private void LimitTransformer()
    {
        if (_transformer == null)
        {
            return;
        }

        var constraints = new OneGrabTranslateTransformer.OneGrabTranslateConstraints
        {
            ConstraintsAreRelative = false,
            MinX = Locked(_rest.x),
            MaxX = Locked(_rest.x),
            MinY = Locked(_rest.y),
            MaxY = Locked(_rest.y),
            MinZ = Locked(_rest.z - _travel),
            MaxZ = Locked(_rest.z),
        };
        _transformer.Constraints = constraints;
    }

    private static FloatConstraint Locked(float value)
    {
        return new FloatConstraint { Constrain = true, Value = value };
    }
}
