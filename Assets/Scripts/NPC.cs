using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Pon este script en el personaje (necesita un Collider que NO sea trigger, en él o en sus hijos).
/// Al mirarlo aparece "Presiona E para hablar" y al presionar E muestra su diálogo línea por línea.
/// La detección la hace el LectorDeCartas de la cámara.
/// </summary>
public class NPC : MonoBehaviour
{
    [Header("Identidad")]
    [Tooltip("Nombre que aparece en el cuadro de diálogo.")]
    public string nombre = "Desconocido";

    [Header("Diálogo")]
    [Tooltip("Lo que dice la primera vez. Cada elemento es una línea; se avanza con E, Espacio o clic.")]
    [TextArea(2, 5)]
    public string[] dialogo;

    [Tooltip("Opcional: lo que dice si le vuelves a hablar. Si está vacío, repite el diálogo principal.")]
    [TextArea(2, 5)]
    public string[] dialogoRepetido;

    [Tooltip("Mensaje que aparece al mirarlo.")]
    public string mensajeInteraccion = "Presiona E para hablar";

    [Tooltip("Letras por segundo al escribir el texto.")]
    public float velocidadTexto = 40f;

    [Tooltip("Sonido corto que se reproduce en cada línea (por ejemplo, un murmullo).")]
    public AudioClip sonidoVoz;

    [Header("Comportamiento")]
    [Tooltip("El jugador no puede moverse ni girar la cámara mientras habla (el tiempo sigue corriendo).")]
    public bool congelarJugador = true;

    [Tooltip("Gira hacia el jugador mientras habla.")]
    public bool mirarAlJugador = true;

    public float velocidadGiro = 6f;

    [Tooltip("Tag del jugador.")]
    public string tagJugador = "Player";

    [Header("Eventos")]
    [Tooltip("Se ejecuta al terminar de escuchar todo el diálogo (por ejemplo, activar una carta o abrir una puerta).")]
    public UnityEvent alTerminarDialogo;

    [Tooltip("El evento solo se ejecuta la primera vez que termina el diálogo.")]
    public bool eventoSoloUnaVez = true;

    /// <summary>True si el jugador ya escuchó el diálogo completo al menos una vez.</summary>
    public bool Hablado { get; private set; }

    public bool PuedeHablar => dialogo != null && dialogo.Length > 0;

    private bool hablando;
    private bool eventoUsado;
    private Transform jugador;

    public void Hablar()
    {
        string[] lineas = (Hablado && dialogoRepetido != null && dialogoRepetido.Length > 0)
            ? dialogoRepetido
            : dialogo;
        if (lineas == null || lineas.Length == 0) return;

        hablando = true;
        CuadroDialogo.Instancia.Iniciar(this, lineas);
    }

    // Lo llama el CuadroDialogo al cerrarse. "completo" es false si se cerró con Escape antes de terminar.
    public void AlTerminar(bool completo)
    {
        hablando = false;
        if (!completo) return;

        Hablado = true;
        if (eventoSoloUnaVez && eventoUsado) return;
        eventoUsado = true;
        alTerminarDialogo?.Invoke();
    }

    private void Update()
    {
        if (!hablando || !mirarAlJugador) return;

        if (jugador == null)
        {
            GameObject obj = GameObject.FindGameObjectWithTag(tagJugador);
            if (obj == null) return;
            jugador = obj.transform;
        }

        Vector3 direccion = jugador.position - transform.position;
        direccion.y = 0f;
        if (direccion.sqrMagnitude < 0.001f) return;

        // Usa el tiempo normal: si se abre el menú de pausa, el NPC deja de girar
        Quaternion objetivo = Quaternion.LookRotation(direccion);
        transform.rotation = Quaternion.Slerp(transform.rotation, objetivo, velocidadGiro * Time.deltaTime);
    }
}