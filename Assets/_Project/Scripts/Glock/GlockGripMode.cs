/// <summary>
/// Modos de pegada da Glock. Cada <see cref="GlockGripZone"/> ativa um deles.
/// </summary>
public enum GlockGripMode
{
    None,

    /// <summary>Empunhadura pelo cabo, com snap. O gatilho do controle aciona o gatilho da arma.</summary>
    Handle,

    /// <summary>Pegada de desmontagem (cabo + ferrolho). O gatilho analógico recua o ferrolho.</summary>
    Takedown,

    /// <summary>Pegou pelo ferrolho com a arma solta: pega a arma inteira, livre.</summary>
    SlideFree,

    /// <summary>A outra mão segura a arma: arrasta o ferrolho no trilho.</summary>
    SlideDrag,

    /// <summary>A outra mão segura a arma: puxa a alavanca de desmontagem.</summary>
    Lever,

    /// <summary>Qualquer outro ponto da arma: pega livre, mantendo a pose atual.</summary>
    Free,

    /// <summary>Segurando o carregador, fora da arma ou correndo no poço.</summary>
    Magazine,

    /// <summary>Segurando uma peça já desmontada (ferrolho, cano, mola).</summary>
    Part,
}

public static class GlockGripModes
{
    /// <summary>
    /// Modos em que a mão segura o corpo da arma (e não só uma peça móvel dela).
    /// </summary>
    public static bool HoldsGun(GlockGripMode mode)
    {
        return mode == GlockGripMode.Handle
            || mode == GlockGripMode.Takedown
            || mode == GlockGripMode.SlideFree
            || mode == GlockGripMode.Free;
    }

    /// <summary>
    /// Modos que só fazem sentido com a outra mão segurando a arma (uma peça móvel dela).
    /// </summary>
    public static bool DependsOnGun(GlockGripMode mode)
    {
        return mode == GlockGripMode.SlideDrag
            || mode == GlockGripMode.Lever;
    }
}
