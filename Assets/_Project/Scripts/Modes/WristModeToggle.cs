using Oculus.Interaction;
using Oculus.Interaction.Input;
using UnityEngine;

/// <summary>
/// Botão de poke na parte de baixo do pulso esquerdo (lado da palma) que troca entre VR e MR.
///
/// Segue o pulso da mão (hand tracking) ou, sem mão, o controle esquerdo. Só aparece, e só aceita o
/// toque, com o lado da palma virado para a cabeça, como quem olha um relógio no lado de dentro do
/// pulso: assim não troca de modo sem querer ao mexer na arma.
///
/// O poke é do Interaction SDK (dedo da mão direita ou ponta do controle direito); o botão em si
/// (PokeInteractable e superfície) fica num filho, montado pelo Tools/Modos.
///
/// No MR aparece embaixo um segundo botão, "MESA", que refaz o alinhamento da mesa
/// (<see cref="TableCalibration"/>).
/// </summary>
[DisallowMultipleComponent]
public class WristModeToggle : MonoBehaviour
{
    [SerializeField]
    private ExperienceModeController _modes;

    [SerializeField, Interface(typeof(IHand))]
    private Object _hand;

    [SerializeField, Interface(typeof(IController))]
    private Object _controller;

    [SerializeField]
    private PokeInteractable _poke;

    [Tooltip("Tudo que some quando o pulso não está virado para a cabeça (botão e visual).")]
    [SerializeField]
    private GameObject _content;

    [Tooltip("Moldura do seletor: muda de cor ao passar e ao apertar o dedo.")]
    [SerializeField]
    private Renderer _frame;

    [Tooltip("Metade do seletor de cada modo: a do modo atual fica acesa.")]
    [SerializeField]
    private Renderer _vrSegment;

    [SerializeField]
    private Renderer _mrSegment;

    [Header("Botão MESA (só no MR)")]
    [SerializeField]
    private TableCalibration _calibration;

    [SerializeField]
    private PokeInteractable _tablePoke;

    [Tooltip("Botão inteiro (poke e visual), ligado só no MR e fora do ajuste.")]
    [SerializeField]
    private GameObject _tableButton;

    [SerializeField]
    private Renderer _tableFrame;

    [Header("Posição na mão (hand tracking, espaço do pulso)")]
    [Tooltip("Quanto o botão sai da pele, para o lado da palma, em metros.")]
    [SerializeField]
    private float _palmarOffset = 0.03f;

    [Tooltip("Quanto o botão fica para o lado do cotovelo (negativo) ou dos dedos (positivo), em metros.")]
    [SerializeField]
    private float _distalOffset = -0.05f;

    [Header("Posição no controle (espaço do controle)")]
    [SerializeField]
    private Vector3 _controllerPosition = new Vector3(0f, -0.04f, -0.09f);

    [Tooltip("Para onde a frente do botão aponta, no espaço do controle.")]
    [SerializeField]
    private Vector3 _controllerOutward = Vector3.down;

    [Tooltip("Para onde fica o topo do texto, no espaço do controle.")]
    [SerializeField]
    private Vector3 _controllerUp = Vector3.forward;

    [Header("Quando aparece")]
    [Tooltip("Cosseno do ângulo entre a frente do botão e a direção da cabeça para ele aparecer.")]
    [SerializeField, Range(-1f, 1f)]
    private float _showFacing = 0.45f;

    [Tooltip("Cosseno para ele sumir de novo (menor que o de aparecer, para não piscar).")]
    [SerializeField, Range(-1f, 1f)]
    private float _hideFacing = 0.25f;

    [Tooltip("Tempo mínimo, em segundos, entre duas trocas.")]
    [SerializeField, Min(0f)]
    private float _cooldown = 0.6f;

    [Header("Cores")]
    [SerializeField]
    private Color _frameColor = new Color(0.12f, 0.13f, 0.15f);

    [SerializeField]
    private Color _frameHoverColor = new Color(0.32f, 0.36f, 0.4f);

    [SerializeField]
    private Color _framePressColor = new Color(0.95f, 0.6f, 0.1f);

    [SerializeField]
    private Color _activeColor = new Color(0.95f, 0.6f, 0.1f);

    [SerializeField]
    private Color _inactiveColor = new Color(0.22f, 0.24f, 0.27f);

    private IHand Hand => _hand as IHand;
    private IController Controller => _controller as IController;

    private bool _shown;
    private float _lastToggle = float.NegativeInfinity;
    private MaterialPropertyBlock _block;
    private InteractableState _state = InteractableState.Normal;
    private InteractableState _tableState = InteractableState.Normal;

    protected virtual void OnEnable()
    {
        if (_poke != null)
        {
            _poke.WhenStateChanged += HandleStateChanged;
        }

        if (_tablePoke != null)
        {
            _tablePoke.WhenStateChanged += HandleTableStateChanged;
        }

        if (_modes != null)
        {
            _modes.WhenModeChanged += HandleModeChanged;
        }

        if (_calibration != null)
        {
            _calibration.WhenPlacingChanged += HandlePlacingChanged;
        }
    }

