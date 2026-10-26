using Oculus.Interaction;
using Oculus.Interaction.HandGrab;
using Oculus.Interaction.Input;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Uma zona de pegada da Glock: um volume próprio (os colliders do seu Rigidbody), os
/// HandGrabInteractables que pegam por ele e um filtro de interactor que só deixa a mão pegar por
/// aqui quando esta é a zona vencedora para aquela mão. Quem decide a vencedora é o
/// <see cref="GlockGripController"/>.
///
/// Zonas com snap têm um HandGrabInteractable por mão, porque cada um só aceita poses de uma mão.
/// </summary>
[DisallowMultipleComponent]
public class GlockGripZone : MonoBehaviour, IGameObjectFilter
{
    public enum OtherHandRule
    {
        Any,
        MustHoldGun,
        MustNotHoldGun,
    }

    /// <summary>Eixo da mão comparado no ângulo de aceitação.</summary>
    public enum HandAxis
    {
        /// <summary>Para onde a palma aponta.</summary>
        Palm,

        /// <summary>Para onde os dedos apontam (do pulso para os nós dos dedos).</summary>
        Fingers,
    }

    [SerializeField]
    private GlockGripController _controller;

    [Tooltip("Todos usam o Rigidbody desta zona. Vazio = os da própria zona e filhos.")]
    [SerializeField]
    private HandGrabInteractable[] _interactables;

    [SerializeField]
    private GlockGripMode _mode = GlockGripMode.Free;

    [Tooltip("Entre as zonas que aceitam a mão, vence a de maior prioridade (empate: a mais próxima).")]
    [SerializeField]
    private int _priority;

    [Tooltip("Exige que a outra mão esteja (ou não) segurando o corpo da arma.")]
    [SerializeField]
    private OtherHandRule _otherHand = OtherHandRule.Any;

    [Tooltip("Folga, em metros, entre o ponto da palma e os volumes da zona.")]
    [SerializeField, Min(0f)]
    private float _palmTolerance = 0.01f;

    [Header("Ângulo de aceitação")]
    [SerializeField]
    private bool _checkAngle;

    [SerializeField]
    private HandAxis _angleAxis = HandAxis.Palm;

    [Tooltip("Para onde o eixo da mão deve apontar, no espaço local desta zona.")]
    [SerializeField, FormerlySerializedAs("_palmDirection")]
    private Vector3 _axisDirection = Vector3.down;

    [SerializeField, Range(0f, 180f)]
    private float _maxAngle = 60f;

    public GlockGripMode Mode => _mode;
    public int Priority => _priority;
    public bool HoldsGun => GlockGripModes.HoldsGun(_mode);

    protected virtual void Awake()
    {
        if (_interactables == null || _interactables.Length == 0)
        {
            _interactables = GetComponentsInChildren<HandGrabInteractable>(true);
        }
    }

    protected virtual void OnEnable()
    {
        foreach (HandGrabInteractable interactable in _interactables)
        {
            interactable.WhenSelectingInteractorAdded.Action += HandleSelectingInteractorAdded;
            interactable.WhenSelectingInteractorRemoved.Action += HandleSelectingInteractorRemoved;
        }
    }

    protected virtual void OnDisable()
    {
        foreach (HandGrabInteractable interactable in _interactables)
        {
            interactable.WhenSelectingInteractorAdded.Action -= HandleSelectingInteractorAdded;
            interactable.WhenSelectingInteractorRemoved.Action -= HandleSelectingInteractorRemoved;
        }
    }

    public bool Filter(GameObject interactorObject)
    {
        var interactor = interactorObject.GetComponent<HandGrabInteractor>();
        return interactor != null && _controller.Resolve(interactor) == this;
    }

    /// <summary>
    /// Se esta zona aceita a mão agora, ignorando as outras zonas. <paramref name="distance"/> é a
    /// distância da palma até o volume (0 dentro dele), usada para desempatar.
    /// </summary>
    internal bool Accepts(HandGrabInteractor interactor, bool otherHandHoldsGun, out float distance)
    {
        distance = float.PositiveInfinity;
        if (!isActiveAndEnabled)
        {
            return false;
        }

        if ((_otherHand == OtherHandRule.MustHoldGun && !otherHandHoldsGun)
            || (_otherHand == OtherHandRule.MustNotHoldGun && otherHandHoldsGun))
        {
            return false;
        }

        distance = DistanceToVolumes(PalmPosition(interactor));
        if (distance > _palmTolerance)
        {
            return false;
        }

        return !_checkAngle || HandAngle(interactor) <= _maxAngle;
    }

    /// <summary>
    /// Ângulo, em graus, entre o eixo da mão (palma ou dedos) e a direção esperada desta zona.
    /// </summary>
    public float HandAngle(HandGrabInteractor interactor)
    {
        IHand hand = interactor.Hand;
        if (hand == null || !hand.GetRootPose(out Pose wrist))
        {
            return 180f;
        }

        bool left = hand.Handedness == Handedness.Left;
        Vector3 axis = _angleAxis == HandAxis.Fingers
            ? (left ? Constants.LeftDistal : Constants.RightDistal)
            : (left ? Constants.LeftPalmar : Constants.RightPalmar);
        return Vector3.Angle(wrist.rotation * axis, transform.TransformDirection(_axisDirection));
    }

    private static Vector3 PalmPosition(HandGrabInteractor interactor)
    {
        Transform palm = interactor.PalmPoint != null ? interactor.PalmPoint : interactor.transform;
        return palm.position;
    }

    private float DistanceToVolumes(Vector3 point)
    {
        Collider[] colliders = _interactables[0].Colliders;
        if (colliders == null)
        {
            return float.PositiveInfinity;
        }

        float best = float.PositiveInfinity;
        foreach (Collider volume in colliders)
        {
            if (!volume.enabled || !volume.gameObject.activeInHierarchy)
            {
                continue;
            }

            best = Mathf.Min(best, Vector3.Distance(volume.ClosestPoint(point), point));
        }

        return best;
    }

    private void HandleSelectingInteractorAdded(HandGrabInteractor interactor)
    {
        _controller.NotifySelected(this, interactor);
    }

    private void HandleSelectingInteractorRemoved(HandGrabInteractor interactor)
    {
        _controller.NotifyUnselected(this, interactor);
    }

    #region Inject

    public void InjectAllGlockGripZone(GlockGripController controller, HandGrabInteractable[] interactables,
        GlockGripMode mode, int priority, OtherHandRule otherHand)
    {
        _controller = controller;
        _interactables = interactables;
        _mode = mode;
        _priority = priority;
        _otherHand = otherHand;
    }

    public void InjectOptionalAngle(HandAxis axis, Vector3 direction, float maxAngle)
    {
        _checkAngle = true;
        _angleAxis = axis;
        _axisDirection = direction;
        _maxAngle = maxAngle;
    }

    #endregion
}
