using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Efectos visuales de la combi, creados por codigo al iniciar:
///  - Faros delanteros (luces Spot). Tecla L para encender/apagar.
///  - Luces de freno: el material "Luz_Trasera" brilla mas al frenar.
///  - Humo del escape, mas denso al acelerar.
///  - Humo de llantas al usar el freno de mano con velocidad.
///  - Reflejos dinamicos: un Reflection Probe en tiempo real que sigue a la
///    combi, para que la pintura y los vidrios reflejen la calle y el cielo.
///
/// Se agrega solo a la combi (la que tiene CombiController). Para cambiar
/// valores en el editor, agreguen el componente a mano y ya no se creara otro.
/// </summary>
[RequireComponent(typeof(CombiController))]
public class EfectosCombi : MonoBehaviour
{
    [Header("Faros")]
    public bool farosEncendidos = true;
    public Color colorFaros = new Color(1f, 0.93f, 0.8f);
    public float intensidadFaros = 6f;
    public float alcanceFaros = 45f;

    [Header("Luces de freno")]
    public string materialLuzTrasera = "Luz_Trasera";
    public Color colorFreno = new Color(1f, 0.06f, 0.03f);
    public float brilloNormal = 1.2f;
    public float brilloFrenando = 8f;

    [Header("Humo")]
    public bool humoEscape = true;
    public bool humoLlantas = true;

    [Header("Reflejos dinamicos")]
    public bool reflejosDinamicos = true;
    [Tooltip("Segundos entre actualizaciones del Reflection Probe")]
    public float intervaloReflejos = 0.2f;

    CombiController combi;
    readonly List<Light> faros = new List<Light>();
    readonly List<Renderer> lucesTraseras = new List<Renderer>();
    MaterialPropertyBlock bloque;
    ParticleSystem escape;
    readonly List<ParticleSystem> humoRuedas = new List<ParticleSystem>();
    ReflectionProbe sonda;
    float siguienteReflejo;
    bool estabaFrenando;

    static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void CrearAutomaticamente()
    {
        foreach (var c in FindObjectsByType<CombiController>(FindObjectsSortMode.None))
            if (c.GetComponent<EfectosCombi>() == null)
                c.gameObject.AddComponent<EfectosCombi>();
    }

    void Start()
    {
        combi = GetComponent<CombiController>();
        bloque = new MaterialPropertyBlock();

        // Caja que envuelve a la combi, en coordenadas locales
        Bounds caja = CajaLocal();
        Vector3 frente = combi.ejeFrente.normalized;
        Vector3 arriba = Vector3.up;
        Vector3 lado = Vector3.Cross(arriba, frente);
        float largo = Mathf.Abs(Vector3.Dot(caja.extents, Abs(frente)));
        float ancho = Mathf.Abs(Vector3.Dot(caja.extents, Abs(lado)));
        float alto = caja.extents.y;
        Vector3 centro = caja.center;

        CrearFaros(centro, frente, lado, largo, ancho, alto);
        BuscarLucesTraseras();

        Material humo = Resources.Load<Material>("Efectos/Humo");
        if (humo != null)
        {
            if (humoEscape)
            {
                Vector3 posEscape = centro - frente * (largo * 0.98f) + lado * (ancho * 0.55f) - arriba * (alto * 0.75f);
                escape = CrearHumo("Humo Escape", posEscape, -frente, humo, 0.25f, 0.9f, new Color(0.55f, 0.55f, 0.55f, 0.35f));
            }
            if (humoLlantas)
            {
                foreach (float s in new[] { -1f, 1f })
                {
                    Vector3 pos = centro - frente * (largo * 0.6f) + lado * (ancho * 0.8f * s) - arriba * (alto * 0.95f);
                    var ps = CrearHumo("Humo Llanta", pos, arriba, humo, 0.8f, 2.6f, new Color(0.85f, 0.85f, 0.85f, 0.45f));
                    var em = ps.emission; em.rateOverTime = 0f;
                    humoRuedas.Add(ps);
                }
            }
        }
        else
        {
            Debug.LogWarning("EfectosCombi: no se encontro Resources/Efectos/Humo.mat, no habra humo.");
        }

        if (reflejosDinamicos) CrearSondaReflejos(caja);
    }

    void Update()
    {
        Keyboard teclado = Keyboard.current;
        if (teclado != null && teclado.lKey.wasPressedThisFrame)
            farosEncendidos = !farosEncendidos;
        foreach (var f in faros) f.enabled = farosEncendidos;

        // Luces de freno
        bool frenando = combi.Frenando;
        if (frenando != estabaFrenando || Time.frameCount < 3)
        {
            Color c = colorFreno * (frenando ? brilloFrenando : brilloNormal);
            foreach (var r in lucesTraseras)
            {
                r.GetPropertyBlock(bloque);
                bloque.SetColor(EmissionColorId, c);
                r.SetPropertyBlock(bloque);
            }
            estabaFrenando = frenando;
        }

        // Humo del escape: mas denso al acelerar
        if (escape != null)
        {
            var em = escape.emission;
            em.rateOverTime = 6f + 40f * Mathf.Max(0f, Mathf.Abs(combi.Avance)) * (1f - combi.VelocidadNormalizada * 0.7f);
        }

        // Humo de llantas con freno de mano y velocidad
        float derrape = combi.FrenoDeMano ? Mathf.InverseLerp(3f, 12f, Mathf.Abs(combi.VelocidadAdelante)) : 0f;
        foreach (var ps in humoRuedas)
        {
            var em = ps.emission;
            em.rateOverTime = 60f * derrape;
        }

        // Reflejos: la sonda sigue a la combi sin girar y se actualiza cada cierto tiempo
        if (sonda != null)
        {
            sonda.transform.SetPositionAndRotation(transform.position + Vector3.up * 1.5f, Quaternion.identity);
            if (Time.time >= siguienteReflejo)
            {
                sonda.RenderProbe();
                siguienteReflejo = Time.time + intervaloReflejos;
            }
        }
    }

