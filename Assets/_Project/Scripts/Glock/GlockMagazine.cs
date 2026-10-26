using System;
using Oculus.Interaction;
using Oculus.Interaction.HandGrab;
using Oculus.Interaction.Input;
using UnityEngine;

/// <summary>
/// Carregador da Glock. Três estados:
/// - <see cref="MagazineState.Seated"/>: inserido e travado, parte da arma. No controle, A/X numa mão
///   que segura a arma aperta o retém do carregador e ele começa a sair; no hand tracking, encostar
///   a ponta do polegar ou do indicador no retém.
/// - <see cref="MagazineState.InWell"/>: corre no eixo do cabo. Sem mão, desce pelo peso (vazio, ele
///   cai sozinho, como na Gen5); com a mão, segue a projeção dela no eixo. Empurrado até o fim, trava.
/// - <see cref="MagazineState.Free"/>: fora da arma, objeto com física, pegável e arremessável.
///   Segurado perto da boca do poço e alinhado com ele, entra no poço.
///
/// É também o transformer do Grabbable do carregador, para trocar entre pegada livre e trilho.
/// </summary>
[DisallowMultipleComponent]
public class GlockMagazine : MonoBehaviour, ITransformer
{
    public enum MagazineState
    {
        Seated,
        InWell,
        Free,
    }

    [SerializeField]
    private GlockGripController _grip;

    [Tooltip("Opcional: o carregador vazio inserido levanta o retém do ferrolho.")]
    [SerializeField]
    private GlockSlideStop _slideStop;

    [Tooltip("Eixo do poço: +Y local aponta para dentro da arma, ao longo do cabo. Pai do carregador inserido.")]
    [SerializeField]
    private Transform _wellAxis;

    [SerializeField]
    private Rigidbody _rigidbody;

    [SerializeField]
    private HandGrabInteractable _interactable;

    [SerializeField]
    private GlockGripZone _zone;

    [Tooltip("Opcional: botão do retém do carregador, que afunda ao ser apertado.")]
    [SerializeField]
    private Transform _releaseButton;

    [Tooltip("Colliders da arma que o carregador ignora, já que ele fica dentro do cabo.")]
    [SerializeField]
    private Collider[] _gunColliders;

    [Header("Poço")]
    [Tooltip("Quanto o carregador corre no eixo, em metros, até sair do poço.")]
    [SerializeField, Min(0f)]
    private float _exitTravel = 0.12f;

    [Tooltip("Velocidade com que o retém empurra o carregador para fora, em m/s.")]
    [SerializeField, Min(0f)]
    private float _ejectSpeed = 0.3f;

    [Tooltip("Distância do fim, em metros, que já conta como inserido ao empurrar.")]
    [SerializeField, Min(0f)]
    private float _seatTolerance = 0.003f;

    [Tooltip("Quanto do curso o carregador precisa entrar empurrado pela mão para subir o resto sozinho e travar.")]
    [SerializeField, Range(0f, 1f)]
    private float _autoSeatInsertion = 0.55f;

    [Tooltip("Velocidade com que o carregador sobe sozinho até travar, em m/s.")]
    [SerializeField, Min(0.01f)]
    private float _autoSeatSpeed = 0.8f;

    [Header("Encaixe")]
    [Tooltip("Distância máxima, em metros, entre o carregador e o eixo do poço para ele entrar.")]
    [SerializeField, Min(0f)]
    private float _captureRadius = 0.015f;

    [Tooltip("Quanto abaixo da boca do poço, em metros, o carregador ainda é puxado para o trilho.")]
    [SerializeField, Min(0f)]
    private float _captureDepth = 0.02f;

    [Tooltip("Ângulo máximo, em graus, entre o carregador e o poço para ele entrar.")]
    [SerializeField, Range(0f, 90f)]
    private float _captureAngle = 25f;

    [Header("Retém do carregador")]
    [Tooltip("Hand tracking: caixa de toque no retém do carregador, no espaço da arma (aparece no Scene com o componente selecionado). Justa na face do botão.")]
    [SerializeField]
    private Bounds _touchBox = new Bounds(new Vector3(-0.0155f, 0.08f, -0.036f), new Vector3(0.008f, 0.012f, 0.012f));

    [Tooltip("Hand tracking: tempo, em segundos, que o dedo precisa ficar no botão para soltar o carregador. Evita soltar com o dedo só de passagem.")]
    [SerializeField, Min(0f)]
    private float _touchHoldTime = 0.15f;

    [Tooltip("Quanto o botão afunda, em metros, no espaço do pai dele.")]
    [SerializeField]
    private Vector3 _buttonTravel = new Vector3(0.0015f, 0f, 0f);

    public event Action WhenReleased = delegate { };
    public event Action WhenSeated = delegate { };

    public MagazineState State { get; private set; } = MagazineState.Seated;

