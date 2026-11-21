using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Control arcade de la combi.
///
/// Cambios respecto a la version original (revision):
///  - Ya no se fija la velocidad de golpe: la combi acelera, frena y se
///    detiene sola poco a poco (inercia).
///  - El giro depende de la velocidad: parada no gira (no es un tanque) y en
///    reversa el volante se invierte, como en un vehiculo real.
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
    [Tooltip("Grados por segundo a velocidad alta")]
    public float velocidadGiro = 70f;

    [Header("Aceleracion (m/s por segundo)")]
    public float aceleracion = 6f;
    public float frenado = 14f;          // al pulsar la tecla contraria
    public float desaceleracion = 3f;    // al soltar el acelerador
    public float frenoDeMano = 20f;

    [Header("Manejo")]
    [Tooltip("Velocidad (m/s) a partir de la cual el giro es completo")]
    public float velocidadGiroCompleto = 6f;
    [Range(0f, 1f), Tooltip("1 = sin derrape, 0 = sobre hielo")]
    public float agarreLateral = 0.9f;

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

    /// <summary>Freno de mano (Espacio) presionado.</summary>
    public bool FrenoDeMano => freno;

    /// <summary>True si la combi esta frenando (para las luces de freno).</summary>
    public bool Frenando => freno
        || (avance < 0f && velocidadAdelante > 0.5f)
        || (avance > 0f && velocidadAdelante < -0.5f);

    Rigidbody rb;
    float avance;       // 1 = W, -1 = S
    float giro;         // -1 = A, 1 = D
    bool freno;         // Espacio
    float velocidadAdelante;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        // La combi solo puede girar sobre el eje Y (no se voltea)
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
    }

    void Update()
    {
        Keyboard teclado = Keyboard.current;
        if (teclado == null) return;

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
        Vector3 frente = transform.TransformDirection(ejeFrente.normalized);
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
            objetivo = velocidad;
            tasa = velocidadAdelante < -0.1f ? frenado : aceleracion;
        }
        else if (avance < 0f)
        {
            objetivo = -velocidadReversa;
            tasa = velocidadAdelante > 0.1f ? frenado : aceleracion;
        }
        else
        {
            objetivo = 0f;
            tasa = desaceleracion;
        }
        velocidadAdelante = Mathf.MoveTowards(velocidadAdelante, objetivo, tasa * dt);

        // --- Agarre lateral: reduce el derrape sin anularlo de golpe ---
        velocidadLado = Mathf.Lerp(velocidadLado, 0f, agarreLateral);

        Vector3 nuevaVelocidad = frente * velocidadAdelante + lado * velocidadLado;
        nuevaVelocidad.y = v.y;   // conserva la gravedad
        rb.linearVelocity = nuevaVelocidad;

        // --- Giro proporcional a la velocidad e invertido en reversa ---
        float factorGiro = Mathf.Clamp(velocidadAdelante / velocidadGiroCompleto, -1f, 1f);
        Quaternion rotacion = Quaternion.Euler(0f, giro * velocidadGiro * factorGiro * dt, 0f);
        rb.MoveRotation(rotacion * rb.rotation);
        rb.angularVelocity = Vector3.zero;
    }
}
