using Oculus.Interaction.GrabAPI;
using Oculus.Interaction.HandGrab;
using Oculus.Interaction.Input;
using UnityEngine;

/// <summary>
/// Texto de depuração que flutua sobre a Glock com o modo de pegada de cada mão, a zona que a
/// mão pegaria agora, o ângulo da mão em relação a uma zona escolhida e a força da pegada de
/// palma dos dedos. Só para cenas de teste.
/// </summary>
[RequireComponent(typeof(TextMesh))]
public class GlockGripDebugLabel : MonoBehaviour
{
    [SerializeField]
    private GlockGripController _controller;

    [Tooltip("Zona cujo ângulo de aceitação (palma ou dedos) é mostrado, ex.: a de desmontagem.")]
    [SerializeField]
    private GlockGripZone _angleZone;

    [Tooltip("Opcional: mostra o curso do gatilho e o estado do percussor.")]
    [SerializeField]
    private GlockTrigger _trigger;

    [Tooltip("Opcional: mostra o curso do ferrolho e quantas vezes ele foi manobrado.")]
    [SerializeField]
    private GlockSlide _slide;

    [Tooltip("Opcional: mostra se o retém está segurando o ferrolho aberto.")]
    [SerializeField]
    private GlockSlideStop _slideStop;

    [Tooltip("Opcional: mostra o estado do carregador.")]
    [SerializeField]
    private GlockMagazine _magazine;

    [Tooltip("Opcional: mostra o curso da alavanca de desmontagem.")]
    [SerializeField]
    private GlockTakedownLever _lever;

    private int _racks;

    protected virtual void OnEnable()
    {
        if (_slide != null)
        {
            _slide.WhenRacked += HandleRacked;
        }
    }

    protected virtual void OnDisable()
    {
        if (_slide != null)
        {
            _slide.WhenRacked -= HandleRacked;
        }
    }

    private void HandleRacked()
    {
        _racks++;
    }

    [SerializeField]
    private Vector3 _offset = new Vector3(0f, 0.25f, 0f);

    private TextMesh _text;

    protected virtual void Awake()
    {
        _text = GetComponent<TextMesh>();
    }

    protected virtual void LateUpdate()
    {
        transform.position = _controller.transform.position + _offset;

        Camera view = Camera.main;
        if (view != null)
        {
            transform.rotation = Quaternion.LookRotation(transform.position - view.transform.position);
        }

        string text = Line(Handedness.Left, "E") + "\n" + Line(Handedness.Right, "D");
        if (_trigger != null)
        {
            text += "\nGatilho " + _trigger.Pull.ToString("F2") + "  | percussor "
                + (_trigger.IsCocked ? "armado" : "disparado");
        }

        if (_slide != null)
        {
            text += "\nFerrolho " + (_slide.IsRemoved ? "fora da arma" : _slide.Travel01.ToString("F2")
                + (_slide.IsUnlocked ? " (destravado)" : "")) + "  | manobras " + _racks;
            if (_slideStop != null)
            {
                text += "  | " + (_slideStop.IsLocked ? "travado aberto" : "livre");
            }
        }

        if (_magazine != null)
        {
            text += "\nCarregador " + (_magazine.State == GlockMagazine.MagazineState.Seated ? "inserido"
                : _magazine.State == GlockMagazine.MagazineState.InWell ? "no poço" : "fora");
        }

        if (_lever != null)
        {
            text += "  | alavanca " + _lever.Down01.ToString("F2") + (_lever.IsDown ? " (abaixada)" : "");
        }

        _text.text = text;
    }

    private string Line(Handedness hand, string label)
    {
        string line = label + ": " + _controller.GetMode(hand);
        HandGrabInteractor interactor = _controller.GetInteractor(hand);
        if (interactor == null)
        {
            if (!_controller.TryGetCandidate(hand, out interactor, out GlockGripZone zone))
            {
                return line;
            }

            line += "  | mira: " + (zone != null ? zone.Mode.ToString() : "-");
            if (_angleZone != null)
            {
                line += "  | " + _angleZone.Mode + " " + _angleZone.HandAngle(interactor).ToString("F0") + "°";
            }
        }

        // Força da pegada de palma de médio, anelar e mínimo (pega acima do início, solta abaixo da soltura).
        HandGrabAPI api = interactor.HandGrabApi;
        line += "  | M/A/m " + api.GetFingerPalmStrength(HandFinger.Middle).ToString("F2")
            + " " + api.GetFingerPalmStrength(HandFinger.Ring).ToString("F2")
            + " " + api.GetFingerPalmStrength(HandFinger.Pinky).ToString("F2");
        return line;
    }
}
