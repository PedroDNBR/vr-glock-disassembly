using System;
using Oculus.Interaction;
using UnityEngine;

/// <summary>
/// Peça que sai de dentro do ferrolho já desmontado: a mola recuperadora e, depois dela, o cano.
///
/// - Montada: presa no ferrolho. Só dá para pegar com o ferrolho fora da arma e seguro na outra
///   mão, e com as peças que precisam sair antes já fora.
/// - No caminho: a mão puxa a peça ao longo do caminho de saída (no espaço do ferrolho). Solta
///   antes do fim, ela volta para o lugar. O caminho pode ter três fases, como a mola da Glock:
///   empurrar contra a frente do ferrolho, inclinar em torno de um pivô (a ponta da frente) e
///   puxar para fora. A cada quadro a peça vai para o ponto do caminho em que o lugar pego por
///   ela fica mais perto da mão, então basta puxar naturalmente.
/// - Solta: passou do fim do caminho e virou peça com física, pegável e arremessável.
///
/// É também o transformer do Grabbable da peça.
/// </summary>
[DisallowMultipleComponent]
public class GlockSlidePart : MonoBehaviour, ITransformer
{
    public enum PartState
    {
        Mounted,
        Pulling,
        Free,
    }

    [SerializeField]
    private GlockSlide _slide;

    [SerializeField]
    private Grabbable _grabbable;

    [SerializeField]
    private Rigidbody _rigidbody;

    [SerializeField]
    private GlockGripZone _zone;

    [Tooltip("Peças que precisam sair antes desta (o cano só sai depois da mola).")]
    [SerializeField]
    private GlockSlidePart[] _requires;

    [Header("Caminho de saída (espaço do ferrolho: +Z para a frente, +Y para o topo)")]
    [Tooltip("Fase 1: quanto a peça é empurrada para a frente antes de inclinar, em metros. 0 pula a fase.")]
    [SerializeField, Min(0f)]
    private float _compressTravel;

    [Tooltip("Fase 2: quanto a peça inclina, em graus, com a traseira saindo por baixo do ferrolho. 0 pula a fase.")]
    [SerializeField, Range(0f, 90f)]
    private float _tiltAngle;

    [Tooltip("Pivô da inclinação, no espaço da peça (a ponta da frente, apoiada na frente do ferrolho).")]
    [SerializeField]
    private Vector3 _tiltPivot;

    [Tooltip("Fase 3: direção de saída (a peça sai por baixo do ferrolho).")]
    [SerializeField]
    private Vector3 _exitDirection = Vector3.down;

    [Tooltip("Fase 3: quanto a peça anda na direção de saída, em metros, até se soltar do ferrolho.")]
    [SerializeField, Min(0.001f)]
    private float _exitTravel = 0.025f;

    [Tooltip("Velocidade com que a peça volta para o lugar se for solta no meio do caminho, em caminhos por segundo.")]
    [SerializeField, Min(0.01f)]
    private float _returnRate = 3f;

    public event Action WhenRemoved = delegate { };

    public PartState State { get; private set; } = PartState.Mounted;

    public bool IsRemoved => State == PartState.Free;

    public bool IsHeld => _grabbable.SelectingPointsCount > 0;

    private IGrabbable _grabbableApi;
    private Pose _grabToPart;
    private bool _mountCaptured;
    private Vector3 _mountedPosition;
    private Quaternion _mountedRotation;
    private float _progress;
    private Vector3 _grabLocal;

    protected virtual void Awake()
    {
        // Montada, a peça fica dentro do frame, do ferrolho e perto do carregador: ignora tudo da arma.
        Transform gun = _slide.transform;
        foreach (Collider own in GetComponentsInChildren<Collider>(true))
        {
            foreach (Collider other in gun.GetComponentsInChildren<Collider>(true))
            {
                if (other != own && !other.transform.IsChildOf(transform))
                {
                    Physics.IgnoreCollision(own, other);
                }
            }
        }

        _rigidbody.isKinematic = true;
        _rigidbody.interpolation = RigidbodyInterpolation.None;
    }

