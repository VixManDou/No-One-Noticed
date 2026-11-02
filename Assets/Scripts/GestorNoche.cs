using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Controla una noche del juego:
/// - Temporizador (15 min por defecto) que se pausa mientras lees una carta.
/// - Cuenta las cartas leídas. Al leerlas todas muestra "Se hace tarde, debo regresar al pueblo".
/// - Al llegar a la zona de regreso, pasa a la siguiente noche (otra escena o eventos en esta misma).
/// - Si se acaba el tiempo, reinicia la noche (opcional).
/// Ponlo en un objeto vacío, por ejemplo "Noche1".
/// </summary>
public class GestorNoche : MonoBehaviour
{
    [Header("Noche")]
    [Tooltip("Nombre que aparece al iniciar la noche.")]
    public string nombreNoche = "Noche 1";

    [Tooltip("Si está activo, usa automáticamente todas las cartas que estén DENTRO de este objeto (aunque estén desactivadas) y la lista de abajo se ignora.")]
    public bool buscarCartasAutomaticamente = true;

    [Tooltip("Las cartas que hay que leer esta noche. Solo se usa si 'Buscar Cartas Automáticamente' está desactivado.")]
    public Carta[] cartas;

    [Tooltip("Duración real de la noche en minutos.")]
    public float duracionMinutos = 15f;

    [Header("Reloj del juego")]
    [Tooltip("Muestra la hora del juego en lugar de la cuenta regresiva.")]
    public bool mostrarHoraDelJuego = true;

    [Tooltip("Hora del juego al iniciar la noche (formato 24 h). 19 = 7:00 PM.")]
    [Range(0f, 24f)]
    public float horaInicio = 19f;

    [Tooltip("Hora del juego al terminar el tiempo (formato 24 h). 22 = 10:00 PM.")]
    [Range(0f, 24f)]
    public float horaFin = 22f;

    [Tooltip("Muestra la hora como 7:00 PM en lugar de 19:00.")]
    public bool formato12Horas = false;

    [Header("Mensajes")]
    public string mensajeCompletado = "Se hace tarde, debo regresar al pueblo.";
    public string objetivoCartas = "Encuentra las cartas";
    public string objetivoRegreso = "Regresa al pueblo";
    public string mensajeTiempoAgotado = "Se hizo demasiado tarde...";

    [Header("Interior")]
    [Tooltip("Zona (un cubo con collider) que cubre el interior donde están las cartas. " +
             "El contador de cartas aparece la primera vez que el jugador entra. Si la dejas vacía, se ve desde el inicio.")]
    public Collider zonaInterior;

    [Header("Regreso al pueblo")]
    [Tooltip("Zona (un cubo con collider) a la que hay que llegar después de leer todas las cartas. " +
             "Si la dejas vacía, se pasa a la siguiente noche apenas aparece el mensaje.")]
    public Collider zonaRegreso;

    [Tooltip("Tag del jugador.")]
    public string tagJugador = "Player";

    [Header("Siguiente noche")]
    [Tooltip("Nombre de la escena de la siguiente noche (por ejemplo 'Noche2'). " +
             "Déjalo vacío para continuar en esta misma escena usando 'Mover Jugador A' y 'Al Pasar Noche'.")]
    public string siguienteEscena = "";

    [Tooltip("Solo si la siguiente noche es en esta misma escena: a dónde aparece el jugador (por ejemplo, su cama).")]
    public Transform moverJugadorA;

    [Tooltip("El objeto de la noche que sigue (por ejemplo 'A_Noche2'). Se activa solo al pasar de noche, junto con sus cartas si son hijas suyas.")]
    public GameObject siguienteNoche;

    [Tooltip("Apaga este gestor al terminar (para que el de la siguiente noche tome el control).")]
    public bool desactivarAlTerminar = true;

    [Tooltip("Se ejecuta al leer todas las cartas (por ejemplo, para activar la zona de regreso).")]
    public UnityEvent alCompletarCartas;

    [Tooltip("Se ejecuta con la pantalla en negro al pasar de noche (por ejemplo, activar 'Noche2' y sus cartas).")]
    public UnityEvent alPasarNoche;

    [Header("Tiempo agotado")]
    [Tooltip("Si se acaba el tiempo, se recarga la escena y la noche empieza de nuevo.")]
    public bool reiniciarAlAgotarTiempo = true;

    [Tooltip("Se ejecuta cuando se acaba el tiempo (por ejemplo, para un susto o un final malo).")]
    public UnityEvent alAgotarTiempo;

    [Header("Apariencia")]
    [Tooltip("Fuente de los textos. Si lo dejas vacío, usa la de Unity.")]
    public Font fuente;