    protected virtual void OnDisable()
    {
        if (_poke != null)
        {
            _poke.WhenStateChanged -= HandleStateChanged;
        }

        if (_tablePoke != null)
        {
            _tablePoke.WhenStateChanged -= HandleTableStateChanged;
        }

        if (_modes != null)
        {
            _modes.WhenModeChanged -= HandleModeChanged;
        }

        if (_calibration != null)
        {
            _calibration.WhenPlacingChanged -= HandlePlacingChanged;
        }
    }

    protected virtual void Start()
    {
        _block = new MaterialPropertyBlock();
        UpdateColors();
        UpdateTableButton();
        Show(false);
    }

    protected virtual void LateUpdate()
    {
        if (!TryGetPose(out Pose pose, out Vector3 outward))
        {
            Show(false);
            return;
        }

        transform.SetPositionAndRotation(pose.position, pose.rotation);

        Camera head = Camera.main;
        if (head == null)
        {
            Show(true);
            return;
        }

        float facing = Vector3.Dot(outward, (head.transform.position - pose.position).normalized);
        Show(_shown ? facing > _hideFacing : facing > _showFacing);
    }

    /// <summary>
    /// Pose do botão: a frente do botão (−Z local, como a UI do Unity e a superfície de poke) aponta para
    /// fora do pulso, e o +Y local fica para o lado dos dedos.
    /// </summary>
    private bool TryGetPose(out Pose pose, out Vector3 outward)
    {
        IHand hand = Hand;
        if (hand != null && hand.IsTrackedDataValid && hand.GetJointPose(HandJointId.HandWristRoot, out Pose wrist))
        {
            outward = wrist.rotation * Constants.LeftPalmar;
            Vector3 distal = wrist.rotation * Constants.LeftDistal;
            pose = new Pose(wrist.position + outward * _palmarOffset + distal * _distalOffset,
                Quaternion.LookRotation(-outward, distal));
            return true;
        }

        IController controller = Controller;
        if (controller != null && controller.IsConnected && controller.TryGetPose(out Pose grip))
        {
            outward = grip.rotation * _controllerOutward.normalized;
            Vector3 up = grip.rotation * _controllerUp.normalized;
            pose = new Pose(grip.position + grip.rotation * _controllerPosition, Quaternion.LookRotation(-outward, up));
            return true;
        }

        pose = default;
        outward = default;
        return false;
    }

    private void Show(bool shown)
    {
        if (_content != null && _content.activeSelf != shown)
        {
            _content.SetActive(shown);
        }

        _shown = shown;
    }

    private void HandleStateChanged(InteractableStateChangeArgs args)
    {
        _state = args.NewState;
        if (args.NewState == InteractableState.Select && Time.time - _lastToggle >= _cooldown && _modes != null)
        {
            _lastToggle = Time.time;
            _modes.Toggle();
        }

        UpdateColors();
    }

    private void HandleTableStateChanged(InteractableStateChangeArgs args)
    {
        _tableState = args.NewState;
        if (args.NewState == InteractableState.Select && Time.time - _lastToggle >= _cooldown && _calibration != null)
        {
            _lastToggle = Time.time;
            _calibration.StartPlacing();
        }

        UpdateColors();
    }

    private void HandleModeChanged(ExperienceMode mode)
    {
        UpdateColors();
        UpdateTableButton();
    }

    private void HandlePlacingChanged(bool placing)
    {
        UpdateTableButton();
    }

    private void UpdateTableButton()
    {
        if (_tableButton == null)
        {
            return;
        }

        bool show = _modes != null && _modes.Mode == ExperienceMode.MR && (_calibration == null || !_calibration.IsPlacing);
        _tableButton.SetActive(show);
        if (!show)
        {
            _tableState = InteractableState.Normal;
        }
    }

    private void UpdateColors()
    {
        if (_block == null)
        {
            return;
        }

        SetColor(_frame, _state == InteractableState.Select ? _framePressColor
            : _state == InteractableState.Hover ? _frameHoverColor
            : _frameColor);

        bool vr = _modes == null || _modes.Mode == ExperienceMode.VR;
        SetColor(_vrSegment, vr ? _activeColor : _inactiveColor);
        SetColor(_mrSegment, vr ? _inactiveColor : _activeColor);

        SetColor(_tableFrame, _tableState == InteractableState.Select ? _framePressColor
            : _tableState == InteractableState.Hover ? _frameHoverColor
            : _inactiveColor);
    }

    private void SetColor(Renderer target, Color color)
    {
        if (target == null)
        {
            return;
        }

        target.GetPropertyBlock(_block);
        _block.SetColor("_BaseColor", color);
        target.SetPropertyBlock(_block);
    }
}
