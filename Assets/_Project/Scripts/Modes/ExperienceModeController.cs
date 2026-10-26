using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum ExperienceMode
{
    VR,
    MR,
}

/// <summary>
/// Troca entre VR e MR.
///
/// - VR: a cena do ambiente (carregada aditiva uma vez só) fica ligada e vira a cena ativa, para o céu,
///   a luz ambiente e o fog virem dela. Passthrough desligado.
/// - MR: os objetos do ambiente são desligados (render, colisores, luzes e volume de pós-processo), o
///   passthrough liga por baixo de tudo e as câmeras limpam com cor transparente.
///
/// Objetos que só valem num modo (a luz da cena de teste, o chão invisível do MR) entram nas listas
/// de cada modo. A mesa com colisor e sem render fica igual nos dois: no VR o ambiente desenha a mesa no
/// mesmo lugar, e no MR ela fica em cima da mesa de verdade.
/// </summary>
[DisallowMultipleComponent]
public class ExperienceModeController : MonoBehaviour
{
    [Tooltip("Cena do ambiente do VR, carregada aditiva. Precisa estar no Build Settings.")]
    [SerializeField]
    private string _environmentScene = "BackgroundIndoor";

    [Tooltip("Camada de passthrough do rig (Underlay). Fica desligada no VR.")]
    [SerializeField]
    private OVRPassthroughLayer _passthrough;

    [Tooltip("Câmeras do rig. Vazio = todas as câmeras filhas do rig do passthrough.")]
    [SerializeField]
    private Camera[] _cameras;

    [Tooltip("Ligados só no VR.")]
    [SerializeField]
    private GameObject[] _vrOnly;

    [Tooltip("Ligados só no MR (por exemplo, uma luz para a arma e um chão invisível com colisor).")]
    [SerializeField]
    private GameObject[] _mrOnly;

    [SerializeField]
    private ExperienceMode _startMode = ExperienceMode.VR;

    [Tooltip("Começa no último modo escolhido, em vez do modo inicial.")]
    [SerializeField]
    private bool _rememberMode = true;

    private const string PrefsKey = "ExperienceMode";

    /// <summary>Mudou o modo (também na primeira aplicação, quando o ambiente termina de carregar).</summary>
    public event Action<ExperienceMode> WhenModeChanged = delegate { };

    public ExperienceMode Mode { get; private set; }

    /// <summary>O ambiente já carregou e o modo já foi aplicado.</summary>
    public bool IsReady { get; private set; }

    private Scene _environment;
    private Scene _main;
    private readonly List<GameObject> _environmentRoots = new List<GameObject>();
    private readonly List<CameraState> _cameraStates = new List<CameraState>();

    private struct CameraState
    {
        public Camera Camera;
        public CameraClearFlags ClearFlags;
        public Color Background;
    }

    protected virtual void Awake()
    {
        _main = gameObject.scene;
        Mode = _rememberMode && PlayerPrefs.HasKey(PrefsKey)
            ? (ExperienceMode)PlayerPrefs.GetInt(PrefsKey)
            : _startMode;

        if ((_cameras == null || _cameras.Length == 0) && _passthrough != null)
        {
            _cameras = _passthrough.GetComponentsInChildren<Camera>(true);
        }

        foreach (Camera cam in _cameras)
        {
            _cameraStates.Add(new CameraState { Camera = cam, ClearFlags = cam.clearFlags, Background = cam.backgroundColor });
        }

        // Até o ambiente carregar, já mostra o passthrough se o modo for MR.
        ApplyRig(Mode);
    }

    protected virtual void Start()
    {
        _environment = SceneManager.GetSceneByName(_environmentScene);
        if (_environment.IsValid() && _environment.isLoaded)
        {
            // Já aberta junto (no editor, com as duas cenas na hierarquia).
            OnEnvironmentLoaded();
            return;
        }

        AsyncOperation load = SceneManager.LoadSceneAsync(_environmentScene, LoadSceneMode.Additive);
        if (load == null)
        {
            Debug.LogError($"Não consegui carregar a cena '{_environmentScene}'. Ela está no Build Settings?", this);
            return;
        }

        load.completed += _ =>
        {
            _environment = SceneManager.GetSceneByName(_environmentScene);
            OnEnvironmentLoaded();
        };
    }

    public void Toggle()
    {
        SetMode(Mode == ExperienceMode.VR ? ExperienceMode.MR : ExperienceMode.VR);
    }

    public void SetMode(ExperienceMode mode)
    {
        if (mode == Mode && IsReady)
        {
            return;
        }

        Mode = mode;
        if (_rememberMode)
        {
            PlayerPrefs.SetInt(PrefsKey, (int)mode);
        }

        Apply();
    }

    private void OnEnvironmentLoaded()
    {
        // Só mexe no que estava ligado na cena do ambiente; o que já vinha desligado continua assim.
        _environmentRoots.Clear();
        foreach (GameObject root in _environment.GetRootGameObjects())
        {
            if (root.activeSelf)
            {
                _environmentRoots.Add(root);
            }
        }

        IsReady = true;
        Apply();
    }

    private void Apply()
    {
        bool vr = Mode == ExperienceMode.VR;
        ApplyRig(Mode);

        if (IsReady)
        {
            foreach (GameObject root in _environmentRoots)
            {
                root.SetActive(vr);
            }

            // Céu, luz ambiente, reflexo e fog vêm da cena ativa.
            SceneManager.SetActiveScene(vr ? _environment : _main);
        }

        SetActive(_vrOnly, vr);
        SetActive(_mrOnly, !vr);
        WhenModeChanged(Mode);
    }

    private void ApplyRig(ExperienceMode mode)
    {
        bool mr = mode == ExperienceMode.MR;
        if (_passthrough != null)
        {
            _passthrough.enabled = mr;
        }

        // No MR o fundo tem que ser transparente para o passthrough aparecer atrás.
        foreach (CameraState state in _cameraStates)
        {
            state.Camera.clearFlags = mr ? CameraClearFlags.SolidColor : state.ClearFlags;
            state.Camera.backgroundColor = mr ? Color.clear : state.Background;
        }
    }

    private static void SetActive(GameObject[] objects, bool active)
    {
        if (objects == null)
        {
            return;
        }

        foreach (GameObject go in objects)
        {
            if (go != null)
            {
                go.SetActive(active);
            }
        }
    }
}
