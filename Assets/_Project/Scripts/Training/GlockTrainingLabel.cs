using TMPro;
using UnityEngine;

/// <summary>
/// Texto simples da etapa atual e dos avisos de erro. Provisório até o feedback da E3.
/// </summary>
[RequireComponent(typeof(TMP_Text))]
public class GlockTrainingLabel : MonoBehaviour
{
    [SerializeField]
    private GlockTrainingSteps _steps;

    [Tooltip("Por quanto tempo um aviso de erro fica na tela, em segundos.")]
    [SerializeField, Min(0f)]
    private float _mistakeDuration = 3f;

    private TMP_Text _text;
    private string _mistake;
    private float _mistakeUntil;

    protected virtual void Awake()
    {
        _text = GetComponent<TMP_Text>();
    }

    protected virtual void OnEnable()
    {
        _steps.WhenMistake += HandleMistake;
    }

    protected virtual void OnDisable()
    {
        _steps.WhenMistake -= HandleMistake;
    }

    protected virtual void LateUpdate()
    {
        GlockTrainingSteps.Step step = _steps.Current;
        string text = step == GlockTrainingSteps.Step.Done
            ? GlockTrainingSteps.Describe(step)
            : "Etapa " + ((int)step + 1) + "/8: " + GlockTrainingSteps.Describe(step);
        if (_steps.Progress > 0f)
        {
            text += "  (" + Mathf.RoundToInt(_steps.Progress * 100f) + "%)";
        }

        if (Time.time < _mistakeUntil)
        {
            text += "\n<color=#ff5050>" + _mistake + "</color>";
        }

        _text.text = text;
    }

    private void HandleMistake(string message)
    {
        _mistake = message;
        _mistakeUntil = Time.time + _mistakeDuration;
    }
}
