using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Pon este script en el objeto 3D de la carta (necesita un Collider que NO sea trigger).
/// Aquí se configura el título, la imagen y el texto que se mostrarán al leerla.
/// </summary>
public class Carta : MonoBehaviour
{
    [Header("Contenido")]
    [Tooltip("Título de la carta. Aparece arriba a la izquierda y en el panel.")]
    public string titulo = "Carta";

    [Tooltip("La imagen de la carta. Debe estar importada como 'Sprite (2D and UI)'.")]
    public Sprite imagen;

    [Tooltip("Texto que se muestra en el panel de la derecha. Admite <b>negritas</b>, <i>cursivas</i> y <color=#E8C547>colores</color>.")]
    [TextArea(5, 30)]
    public string texto;

    [Header("Presentación")]
    [Tooltip("Inclinación de la imagen en grados, para que parezca un papel suelto. 0 = derecha.")]
    [Range(-20f, 20f)]
    public float inclinacion = -6f;

    [Header("Opcional")]
    [Tooltip("Mensaje que aparece al mirar la carta.")]
    public string mensajeInteraccion = "Presiona E para leer";

    [Tooltip("Sonido al abrir la carta (por ejemplo, papel).")]
    public AudioClip sonido;

    [Tooltip("Se ejecuta cada vez que el jugador lee la carta (para misiones, abrir puertas, etc.).")]
    public UnityEvent alLeer;

    /// <summary>True si el jugador ya la leyó al menos una vez.</summary>
    public bool Leida { get; private set; }

    public void MarcarLeida()
    {
        Leida = true;
        alLeer?.Invoke();
    }
}