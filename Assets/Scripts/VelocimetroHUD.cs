using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Velocimetro sencillo en pantalla (km/h + barra).
/// Se crea solo al iniciar la escena si encuentra una CombiController,
/// asi que no hace falta configurar nada en el editor. Si prefieren
/// diseñarlo a mano, borren el metodo CrearAutomaticamente y armen su
/// propio Canvas usando VelocidadKmh / VelocidadNormalizada.
/// </summary>
public class VelocimetroHUD : MonoBehaviour
{
    public CombiController combi;
    public Color colorBarra = new Color(1f, 0.78f, 0.2f);

    Text texto;
    Image barra;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void CrearAutomaticamente()
    {
        if (FindAnyObjectByType<VelocimetroHUD>() != null) return;
        CombiController c = FindAnyObjectByType<CombiController>();
        if (c == null) return;

        var go = new GameObject("HUD Velocimetro");
        go.AddComponent<VelocimetroHUD>().combi = c;
    }

    void Start()
    {
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var escalador = gameObject.AddComponent<CanvasScaler>();
        escalador.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        escalador.referenceResolution = new Vector2(1920, 1080);

        // Panel semitransparente en la esquina inferior derecha
        RectTransform panel = CrearRect("Panel", transform, new Vector2(1, 0), new Vector2(-40, 40), new Vector2(320, 130));
        panel.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.45f);

        RectTransform rt = CrearRect("Velocidad", panel, new Vector2(0.5f, 1f), new Vector2(0, -10), new Vector2(300, 80));
        texto = rt.gameObject.AddComponent<Text>();
        texto.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        texto.fontSize = 56;
        texto.fontStyle = FontStyle.Bold;
        texto.alignment = TextAnchor.MiddleCenter;
        texto.color = Color.white;

        RectTransform fondo = CrearRect("BarraFondo", panel, new Vector2(0.5f, 0f), new Vector2(0, 18), new Vector2(280, 14));
        fondo.gameObject.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.15f);

        RectTransform relleno = CrearRect("BarraRelleno", fondo, new Vector2(0f, 0.5f), Vector2.zero, new Vector2(280, 14));
        relleno.pivot = new Vector2(0f, 0.5f);
        barra = relleno.gameObject.AddComponent<Image>();
        barra.color = colorBarra;
    }

    void Update()
    {
        if (combi == null || texto == null) return;
        texto.text = $"{Mathf.Abs(combi.VelocidadKmh):0} km/h";
        barra.rectTransform.localScale = new Vector3(combi.VelocidadNormalizada, 1f, 1f);
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
