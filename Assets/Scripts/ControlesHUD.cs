using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Panel de controles en pantalla (esquina superior izquierda).
/// Cada tecla se ilumina mientras esta presionada y la fila de faros indica
/// si estan encendidos. Con H se oculta o se muestra el panel.
/// Igual que VelocimetroHUD, se crea solo al iniciar la escena si encuentra
/// una CombiController, asi que no hay que configurar nada en el editor.
/// </summary>
public class ControlesHUD : MonoBehaviour
{
    public CombiController combi;
    public EfectosCombi efectos;
    [Tooltip("Mostrar el panel al iniciar")]
    public bool visibleAlIniciar = true;
    public Color colorTeclaActiva = new Color(1f, 0.78f, 0.2f);

    static readonly Color ColorTecla = new Color(1f, 1f, 1f, 0.14f);
    static readonly Color ColorTextoTecla = Color.white;
    static readonly Color ColorTextoTeclaActiva = new Color(0.08f, 0.08f, 0.08f);

    /// <summary>True si el panel completo esta visible (false = solo el aviso de H).</summary>
    public bool Visible => panel != null && panel.gameObject.activeSelf;

    /// <summary>Texto del estado de los faros, para pruebas.</summary>
    public string EstadoFaros => estadoFaros != null ? estadoFaros.text : "";

    class Tecla
    {
        public Key[] controles;
        public Image fondo;
        public Text texto;
    }

    readonly List<Tecla> teclas = new List<Tecla>();
    RectTransform panel;
    RectTransform aviso;
    Text estadoFaros;
    Font fuente;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void CrearAutomaticamente()
    {
        if (FindAnyObjectByType<ControlesHUD>() != null) return;
        CombiController c = FindAnyObjectByType<CombiController>();
        if (c == null) return;

        var go = new GameObject("HUD Controles");
        var hud = go.AddComponent<ControlesHUD>();
        hud.combi = c;
        hud.efectos = c.GetComponent<EfectosCombi>();
    }

    void Start()
    {
        if (efectos == null && combi != null) efectos = combi.GetComponent<EfectosCombi>();
        if (efectos == null) efectos = FindAnyObjectByType<EfectosCombi>();

        fuente = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;
        var escalador = gameObject.AddComponent<CanvasScaler>();
        escalador.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        escalador.referenceResolution = new Vector2(1920, 1080);
        escalador.matchWidthOrHeight = 0.5f;

        const float ancho = 430f;
        const float alto = 330f;
        panel = CrearRect("Panel", transform, new Vector2(0, 1), new Vector2(40, -40), new Vector2(ancho, alto));
        panel.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.45f);

        CrearTexto("Titulo", panel, new Vector2(20, -14), new Vector2(ancho - 40, 34), "CONTROLES", 24,
                   FontStyle.Bold, TextAnchor.MiddleLeft, new Color(1f, 1f, 1f, 0.85f));

        float y = -60f;
        const float paso = 46f;
        Fila(ref y, paso, "Acelerar", Grupo("W", Key.W), Grupo("↑", Key.UpArrow));
        Fila(ref y, paso, "Frenar / reversa", Grupo("S", Key.S), Grupo("↓", Key.DownArrow));
        Fila(ref y, paso, "Girar", Grupo("A", Key.A), Grupo("D", Key.D),
             Grupo("←", Key.LeftArrow), Grupo("→", Key.RightArrow));
        Fila(ref y, paso, "Freno de mano", Grupo("Espacio", Key.Space, 112f));
        Text faros = Fila(ref y, paso, "Faros", Grupo("L", Key.L));
        estadoFaros = CrearTexto("EstadoFaros", panel, new Vector2(ancho - 140, y + paso), new Vector2(120, 36), "",
                                 20, FontStyle.Bold, TextAnchor.MiddleRight, Color.white);
        if (efectos == null) faros.color = new Color(1f, 1f, 1f, 0.4f);

        CrearTexto("Ocultar", panel, new Vector2(20, -(alto - 40)), new Vector2(ancho - 40, 26),
                   "H  ocultar controles", 18, FontStyle.Normal, TextAnchor.MiddleLeft, new Color(1f, 1f, 1f, 0.55f));

