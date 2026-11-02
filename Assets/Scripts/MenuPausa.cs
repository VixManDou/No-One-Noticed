using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

/// <summary>
/// Menú de pausa: se abre y se cierra con Escape.
/// Opciones: Continuar, Menú principal y Salir. La interfaz se crea sola al iniciar.
/// Ponlo en la cámara del jugador (junto al LectorDeCartas) o en cualquier objeto activo.
/// </summary>
public class MenuPausa : MonoBehaviour
{
    [Header("Escenas")]
    [Tooltip("Nombre exacto de la escena del menú principal (debe estar en el Scene List).")]
    public string escenaMenuPrincipal = "MainMenu";

    [Header("Jugador")]
    [Tooltip("Scripts que se apagan mientras el juego está en pausa (por ejemplo, el de mirar con el mouse).")]
    public MonoBehaviour[] scriptsADesactivar;

    [Tooltip("Al continuar, vuelve a bloquear y ocultar el cursor.")]
    public bool bloquearCursorAlContinuar = true;

    [Header("Apariencia")]
    [Tooltip("Fuente de los textos. Si lo dejas vacío, usa la de Unity.")]
    public Font fuente;

    public Color colorTitulo = new Color(0.95f, 0.42f, 0.1f);

    [Range(0f, 1f)]
    public float oscuridadFondo = 0.8f;

    private GameObject panel;
    private float escalaTiempoAnterior = 1f;
    private bool[] estabanActivos;

    private const float AltoReferencia = 1080f;

    private void Awake()
    {
        if (fuente == null)
            fuente = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        AsegurarEventSystem();
        CrearInterfaz();
    }

    private void Update()
    {
        if (!EstadoJuego.TeclaEscape()) return;

        if (EstadoJuego.Pausado)
        {
            Continuar();
            return;
        }

        // Escape también cierra cartas y diálogos: en ese caso no se abre la pausa
        if (EstadoJuego.CartaAbierta || EstadoJuego.DialogoAbierto || EstadoJuego.SeCerroEsteFrame) return;

        Pausar();
    }

    private void OnDisable()
    {
        if (EstadoJuego.Pausado) Continuar();
    }

    // ---------- Acciones ----------

    public void Pausar()
    {
        EstadoJuego.Pausado = true;

        escalaTiempoAnterior = Time.timeScale > 0f ? Time.timeScale : 1f;
        Time.timeScale = 0f;
        AudioListener.pause = true;

        estabanActivos = new bool[scriptsADesactivar.Length];
        for (int i = 0; i < scriptsADesactivar.Length; i++)
        {
            if (scriptsADesactivar[i] == null) continue;
            estabanActivos[i] = scriptsADesactivar[i].enabled;
            scriptsADesactivar[i].enabled = false;
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        panel.SetActive(true);
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
    }

    public void Continuar()
    {
        // Al detener el juego la interfaz puede destruirse antes que este script
        if (panel != null) panel.SetActive(false);

        Time.timeScale = escalaTiempoAnterior;
        AudioListener.pause = false;

        for (int i = 0; i < scriptsADesactivar.Length; i++)
        {
            if (scriptsADesactivar[i] == null) continue;
            scriptsADesactivar[i].enabled = estabanActivos == null || estabanActivos[i];
        }

        if (bloquearCursorAlContinuar)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        EstadoJuego.Pausado = false;
        EstadoJuego.MarcarCierre();
    }

    public void IrAlMenuPrincipal()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;
        EstadoJuego.Pausado = false;
        EstadoJuego.CartaAbierta = false;
        EstadoJuego.DialogoAbierto = false;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        SceneManager.LoadScene(escenaMenuPrincipal);
    }

    public void Salir()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;
        EstadoJuego.Pausado = false;

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ---------- Interfaz ----------

