using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

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

    [Header("Final")]
    [Tooltip("Márcalo en la ÚLTIMA noche de un interior: al salir por la puerta se muestra la pantalla del final en lugar de pasar a otra noche.")]
    public bool esFinal = false;

    public string tituloJuego = "NO ONE NOTICED";

    [Tooltip("Texto que aparece debajo del título.")]
    [TextArea(3, 8)]
    public string leyendaFinal = "";

    public string textoNumeroFinal = "Final 1 de 3";

    [Tooltip("Escena que se carga al presionar E en la pantalla del final. Déjalo vacío para quedarse en el final.")]
    public string escenaAlTerminar = "MainMenu";

    [Tooltip("Segundos antes de que aparezca 'Presiona E para continuar'.")]
    public float esperaAntesDeContinuar = 3f;

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
    private Transform canvasUI;

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

        if (esFinal)
        {
            yield return MostrarFinal();
            yield break;
        }

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

    // ---------- Pantalla del final ----------

    private IEnumerator MostrarFinal()
    {
        // Congela el juego y apaga lo que podría reaccionar a las teclas
        Time.timeScale = 0f;
        foreach (MenuPausa m in FindObjectsByType<MenuPausa>(FindObjectsSortMode.None)) m.enabled = false;
        foreach (LectorDeCartas l in FindObjectsByType<LectorDeCartas>(FindObjectsSortMode.None)) l.enabled = false;

        mostrarTemporizador = false;
        temporizadorUI.gameObject.SetActive(false);
        objetivoUI.gameObject.SetActive(false);
        SetAlfa(fundidoUI, 1f);

        // El final tiene su propio Canvas encima de todo (transiciones, cartas, pausa)
        GameObject goFinal = new GameObject("UI_Final");
        Canvas canvasFinal = goFinal.AddComponent<Canvas>();
        canvasFinal.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasFinal.sortingOrder = 500;
        CanvasScaler escaladorFinal = goFinal.AddComponent<CanvasScaler>();
        escaladorFinal.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        escaladorFinal.referenceResolution = new Vector2(1920f, AltoReferencia);
        escaladorFinal.matchWidthOrHeight = 1f;

        GameObject goNegro = new GameObject("Negro", typeof(RectTransform), typeof(Image));
        goNegro.transform.SetParent(goFinal.transform, false);
        RectTransform rn = (RectTransform)goNegro.transform;
        rn.anchorMin = Vector2.zero;
        rn.anchorMax = Vector2.one;
        rn.offsetMin = rn.offsetMax = Vector2.zero;
        Image negro = goNegro.GetComponent<Image>();
        negro.color = Color.black;
        negro.raycastTarget = false;

        Transform padreFinal = goFinal.transform;
        Debug.Log($"[GestorNoche] '{name}' es la última noche: se muestra el final.", this);

        // Título arriba
        Text titulo = CrearTexto("FinalTitulo", padreFinal, 96, TextAnchor.MiddleCenter,
            new Vector2(0.5f, 1f), new Vector2(0f, -100f), new Vector2(1700f, 170f));
        titulo.fontStyle = FontStyle.Bold;
        titulo.text = tituloJuego;

        // Zona visible de la leyenda (recorta lo que se sale)
        GameObject goVista = new GameObject("FinalVista", typeof(RectTransform), typeof(RectMask2D));
        goVista.transform.SetParent(padreFinal, false);
        RectTransform vistaFinal = (RectTransform)goVista.transform;
        vistaFinal.anchorMin = new Vector2(0.5f, 0f);
        vistaFinal.anchorMax = new Vector2(0.5f, 1f);
        vistaFinal.pivot = new Vector2(0.5f, 0.5f);
        vistaFinal.offsetMin = new Vector2(-650f, 190f);
        vistaFinal.offsetMax = new Vector2(650f, -300f);

        Text leyenda = CrearTexto("FinalLeyenda", vistaFinal, 32, TextAnchor.UpperCenter,
            new Vector2(0.5f, 1f), Vector2.zero, new Vector2(1300f, 100f));
        RectTransform rLeyenda = leyenda.rectTransform;
        rLeyenda.anchorMin = new Vector2(0f, 1f);
        rLeyenda.anchorMax = new Vector2(1f, 1f);
        rLeyenda.pivot = new Vector2(0.5f, 1f);
        rLeyenda.anchoredPosition = Vector2.zero;
        rLeyenda.sizeDelta = new Vector2(0f, 100f);
        leyenda.fontStyle = FontStyle.Italic;
        leyenda.color = new Color(0.85f, 0.85f, 0.85f);
        leyenda.verticalOverflow = VerticalWrapMode.Overflow;
        leyenda.lineSpacing = 1.1f;
        leyenda.text = leyendaFinal;

        // Barra de desplazamiento a la derecha de la leyenda
        Image barraFinal = CrearImagenFinal("FinalBarra", padreFinal, new Color(1f, 1f, 1f, 0.12f));
        RectTransform rBarra = barraFinal.rectTransform;
        rBarra.anchorMin = new Vector2(0.5f, 0f);
        rBarra.anchorMax = new Vector2(0.5f, 1f);
        rBarra.offsetMin = new Vector2(675f, 190f);
        rBarra.offsetMax = new Vector2(679f, -300f);
        Image manijaFinal = CrearImagenFinal("FinalManija", rBarra, new Color(1f, 1f, 1f, 0.7f));
        RectTransform rManija = manijaFinal.rectTransform;
        rManija.anchorMin = new Vector2(0f, 1f);
        rManija.anchorMax = new Vector2(1f, 1f);
        rManija.pivot = new Vector2(0.5f, 1f);
        barraFinal.gameObject.SetActive(false);

        // Invisibles desde el inicio: si no, se verían un instante mientras se calcula el tamaño
        SetAlfa(titulo, 0f);
        SetAlfa(leyenda, 0f);

        // Espera un frame para que Unity calcule el tamaño del texto
        yield return null;
        Canvas.ForceUpdateCanvases();
        float altoTexto = leyenda.preferredHeight + 20f;
        float altoVista = vistaFinal.rect.height;
        rLeyenda.sizeDelta = new Vector2(0f, altoTexto);
        float maximo = Mathf.Max(0f, altoTexto - altoVista);

        if (maximo > 0f)
        {
            StartCoroutine(DesplazarFinal(rLeyenda, rManija, altoVista, altoTexto, maximo));
        }
        else
        {
            // Si el texto es corto, se centra en la zona visible
            rLeyenda.anchoredPosition = new Vector2(0f, -(altoVista - altoTexto) * 0.5f);
        }

        Text numero = CrearTexto("FinalNumero", padreFinal, 32, TextAnchor.MiddleCenter,
            new Vector2(0.5f, 0f), new Vector2(0f, 110f), new Vector2(900f, 50f));
        numero.color = new Color(0.95f, 0.42f, 0.1f);
        numero.text = textoNumeroFinal;

        Text continuar = CrearTexto("FinalContinuar", padreFinal, 22, TextAnchor.MiddleCenter,
            new Vector2(0.5f, 0f), new Vector2(0f, 50f), new Vector2(900f, 40f));
        continuar.color = new Color(1f, 1f, 1f, 0.6f);
        continuar.text = maximo > 0f
            ? "[E] Continuar        [Rueda / W S] Desplazar"
            : "Presiona E para continuar";

        SetAlfa(titulo, 0f);
        SetAlfa(leyenda, 0f);
        SetAlfa(numero, 0f);
        SetAlfa(continuar, 0f);

        // Aparecen uno tras otro
        yield return new WaitForSecondsRealtime(1f);
        yield return Aparecer(titulo, 2f);
        yield return new WaitForSecondsRealtime(1f);
        if (!string.IsNullOrEmpty(leyendaFinal))
        {
            yield return Aparecer(leyenda, 2f);
            if (maximo > 0f) barraFinal.gameObject.SetActive(true);
            yield return new WaitForSecondsRealtime(1f);
        }
        yield return Aparecer(numero, 1.5f);

        if (string.IsNullOrEmpty(escenaAlTerminar)) yield break;

        yield return new WaitForSecondsRealtime(esperaAntesDeContinuar);
        yield return Aparecer(continuar, 1f, 0.6f);

        while (!EstadoJuego.TeclaInteractuar())
            yield return null;

        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        SceneManager.LoadScene(escenaAlTerminar);
    }

    // Mueve la leyenda con la rueda del mouse, W/S o las flechas
    private IEnumerator DesplazarFinal(RectTransform texto, RectTransform manija, float altoVista, float altoTexto, float maximo)
    {
        float desplazamiento = 0f;
        RectTransform barra = (RectTransform)manija.parent;

        while (true)
        {
            desplazamiento += LeerDesplazamiento();
            desplazamiento = Mathf.Clamp(desplazamiento, 0f, maximo);
            texto.anchoredPosition = new Vector2(0f, desplazamiento);

            float altoBarra = barra.rect.height;
            float altoManija = Mathf.Max(30f, altoBarra * (altoVista / altoTexto));
            manija.sizeDelta = new Vector2(0f, altoManija);
            manija.anchoredPosition = new Vector2(0f, -(altoBarra - altoManija) * (desplazamiento / maximo));

            yield return null;
        }
    }

    private static float LeerDesplazamiento()
    {
        const float pasoRueda = 80f;
        const float velocidadTeclas = 600f;
        float cambio = 0f;
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null)
        {
            float rueda = Mouse.current.scroll.ReadValue().y;
            if (rueda != 0f) cambio -= Mathf.Sign(rueda) * pasoRueda;
        }
        if (Keyboard.current != null)
        {
            if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed)
                cambio += velocidadTeclas * Time.unscaledDeltaTime;
            if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed)
                cambio -= velocidadTeclas * Time.unscaledDeltaTime;
        }
#else
        float rueda = Input.mouseScrollDelta.y;
        if (rueda != 0f) cambio -= Mathf.Sign(rueda) * pasoRueda;
        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))
            cambio += velocidadTeclas * Time.unscaledDeltaTime;
        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))
            cambio -= velocidadTeclas * Time.unscaledDeltaTime;
#endif
        return cambio;
    }

    private Image CrearImagenFinal(string nombre, Transform padre, Color color)
    {
        GameObject go = new GameObject(nombre, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(padre, false);
        Image img = go.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    private IEnumerator Aparecer(Text texto, float duracion, float alfaFinal = 1f)
    {
        for (float t = 0f; t < duracion; t += Time.unscaledDeltaTime)
        {
            SetAlfa(texto, alfaFinal * (t / duracion));
            yield return null;
        }
        SetAlfa(texto, alfaFinal);
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
        canvasUI = goCanvas.transform;
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