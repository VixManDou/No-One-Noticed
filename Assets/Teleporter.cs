using System.Collections;
using UnityEngine;

/// <summary>
/// Teletransporta al jugador a un punto de destino cuando entra en esta zona.
/// Ponlo en un objeto con un Collider (de preferencia marcado como "Is Trigger").
/// En lugar de depender de los eventos de física, revisa cada frame si el
/// jugador está tocando la zona, lo que lo hace mucho más confiable.
/// </summary>
[RequireComponent(typeof(Collider))]
public class Teleporter : MonoBehaviour
{
    [Header("Destino")]
    [Tooltip("Objeto vacío que marca dónde aparece el jugador (posición y rotación).")]
    public Transform destino;

    [Tooltip("Si está activo, el jugador mira hacia donde apunta el destino (su eje Z azul).")]
    public bool usarRotacionDelDestino = true;

    [Header("Detección")]
    [Tooltip("Tag del jugador.")]
    public string tagJugador = "Player";

    [Tooltip("Segundos en los que el jugador no puede volver a teletransportarse (evita rebotes entre dos teletransportes).")]
    public float enfriamiento = 1f;

    [Tooltip("Muestra mensajes en la Consola para revisar qué está pasando.")]
    public bool mostrarMensajes = false;

    // Compartido entre todos los teletransportes para evitar ir y volver al instante
    private static float ultimoTeletransporte = -999f;

    private Collider zona;
    private Transform jugador;
    private Collider colliderJugador;
    private bool teletransportando;

    // Reinicia el contador cada vez que se da Play (aunque la recarga de dominio esté desactivada)
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ReiniciarContador()
    {
        ultimoTeletransporte = -999f;
    }

    private void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    private void Awake()
    {
        zona = GetComponent<Collider>();
    }

    private void Update()
    {
        if (teletransportando || destino == null) return;

        if (!BuscarJugador()) return;
        if (!colliderJugador.enabled) return;

        // ¿La caja del jugador toca la caja de la zona?
        if (!zona.bounds.Intersects(colliderJugador.bounds)) return;

        if (Time.time - ultimoTeletransporte < enfriamiento)
        {
            if (mostrarMensajes) Debug.Log($"[Teleporter] '{name}': en enfriamiento.");
            return;
        }

        ultimoTeletransporte = Time.time;
        if (mostrarMensajes) Debug.Log($"[Teleporter] '{name}': teletransportando a '{destino.name}'.");
        StartCoroutine(Teletransportar());
    }

    private bool BuscarJugador()
    {
        if (jugador != null && colliderJugador != null) return true;

        GameObject obj = GameObject.FindGameObjectWithTag(tagJugador);
        if (obj == null) return false;

        jugador = obj.transform;
        colliderJugador = obj.GetComponent<CharacterController>();
        if (colliderJugador == null) colliderJugador = obj.GetComponentInChildren<Collider>();

        if (colliderJugador == null)
        {
            Debug.LogWarning($"[Teleporter] El jugador '{obj.name}' no tiene collider ni Character Controller.");
            jugador = null;
            return false;
        }
        return true;
    }

    private IEnumerator Teletransportar()
    {
        teletransportando = true;

        // El CharacterController sobrescribe la posición si está activo, hay que apagarlo
        CharacterController cc = jugador.GetComponent<CharacterController>();
        Rigidbody rb = jugador.GetComponent<Rigidbody>();

        if (cc != null) cc.enabled = false;

        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;   // En Unity 2022 o anterior usa: rb.velocity
            rb.angularVelocity = Vector3.zero;
            rb.position = destino.position;
            if (usarRotacionDelDestino) rb.rotation = destino.rotation;
        }

        jugador.position = destino.position;
        if (usarRotacionDelDestino)
            jugador.rotation = Quaternion.Euler(0f, destino.eulerAngles.y, 0f);

        Physics.SyncTransforms();
        yield return null; // espera un frame antes de reactivar

        if (cc != null) cc.enabled = true;
        teletransportando = false;
    }

    // Dibuja una línea en el editor desde la zona hasta el destino
    private void OnDrawGizmos()
    {
        if (destino == null) return;
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(transform.position, destino.position);
        Gizmos.DrawWireSphere(destino.position, 0.5f);
        Gizmos.DrawRay(destino.position, destino.forward);
    }
}