    // Los botones necesitan un EventSystem en la escena; si no existe, se crea
    private void AsegurarEventSystem()
    {
        if (FindFirstObjectByType<EventSystem>() != null) return;

        GameObject es = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
        es.AddComponent<InputSystemUIInputModule>();
#else
        es.AddComponent<StandaloneInputModule>();
#endif
    }

    private void CrearInterfaz()
    {
        GameObject goCanvas = new GameObject("UI_Pausa");
        Canvas canvas = goCanvas.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200; // encima de todo
        CanvasScaler escalador = goCanvas.AddComponent<CanvasScaler>();
        escalador.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        escalador.referenceResolution = new Vector2(1920f, AltoReferencia);
        escalador.matchWidthOrHeight = 1f;
        goCanvas.AddComponent<GraphicRaycaster>();

        // Fondo oscuro
        panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(goCanvas.transform, false);
        RectTransform rp = (RectTransform)panel.transform;
        rp.anchorMin = Vector2.zero;
        rp.anchorMax = Vector2.one;
        rp.offsetMin = rp.offsetMax = Vector2.zero;
        panel.GetComponent<Image>().color = new Color(0f, 0f, 0f, oscuridadFondo);

        // Título
        Text titulo = CrearTexto("Titulo", panel.transform, 90, colorTitulo, new Vector2(0f, 240f), new Vector2(900f, 140f));
        titulo.text = "PAUSA";
        titulo.fontStyle = FontStyle.Bold;
        titulo.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(3f, -3f);

        // Botones
        CrearBoton(panel.transform, "Continuar", new Vector2(0f, 60f), Continuar);
        CrearBoton(panel.transform, "Menú principal", new Vector2(0f, -40f), IrAlMenuPrincipal);
        CrearBoton(panel.transform, "Salir", new Vector2(0f, -140f), Salir);

        // Indicación
        Text indicacion = CrearTexto("Indicacion", panel.transform, 24, new Color(1f, 1f, 1f, 0.6f),
            new Vector2(0f, -330f), new Vector2(900f, 40f));
        indicacion.text = "[Esc] Continuar";

        panel.SetActive(false);
    }

    private void CrearBoton(Transform padre, string texto, Vector2 posicion, UnityAction accion)
    {
        GameObject go = new GameObject("Boton_" + texto, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(padre, false);
        RectTransform rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = posicion;
        rt.sizeDelta = new Vector2(440f, 76f);

        Image img = go.GetComponent<Image>();
        img.color = Color.white;
        go.AddComponent<Outline>().effectColor = new Color(1f, 1f, 1f, 0.35f);

        Button boton = go.GetComponent<Button>();
        ColorBlock colores = boton.colors;
        colores.normalColor = new Color(0.08f, 0.08f, 0.08f, 0.85f);
        colores.highlightedColor = new Color(0.95f, 0.42f, 0.1f, 0.9f);
        colores.pressedColor = new Color(0.7f, 0.3f, 0.07f, 1f);
        colores.selectedColor = colores.normalColor;
        colores.fadeDuration = 0.08f;
        boton.colors = colores;
        boton.onClick.AddListener(accion);

        Text t = CrearTexto("Texto", go.transform, 34, Color.white, Vector2.zero, Vector2.zero);
        RectTransform rtt = t.rectTransform;
        rtt.anchorMin = Vector2.zero;
        rtt.anchorMax = Vector2.one;
        rtt.offsetMin = rtt.offsetMax = Vector2.zero;
        t.text = texto;
    }

    private Text CrearTexto(string nombre, Transform padre, int tamano, Color color, Vector2 posicion, Vector2 tamanoCaja)
    {
        GameObject go = new GameObject(nombre, typeof(RectTransform));
        go.transform.SetParent(padre, false);
        RectTransform rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = posicion;
        rt.sizeDelta = tamanoCaja;

        Text t = go.AddComponent<Text>();
        t.font = fuente;
        t.fontSize = tamano;
        t.alignment = TextAnchor.MiddleCenter;
        t.color = color;
        t.raycastTarget = false;
        return t;
    }
}