    void CrearFaros(Vector3 centro, Vector3 frente, Vector3 lado, float largo, float ancho, float alto)
    {
        foreach (float s in new[] { -1f, 1f })
        {
            var go = new GameObject(s < 0 ? "Faro Izquierdo" : "Faro Derecho");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = centro + frente * (largo * 1.01f) + lado * (ancho * 0.6f * s) - Vector3.up * (alto * 0.25f);
            go.transform.localRotation = Quaternion.LookRotation(frente - Vector3.up * 0.12f, Vector3.up);

            var luz = go.AddComponent<Light>();
            luz.type = LightType.Spot;
            luz.color = colorFaros;
            luz.intensity = intensidadFaros;
            luz.range = alcanceFaros;
            luz.spotAngle = 70f;
            luz.innerSpotAngle = 35f;
            luz.shadows = LightShadows.None;
            luz.renderMode = LightRenderMode.ForcePixel;
            faros.Add(luz);
        }
    }

    void BuscarLucesTraseras()
    {
        foreach (var r in GetComponentsInChildren<Renderer>())
            foreach (var m in r.sharedMaterials)
                if (m != null && m.name.StartsWith(materialLuzTrasera))
                {
                    lucesTraseras.Add(r);
                    break;
                }
    }

    ParticleSystem CrearHumo(string nombre, Vector3 posLocal, Vector3 direccionLocal, Material material,
                             float tamano, float vida, Color color)
    {
        var go = new GameObject(nombre);
        go.transform.SetParent(transform, false);
        go.transform.localPosition = posLocal;
        go.transform.localRotation = Quaternion.LookRotation(direccionLocal, Vector3.up);

        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.duration = 1f;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(vida * 0.7f, vida * 1.3f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 1.2f);
        main.startSize = new ParticleSystem.MinMaxCurve(tamano * 0.7f, tamano * 1.3f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = color;
        main.gravityModifier = -0.05f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Shape; // el tamano no depende de la escala de la combi
        main.maxParticles = 300;

        var em = ps.emission;
        em.rateOverTime = 10f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 12f;
        shape.radius = 0.05f;

        var tam = ps.sizeOverLifetime;
        tam.enabled = true;
        tam.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.6f, 1f, 3f));

        var col = ps.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0f, 1f) });
        col.color = grad;

        var rot = ps.rotationOverLifetime;
        rot.enabled = true;
        rot.z = new ParticleSystem.MinMaxCurve(-0.5f, 0.5f);

        var ruido = ps.noise;
        ruido.enabled = true;
        ruido.strength = 0.4f;
        ruido.frequency = 0.6f;

        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = material;
        r.renderMode = ParticleSystemRenderMode.Billboard;
        r.sortMode = ParticleSystemSortMode.Distance;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        ps.Play();
        return ps;
    }

    void CrearSondaReflejos(Bounds caja)
    {
        var go = new GameObject("Reflejos Dinamicos");
        go.transform.position = transform.position + Vector3.up * 1.5f;
        sonda = go.AddComponent<ReflectionProbe>();
        sonda.mode = UnityEngine.Rendering.ReflectionProbeMode.Realtime;
        sonda.refreshMode = UnityEngine.Rendering.ReflectionProbeRefreshMode.ViaScripting;
        sonda.timeSlicingMode = UnityEngine.Rendering.ReflectionProbeTimeSlicingMode.IndividualFaces;
        sonda.resolution = 128;
        sonda.size = new Vector3(120f, 60f, 120f);
        sonda.boxProjection = false;
        sonda.importance = 10;
        sonda.hdr = true;
        sonda.farClipPlane = 300f;

        // La sonda esta dentro de la combi: hay que excluirla para no reflejar su interior.
        // Si existe la capa "Jugador", se usa; si no, se recorta con el plano cercano.
        int capa = LayerMask.NameToLayer("Jugador");
        if (capa >= 0)
        {
            foreach (var t in GetComponentsInChildren<Transform>(true)) t.gameObject.layer = capa;
            sonda.cullingMask = ~(1 << capa);
            sonda.nearClipPlane = 0.3f;
        }
        else
        {
            sonda.nearClipPlane = Mathf.Max(0.3f, caja.extents.magnitude * transform.lossyScale.x * 0.9f);
        }
        sonda.RenderProbe();
    }

    Bounds CajaLocal()
    {
        var renderers = GetComponentsInChildren<Renderer>();
        var b = new Bounds(Vector3.zero, Vector3.zero);
        bool primero = true;
        foreach (var r in renderers)
        {
            if (r is ParticleSystemRenderer) continue;
            var wb = r.bounds;
            // Las 8 esquinas de cada caja, pasadas a espacio local
            for (int i = 0; i < 8; i++)
            {
                Vector3 p = wb.center + Vector3.Scale(wb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                Vector3 l = transform.InverseTransformPoint(p);
                if (primero) { b = new Bounds(l, Vector3.zero); primero = false; }
                else b.Encapsulate(l);
            }
        }
        return b;
    }

    static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
}
