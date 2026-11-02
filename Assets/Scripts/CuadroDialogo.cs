using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Cuadro de diálogo que usan los NPC. No hay que ponerlo en ningún objeto:
/// se crea solo la primera vez que un NPC habla.
/// </summary>
public class CuadroDialogo : MonoBehaviour
{
    private static CuadroDialogo instancia;

    public static CuadroDialogo Instancia
    {
        get
        {
            if (instancia == null)
                instancia = new GameObject("UI_Dialogo").AddComponent<CuadroDialogo>();
            return instancia;
        }
    }

    private Font fuente;
    private GameObject panel;
    private Text nombreUI;
    private Text textoUI;
    private Text indicacionUI;
    private AudioSource audioVoz;

    private NPC npcActual;
    private string[] lineas;
    private int indice;
    private float letrasMostradas;
    private bool escribiendo;
    private int frameApertura;
    private readonly System.Collections.Generic.List<MonoBehaviour> scriptsCongelados =
        new System.Collections.Generic.List<MonoBehaviour>();

    private const float AltoReferencia = 1080f;

    private void Awake()
    {
        instancia = this;
        fuente = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        audioVoz = gameObject.AddComponent<AudioSource>();
        audioVoz.playOnAwake = false;
        CrearInterfaz();
    }

    private void OnDestroy()
    {
        if (instancia == this) instancia = null;
    }

    public void Iniciar(NPC npc, string[] nuevasLineas)
    {
        if (npc == null || nuevasLineas == null || nuevasLineas.Length == 0) return;

        npcActual = npc;
        lineas = nuevasLineas;
        indice = 0;
        frameApertura = Time.frameCount;

        // El tiempo sigue corriendo; solo se inmoviliza al jugador mientras habla
        EstadoJuego.DialogoAbierto = true;
        if (npc.congelarJugador) CongelarJugador(npc.tagJugador);

        nombreUI.text = npc.nombre;
        panel.SetActive(true);
        MostrarLinea();
    }

    private void Update()
    {
        if (npcActual == null || EstadoJuego.Pausado) return;

        // La misma E que abrió el diálogo no debe adelantarlo
        if (Time.frameCount == frameApertura) return;

        if (EstadoJuego.TeclaEscape())
        {
            Terminar(false);
            return;
        }

        string linea = lineas[indice];

        if (escribiendo)
        {
            letrasMostradas += npcActual.velocidadTexto * Time.unscaledDeltaTime;
            int n = Mathf.Min(linea.Length, Mathf.FloorToInt(letrasMostradas));
            textoUI.text = linea.Substring(0, n);
            if (n >= linea.Length) escribiendo = false;
        }

        indicacionUI.text = indice >= lineas.Length - 1 ? "[E] Cerrar" : "[E] Continuar";
        indicacionUI.gameObject.SetActive(!escribiendo);

        if (EstadoJuego.TeclaAvanzar())
        {
            if (escribiendo)
            {
                // Primer toque: muestra la línea completa
                textoUI.text = linea;
                escribiendo = false;
            }
            else
            {
                indice++;
                if (indice >= lineas.Length) Terminar(true);
                else MostrarLinea();
            }
        }
    }

    private void MostrarLinea()
    {
        letrasMostradas = 0f;
        escribiendo = true;
        textoUI.text = "";
        indicacionUI.gameObject.SetActive(false);

        if (npcActual.sonidoVoz != null)
            audioVoz.PlayOneShot(npcActual.sonidoVoz);
    }

    private void Terminar(bool completo)
    {
        panel.SetActive(false);
        LiberarJugador();
        EstadoJuego.DialogoAbierto = false;
        EstadoJuego.MarcarCierre();

        NPC npc = npcActual;
        npcActual = null;
        npc.AlTerminar(completo);
    }

    // ---------- Inmovilizar al jugador ----------

    // Apaga los scripts del jugador (movimiento y cámara), excepto los de interacción y pausa
    private void CongelarJugador(string tagJugador)
    {
        scriptsCongelados.Clear();
        GameObject jugador = GameObject.FindGameObjectWithTag(tagJugador);
        if (jugador == null) return;

        foreach (MonoBehaviour script in jugador.GetComponentsInChildren<MonoBehaviour>())
        {
            if (!script.enabled) continue;
            if (script is LectorDeCartas || script is MenuPausa) continue;
            // Los componentes de Unity (por ejemplo, los de URP en la cámara) no se tocan
            if (script.GetType().Namespace != null && script.GetType().Namespace.StartsWith("UnityEngine")) continue;

            script.enabled = false;
            scriptsCongelados.Add(script);
        }
    }