    private IGrabbable _grabbable;
    private Pose _grabToMagazine;
    private bool _held;

    private Vector3 _seatedPosition;
    private Quaternion _seatedRotation;
    private float _offset;
    private float _velocity;

    private readonly bool[] _buttonWasDown = new bool[2];
    private Vector3 _buttonRest;
    private float _buttonPress;
    private bool _dropGrabPending;
    private float _touchTime;
    private bool _touchFired;
    private bool _autoSeating;

    protected virtual void Awake()
    {
        _seatedPosition = transform.localPosition;
        _seatedRotation = transform.localRotation;
        if (_releaseButton != null)
        {
            _buttonRest = _releaseButton.localPosition;
        }

        foreach (Collider magazineCollider in GetComponentsInChildren<Collider>(true))
        {
            foreach (Collider gunCollider in _gunColliders)
            {
                Physics.IgnoreCollision(magazineCollider, gunCollider);
            }
        }

        Seat();
        SetGrabbable(false);
        _dropGrabPending = false;
    }

    protected virtual void Update()
    {
        bool pressed = ReadReleaseButton(out bool held);
        if (pressed && State == MagazineState.Seated)
        {
            Release();
        }

        if (_releaseButton != null)
        {
            _buttonPress = Mathf.MoveTowards(_buttonPress, held ? 1f : 0f, 20f * Time.deltaTime);
            _releaseButton.localPosition = _buttonRest + _buttonTravel * _buttonPress;
        }
    }

    protected virtual void LateUpdate()
    {
        if (_dropGrabPending)
        {
            // Fora do processamento do Grabbable: desligar a pegada solta a mão que empurrou.
            _dropGrabPending = false;
            SetGrabbable(false);
        }

        if (State == MagazineState.Free)
        {
            // Interpolação só caindo ou arremessado; na mão o Grabbable move o transform direto.
            _rigidbody.interpolation = _held ? RigidbodyInterpolation.None : RigidbodyInterpolation.Interpolate;
            return;
        }

        // Dentro da arma o carregador é cinemático, mesmo que o Grabbable tenha soltado a trava dele,
        // e sem interpolação: um corpo cinemático interpolado, filho de algo que se move (a arma na
        // mão), fica para trás e torto.
        _rigidbody.isKinematic = true;
        _rigidbody.interpolation = RigidbodyInterpolation.None;

        if (State == MagazineState.InWell && _autoSeating)
        {
            _offset = Mathf.MoveTowards(_offset, 0f, _autoSeatSpeed * Time.deltaTime);
            if (_offset <= _seatTolerance)
            {
                Seat();
            }
            else
            {
                ApplyWellPose();
            }
        }
        else if (State == MagazineState.InWell && !_held)
        {
            // Desce pelo peso ao longo do eixo; para cima o peso só o encosta, não trava.
            _velocity += Vector3.Dot(Physics.gravity, -_wellAxis.up) * Time.deltaTime;
            _offset = Mathf.Max(_seatTolerance, _offset + _velocity * Time.deltaTime);
            if (_offset <= _seatTolerance)
            {
                _velocity = 0f;
            }

            if (_offset >= _exitTravel)
            {
                LeaveWell(-_wellAxis.up * _velocity);
                return;
            }

            ApplyWellPose();
        }
    }

    /// <summary>Aperta o retém do carregador: ele destrava e começa a sair.</summary>
    public void Release()
    {
        if (State != MagazineState.Seated)
        {
            return;
        }

        State = MagazineState.InWell;
        _offset = 0f;
        _velocity = _ejectSpeed;
        _dropGrabPending = false;
        _autoSeating = false;
        SetGrabbable(true);
        if (_slideStop != null)
        {
            _slideStop.EmptyMagazineInserted = false;
        }

        WhenReleased();
    }

    private void Seat()
    {
        State = MagazineState.Seated;
        _offset = 0f;
        _velocity = 0f;
        _autoSeating = false;
        _rigidbody.isKinematic = true;
        _rigidbody.interpolation = RigidbodyInterpolation.None;
        if (transform.parent != _wellAxis)
        {
            transform.SetParent(_wellAxis, true);
        }

        ApplyWellPose();
        _dropGrabPending = true;
        if (_slideStop != null)
        {
            _slideStop.EmptyMagazineInserted = true;
        }

        WhenSeated();
    }

    private void EnterWell(float offset)
    {
        State = MagazineState.InWell;
        transform.SetParent(_wellAxis, true);
        _rigidbody.isKinematic = true;
        _rigidbody.interpolation = RigidbodyInterpolation.None;
        _offset = offset;
        _velocity = 0f;
        ApplyWellPose();
    }

    private void LeaveWell(Vector3 velocity)
    {
        State = MagazineState.Free;
        transform.SetParent(null, true);
        if (!_held)
        {
            _rigidbody.isKinematic = false;
            _rigidbody.linearVelocity = velocity;
        }
    }

