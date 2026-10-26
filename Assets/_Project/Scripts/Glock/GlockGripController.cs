using System;
using Oculus.Interaction.HandGrab;
using Oculus.Interaction.Input;
using UnityEngine;

/// <summary>
/// Fica na raiz da Glock. Para cada mão, escolhe qual <see cref="GlockGripZone"/> vale
/// (estado da outra mão, volume, ângulo e prioridade) e guarda o modo de pegada atual.
///
/// Regras entre as mãos:
/// - pegar o corpo da arma com uma mão solta a outra, se ela também segurava o corpo (troca de mão);
/// - soltar o corpo da arma solta também a mão que segurava uma peça móvel (ferrolho, alavanca);
///   o carregador não conta, porque ele sai da arma.
/// </summary>
[DisallowMultipleComponent]
public class GlockGripController : MonoBehaviour
{
    [Tooltip("Vazio = todas as zonas filhas.")]
    [SerializeField]
    private GlockGripZone[] _zones;

    /// <summary>Disparado quando o modo de pegada de uma mão muda.</summary>
    public event Action<Handedness, GlockGripMode> WhenModeChanged = delegate { };

    private struct HandSlot
    {
        public GlockGripZone Zone;
        public HandGrabInteractor Interactor;

        public HandGrabInteractor ResolvedFor;
        public GlockGripZone Resolved;
        public int ResolvedFrame;
    }

    private readonly HandSlot[] _hands = new HandSlot[2];

    private static readonly HandJointId[] Fingertips = { HandJointId.HandThumbTip, HandJointId.HandIndexTip };

    public bool IsGunHeld => HoldsGun(Handedness.Left) || HoldsGun(Handedness.Right);

    public GlockGripMode GetMode(Handedness hand)
    {
        GlockGripZone zone = _hands[(int)hand].Zone;
        return zone != null ? zone.Mode : GlockGripMode.None;
    }

    public HandGrabInteractor GetInteractor(Handedness hand)
    {
        return _hands[(int)hand].Interactor;
    }

    public bool HoldsGun(Handedness hand)
    {
        GlockGripZone zone = _hands[(int)hand].Zone;
        return zone != null && zone.HoldsGun;
    }

    /// <summary>
    /// A zona que esta mão pegaria agora (<paramref name="zone"/> nula se nenhuma aceita). Só é
    /// atualizada enquanto a mão procura o que pegar, então serve para depurar zonas e ângulos.
    /// </summary>
    public bool TryGetCandidate(Handedness hand, out HandGrabInteractor interactor, out GlockGripZone zone)
    {
        HandSlot slot = _hands[(int)hand];
        bool fresh = slot.ResolvedFor != null && Time.frameCount - slot.ResolvedFrame <= 1;
        interactor = fresh ? slot.ResolvedFor : null;
        zone = fresh ? slot.Resolved : null;
        return fresh;
    }

    /// <summary>
    /// Se a ponta do polegar ou do indicador de alguma mão do hand tracking está dentro da caixa,
    /// no espaço local da arma. É o "botão" dos reténs no hand tracking; mãos de controle ficam de
    /// fora (nelas os reténs usam os botões), a não ser com <paramref name="includeControllers"/>.
    /// Com <paramref name="freeHandsOnly"/>, só contam mãos que não estão pegando nada da arma.
    /// </summary>
    public bool IsFingertipIn(Bounds localBox, bool includeControllers = false, bool freeHandsOnly = false)
    {
        for (int i = 0; i < 2; i++)
        {
            if (freeHandsOnly && _hands[i].Interactor != null)
            {
                continue;
            }

            HandGrabInteractor interactor = TrackedInteractor((Handedness)i);
            if (interactor == null || interactor.Hand == null
                || (!includeControllers && interactor.GetComponent<IController>() != null))
            {
                continue;
            }

            foreach (HandJointId joint in Fingertips)
            {
                if (interactor.Hand.GetJointPose(joint, out Pose tip)
                    && localBox.Contains(transform.InverseTransformPoint(tip.position)))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>O interactor da mão: o que está pegando algo da arma ou, senão, o que está procurando.</summary>
    private HandGrabInteractor TrackedInteractor(Handedness hand)
    {
        HandSlot slot = _hands[(int)hand];
        if (slot.Interactor != null)
        {
            return slot.Interactor;
        }

        return Time.frameCount - slot.ResolvedFrame <= 1 ? slot.ResolvedFor : null;
    }

    protected virtual void Awake()
    {
        if (_zones == null || _zones.Length == 0)
        {
            _zones = GetComponentsInChildren<GlockGripZone>(true);
        }
    }

    /// <summary>
    /// Escolhe a zona vencedora para o interactor. O resultado é guardado por quadro, porque
    /// cada zona chama isto pelo seu filtro.
    /// </summary>
    internal GlockGripZone Resolve(HandGrabInteractor interactor)
    {
        if (interactor.Hand == null)
        {
            return null;
        }

        int index = (int)interactor.Hand.Handedness;
        ref HandSlot slot = ref _hands[index];
        if (slot.ResolvedFrame == Time.frameCount && slot.ResolvedFor == interactor)
        {
            return slot.Resolved;
        }

        GlockGripMode otherHandMode = GetMode((Handedness)(1 - index));
        GlockGripZone best = null;
        float bestDistance = float.PositiveInfinity;
        foreach (GlockGripZone zone in _zones)
        {
            if (!zone.Accepts(interactor, otherHandMode, out float distance))
            {
                continue;
            }

            if (best == null
                || zone.Priority > best.Priority
                || (zone.Priority == best.Priority && distance < bestDistance))
            {
                best = zone;
                bestDistance = distance;
            }
        }

        slot.ResolvedFor = interactor;
        slot.Resolved = best;
        slot.ResolvedFrame = Time.frameCount;
        return best;
    }

    internal void NotifySelected(GlockGripZone zone, HandGrabInteractor interactor)
    {
        if (interactor.Hand == null)
        {
            return;
        }

        int index = (int)interactor.Hand.Handedness;
        HandSlot other = _hands[1 - index];
        if (zone.HoldsGun && other.Zone != null && other.Zone.HoldsGun)
        {
            other.Interactor.ForceRelease();
        }

        SetZone(index, zone, interactor);
    }

    internal void NotifyUnselected(GlockGripZone zone, HandGrabInteractor interactor)
    {
        if (interactor.Hand == null)
        {
            return;
        }

        int index = (int)interactor.Hand.Handedness;
        if (_hands[index].Zone != zone || _hands[index].Interactor != interactor)
        {
            return;
        }

        SetZone(index, null, null);

        HandSlot other = _hands[1 - index];
        if (zone.HoldsGun && other.Zone != null && GlockGripModes.DependsOnGun(other.Zone.Mode))
        {
            other.Interactor.ForceRelease();
        }
    }

    private void SetZone(int index, GlockGripZone zone, HandGrabInteractor interactor)
    {
        ref HandSlot slot = ref _hands[index];
        GlockGripMode previous = slot.Zone != null ? slot.Zone.Mode : GlockGripMode.None;
        slot.Zone = zone;
        slot.Interactor = interactor;

        // A escolha de zona depende do estado das duas mãos: descarta o cache.
        _hands[0].ResolvedFrame = -1;
        _hands[1].ResolvedFrame = -1;

        GlockGripMode current = zone != null ? zone.Mode : GlockGripMode.None;
        if (current != previous)
        {
            WhenModeChanged((Handedness)index, current);
        }
    }
}