    private void LiberarJugador()
    {
        foreach (MonoBehaviour script in scriptsCongelados)
            if (script != null) script.enabled = true;
        scriptsCongelados.Clear();
    }

    // ---------- Interfaz ----------

    private void CrearInterfaz()
    {
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 95; // encima del reloj, debajo de las cartas y la pausa
        CanvasScaler escalador = gameObject.AddComponent<CanvasScaler>();
        escalador.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        escalador.referenceResolution = new Vector2(1920f, AltoReferencia);
        escalador.matchWidthOrHeight = 1f;

        // Caja en la parte de abajo
        panel = new GameObject("Caja", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(transform, false);
        RectTransform rp = (RectTransform)panel.transform;
        rp.anchorMin = new Vector2(0.5f, 0f);
        rp.anchorMax = new Vector2(0.5f, 0f);
        rp.pivot = new Vector2(0.5f, 0f);
        rp.anchoredPosition = new Vector2(0f, 50f);
        rp.sizeDelta = new Vector2(1400f, 230f);
        // Mismo estilo que el panel de las cartas: fondo negro y borde blanco fino
        panel.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.82f);

        Color colorBorde = new Color(1f, 1f, 1f, 0.35f);
        Colocar(CrearImagen("BordeArriba", rp, colorBorde), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 2f));
        Colocar(CrearImagen("BordeAbajo", rp, colorBorde), new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(0f, 2f));
        Colocar(CrearImagen("BordeIzq", rp, colorBorde), new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(2f, 0f));
        Colocar(CrearImagen("BordeDer", rp, colorBorde), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(2f, 0f));

        // Nombre del personaje (blanco, como el título del panel de las cartas)
        nombreUI = CrearTexto("Nombre", rp, 32, Color.white, TextAnchor.UpperLeft);
        Colocar(nombreUI.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -16f), new Vector2(-80f, 44f));

        // Línea divisoria debajo del nombre
        Colocar(CrearImagen("Divisor", rp, colorBorde), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -64f), new Vector2(-80f, 2f));

        // Texto
        textoUI = CrearTexto("Texto", rp, 30, new Color(0.92f, 0.92f, 0.92f), TextAnchor.UpperLeft);
        RectTransform rt = textoUI.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(40f, 50f);
        rt.offsetMax = new Vector2(-40f, -80f);
        textoUI.horizontalOverflow = HorizontalWrapMode.Wrap;
        textoUI.verticalOverflow = VerticalWrapMode.Overflow;
        textoUI.lineSpacing = 1.1f;

        // Indicación
        indicacionUI = CrearTexto("Indicacion", rp, 20, new Color(1f, 1f, 1f, 0.75f), TextAnchor.LowerRight);
        Colocar(indicacionUI.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f),
            new Vector2(-30f, 14f), new Vector2(400f, 34f));

        panel.SetActive(false);
    }

    private Text CrearTexto(string nombre, Transform padre, int tamano, Color color, TextAnchor alineacion)
    {
        GameObject go = new GameObject(nombre, typeof(RectTransform));
        go.transform.SetParent(padre, false);
        Text t = go.AddComponent<Text>();
        t.font = fuente;
        t.fontSize = tamano;
        t.color = color;
        t.alignment = alineacion;
        t.raycastTarget = false;
        t.supportRichText = false;
        return t;
    }

    private RectTransform CrearImagen(string nombre, Transform padre, Color color)
    {
        GameObject go = new GameObject(nombre, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(padre, false);
        Image img = go.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return (RectTransform)go.transform;
    }

    private static void Colocar(RectTransform rt, Vector2 anclaMin, Vector2 anclaMax, Vector2 pivote, Vector2 posicion, Vector2 tamano)
    {
        rt.anchorMin = anclaMin;
        rt.anchorMax = anclaMax;
        rt.pivot = pivote;
        rt.anchoredPosition = posicion;
        rt.sizeDelta = tamano;
    }
}