    private void ApplyWellPose()
    {
        transform.localPosition = _seatedPosition + Vector3.down * _offset;
        transform.localRotation = _seatedRotation;
    }

    private void SetGrabbable(bool grabbable)
    {
        // A ordem importa: desligar o interactable avisa a zona (e o controlador) que a mão soltou.
        _interactable.enabled = grabbable;
        _zone.enabled = grabbable;
    }

    /// <summary>
    /// Se a pose está perto da boca do poço e alinhada com ele; <paramref name="offset"/> é o
    /// quanto o carregador estaria para fora no trilho.
    /// </summary>
    private bool CanEnterWell(Pose pose, out float offset)
    {
        Vector3 local = _wellAxis.InverseTransformPoint(pose.position) - _seatedPosition;
        offset = -local.y;
        bool nearAxis = new Vector2(local.x, local.z).magnitude <= _captureRadius;
        bool atMouth = offset >= _exitTravel - 0.03f && offset <= _exitTravel + _captureDepth;
        bool aligned = Quaternion.Angle(pose.rotation, _wellAxis.rotation * _seatedRotation) <= _captureAngle;
        return nearAxis && atMouth && aligned;
    }

    /// <summary>
    /// A/X dos controles cujas mãos seguram o corpo da arma: <paramref name="held"/> se algum está
    /// apertado; retorna se algum foi apertado neste quadro.
    /// </summary>
    private bool ReadReleaseButton(out bool held)
    {
        held = false;
        bool pressed = false;
        for (int i = 0; i < 2; i++)
        {
            Handedness hand = (Handedness)i;
            HandGrabInteractor interactor = _grip.HoldsGun(hand) ? _grip.GetInteractor(hand) : null;
            var controller = interactor != null ? interactor.GetComponent<IController>() : null;
            bool down = controller != null && controller.ControllerInput.PrimaryButton;

            held |= down;
            pressed |= down && !_buttonWasDown[i];
            _buttonWasDown[i] = down;
        }

        // Hand tracking: dedo parado no retém do carregador por um instante, com a arma na mão.
        bool touching = _grip.IsGunHeld && _grip.IsFingertipIn(_touchBox);
        _touchTime = touching ? _touchTime + Time.deltaTime : 0f;
        if (!touching)
        {
            _touchFired = false;
        }
        else if (!_touchFired && _touchTime >= _touchHoldTime)
        {
            _touchFired = true;
            pressed = true;
        }

        held |= touching;
        return pressed;
    }

    protected virtual void OnDrawGizmosSelected()
    {
        // Caixa de toque do hand tracking, no espaço da arma.
        if (_grip == null)
        {
            return;
        }

        Gizmos.color = new Color(1f, 0.6f, 0f, 0.9f);
        Gizmos.matrix = _grip.transform.localToWorldMatrix;
        Gizmos.DrawWireCube(_touchBox.center, _touchBox.size);
    }

    #region ITransformer

    public void Initialize(IGrabbable grabbable)
    {
        _grabbable = grabbable;
    }

    public void BeginTransform()
    {
        _held = true;
        Pose grab = _grabbable.GrabPoints[0];
        Quaternion inverse = Quaternion.Inverse(grab.rotation);
        _grabToMagazine = new Pose(inverse * (transform.position - grab.position), inverse * transform.rotation);
    }

    public void UpdateTransform()
    {
        Pose grab = _grabbable.GrabPoints[0];
        var target = new Pose(grab.position + grab.rotation * _grabToMagazine.position,
            grab.rotation * _grabToMagazine.rotation);

        switch (State)
        {
            case MagazineState.Free:
                if (CanEnterWell(target, out float entry))
                {
                    EnterWell(Mathf.Min(entry, _exitTravel));
                }
                else
                {
                    transform.SetPositionAndRotation(target.position, target.rotation);
                }

                break;

            case MagazineState.InWell:
                if (_autoSeating)
                {
                    break;
                }

                float offset = _seatedPosition.y - _wellAxis.InverseTransformPoint(target.position).y;
                if (offset <= _exitTravel * (1f - _autoSeatInsertion))
                {
                    // Entrou o bastante: solta da mão e sobe o resto sozinho (LateUpdate).
                    _offset = Mathf.Max(offset, _seatTolerance);
                    _autoSeating = true;
                    _dropGrabPending = true;
                    ApplyWellPose();
                }
                else if (offset >= _exitTravel + _captureDepth + 0.005f)
                {
                    // Puxado para fora além da boca do poço: volta a ser livre, ainda na mão.
                    LeaveWell(Vector3.zero);
                    transform.SetPositionAndRotation(target.position, target.rotation);
                }
                else
                {
                    _offset = offset;
                    ApplyWellPose();
                }

                break;
        }
    }

    public void EndTransform()
    {
        _held = false;
    }

    #endregion
}