        // Aviso pequeno que queda cuando el panel esta oculto
        aviso = CrearRect("Aviso", transform, new Vector2(0, 1), new Vector2(40, -40), new Vector2(230, 40));
        aviso.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.45f);
        CrearTexto("Texto", aviso, new Vector2(14, -4), new Vector2(210, 32), "H  mostrar controles", 18,
                   FontStyle.Normal, TextAnchor.MiddleLeft, new Color(1f, 1f, 1f, 0.75f));

        MostrarPanel(visibleAlIniciar);
        ActualizarFaros();
    }

    void Update()
    {
        Keyboard k = Keyboard.current;
        if (k != null && k.hKey.wasPressedThisFrame) MostrarPanel(!Visible);
        if (!Visible) return;

        foreach (var t in teclas)
        {
            bool activa = false;
            if (k != null)
                foreach (var c in t.controles) activa |= k[c].isPressed;
            t.fondo.color = activa ? colorTeclaActiva : ColorTecla;
            t.texto.color = activa ? ColorTextoTeclaActiva : ColorTextoTecla;
        }
        ActualizarFaros();
    }

    /// <summary>Muestra u oculta el panel completo.</summary>
    public void MostrarPanel(bool mostrar)
    {
        if (panel == null) return;
        panel.gameObject.SetActive(mostrar);
        aviso.gameObject.SetActive(!mostrar);
    }

    void ActualizarFaros()
    {
        if (estadoFaros == null) return;
        if (efectos == null) { estadoFaros.text = ""; return; }
        bool on = efectos.farosEncendidos;
        estadoFaros.text = on ? "Encendidos" : "Apagados";
        estadoFaros.color = on ? colorTeclaActiva : new Color(1f, 1f, 1f, 0.5f);
    }

    // ---------- construccion ----------

    struct GrupoTecla
    {
        public string etiqueta;
        public Key control;
        public float ancho;
    }

    static GrupoTecla Grupo(string etiqueta, Key control, float ancho = 40f)
        => new GrupoTecla { etiqueta = etiqueta, control = control, ancho = ancho };

    Text Fila(ref float y, float paso, string accion, params GrupoTecla[] grupo)
    {
        float x = 20f;
        foreach (var g in grupo)
        {
            RectTransform caja = CrearRect("Tecla " + g.etiqueta, panel, new Vector2(0, 1), new Vector2(x, y),
                                           new Vector2(g.ancho, 36));
            var fondo = caja.gameObject.AddComponent<Image>();
            fondo.color = ColorTecla;
            Text t = CrearTexto("Texto", caja, Vector2.zero, new Vector2(g.ancho, 36), g.etiqueta,
                                g.etiqueta.Length > 1 ? 18 : 22, FontStyle.Bold, TextAnchor.MiddleCenter, ColorTextoTecla);
            teclas.Add(new Tecla { controles = new[] { g.control }, fondo = fondo, texto = t });
            x += g.ancho + 6f;
        }
        Text accionTexto = CrearTexto("Accion " + accion, panel, new Vector2(Mathf.Max(x + 8f, 210f), y),
                                      new Vector2(200, 36), accion, 20, FontStyle.Normal, TextAnchor.MiddleLeft, Color.white);
        y -= paso;
        return accionTexto;
    }

    Text CrearTexto(string nombre, Transform padre, Vector2 posicion, Vector2 tamano, string contenido,
                    int tamanoFuente, FontStyle estilo, TextAnchor alineacion, Color color)
    {
        RectTransform rt = CrearRect(nombre, padre, new Vector2(0, 1), posicion, tamano);
        var t = rt.gameObject.AddComponent<Text>();
        t.font = fuente;
        t.text = contenido;
        t.fontSize = tamanoFuente;
        t.fontStyle = estilo;
        t.alignment = alineacion;
        t.color = color;
        t.raycastTarget = false;
        return t;
    }

    static RectTransform CrearRect(string nombre, Transform padre, Vector2 ancla, Vector2 posicion, Vector2 tamano)
    {
        var go = new GameObject(nombre, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(padre, false);
        rt.anchorMin = rt.anchorMax = ancla;
        rt.pivot = ancla;
        rt.anchoredPosition = posicion;
        rt.sizeDelta = tamano;
        return rt;
    }
}