    protected virtual void LateUpdate()
    {
        // Só pega com o ferrolho fora da arma, seguro na outra mão, e sem nada na frente desta peça.
        _zone.Blocked = State == PartState.Mounted && !CanBeRemoved();

        if (State == PartState.Free)
        {
            _rigidbody.isKinematic = IsHeld;
            _rigidbody.interpolation = IsHeld ? RigidbodyInterpolation.None : RigidbodyInterpolation.Interpolate;
            return;
        }

        // Dentro do ferrolho (ou da arma) é cinemática e sem interpolação: um corpo interpolado,
        // filho de algo que se move, fica atrasado e torto.
        _rigidbody.isKinematic = true;
        _rigidbody.interpolation = RigidbodyInterpolation.None;

        if (State == PartState.Pulling && !IsHeld)
        {
            _progress = Mathf.MoveTowards(_progress, 0f, _returnRate * Time.deltaTime);
            ApplyPathPose();
            if (_progress <= 0f)
            {
                State = PartState.Mounted;
            }
        }
    }

    private bool CanBeRemoved()
    {
        if (!_slide.IsRemoved || !_slide.IsHeld || transform.parent != _slide.SlideTransform)
        {
            return false;
        }

        foreach (GlockSlidePart part in _requires)
        {
            if (!part.IsRemoved)
            {
                return false;
            }
        }

        return true;
    }

    private void CaptureMount()
    {
        if (_mountCaptured)
        {
            return;
        }

        // A pose montada é a do momento em que o ferrolho saiu e a peça virou filha dele.
        _mountCaptured = true;
        _mountedPosition = transform.localPosition;
        _mountedRotation = transform.localRotation;
    }

    private void ApplyPathPose()
    {
        PathPose(_progress, out Vector3 position, out Quaternion rotation);
        transform.localPosition = position;
        transform.localRotation = rotation;
    }

    /// <summary>
    /// Pose da peça no espaço do ferrolho para um ponto do caminho, de 0 (montada) a 1 (solta).
    /// Inclinar (com o empurrão para a frente junto) ocupa 40% do caminho; puxar, o resto. O
    /// empurrão vai junto com a inclinação, e não antes, para a mão que puxa para trás não emperrar
    /// no começo do caminho.
    /// </summary>
    private void PathPose(float t, out Vector3 position, out Quaternion rotation)
    {
        float tiltEnd = _tiltAngle > 0f || _compressTravel > 0f ? 0.4f : 0f;
        float tilt = tiltEnd > 0f ? Mathf.Clamp01(t / tiltEnd) : 1f;
        float compress = tilt;
        float exit = Mathf.Clamp01((t - tiltEnd) / Mathf.Max(0.0001f, 1f - tiltEnd));

        // Rotação negativa em X leva o que fica atrás do pivô para baixo (para fora do ferrolho).
        Quaternion turn = Quaternion.Euler(-_tiltAngle * tilt, 0f, 0f);
        Vector3 pivot = _mountedPosition + _mountedRotation * _tiltPivot;
        rotation = turn * _mountedRotation;
        position = pivot + turn * (_mountedPosition - pivot)
            + Vector3.forward * (_compressTravel * compress)
            + _exitDirection.normalized * (_exitTravel * exit);
    }

    /// <summary>Onde fica, no espaço do ferrolho, o ponto pego da peça num ponto do caminho.</summary>
    private Vector3 GrabbedPointAt(float t)
    {
        PathPose(t, out Vector3 position, out Quaternion rotation);
        return position + rotation * _grabLocal;
    }

    private void Release()
    {
        State = PartState.Free;
        transform.SetParent(null, true);
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
        _grabToPart = new Pose(inverse * (transform.position - grab.position), inverse * transform.rotation);
        _grabLocal = transform.InverseTransformPoint(grab.position);
        if (State == PartState.Mounted)
        {
            CaptureMount();
            State = PartState.Pulling;
        }
    }

    public void UpdateTransform()
    {
        Pose grab = _grabbableApi.GrabPoints[0];
        Vector3 position = grab.position + grab.rotation * _grabToPart.position;
        Quaternion rotation = grab.rotation * _grabToPart.rotation;

        if (State == PartState.Free)
        {
            transform.SetPositionAndRotation(position, rotation);
            return;
        }

        // Ponto do caminho, perto do atual, em que o lugar pego da peça fica mais perto da mão.
        Vector3 hand = transform.parent.InverseTransformPoint(grab.position);
        float best = _progress;
        float bestDistance = float.PositiveInfinity;
        const int Samples = 24;
        const float Window = 0.12f;
        for (int i = 0; i <= Samples; i++)
        {
            float t = Mathf.Clamp01(_progress - Window + 2f * Window * i / Samples);
            float distance = (GrabbedPointAt(t) - hand).sqrMagnitude;
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = t;
            }
        }

        _progress = best;
        if (_progress >= 1f)
        {
            Release();
            transform.SetPositionAndRotation(position, rotation);
            return;
        }

        ApplyPathPose();
    }

    public void EndTransform()
    {
    }

    #endregion
}
