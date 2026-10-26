using System;
using Oculus.Interaction;
using Oculus.Interaction.HandGrab;
using Oculus.Interaction.Input;
using UnityEngine;

/// <summary>
/// Ferrolho da Glock no trilho. O Grabbable dele (arrasto com a outra mão) usa este componente
/// como transformer: na arma, só o deslocamento ao longo do trilho conta; o curso é limitado e,
/// solto, o ferrolho volta para a frente pela mola.
///
/// Ao voltar à bateria depois de ter ido quase até o fim, arma o percussor, como na Glock real:
/// o percussor é preso pela barra do gatilho na volta do ferrolho.
///
/// Na pegada de desmontagem (modo <see cref="GlockGripMode.Takedown"/>) a mão segura o ferrolho e
/// o polegar empurra a arma para a frente, alguns milímetros: no controle, pelo gatilho analógico
/// ou pelo analógico (eixo Y, para qualquer lado); no hand tracking, pelo movimento do polegar.
/// Por padrão quem anda é a arma, e o ferrolho fica parado na mão.
///
/// Desmontagem: com a alavanca abaixada e o percussor disparado, o ferrolho perde o batente da
/// frente. Puxado para a frente até sair dos trilhos (pela outra mão, ou pela própria pegada de
/// desmontagem com a entrada para o lado oposto ao de recuar), vira uma peça solta (com física),
/// levando cano e mola (ver <see cref="GlockSlideLinkage"/>).
/// </summary>
[DisallowMultipleComponent]
public class GlockSlide : MonoBehaviour, ITransformer
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

    [Tooltip("Grabbable do ferrolho. O transformer dele deve ser este componente.")]
    [SerializeField]
    private Grabbable _grabbable;

    [Tooltip("Opcional: percussor armado ao manobrar o ferrolho.")]
    [SerializeField]
    private GlockTrigger _trigger;

    [Tooltip("Opcional: retém que segura o ferrolho aberto.")]
    [SerializeField]
    private GlockSlideStop _slideStop;

    [Tooltip("Opcional: alavanca de desmontagem. Abaixada, com o percussor disparado, deixa o ferrolho sair pela frente.")]
    [SerializeField]
    private GlockTakedownLever _lever;

    [Tooltip("Zona de pegada do ferrolho (arrasto). Quando ele sai da arma, vira pegada de peça solta.")]
    [SerializeField]
    private GlockGripZone _slideZone;

    [Tooltip("Zonas do frame que dependem do ferrolho (desmontagem, pegar pelo ferrolho). Bloqueadas quando ele sai; quem já está pegando continua.")]
    [SerializeField]
    private GlockGripZone[] _frameSlideZones;

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

    [Header("Desmontagem")]
    [Tooltip("Com a alavanca abaixada, quanto a mola joga o ferrolho à frente da bateria ao soltar a pegada de desmontagem, em metros. Daí a alavanca fica presa embaixo.")]
    [SerializeField, Min(0f)]
    private float _popForward = 0.008f;

    [Tooltip("Quanto o ferrolho anda para a frente da bateria, em metros, até sair dos trilhos.")]
    [SerializeField, Min(0f)]
    private float _removeTravel = 0.08f;

    [Tooltip("Até que parte do caminho para sair dos trilhos (0–1) a pegada de desmontagem empurra o ferrolho. O resto é puxando com a mão.")]
    [SerializeField, Range(0f, 1f)]
    private float _takedownPushFraction = 0.6f;

    [Tooltip("Massa do ferrolho solto (com cano e mola), em kg.")]
    [SerializeField, Min(0.01f)]
    private float _removedMass = 0.3f;

    /// <summary>O ferrolho foi até perto do fim e voltou à bateria.</summary>
    public event Action WhenRacked = delegate { };

    /// <summary>O ferrolho saiu dos trilhos e virou peça solta.</summary>
    public event Action WhenRemoved = delegate { };

    public Transform SlideTransform => _slide;

    public bool IsRemoved { get; private set; }

    /// <summary>
    /// Sem o batente da frente: alavanca abaixada com o percussor disparado, ou o ferrolho já à
    /// frente da bateria (aí a alavanca fica presa embaixo).
    /// </summary>
    public bool IsUnlocked => !IsRemoved
        && (_offset < 0f || (_lever != null && _lever.IsDown && (_trigger == null || !_trigger.IsCocked)));

    /// <summary>Curso atual, de 0 (em bateria) a 1 (fim do curso).</summary>
    public float Travel01 => _travel > 0f ? _offset / _travel : 0f;

    /// <summary>Recuo atual do ferrolho, em metros.</summary>
    public float Offset => _offset;

    public bool IsHeld => _grabbable.SelectingPointsCount > 0;

    private IGrabbable _grabbableApi;
    private Pose _grabToSlide;
    private float _handOffset;
    private Rigidbody _body;

    private Vector3 _rest;
    private float _offset;
    private float _velocity;
    private bool _rackStarted;

    private HandGrabInteractor _takedownHand;
    private float _thumbAtGrab;
    private float _retractSign;
    private Vector3 _gunPositionAfterShift;
    private float _gunShift;

    protected virtual void Awake()
    {
        _rest = _slide.localPosition;
    }

    protected virtual void LateUpdate()
    {
        if (IsRemoved)
        {
            // Peça solta: cinemática na mão, com física fora dela. Sem interpolação na mão, que o
            // Grabbable move o transform direto.
            _body.isKinematic = IsHeld;
            _body.interpolation = IsHeld ? RigidbodyInterpolation.None : RigidbodyInterpolation.Interpolate;
            return;
        }

        // A mão (UpdateTransform) já disse onde quer o ferrolho; aqui entram limites, mola, retém
        // e o recuo da pegada de desmontagem.
        float offset = IsHeld ? _handOffset : _offset;
        float locked = _slideStop != null && _slideStop.IsLocked ? _slideStop.LockTravel * _travel : 0f;
        float input = TakedownInput();
        float takedown = Mathf.Max(0f, input) * _takedownTravel;

        // Com a alavanca abaixada, a entrada da pegada de desmontagem para o lado oposto ao de recuar
        // empurra o ferrolho para a frente, até sair dos trilhos.
        float push = input < 0f && IsUnlocked ? -input * _removeTravel * _takedownPushFraction : 0f;

        // Até onde a mola leva o ferrolho: o retém, a pegada de desmontagem ou, com a alavanca
        // abaixada, um pouco à frente da bateria (o ferrolho "pula" para a frente, como na arma real).
        float held = locked > 0f ? locked : takedown > 0f ? takedown
            : IsUnlocked ? -Mathf.Max(_popForward, push) : 0f;
        if (IsHeld)
        {
            _velocity = 0f;
        }
        else if (offset < 0f && offset <= held)
        {
            // Mais à frente que isso (desmontando) a mola não empurra: fica onde foi deixado.
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

        // Com o retém em cima, o ferrolho não passa da trava para a frente; destravado para a
        // desmontagem, pode ir até sair dos trilhos.
        float forwardLimit = locked > 0f ? locked : IsUnlocked ? -_removeTravel : 0f;
        _offset = Mathf.Clamp(offset, forwardLimit, _travel);
        if (_lever != null)
        {
            _lever.Latched = _offset < 0f;
        }

        // À frente da bateria o ferrolho já está saindo: pegar nele é puxar pelo trilho, com ou sem a
        // outra mão segurando a arma, e as zonas que pegariam a arma inteira pelo ferrolho ficam bloqueadas.
        bool forward = _offset < 0f;
        if (_slideZone != null)
        {
            _slideZone.SetMode(GlockGripMode.SlideDrag,
                forward ? GlockGripZone.OtherHandRule.Any : GlockGripZone.OtherHandRule.MustHoldGun);
        }

        foreach (GlockGripZone zone in _frameSlideZones)
        {
            zone.Blocked = forward;
        }

        // Sai na mão de quem puxa.
        if (IsHeld && IsUnlocked && offset <= -_removeTravel)
        {
            Remove();
            return;
        }

        _slide.localPosition = new Vector3(_rest.x, _rest.y, _rest.z - _offset);
        if (_trigger != null && _offset >= _triggerResetTravel)
        {
            _trigger.ResetTrigger();
        }

        // Só enquanto a pegada de desmontagem segura o ferrolho atrás. Travado aberto quem segura é o
        // retém; à frente da bateria (desmontando) o ferrolho já saiu da mão.
        bool handHoldsSlideBack = _takedownMovesGun && _takedownHand != null && !IsHeld && locked <= 0f && _offset > 0f;
        ShiftGun(handHoldsSlideBack ? _offset : 0f);
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

    /// <summary>
    /// Entrada da pegada de desmontagem, de −1 a 1: positivo recua o ferrolho; negativo (só com a
    /// alavanca abaixada) empurra para a frente.
    /// </summary>
    private float TakedownInput()
    {
        HandGrabInteractor interactor = TakedownInteractor();
        bool newGrab = interactor != _takedownHand;
        _takedownHand = interactor;
        if (interactor == null)
        {
            return 0f;
        }

        if (newGrab)
        {
            _retractSign = 0f;
        }

        var controller = interactor.GetComponent<IController>();
        if (controller == null)
        {
            // Hand tracking: o polegar, apoiado no beavertail, empurra a arma. Vale o quanto ele se
            // mexeu desde a pegada.
            float thumb = interactor.HandGrabApi.GetFingerPalmStrength(HandFinger.Thumb);
            if (newGrab)
            {
                _thumbAtGrab = thumb;
            }

            return Signed(thumb - _thumbAtGrab, _thumbInputRange, 0f);
        }

        // Controle: a força do indicador é o gatilho analógico (ControllerPinchAPI do SDK); o
        // gatilho só recua.
        float trigger = _takedownControl != TakedownControl.Stick
            ? Mathf.InverseLerp(_takedownInputRange.x, _takedownInputRange.y,
                interactor.HandGrabApi.GetFingerPalmStrength(HandFinger.Index))
            : 0f;
        float stick = _takedownControl != TakedownControl.Trigger
            ? Signed(controller.ControllerInput.Primary2DAxis.y, _takedownInputRange, -1f)
            : 0f;
        return trigger >= Mathf.Abs(stick) ? trigger : stick;
    }

    /// <summary>
    /// Entrada com sentido. Com a alavanca em cima, qualquer lado recua, e o lado usado passa a ser o
    /// de recuar. Com a alavanca abaixada, o lado oposto vira negativo (empurrar para a frente). Se o
    /// recuo não usou esta entrada (no controle, recuou pelo gatilho), vale
    /// <paramref name="defaultRetractSign"/>: no analógico, puxar recua e empurrar leva para a frente.
    /// </summary>
    private float Signed(float raw, Vector2 range, float defaultRetractSign)
    {
        float amount = Mathf.InverseLerp(range.x, range.y, Mathf.Abs(raw));
        if (amount <= 0f)
        {
            return 0f;
        }

        float sign = Mathf.Sign(raw);
        if (!IsUnlocked)
        {
            if (amount > 0.3f)
            {
                _retractSign = sign;
            }

            return amount;
        }

        float retract = _retractSign != 0f ? _retractSign : defaultRetractSign;
        return retract == 0f || sign == retract ? amount : -amount;
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

    /// <summary>O ferrolho sai dos trilhos: vira peça solta com física, ainda na mão.</summary>
    private void Remove()
    {
        IsRemoved = true;
        ShiftGun(0f);

        // O ferrolho sai encostado no frame: ignora a colisão com tudo que fica na arma. Achado
        // agora (e não serializado) porque o builder recria as caixas de colisão.
        Collider[] gunColliders = _grip.transform.GetComponentsInChildren<Collider>(true);
        Collider[] slideColliders = _slide.GetComponentsInChildren<Collider>(true);
        foreach (Collider slideCollider in slideColliders)
        {
            foreach (Collider gunCollider in gunColliders)
            {
                if (!gunCollider.transform.IsChildOf(_slide))
                {
                    Physics.IgnoreCollision(slideCollider, gunCollider);
                }
            }
        }

        _slide.SetParent(null, true);

        _body = _slide.gameObject.AddComponent<Rigidbody>();
        _body.mass = _removedMass;
        _body.isKinematic = true;
        _body.interpolation = RigidbodyInterpolation.None;
        _body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

        if (_lever != null)
        {
            _lever.Latched = false;
        }

        if (_slideZone != null)
        {
            _slideZone.SetMode(GlockGripMode.Part, GlockGripZone.OtherHandRule.Any);
        }

        foreach (GlockGripZone zone in _frameSlideZones)
        {
            zone.Blocked = true;
        }

        WhenRemoved();
    }

    #region ITransformer

    public void Initialize(IGrabbable grabbable)
    {
        _grabbableApi = grabbable;
    }

    public void BeginTransform()
    {
        Pose grab = _grabbableApi.GrabPoints[0];
        Quaternion inverse = Quaternion.Inverse(grab.rotation);
        _grabToSlide = new Pose(inverse * (_slide.position - grab.position), inverse * _slide.rotation);
        _handOffset = _offset;
    }

    public void UpdateTransform()
    {
        Pose grab = _grabbableApi.GrabPoints[0];
        Vector3 position = grab.position + grab.rotation * _grabToSlide.position;
        if (IsRemoved)
        {
            _slide.SetPositionAndRotation(position, grab.rotation * _grabToSlide.rotation);
            return;
        }

        // No trilho só o deslocamento em Z (no espaço da arma) conta; o LateUpdate limita.
        _handOffset = _rest.z - _slide.parent.InverseTransformPoint(position).z;
    }

    public void EndTransform()
    {
    }

    #endregion
}
