using System;
using Oculus.Interaction;
using Oculus.Interaction.Input;
using UnityEngine;

/// <summary>
/// No MR, alinha a mesa virtual (com colisor e sem render) com uma mesa de verdade.
///
/// Ao entrar no MR pela primeira vez, ou pelo botão "MESA" do pulso, aparece uma mesa fantasma que segue
/// a mão direita (ou o controle direito): a palma apoiada em cima da mesa real, no meio da borda mais
/// perto do jogador, com os dedos apontando para o outro lado. Segurar a pinça da mão esquerda (ou o
/// gatilho esquerdo, ou o A) confirma; a fantasma some e a mesa virtual passa a ficar ali.
///
/// Quem se move é o rig, não a mesa: assim a arma, as peças e a área segura continuam na mesa virtual, e
/// voltar para o VR é só devolver o rig para onde ele estava (tudo volta para a mesa do VR). O chão
/// invisível do MR vai para a altura do chão de verdade.
/// </summary>
[DisallowMultipleComponent]
public class TableCalibration : MonoBehaviour
{
    [SerializeField]
    private ExperienceModeController _modes;

    [Tooltip("Raiz do rig (OVRCameraRig), que é o que se move para alinhar a mesa.")]
    [SerializeField]
    private Transform _rig;

    [Tooltip("Ponto da mesa virtual que o jogador encosta: meio da borda de cima mais perto dele, com o +Z apontando para o outro lado da mesa.")]
    [SerializeField]
    private Transform _tableReference;

    [Tooltip("Mesa fantasma mostrada enquanto ajusta. Tem que ter um filho com o mesmo ponto de referência.")]
    [SerializeField]
    private Transform _ghost;

    [SerializeField]
    private Transform _ghostReference;

    [Tooltip("Texto de instrução e progresso, preso na fantasma.")]
    [SerializeField]
    private TextMesh _label;

    [Tooltip("Chão invisível do MR: vai para a altura do chão real depois de alinhar.")]
    [SerializeField]
    private Transform _mrFloor;

    [Header("Mão que mede (direita)")]
    [SerializeField, Interface(typeof(IHand))]
    private UnityEngine.Object _hand;

    [SerializeField, Interface(typeof(IController))]
    private UnityEngine.Object _controller;

    [Tooltip("Altura da palma acima do tampo com a mão apoiada, em metros.")]
    [SerializeField]
    private float _palmHeight = 0.02f;

    [Tooltip("Altura do ponto do controle acima do tampo com o controle apoiado, em metros.")]
    [SerializeField]
    private float _controllerHeight = 0.035f;

    [Header("Confirmar (mão esquerda, gatilho esquerdo ou A)")]
    [SerializeField, Interface(typeof(IHand))]
    private UnityEngine.Object _confirmHand;

    [SerializeField, Interface(typeof(IController))]
    private UnityEngine.Object _confirmController;

    [Tooltip("Tempo segurando para confirmar, em segundos.")]
    [SerializeField, Min(0.1f)]
    private float _holdTime = 0.8f;

    /// <summary>Começou ou terminou o ajuste da mesa.</summary>
    public event Action<bool> WhenPlacingChanged = delegate { };

    public bool IsPlacing { get; private set; }

    public bool IsCalibrated { get; private set; }

    private Pose _rigVr;
    private Pose _rigMr;
    private float _held;
    private bool _waitRelease;

    private IHand Hand => _hand as IHand;
    private IController Controller => _controller as IController;
    private IHand ConfirmHand => _confirmHand as IHand;
    private IController ConfirmController => _confirmController as IController;

    protected virtual void Awake()
    {
        _rigVr = new Pose(_rig.position, _rig.rotation);
        _ghost.gameObject.SetActive(false);
    }

    protected virtual void OnEnable()
    {
        _modes.WhenModeChanged += HandleModeChanged;
    }

    protected virtual void OnDisable()
    {
        _modes.WhenModeChanged -= HandleModeChanged;
    }

    /// <summary>Mostra a fantasma e espera o jogador confirmar a posição da mesa real.</summary>
    public void StartPlacing()
    {
        if (_modes.Mode != ExperienceMode.MR)
        {
            return;
        }

        IsPlacing = true;
        _held = 0f;
        // Se a pinça ou o gatilho já estiver apertado (por exemplo, do toque no botão), espera soltar.
        _waitRelease = true;
        _ghost.gameObject.SetActive(true);
        WhenPlacingChanged(true);
    }

