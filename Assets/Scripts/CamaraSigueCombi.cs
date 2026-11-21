using UnityEngine;

/// <summary>
/// Camara en tercera persona que sigue a la combi.
///
/// Cambios respecto a la version original (revision):
///  - El campo de vision (FOV) se abre con la velocidad: da sensacion de rapidez.
///  - Si un edificio/poste queda entre la combi y la camara, la camara se acerca
///    en lugar de atravesarlo (SphereCast).
/// </summary>
[RequireComponent(typeof(Camera))]
public class CamaraSigueCombi : MonoBehaviour
{
    [Header("Objetivo")]
    public Transform objetivo;                       // arrastra aqui Combi1

    [Header("Posicion de la camara (relativa a la combi)")]
    // X = lados, Y = altura, Z = adelante/atras.
    // Si el frente de la combi es +Z, atras es -Z.
    // Si el frente es +X, usa (-10, 4, 0).
    public Vector3 desplazamiento = new Vector3(0f, 4f, -10f);

    [Header("Ajustes")]
    public float alturaDeMira = 1.5f;                // punto de la combi al que mira
    public float suavizado = 5f;                     // mas alto = camara mas pegada

    [Header("Campo de vision dinamico")]
    public float fovParado = 60f;
    public float fovVelocidadMaxima = 72f;

    [Header("Evitar atravesar paredes")]
    public bool evitarObstaculos = true;
    public float radioColision = 0.4f;
    public LayerMask capasObstaculo = ~0;

    Camera cam;
    CombiController combi;

    void Awake()
    {
        cam = GetComponent<Camera>();
        if (objetivo != null) combi = objetivo.GetComponentInParent<CombiController>();
    }

    void LateUpdate()
    {
        if (objetivo == null) return;

        // Solo se usa el giro en Y de la combi, para que la camara no se incline
        Quaternion giro = Quaternion.Euler(0f, objetivo.eulerAngles.y, 0f);
        Vector3 puntoMira = objetivo.position + Vector3.up * alturaDeMira;
        Vector3 posicionDeseada = objetivo.position + giro * desplazamiento;

        if (evitarObstaculos)
            posicionDeseada = AjustarPorObstaculos(puntoMira, posicionDeseada);

        float t = 1f - Mathf.Exp(-suavizado * Time.deltaTime);
        transform.position = Vector3.Lerp(transform.position, posicionDeseada, t);

        Quaternion rotacionDeseada = Quaternion.LookRotation(puntoMira - transform.position);
        transform.rotation = Quaternion.Slerp(transform.rotation, rotacionDeseada, t);

        if (combi != null)
        {
            float fov = Mathf.Lerp(fovParado, fovVelocidadMaxima, combi.VelocidadNormalizada);
            cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, fov, t);
        }
    }

    Vector3 AjustarPorObstaculos(Vector3 desde, Vector3 hasta)
    {
        Vector3 dir = hasta - desde;
        float distancia = dir.magnitude;
        if (distancia < 0.01f) return hasta;
        dir /= distancia;

        RaycastHit[] golpes = Physics.SphereCastAll(desde, radioColision, dir, distancia,
                                                    capasObstaculo, QueryTriggerInteraction.Ignore);
        float masCercano = distancia;
        foreach (RaycastHit g in golpes)
        {
            // Ignorar los colliders de la propia combi
            if (g.transform.IsChildOf(objetivo.root)) continue;
            if (g.distance > 0f && g.distance < masCercano) masCercano = g.distance;
        }
        return desde + dir * masCercano;
    }
}
