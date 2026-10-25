using Oculus.Interaction.GrabAPI;
using UnityEngine;

/// <summary>
/// Ajusta os limiares da pegada de palma no hand tracking. O padrão do Interaction SDK fecha a
/// pegada com 0,9 de força (mão quase fechada) e solta com 0,85. Segurando um objeto virtual não
/// há nada contra os dedos: era preciso fechar a mão por completo para pegar, e a pegada caía à
/// toa perto do limite. Aqui uma mão meio fechada já pega, e a soltura fica bem abaixo do início.
/// Fica na raiz do rig de interação.
///
/// Os controles não passam por aqui: neles a pegada de palma segue o botão de grip.
/// </summary>
public class PalmGrabTuning : MonoBehaviour
{
    [Tooltip("Força dos dedos a partir da qual a pegada de palma começa (o padrão do SDK é 0,9).")]
    [SerializeField, Range(0f, 1f)]
    private float _startThreshold = 0.55f;

    [Tooltip("Força dos dedos abaixo da qual a pegada de palma solta (o padrão do SDK é 0,85).")]
    [SerializeField, Range(0f, 1f)]
    private float _releaseThreshold = 0.35f;

    protected virtual void Awake()
    {
        foreach (PalmGrabConfigurableAPI palmGrab in GetComponentsInChildren<PalmGrabConfigurableAPI>(true))
        {
            palmGrab.StartThreshold = _startThreshold;
            palmGrab.ReleaseThreshold = Mathf.Min(_releaseThreshold, _startThreshold);
        }
    }
}
