using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Control arcade de la combi.
///
/// Cambios respecto a la version original (revision):
///  - Ya no se fija la velocidad de golpe: la combi acelera, frena y se
///    detiene sola poco a poco (inercia).
///  - Volante y acelerador analogicos: las teclas no pasan de 0 a 100% en un
///    cuadro, el volante se mueve y regresa al centro con una rampa.
///  - Gira sobre su centro (velocidad angular del Rigidbody) y no sobre el
///    origen del modelo, que en esta combi esta en la defensa delantera.
///  - El giro depende de la velocidad: parada no gira, en reversa el volante
///    se invierte y a alta velocidad gira menos (menos nerviosa).
///  - "Agarre" lateral: elimina el derrape poco a poco en lugar de borrarlo,
///    asi los choques empujan a la combi en vez de ignorarse.
///  - Freno de mano con Espacio y soporte para flechas.
///  - Expone la velocidad actual (km/h) para el HUD.
/// Los nombres de los campos originales se conservan para no perder los
/// valores ya ajustados en la escena.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class CombiController : MonoBehaviour
{
    [Header("Movimiento")]
    [Tooltip("Velocidad maxima hacia adelante (m/s). 15 m/s = 54 km/h")]
    public float velocidad = 15f;
    [Tooltip("Velocidad maxima en reversa (m/s)")]
    public float velocidadReversa = 4f;
    [Tooltip("Grados por segundo con el volante a tope")]
    public float velocidadGiro = 70f;

    [Header("Aceleracion (m/s por segundo)")]
    public float aceleracion = 6f;
    public float frenado = 14f;          // al pulsar la tecla contraria
    public float desaceleracion = 3f;    // al soltar el acelerador
    public float frenoDeMano = 20f;

    [Header("Manejo")]
    [Tooltip("Velocidad (m/s) a partir de la cual el giro es completo")]
    public float velocidadGiroCompleto = 6f;
    [Range(0f, 1f), Tooltip("Giro que queda a velocidad maxima (1 = igual que a baja velocidad)")]
    public float giroAVelocidadMaxima = 0.55f;
    [Tooltip("Que tan rapido se mueve el volante al girar (1/s)")]
    public float respuestaVolante = 3.5f;
    [Tooltip("Que tan rapido regresa el volante al centro al soltar (1/s)")]
    public float retornoVolante = 5f;
    [Tooltip("Que tan rapido responde el pedal (1/s)")]
    public float respuestaPedal = 4f;
    [Range(0f, 1f), Tooltip("1 = sin derrape, 0 = sobre hielo")]
    public float agarreLateral = 0.9f;
    [Range(0f, 1f), Tooltip("Agarre lateral con el freno de mano (derrapa mas)")]
    public float agarreFrenoDeMano = 0.35f;

    [Header("Direccion del frente (eje local)")]
    // Largo de la combi: prueba (0,0,1) o (0,0,-1)
    public Vector3 ejeFrente = new Vector3(0f, 0f, 1f);

    /// <summary>Velocidad hacia adelante en km/h (negativa en reversa).</summary>
    public float VelocidadKmh => velocidadAdelante * 3.6f;

    /// <summary>0..1 respecto a la velocidad maxima, util para camara/HUD.</summary>
    public float VelocidadNormalizada => Mathf.Clamp01(Mathf.Abs(velocidadAdelante) / Mathf.Max(0.01f, velocidad));

    /// <summary>Velocidad hacia adelante en m/s (negativa en reversa).</summary>
    public float VelocidadAdelante => velocidadAdelante;

    /// <summary>Entrada del acelerador: 1 = W, -1 = S, 0 = nada.</summary>
    public float Avance => avance;

    /// <summary>Posicion del volante, -1 (izquierda) a 1 (derecha), ya suavizada.</summary>
    public float Volante => volante;

    /// <summary>Freno de mano (Espacio) presionado.</summary>
    public bool FrenoDeMano => freno;

    /// <summary>True si la combi esta frenando (para las luces de freno).</summary>
    public bool Frenando => freno
        || (avance < 0f && velocidadAdelante > 0.5f)
        || (avance > 0f && velocidadAdelante < -0.5f);

    /// <summary>Centro de la combi en el mundo (el origen del modelo esta en la defensa).</summary>
    public Vector3 Centro => rb != null ? rb.worldCenterOfMass : transform.position;

    Rigidbody rb;
    float avance;       // 1 = W, -1 = S (tecla cruda)
    float giro;         // -1 = A, 1 = D (tecla cruda)
    bool freno;         // Espacio
    float pedal;        // avance suavizado
    float volante;      // giro suavizado
    float velocidadAdelante;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        // La combi solo puede girar sobre el eje Y (no se voltea)
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        // Sin interpolacion el cuerpo avanza a saltos de 50 Hz y se ve entrecortado
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        rb.angularDamping = 0f;

        // La caja de colision se arrastra sobre el suelo. Con la friccion por
        // defecto (0.6) el suelo le quita ~0.12 m/s en cada paso de fisica, casi
        // lo mismo que suma la aceleracion, y la combi se queda en ~0.4 km/h.
        // El agarre ya lo controla agarreLateral, asi que el contacto va sin friccion.
        foreach (var col in GetComponentsInChildren<Collider>())
        {
            if (col.isTrigger || col.sharedMaterial != null) continue;
            col.sharedMaterial = MaterialSinFriccion;
        }
    }

    static PhysicsMaterial materialSinFriccion;
    static PhysicsMaterial MaterialSinFriccion => materialSinFriccion != null ? materialSinFriccion
        : materialSinFriccion = new PhysicsMaterial("Combi sin friccion")
        {
            dynamicFriction = 0f,
            staticFriction = 0f,
            frictionCombine = PhysicsMaterialCombine.Minimum,
        };

    void Update()
    {
        Keyboard teclado = Keyboard.current;
        if (teclado == null) { avance = giro = 0f; freno = false; return; }

        avance = (Presionada(teclado.wKey, teclado.upArrowKey) ? 1f : 0f)
               - (Presionada(teclado.sKey, teclado.downArrowKey) ? 1f : 0f);
        giro   = (Presionada(teclado.dKey, teclado.rightArrowKey) ? 1f : 0f)
               - (Presionada(teclado.aKey, teclado.leftArrowKey) ? 1f : 0f);
        freno  = teclado.spaceKey.isPressed;
    }

    static bool Presionada(UnityEngine.InputSystem.Controls.KeyControl a,
                           UnityEngine.InputSystem.Controls.KeyControl b)
        => a.isPressed || b.isPressed;

    void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;

        // --- Entradas suavizadas: el volante y el pedal no saltan de golpe ---
        // Si el volante va al lado contrario o regresa al centro, se mueve mas rapido.
        bool regresando = Mathf.Abs(giro) < 0.01f || Mathf.Sign(giro) != Mathf.Sign(volante);
        volante = Mathf.MoveTowards(volante, giro, (regresando ? retornoVolante : respuestaVolante) * dt);
        pedal = Mathf.MoveTowards(pedal, avance, respuestaPedal * dt);

        Vector3 frente = (rb.rotation * ejeFrente).normalized;
        frente.y = 0f;
        frente.Normalize();
        Vector3 lado = Vector3.Cross(Vector3.up, frente);

        // Descomponer la velocidad real (incluye empujes de choques)
        Vector3 v = rb.linearVelocity;
        velocidadAdelante = Vector3.Dot(v, frente);
        float velocidadLado = Vector3.Dot(v, lado);

        // --- Acelerar / frenar ---
        float objetivo;
        float tasa;
        if (freno)
        {
            objetivo = 0f;
            tasa = frenoDeMano;
        }
        else if (avance > 0f)
        {
            objetivo = velocidad * Mathf.Abs(pedal);
            tasa = velocidadAdelante < -0.1f ? frenado : aceleracion;
        }
        else if (avance < 0f)
        {
            objetivo = -velocidadReversa * Mathf.Abs(pedal);
            tasa = velocidadAdelante > 0.1f ? frenado : aceleracion;
        }
        else
        {
            objetivo = 0f;
            tasa = desaceleracion;
        }
        // Cerca de la velocidad objetivo la aceleracion se va suavizando
        float suave = Mathf.Clamp01(Mathf.Abs(objetivo - velocidadAdelante) / 2f);
        velocidadAdelante = Mathf.MoveTowards(velocidadAdelante, objetivo, tasa * Mathf.Max(0.25f, suave) * dt);

        // --- Agarre lateral, independiente de los FPS de la fisica ---
        float agarre = freno ? agarreFrenoDeMano : agarreLateral;
        // agarre 0.9 = quita el 90% del derrape cada 0.02 s
        float k = -Mathf.Log(Mathf.Max(1e-4f, 1f - agarre)) / 0.02f;
        velocidadLado *= Mathf.Exp(-k * dt);

        Vector3 nuevaVelocidad = frente * velocidadAdelante + lado * velocidadLado;
        nuevaVelocidad.y = v.y;   // conserva la gravedad
        rb.linearVelocity = nuevaVelocidad;

        // --- Giro: sobre el centro de masa, segun la velocidad e invertido en reversa ---
        float rapidez = Mathf.Abs(velocidadAdelante);
        float factorGiro = Mathf.Clamp01(rapidez / velocidadGiroCompleto)
                         * Mathf.Lerp(1f, giroAVelocidadMaxima, Mathf.Clamp01(rapidez / Mathf.Max(0.01f, velocidad)));
        if (velocidadAdelante < 0f) factorGiro = -factorGiro;
        float giroGradosPorSegundo = volante * velocidadGiro * factorGiro;
        rb.angularVelocity = new Vector3(0f, giroGradosPorSegundo * Mathf.Deg2Rad, 0f);
    }
}