    public bool mostrarTemporizador = true;

    [Tooltip("Segundos que tarda la pantalla en oscurecerse o aclararse.")]
    public float duracionFundido = 1.5f;

    // Estado
    private float tiempoRestante;
    private bool completado;
    private bool terminado;
    private bool mensajePendiente;
    private bool objetivoVisible;
    private Collider colliderJugador;
    private Teleporter teleporterPuerta;

    // Interfaz
    private Text temporizadorUI;
    private Text objetivoUI;
    private Text mensajeUI;
    private Text tituloUI;
    private Image fundidoUI;

    private const float AltoReferencia = 1080f;

    private void Start()
    {
        tiempoRestante = duracionMinutos * 60f;
        objetivoVisible = zonaInterior == null;

        // Toma todas las cartas que son hijas de esta noche (incluidas las desactivadas)
        if (buscarCartasAutomaticamente)
            cartas = GetComponentsInChildren<Carta>(true);

        Debug.Log($"[GestorNoche] Inicia '{name}' con {(cartas != null ? cartas.Length : 0)} cartas.", this);

        if (fuente == null)
            fuente = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        CrearInterfaz();
        ActualizarInterfaz();
        StartCoroutine(Inicio());
    }

    private void Update()
    {
        if (terminado) return;

        // Time.deltaTime vale 0 mientras una carta está abierta, así que el tiempo se pausa al leer
        tiempoRestante -= Time.deltaTime;
        if (tiempoRestante <= 0f)
        {
            tiempoRestante = 0f;
            terminado = true;
            ActualizarInterfaz();
            StartCoroutine(TiempoAgotado());
            return;
        }

        // El contador de cartas aparece al entrar por primera vez al interior
        if (!objetivoVisible && JugadorEnZona(zonaInterior))
            objetivoVisible = true;

        if (!completado && TodasLeidas())
        {
            completado = true;
            mensajePendiente = true;

            // Apaga el teletransporte normal de la puerta para que la salida la maneje este gestor
            if (zonaRegreso != null)
            {
                teleporterPuerta = zonaRegreso.GetComponent<Teleporter>();
                if (teleporterPuerta != null) teleporterPuerta.enabled = false;
            }

            alCompletarCartas?.Invoke();
        }

        // Espera a que el jugador cierre la carta antes de mostrar el mensaje
        if (mensajePendiente && Time.timeScale > 0f)
        {
            mensajePendiente = false;
            if (zonaRegreso == null)
            {
                terminado = true;
                StartCoroutine(MensajeYPasarNoche());
            }
            else
            {
                StartCoroutine(MostrarTexto(mensajeUI, mensajeCompletado, 4f));
            }
        }

        if (completado && !mensajePendiente && zonaRegreso != null && JugadorEnZona(zonaRegreso))
        {
            terminado = true;
            StartCoroutine(PasarNoche());
        }

        ActualizarInterfaz();
    }

    // ---------- Lógica ----------

    private int CartasLeidas()
    {
        int n = 0;
        foreach (Carta c in cartas)
            if (c != null && c.Leida) n++;
        return n;
    }

    private bool TodasLeidas()
    {
        return cartas != null && cartas.Length > 0 && CartasLeidas() >= cartas.Length;
    }

    private bool JugadorEnZona(Collider zona)
    {
        if (zona == null || !zona.enabled || !zona.gameObject.activeInHierarchy) return false;

        if (colliderJugador == null)
        {
            GameObject jugador = GameObject.FindGameObjectWithTag(tagJugador);
            if (jugador == null) return false;
            colliderJugador = jugador.GetComponent<CharacterController>();
            if (colliderJugador == null) colliderJugador = jugador.GetComponentInChildren<Collider>();
            if (colliderJugador == null) return false;
        }

        return colliderJugador.enabled && zona.bounds.Intersects(colliderJugador.bounds);
    }

    private void MoverJugador()
    {
        if (moverJugadorA == null) return;
        GameObject jugador = GameObject.FindGameObjectWithTag(tagJugador);
        if (jugador == null) return;

        // El Character Controller se apaga un instante para que no regrese al jugador a su lugar
        CharacterController cc = jugador.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;
        jugador.transform.SetPositionAndRotation(moverJugadorA.position,
            Quaternion.Euler(0f, moverJugadorA.eulerAngles.y, 0f));
        Physics.SyncTransforms();
        if (cc != null) cc.enabled = true;
    }

    // ---------- Secuencias ----------

