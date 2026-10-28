using UnityEngine;

public class FirstPersonMovement : MonoBehaviour
{
    [Header("Configuración de Velocidad (Súbela aquí)")]
    [SerializeField] private float velocidadMovimiento = 150f; // Valor base aumentado drásticamente
    [SerializeField] private float multiplicadorSprint = 3f;  // Sprint mucho más potente con Shift
    [SerializeField] private float multiplicadorGeneralVelocidad = 2.5f; // Multiplicador extra global

    [Header("Gravedad")]
    [SerializeField] private float gravedad = -25f;

    [Header("Referencias y Controles")]
    [SerializeField] private bool usaGetAxisRaw = true;
    [SerializeField] private Transform camara;

    private CharacterController controlador;
    private Vector3 velocidadVertical;

    private void Awake()
    {
        controlador = GetComponent<CharacterController>();

        if (camara == null && Camera.main)
            camara = Camera.main.transform;
    }

    void Update()
    {
        // 1. Leer las entradas del teclado de forma totalmente limpia
        float valorHorizontal = usaGetAxisRaw ? Input.GetAxisRaw("Horizontal") : Input.GetAxis("Horizontal");
        float valorVertical = usaGetAxisRaw ? Input.GetAxisRaw("Vertical") : Input.GetAxis("Vertical");

        // 2. Obtener vectores de la cámara aplanados
        Vector3 adelanteCamara = camara.forward;
        Vector3 derechaCamara = camara.right;

        adelanteCamara.y = 0f;
        derechaCamara.y = 0f;

        adelanteCamara.Normalize();
        derechaCamara.Normalize();

        // 3. Dirección de movimiento plana
        Vector3 direccionMovimiento = (adelanteCamara * valorVertical + derechaCamara * valorHorizontal);

        if (direccionMovimiento.sqrMagnitude > 0.0001f)
        {
            direccionMovimiento.Normalize();
        }

        // 4. Calcular velocidad actual aplicando Sprint y multiplicadores
        float velocidadActual = velocidadMovimiento;
        if (Input.GetKey(KeyCode.LeftShift))
        {
            velocidadActual *= multiplicadorSprint;
        }

        // Multiplicador global para escalar toda la velocidad al instante
        velocidadActual *= multiplicadorGeneralVelocidad;

        // 5. Gestionar la gravedad para mantenerlo pegado al suelo
        if (controlador.isGrounded)
        {
            velocidadVertical.y = -2f; 
        }
        else
        {
            velocidadVertical.y += gravedad * Time.deltaTime;
        }

        // 6. Aplicar movimiento final directo
        Vector3 movimientoFinal = (direccionMovimiento * velocidadActual + velocidadVertical) * Time.deltaTime;
        controlador.Move(movimientoFinal);
    }
}