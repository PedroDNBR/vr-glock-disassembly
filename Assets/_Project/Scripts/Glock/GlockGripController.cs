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
/// - soltar o corpo da arma solta também a mão que segurava uma peça móvel (ferrolho, alavanca).
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

        bool otherHandHoldsGun = HoldsGun((Handedness)(1 - index));
        GlockGripZone best = null;
        float bestDistance = float.PositiveInfinity;
        foreach (GlockGripZone zone in _zones)
        {
            if (!zone.Accepts(interactor, otherHandHoldsGun, out float distance))
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
        if (zone.HoldsGun && other.Zone != null && !other.Zone.HoldsGun)
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
