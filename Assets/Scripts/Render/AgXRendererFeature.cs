using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Aplica el tonemapping AgX despues del post-proceso de URP.
///
/// Con el Tonemapping de URP en "None" y Grading Mode en HDR, la salida del
/// post-proceso de URP (bloom, color grading, etc.) sigue en HDR; este pase la
/// convierte a rango de pantalla con la curva AgX. Los valores se leen del
/// override de Volume <see cref="AgXTonemapping"/>.
/// </summary>
public class AgXRendererFeature : ScriptableRendererFeature
{
    [SerializeField] Shader shader;

    Material material;
    AgXPass pass;

    public override void Create()
    {
        if (shader == null) shader = Shader.Find("Hidden/OwlProject/AgX");
        if (shader == null) return;

        material = CoreUtils.CreateEngineMaterial(shader);
        pass = new AgXPass(material)
        {
            renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing
        };
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (pass == null) return;

        if (!renderingData.cameraData.postProcessEnabled) return;
        CameraType tipo = renderingData.cameraData.cameraType;
        if (tipo == CameraType.Preview || tipo == CameraType.Reflection) return;

        var agx = VolumeManager.instance.stack.GetComponent<AgXTonemapping>();
        if (agx == null || !agx.IsActive()) return;

        pass.Configurar(agx);
        renderer.EnqueuePass(pass);
    }

    protected override void Dispose(bool disposing)
    {
        CoreUtils.Destroy(material);
    }

    class AgXPass : ScriptableRenderPass
    {
        static readonly int ParamsId = Shader.PropertyToID("_AgXParams");
        static readonly int LookId = Shader.PropertyToID("_AgXLook");

        readonly Material material;

        public AgXPass(Material material)
        {
            this.material = material;
            // Necesitamos leer el color de la camara, no el backbuffer.
            requiresIntermediateTexture = true;
        }

        public void Configurar(AgXTonemapping agx)
        {
            material.SetVector(ParamsId, new Vector4(
                Mathf.Pow(2f, agx.exposicion.value),
                agx.saturacion.value,
                agx.contraste.value,
                0f));
            material.SetFloat(LookId, (float)agx.look.value);
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var recursos = frameData.Get<UniversalResourceData>();
            if (recursos.isActiveTargetBackBuffer) return;

            TextureHandle origen = recursos.activeColorTexture;
            TextureDesc desc = renderGraph.GetTextureDesc(origen);
            desc.name = "_AgXTonemapping";
            desc.clearBuffer = false;
            TextureHandle destino = renderGraph.CreateTexture(desc);

            var parametros = new RenderGraphUtils.BlitMaterialParameters(origen, destino, material, 0);
            renderGraph.AddBlitPass(parametros, "AgX Tonemapping");

            // A partir de aqui, el color de la camara es el resultado con AgX.
            recursos.cameraColor = destino;
        }
    }
}
