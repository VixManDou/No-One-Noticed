using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Teletransporta al jugador a un punto de destino cuando entra en esta zona,
/// con un fundido a negro al salir y al llegar.
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

    [Header("Transición")]
    [Tooltip("Oscurece la pantalla al cruzar la puerta y la aclara al llegar.")]
    public bool usarTransicion = true;

    [Tooltip("Segundos que tarda la pantalla en oscurecerse (y en aclararse).")]
    public float duracionFundido = 0.6f;

    [Tooltip("Segundos que la pantalla se queda en negro antes de aclararse.")]
    public float esperaEnNegro = 0.25f;

    [Tooltip("Opcional: sonido al cruzar la puerta (por ejemplo, una puerta abriéndose).")]
    public AudioClip sonido;

    [Header("Detección")]
    [Tooltip("Tag del jugador.")]
    public string tagJugador = "Player";

    [Tooltip("Segundos en los que el jugador no puede volver a teletransportarse (evita rebotes entre dos teletransportes).")]
    public float enfriamiento = 1f;

    [Tooltip("Muestra mensajes en la Consola para revisar qué está pasando.")]
    public bool mostrarMensajes = false;

    // Compartido entre todos los teletransportes para evitar ir y volver al instante
    private static float ultimoTeletransporte = -999f;

    // Pantalla negra compartida por todas las puertas (se crea sola la primera vez)
    private static Image pantallaNegra;

    private Collider zona;
    private Transform jugador;
    private Collider colliderJugador;
    private bool teletransportando;

    // Reinicia el contador cada vez que se da Play (aunque la recarga de dominio esté desactivada)
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ReiniciarContador()
    {
        ultimoTeletransporte = -999f;
        pantallaNegra = null;
    }

    private void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    private void Awake()
    {
        zona = GetComponent<Collider>();
    }

    private void OnDisable()
    {
        // Si la puerta se apaga a mitad de la transición, que la pantalla no se quede en negro
        if (teletransportando && pantallaNegra != null) SetAlfa(0f);
        teletransportando = false;
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

        if (sonido != null)
            AudioSource.PlayClipAtPoint(sonido, jugador.position);

        if (usarTransicion)
            yield return Fundir(0f, 1f);

        MoverJugador();

        // El enfriamiento empieza al llegar, no al cruzar
        ultimoTeletransporte = Time.time;

        if (usarTransicion)
        {
            yield return new WaitForSecondsRealtime(esperaEnNegro);
            yield return Fundir(1f, 0f);
        }
        else
        {
            yield return null;
        }

        teletransportando = false;
    }

    private void MoverJugador()
    {
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

        // Se reactiva en el mismo frame, para que el script de movimiento nunca lo encuentre apagado
        if (cc != null) cc.enabled = true;
    }

    // ---------- Fundido a negro ----------

    private IEnumerator Fundir(float desde, float hasta)
    {
        AsegurarPantallaNegra();

        // Usa tiempo sin escala: funciona aunque el juego esté en pausa
        float t = 0f;
        while (t < duracionFundido)
        {
            t += Time.unscaledDeltaTime;
            SetAlfa(Mathf.Lerp(desde, hasta, t / duracionFundido));
            yield return null;
        }
        SetAlfa(hasta);
    }

    private static void SetAlfa(float alfa)
    {
        if (pantallaNegra == null) return;
        pantallaNegra.color = new Color(0f, 0f, 0f, alfa);
        pantallaNegra.enabled = alfa > 0f;
    }

    private static void AsegurarPantallaNegra()
    {
        if (pantallaNegra != null) return;

        GameObject goCanvas = new GameObject("UI_Transicion");
        Canvas canvas = goCanvas.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 150; // encima del reloj, cartas y diálogos; debajo del menú de pausa

        GameObject goImagen = new GameObject("Negro", typeof(RectTransform), typeof(Image));
        goImagen.transform.SetParent(goCanvas.transform, false);
        RectTransform rt = (RectTransform)goImagen.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;

        pantallaNegra = goImagen.GetComponent<Image>();
        pantallaNegra.raycastTarget = false;
        SetAlfa(0f);
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