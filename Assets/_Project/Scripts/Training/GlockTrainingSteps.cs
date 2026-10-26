using System;
using UnityEngine;

/// <summary>
/// As 8 etapas guiadas da desmontagem, em ordem. Cada etapa só conta na vez dela; ações fora de
/// ordem (por exemplo, manobrar com o carregador dentro ou disparar antes de inspecionar a câmara)
/// não avançam e geram um aviso (<see cref="WhenMistake"/>).
///
/// As etapas mecânicas vêm dos eventos das peças; as inspeções e a direção segura são detectadas
/// aqui. Fica na cena, porque a área segura e a câmera do jogador são da cena.
/// </summary>
public class GlockTrainingSteps : MonoBehaviour
{
    public enum Step
    {
        RemoveMagazine,
        RackThreeTimes,
        InspectVisual,
        InspectPhysical,
        DryFireSafe,
        Takedown,
        RemoveSpring,
        RemoveBarrel,
        Done,
    }

    [Header("Arma")]
    [SerializeField]
    private GlockGripController _grip;

    [SerializeField]
    private GlockMagazine _magazine;

    [SerializeField]
    private GlockSlide _slide;

    [SerializeField]
    private GlockTrigger _trigger;

    [SerializeField]
    private GlockSlidePart _spring;

    [SerializeField]
    private GlockSlidePart _barrel;

    [Header("Etapa 2: manobrar")]
    [SerializeField, Min(1)]
    private int _racksRequired = 3;

    [Header("Etapas 3 e 4: inspeção da câmara")]
    [Tooltip("Ponto da câmara, no espaço da arma (aparece no Scene com este componente selecionado).")]
    [SerializeField]
    private Vector3 _chamberPoint = new Vector3(0f, 0.13f, -0.035f);

    [Tooltip("Curso do ferrolho (0–1) a partir do qual a câmara conta como aberta.")]
    [SerializeField, Range(0f, 1f)]
    private float _openTravel = 0.85f;

    [Tooltip("Distância máxima da cabeça até a câmara, em metros, para a inspeção visual.")]
    [SerializeField, Min(0f)]
    private float _lookDistance = 0.45f;

    [Tooltip("Ângulo máximo, em graus, entre o olhar e a câmara.")]
    [SerializeField, Range(0f, 90f)]
    private float _lookAngle = 20f;

    [Tooltip("Tempo, em segundos, olhando a câmara aberta.")]
    [SerializeField, Min(0f)]
    private float _lookTime = 1.5f;

    [Tooltip("Caixa de toque da câmara, no espaço da arma: ponta do indicador ou do polegar (mão ou controle).")]
    [SerializeField]
    private Bounds _chamberTouchBox = new Bounds(new Vector3(0f, 0.13f, -0.035f), new Vector3(0.03f, 0.03f, 0.04f));

    [Header("Etapa 5: disparo a seco")]
    [Tooltip("Área segura da cena. O disparo só vale com o cano apontado para ela.")]
    [SerializeField]
    private Collider _safeZone;

    [Tooltip("Boca do cano, no espaço da arma; o cano aponta para +Z.")]
    [SerializeField]
    private Vector3 _muzzlePoint = new Vector3(0f, 0.1244f, 0.097f);

    [Tooltip("Tempo mínimo, em segundos, entre dois avisos iguais.")]
    [SerializeField, Min(0f)]
    private float _mistakeCooldown = 2f;

    /// <summary>Mudou a etapa atual.</summary>
    public event Action<Step> WhenStepChanged = delegate { };

    /// <summary>Ação fora de ordem ou insegura, com a mensagem para o jogador.</summary>
    public event Action<string> WhenMistake = delegate { };

    public Step Current { get; private set; } = Step.RemoveMagazine;

    public int Racks { get; private set; }

    /// <summary>Progresso da etapa atual, de 0 a 1, quando ela tem um (manobras, inspeção visual).</summary>
    public float Progress =>
        Current == Step.RackThreeTimes ? (float)Racks / _racksRequired
        : Current == Step.InspectVisual ? Mathf.Clamp01(_looked / Mathf.Max(0.01f, _lookTime))
        : 0f;

    private float _looked;
    private string _lastMistake;
    private float _lastMistakeTime = float.NegativeInfinity;

    public static string Describe(Step step)
    {
        switch (step)
        {
            case Step.RemoveMagazine: return "Remova o carregador.";
            case Step.RackThreeTimes: return "Manobre o ferrolho 3 vezes.";
            case Step.InspectVisual: return "Trave o ferrolho aberto e olhe a câmara.";
            case Step.InspectPhysical: return "Encoste o dedo na câmara para conferir que está vazia.";
            case Step.DryFireSafe: return "Solte o ferrolho, aponte para a área segura e faça o disparo a seco.";
            case Step.Takedown: return "Empunhadura de desmontagem: recue o ferrolho, puxe a alavanca e tire o ferrolho pela frente.";
            case Step.RemoveSpring: return "Tire o conjunto da mola recuperadora do ferrolho.";
            case Step.RemoveBarrel: return "Tire o cano do ferrolho.";
            default: return "Desmontagem concluída!";
        }
    }