    private void StopPlacing()
    {
        if (!IsPlacing)
        {
            return;
        }

        IsPlacing = false;
        _ghost.gameObject.SetActive(false);
        WhenPlacingChanged(false);
    }

    private void HandleModeChanged(ExperienceMode mode)
    {
        if (mode == ExperienceMode.VR)
        {
            // Volta tudo para a mesa do VR: o rig volta para onde estava.
            StopPlacing();
            SetRig(_rigVr);
            return;
        }

        if (IsCalibrated)
        {
            SetRig(_rigMr);
        }
        else
        {
            StartPlacing();
        }
    }

    protected virtual void Update()
    {
        if (!IsPlacing)
        {
            return;
        }

        if (!TryGetMeasuredPose(out Pose measured))
        {
            _label.text = "Mostre a mão direita\n(ou o controle direito)";
            return;
        }

        // A fantasma vai para onde a mão está, com o ponto de referência dela na mão.
        Quaternion turn = measured.rotation * Quaternion.Inverse(_ghostReference.localRotation);
        _ghost.SetPositionAndRotation(measured.position - turn * _ghostReference.localPosition, turn);

        bool pressing = ConfirmPressed();
        if (_waitRelease)
        {
            _waitRelease = pressing;
            pressing = false;
        }

        _held = pressing ? _held + Time.deltaTime : 0f;
        int percent = Mathf.RoundToInt(Mathf.Clamp01(_held / _holdTime) * 100f);
        _label.text = _held > 0f
            ? $"Confirmando... {percent}%"
            : "Apoie a mão direita (ou o controle) na mesa real,\nno meio da borda mais perto de você.\nSegure a pinça esquerda (ou o gatilho esquerdo) para confirmar.";

        if (_held >= _holdTime)
        {
            Align(measured);
        }
    }

    /// <summary>
    /// Move o rig para que o ponto medido na mesa real caia em cima do ponto de referência da mesa virtual.
    /// Só gira em torno do eixo vertical.
    /// </summary>
    private void Align(Pose measured)
    {
        Quaternion turn = Quaternion.Euler(0f, _tableReference.eulerAngles.y - measured.rotation.eulerAngles.y, 0f);
        Vector3 rigPosition = _tableReference.position + turn * (_rig.position - measured.position);
        _rigMr = new Pose(rigPosition, turn * _rig.rotation);
        IsCalibrated = true;
        SetRig(_rigMr);
        StopPlacing();
    }

    private void SetRig(Pose pose)
    {
        _rig.SetPositionAndRotation(pose.position, pose.rotation);
        if (_mrFloor != null)
        {
            // O rig está no nível do chão (Floor Level): o chão real fica na altura dele.
            _mrFloor.position = new Vector3(_mrFloor.position.x, pose.position.y - 0.05f, _mrFloor.position.z);
        }
    }

    /// <summary>Ponto do tampo debaixo da mão direita, com o +Z para o lado dos dedos (só yaw).</summary>
    private bool TryGetMeasuredPose(out Pose pose)
    {
        IHand hand = Hand;
        if (hand != null && hand.IsTrackedDataValid && hand.GetJointPose(HandJointId.HandPalm, out Pose palm))
        {
            Vector3 distal = palm.rotation * Constants.RightDistal;
            pose = new Pose(palm.position + Vector3.down * _palmHeight, YawOnly(distal));
            return true;
        }

        IController controller = Controller;
        if (controller != null && controller.IsConnected && controller.TryGetPose(out Pose grip))
        {
            pose = new Pose(grip.position + Vector3.down * _controllerHeight, YawOnly(grip.rotation * Vector3.forward));
            return true;
        }

        pose = default;
        return false;
    }

    private bool ConfirmPressed()
    {
        IHand hand = ConfirmHand;
        if (hand != null && hand.IsTrackedDataValid && hand.GetIndexFingerIsPinching())
        {
            return true;
        }

        IController left = ConfirmController;
        if (left != null && left.IsConnected && left.ControllerInput.TriggerButton)
        {
            return true;
        }

        IController right = Controller;
        return right != null && right.IsConnected && right.ControllerInput.PrimaryButton;
    }

    private static Quaternion YawOnly(Vector3 forward)
    {
        forward.y = 0f;
        return forward.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(forward.normalized, Vector3.up) : Quaternion.identity;
    }
}
