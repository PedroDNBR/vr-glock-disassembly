using Oculus.Interaction.HandGrab;
using Oculus.Interaction.Input;
using UnityEngine;

/// <summary>
/// Alavanca de desmontagem (a barra que atravessa o frame acima do gatilho). Na Glock ela não
/// gira: desce alguns milímetros no rasgo e volta pela mola ao soltar.
///
/// - Controle: com a mão na alavanca (modo <see cref="GlockGripMode.Lever"/>), o gatilho analógico
///   puxa a alavanca para baixo, proporcionalmente.
/// - Hand tracking: o mesmo pegando a alavanca e dobrando o indicador, ou encostando o polegar e o
///   indicador nas duas pontas dela (uma de cada lado).
///
/// Só desce com o ferrolho um pouco recuado, como na arma real (é o que a pegada de desmontagem faz).
/// </summary>
[DisallowMultipleComponent]
public class GlockTakedownLever : MonoBehaviour
{
    [SerializeField]
    private GlockGripController _grip;

    [SerializeField]
    private GlockSlide _slide;

    [Tooltip("Pivô da alavanca (TakedownLeverPivot).")]
    [SerializeField]
    private Transform _lever;

    [Tooltip("Quanto a alavanca desce, em metros.")]
    [SerializeField, Min(0f)]
    private float _travel = 0.0025f;

    [Tooltip("Recuo mínimo do ferrolho, em metros, para a alavanca descer.")]
    [SerializeField, Min(0f)]
    private float _requiredSlideOffset = 0.002f;

    [Tooltip("Parte do curso (0–1) que conta como alavanca abaixada.")]
    [SerializeField, Range(0f, 1f)]
    private float _downThreshold = 0.9f;

    [Tooltip("Velocidade da alavanca, em cursos por segundo. A volta pela mola usa a mesma.")]
    [SerializeField, Min(0.1f)]
    private float _speed = 10f;

    [Tooltip("Controle: faixa do gatilho analógico que vira curso 0–1.")]
    [SerializeField]
    private Vector2 _inputRange = new Vector2(0.05f, 0.95f);

    [Tooltip("Hand tracking: faixa da curvatura do indicador que vira curso 0–1.")]
    [SerializeField]
    private Vector2 _handInputRange = new Vector2(0.15f, 0.55f);

    [Tooltip("Hand tracking: caixa de toque na ponta da alavanca do lado +X, no espaço da arma. O lado −X é espelhado.")]
    [SerializeField]
    private Bounds _touchBox = new Bounds(new Vector3(0.018f, 0.106f, 0.0042f), new Vector3(0.016f, 0.014f, 0.016f));

    /// <summary>Curso da alavanca, de 0 (em cima) a 1 (abaixada).</summary>
    public float Down01 { get; private set; }

    public bool IsDown => Down01 >= _downThreshold;

    private Vector3 _rest;

    protected virtual void Awake()
    {
        _rest = _lever.localPosition;
    }

    // LateUpdate: o Grabbable da alavanca (pegada sem movimento) a recoloca no lugar no Update.
    protected virtual void LateUpdate()
    {
        float target = Mathf.Max(LeverHandInput(), BothEndsTouched() ? 1f : 0f);
        if (_slide.Offset < _requiredSlideOffset)
        {
            // Com o ferrolho fechado o ressalto do cano prende a alavanca.
            target = 0f;
        }

        Down01 = Mathf.MoveTowards(Down01, target, _speed * Time.deltaTime);
        _lever.localPosition = _rest + Vector3.down * (_travel * Down01);
    }

    private float LeverHandInput()
    {
        for (int i = 0; i < 2; i++)
        {
            Handedness hand = (Handedness)i;
            if (_grip.GetMode(hand) != GlockGripMode.Lever)
            {
                continue;
            }

            HandGrabInteractor interactor = _grip.GetInteractor(hand);
            float strength = interactor.HandGrabApi.GetFingerPalmStrength(HandFinger.Index);
            Vector2 range = interactor.GetComponent<IController>() != null ? _inputRange : _handInputRange;
            return Mathf.InverseLerp(range.x, range.y, strength);
        }

        return 0f;
    }

    private bool BothEndsTouched()
    {
        if (!_grip.IsGunHeld)
        {
            return false;
        }

        Bounds other = new Bounds(Vector3.Scale(_touchBox.center, new Vector3(-1f, 1f, 1f)), _touchBox.size);
        return _grip.IsFingertipIn(_touchBox) && _grip.IsFingertipIn(other);
    }
}