    protected virtual void OnEnable()
    {
        _magazine.WhenReleased += HandleMagazineReleased;
        _magazine.WhenSeated += HandleMagazineSeated;
        _slide.WhenRacked += HandleRacked;
        _slide.WhenRemoved += HandleSlideRemoved;
        _trigger.WhenStrikerReleased += HandleStrikerReleased;
        _spring.WhenRemoved += HandleSpringRemoved;
        _barrel.WhenRemoved += HandleBarrelRemoved;
    }

    protected virtual void OnDisable()
    {
        _magazine.WhenReleased -= HandleMagazineReleased;
        _magazine.WhenSeated -= HandleMagazineSeated;
        _slide.WhenRacked -= HandleRacked;
        _slide.WhenRemoved -= HandleSlideRemoved;
        _trigger.WhenStrikerReleased -= HandleStrikerReleased;
        _spring.WhenRemoved -= HandleSpringRemoved;
        _barrel.WhenRemoved -= HandleBarrelRemoved;
    }

    protected virtual void Update()
    {
        if (Current == Step.InspectVisual)
        {
            _looked = ChamberOpen() && LookingAtChamber() ? _looked + Time.deltaTime : 0f;
            if (_looked >= _lookTime)
            {
                Advance();
            }
        }
        else if (Current == Step.InspectPhysical)
        {
            // Só a mão livre conta: a que segura a arma ou o ferrolho fica perto da câmara e passava a
            // etapa sozinha.
            if (ChamberOpen() && _grip.IsFingertipIn(_chamberTouchBox, true, true))
            {
                Advance();
            }
        }
    }

    private void HandleMagazineReleased()
    {
        if (Current == Step.RemoveMagazine)
        {
            Advance();
        }
    }

    private void HandleMagazineSeated()
    {
        if (Current > Step.RemoveMagazine && Current < Step.Done)
        {
            Mistake("O carregador precisa ficar fora da arma.");
        }
    }

    private void HandleRacked()
    {
        if (Current == Step.RemoveMagazine)
        {
            Mistake("Tire o carregador antes de manobrar o ferrolho.");
        }
        else if (Current == Step.RackThreeTimes)
        {
            if (_magazine.State == GlockMagazine.MagazineState.Seated)
            {
                Mistake("Tire o carregador antes de manobrar o ferrolho.");
                return;
            }

            Racks++;
            if (Racks >= _racksRequired)
            {
                Advance();
            }
        }
    }

    private void HandleStrikerReleased()
    {
        if (Current < Step.DryFireSafe)
        {
            Mistake("Não dispare antes de remover o carregador e inspecionar a câmara.");
        }
        else if (Current == Step.DryFireSafe)
        {
            if (PointingAtSafeZone())
            {
                Advance();
            }
            else
            {
                Mistake("Aponte para a área segura antes de disparar.");
            }
        }
    }

    private void HandleSlideRemoved()
    {
        if (Current == Step.Takedown)
        {
            Advance();
        }
    }

    private void HandleSpringRemoved()
    {
        if (Current == Step.RemoveSpring)
        {
            Advance();
        }
    }

    private void HandleBarrelRemoved()
    {
        if (Current == Step.RemoveBarrel)
        {
            Advance();
        }
    }

    private void Advance()
    {
        Current++;
        _looked = 0f;
        WhenStepChanged(Current);
    }

    private void Mistake(string message)
    {
        if (message == _lastMistake && Time.time - _lastMistakeTime < _mistakeCooldown)
        {
            return;
        }

        _lastMistake = message;
        _lastMistakeTime = Time.time;
        WhenMistake(message);
    }

    private bool ChamberOpen()
    {
        return !_slide.IsRemoved && _slide.Travel01 >= _openTravel;
    }

    private bool LookingAtChamber()
    {
        Camera view = Camera.main;
        if (view == null)
        {
            return false;
        }

        Vector3 chamber = _grip.transform.TransformPoint(_chamberPoint);
        Vector3 toChamber = chamber - view.transform.position;
        return toChamber.magnitude <= _lookDistance
            && Vector3.Angle(view.transform.forward, toChamber) <= _lookAngle;
    }

    private bool PointingAtSafeZone()
    {
        if (_safeZone == null)
        {
            return true;
        }

        Transform gun = _grip.transform;
        var ray = new Ray(gun.TransformPoint(_muzzlePoint), gun.forward);
        return _safeZone.Raycast(ray, out _, 20f);
    }

    protected virtual void OnDrawGizmosSelected()
    {
        if (_grip == null)
        {
            return;
        }

        Gizmos.matrix = _grip.transform.localToWorldMatrix;
        Gizmos.color = new Color(1f, 0.6f, 0f, 0.9f);
        Gizmos.DrawWireCube(_chamberTouchBox.center, _chamberTouchBox.size);
        Gizmos.DrawWireSphere(_chamberPoint, 0.004f);
        Gizmos.color = Color.green;
        Gizmos.DrawLine(_muzzlePoint, _muzzlePoint + Vector3.forward * 0.3f);
    }
}
