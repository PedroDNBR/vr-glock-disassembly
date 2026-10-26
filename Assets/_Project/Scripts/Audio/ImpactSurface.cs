using UnityEngine;

/// <summary>
/// Marca o colisor como mesa para o <see cref="ImpactSound"/>: o que cair aqui toca o som "na mesa".
/// Colisores sem marca contam como chão.
/// </summary>
[DisallowMultipleComponent]
public class ImpactSurface : MonoBehaviour
{
}