    private IEnumerator Inicio()
    {
        SetAlfa(fundidoUI, 1f);
        yield return Fundir(1f, 0f);
        yield return MostrarTexto(tituloUI, nombreNoche, 2.5f);
    }

    private IEnumerator MensajeYPasarNoche()
    {
        yield return MostrarTexto(mensajeUI, mensajeCompletado, 3.5f);
        yield return PasarNoche();
    }

    private IEnumerator PasarNoche()
    {
        yield return Fundir(0f, 1f);

        if (!string.IsNullOrEmpty(siguienteEscena))
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(siguienteEscena);
            yield break;
        }

        MoverJugador();

        // La puerta vuelve a funcionar normal para la siguiente noche
        if (teleporterPuerta != null) teleporterPuerta.enabled = true;

        ActivarSiguienteNoche();
        alPasarNoche?.Invoke();
        yield return new WaitForSecondsRealtime(0.5f);
        yield return Fundir(1f, 0f);

        if (desactivarAlTerminar) gameObject.SetActive(false);
    }

    private void ActivarSiguienteNoche()
    {
        if (siguienteNoche == null)
        {
            Debug.LogWarning($"[GestorNoche] '{name}' terminó, pero su campo 'Siguiente Noche' está vacío. " +
                             "Arrastra ahí el objeto de la noche que sigue.", this);
            return;
        }

        if (siguienteNoche == gameObject)
        {
            Debug.LogWarning($"[GestorNoche] '{name}' tiene a sí misma en 'Siguiente Noche'.", this);
            return;
        }

        // Si la siguiente noche quedó DENTRO de esta, se apagaría junto con ella: la sacamos al mismo nivel
        if (siguienteNoche.transform.IsChildOf(transform))
        {
            Debug.LogWarning($"[GestorNoche] '{siguienteNoche.name}' estaba dentro de '{name}'. " +
                             "Se movió al mismo nivel para que no se apague. Corrígelo también en la Hierarchy.", this);
            siguienteNoche.transform.SetParent(transform.parent, true);
        }

        siguienteNoche.SetActive(true);

        GestorNoche gestor = siguienteNoche.GetComponent<GestorNoche>();
        if (gestor == null)
            Debug.LogWarning($"[GestorNoche] '{siguienteNoche.name}' no tiene el componente GestorNoche.", this);
        else if (!gestor.enabled)
        {
            Debug.LogWarning($"[GestorNoche] El GestorNoche de '{siguienteNoche.name}' estaba desmarcado. Se activó.", this);
            gestor.enabled = true;
        }

        if (!siguienteNoche.activeInHierarchy)
            Debug.LogWarning($"[GestorNoche] '{siguienteNoche.name}' no se pudo mostrar porque uno de sus objetos padre está desactivado.", this);
        else
            Debug.Log($"[GestorNoche] '{name}' terminó. Empieza '{siguienteNoche.name}'.", this);
    }

    private IEnumerator TiempoAgotado()
    {
        yield return MostrarTexto(mensajeUI, mensajeTiempoAgotado, 3f);
        alAgotarTiempo?.Invoke();

        if (reiniciarAlAgotarTiempo)
        {
            yield return Fundir(0f, 1f);
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
    }

    private IEnumerator Fundir(float desde, float hasta)
    {
        float t = 0f;
        while (t < duracionFundido)
        {
            t += Time.unscaledDeltaTime;
            SetAlfa(fundidoUI, Mathf.Lerp(desde, hasta, t / duracionFundido));
            yield return null;
        }
        SetAlfa(fundidoUI, hasta);
    }

    private IEnumerator MostrarTexto(Text texto, string contenido, float duracion)
    {
        texto.text = contenido;
        const float fundido = 0.6f;
        for (float t = 0f; t < fundido; t += Time.unscaledDeltaTime)
        {
            SetAlfa(texto, t / fundido);
            yield return null;
        }
        SetAlfa(texto, 1f);
        yield return new WaitForSecondsRealtime(duracion);
        for (float t = 0f; t < fundido; t += Time.unscaledDeltaTime)
        {
            SetAlfa(texto, 1f - t / fundido);
            yield return null;
        }
        SetAlfa(texto, 0f);
    }

    // ---------- Interfaz ----------

    private void ActualizarInterfaz()
    {
        temporizadorUI.gameObject.SetActive(mostrarTemporizador);
        objetivoUI.gameObject.SetActive(mostrarTemporizador && (objetivoVisible || completado));

        if (mostrarHoraDelJuego)
        {
            // Convierte el tiempo real transcurrido en la hora del juego (por ejemplo, de 19:00 a 22:00)
            float total = duracionMinutos * 60f;
            float progreso = total > 0f ? 1f - tiempoRestante / total : 1f;
            float duracionHoras = horaFin - horaInicio;
            if (duracionHoras <= 0f) duracionHoras += 24f; // por si la noche cruza la medianoche
            float horaActual = horaInicio + duracionHoras * Mathf.Clamp01(progreso);

            int minutosTotales = Mathf.FloorToInt(horaActual * 60f);
            int hora = (minutosTotales / 60) % 24;
            int minuto = minutosTotales % 60;

            if (formato12Horas)
            {
                int hora12 = hora % 12 == 0 ? 12 : hora % 12;
                temporizadorUI.text = $"{hora12}:{minuto:00} {(hora < 12 ? "AM" : "PM")}";
            }
            else
            {
                temporizadorUI.text = $"{hora:00}:{minuto:00}";
            }
        }
        else
        {
            int segundos = Mathf.CeilToInt(tiempoRestante);
            temporizadorUI.text = $"{segundos / 60:00}:{segundos % 60:00}";
        }
        temporizadorUI.color = tiempoRestante <= 60f ? new Color(0.9f, 0.2f, 0.15f) : Color.white;

        objetivoUI.text = completado
            ? objetivoRegreso
            : $"{objetivoCartas} ({CartasLeidas()}/{(cartas != null ? cartas.Length : 0)})";
    }

    private void CrearInterfaz()
    {
        GameObject goCanvas = new GameObject("UI_Noche");
        goCanvas.transform.SetParent(transform, false); // se oculta junto con el gestor
        Canvas canvas = goCanvas.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 90; // debajo de la pantalla de lectura de cartas
        CanvasScaler escalador = goCanvas.AddComponent<CanvasScaler>();
        escalador.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        escalador.referenceResolution = new Vector2(1920f, AltoReferencia);
        escalador.matchWidthOrHeight = 1f;

        // Temporizador arriba a la derecha
        temporizadorUI = CrearTexto("Temporizador", goCanvas.transform, 44, TextAnchor.UpperRight,
            new Vector2(1f, 1f), new Vector2(-40f, -30f), new Vector2(400f, 60f));

        // Objetivo debajo del temporizador
        objetivoUI = CrearTexto("Objetivo", goCanvas.transform, 24, TextAnchor.UpperRight,
            new Vector2(1f, 1f), new Vector2(-40f, -88f), new Vector2(700f, 40f));
        objetivoUI.color = new Color(1f, 1f, 1f, 0.85f);

        // Mensaje en la parte de abajo
        mensajeUI = CrearTexto("Mensaje", goCanvas.transform, 38, TextAnchor.MiddleCenter,
            new Vector2(0.5f, 0f), new Vector2(0f, 220f), new Vector2(1500f, 80f));
        mensajeUI.fontStyle = FontStyle.Italic;
        SetAlfa(mensajeUI, 0f);

        // Título de la noche en el centro
        tituloUI = CrearTexto("TituloNoche", goCanvas.transform, 80, TextAnchor.MiddleCenter,
            new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1400f, 140f));
        SetAlfa(tituloUI, 0f);

        // Pantalla negra para las transiciones (encima de todo lo demás)
        GameObject goFundido = new GameObject("Fundido", typeof(RectTransform), typeof(Image));
        goFundido.transform.SetParent(goCanvas.transform, false);
        RectTransform rf = (RectTransform)goFundido.transform;
        rf.anchorMin = Vector2.zero;
        rf.anchorMax = Vector2.one;
        rf.offsetMin = rf.offsetMax = Vector2.zero;
        fundidoUI = goFundido.GetComponent<Image>();
        fundidoUI.color = Color.black;
        fundidoUI.raycastTarget = false;
        tituloUI.transform.SetAsLastSibling(); // el título se ve encima del negro
    }

    private Text CrearTexto(string nombre, Transform padre, int tamano, TextAnchor alineacion,
        Vector2 ancla, Vector2 posicion, Vector2 tamanoCaja)
    {
        GameObject go = new GameObject(nombre, typeof(RectTransform));
        go.transform.SetParent(padre, false);
        RectTransform rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = ancla;
        rt.pivot = ancla;
        rt.anchoredPosition = posicion;
        rt.sizeDelta = tamanoCaja;

        Text t = go.AddComponent<Text>();
        t.font = fuente;
        t.fontSize = tamano;
        t.alignment = alineacion;
        t.color = Color.white;
        t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        go.AddComponent<Shadow>().effectDistance = new Vector2(2f, -2f);
        return t;
    }

    private static void SetAlfa(Graphic g, float alfa)
    {
        Color c = g.color;
        c.a = alfa;
        g.color = c;
    }
}