using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>Variantes de "look" de AgX (las mismas que trae Blender).</summary>
public enum AgXLook
{
    Base = 0,
    Punchy = 1,   // mas contraste y saturacion
    Golden = 2,   // calido, tipo pelicula
}

[Serializable]
public sealed class AgXLookParameter : VolumeParameter<AgXLook>
{
    public AgXLookParameter(AgXLook value, bool overrideState = false) : base(value, overrideState) { }
}

/// <summary>
/// Tonemapping AgX como override de Volume.
///
/// URP solo trae Neutral y ACES. AgX (el de Blender 4) maneja mejor las luces
/// muy intensas: el sol y los faros se van a blanco de forma natural en lugar
/// de saturarse en colores raros, y los colores intensos no "se queman".
///
/// Para que funcione:
///  1. El renderer (PC_Renderer) debe tener el Renderer Feature "AgX Renderer Feature".
///  2. En el mismo Volume, el override "Tonemapping" de URP debe estar en "None".
///  3. El URP Asset debe usar Grading Mode = High Dynamic Range.
/// </summary>
[Serializable, VolumeComponentMenu("Owl Project/AgX Tonemapping")]
[SupportedOnRenderPipeline(typeof(UniversalRenderPipelineAsset))]
public sealed class AgXTonemapping : VolumeComponent, IPostProcessComponent
{
    [Tooltip("Activa el tonemapping AgX.")]
    public BoolParameter activo = new BoolParameter(false);

    [Tooltip("Exposicion en pasos (EV). +1 = el doble de luz.")]
    public FloatParameter exposicion = new FloatParameter(0f);

    [Tooltip("Base = neutro, Punchy = mas contraste, Golden = calido.")]
    public AgXLookParameter look = new AgXLookParameter(AgXLook.Base);

    [Tooltip("Saturacion extra despues del look (1 = sin cambio).")]
    public ClampedFloatParameter saturacion = new ClampedFloatParameter(1f, 0f, 2f);

    [Tooltip("Contraste extra despues del look (1 = sin cambio).")]
    public ClampedFloatParameter contraste = new ClampedFloatParameter(1f, 0.5f, 2f);

    public bool IsActive() => activo.value;
}
