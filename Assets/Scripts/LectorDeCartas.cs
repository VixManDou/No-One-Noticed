using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Pon este script en la cámara del jugador.
/// Detecta cuando miras una Carta y la abre al estilo Resident Evil:
/// la imagen inclinada a la izquierda y un panel con el texto a la derecha.
/// La interfaz se crea sola al iniciar el juego.
/// </summary>
public class LectorDeCartas : MonoBehaviour
{
    [Header("Interacción")]
    [Tooltip("Distancia máxima (en metros) para poder leer una carta.")]
    public float distancia = 3f;

    [Tooltip("Grosor del rayo de detección. Más grueso hace más fácil apuntar a cartas pequeñas.")]
    [Range(0f, 0.3f)]
    public float radioDeteccion = 0.1f;

    [Tooltip("Al presionar E, escribe en la Consola qué objetos toca el rayo. Útil si una carta no se detecta.")]
    public bool depurar = false;

    [Tooltip("Pausa el juego mientras lees (el jugador y los enemigos se detienen).")]
    public bool pausarJuego = true;

    [Tooltip("Scripts extra que se apagan mientras lees (por ejemplo, el de mirar con el mouse).")]
    public MonoBehaviour[] scriptsADesactivar;

    [Header("Imagen de la carta")]
    [Tooltip("Qué tanto del alto de la pantalla ocupa la imagen.")]
    [Range(0.4f, 0.95f)]
    public float altoImagen = 0.72f;

    [Tooltip("Dibuja una sombra debajo de la imagen.")]
    public bool sombra = true;

    [Header("Fondo")]
    [Tooltip("Qué tan oscuro se ve el juego detrás de la carta.")]
    [Range(0f, 1f)]
    public float oscuridadFondo = 0.88f;

    [Tooltip("Opcional: una textura de fondo (piso, mesa, papel viejo...).")]
    public Sprite texturaFondo;

    [Header("Texto")]
    [Tooltip("Fuente del texto. Si lo dejas vacío, usa la de Unity.")]
    public Font fuente;

    [Tooltip("Fuente del título grande. Si lo dejas vacío, usa la misma del texto.")]
    public Font fuenteTitulo;

    public Color colorTituloPantalla = new Color(0.95f, 0.42f, 0.1f);
    public Color colorTexto = new Color(0.92f, 0.92f, 0.92f);
    public int tamanoTexto = 28;

    [Tooltip("Velocidad al desplazar el texto con W/S o las flechas.")]
    public float velocidadDesplazamiento = 600f;

    // Interfaz creada por código
    private GameObject panel;
    private Text aviso;
    private Text tituloPantalla;
    private RectTransform grupoCarta;
    private Image imagenCarta;
    private Image imagenSombra;
    private Text tituloPanel;
    private RectTransform vista;
    private Text textoUI;
    private RectTransform barra;
    private RectTransform manija;
    private Text indicacion;

    private Camera cam;
    private Transform jugadorRaiz;
    private Carta cartaMirada;
    private Carta cartaAbierta;
    private bool[] estabanActivos;
    private float escalaTiempoAnterior = 1f;
    private float desplazamiento;
    private float desplazamientoMaximo;

    private const float AltoReferencia = 1080f;

    private void Awake()
    {
        cam = GetComponent<Camera>();
        if (cam == null) cam = Camera.main;

        // El jugador es el objeto que tiene el Character Controller (o el padre de la cámara)
        CharacterController cc = GetComponentInParent<CharacterController>();
        if (cc != null) jugadorRaiz = cc.transform;
        else jugadorRaiz = transform.parent != null ? transform.parent : transform;

        if (fuente == null)
            fuente = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (fuenteTitulo == null)
            fuenteTitulo = fuente;

        CrearInterfaz();
    }

    private void Update()
    {
        if (cartaAbierta != null)
        {
            if (PresionoInteractuar() || PresionoEscape())
            {
                CerrarCarta();
                return;
            }
            Desplazar();
            return;
        }

        BuscarCarta();

        if (cartaMirada != null && PresionoInteractuar())
            AbrirCarta(cartaMirada);
    }

    private void OnDisable()
    {
        if (cartaAbierta != null) CerrarCarta();
    }

    // ---------- Detección ----------

    private void BuscarCarta()
    {
        Carta encontrada = null;

        // Lanza un rayo "grueso" que atraviesa todo e ignora triggers (como las zonas de teletransporte)
        RaycastHit[] golpes = Physics.SphereCastAll(cam.transform.position, radioDeteccion,
            cam.transform.forward, distancia, ~0, QueryTriggerInteraction.Ignore);
        System.Array.Sort(golpes, (a, b) => a.distance.CompareTo(b.distance));

        // 1. Busca la carta más cercana en la línea de visión
        float distanciaCarta = 0f;
        foreach (RaycastHit g in golpes)
        {
            if (EsDelJugador(g)) continue;
            Carta c = g.collider.GetComponentInParent<Carta>();
            if (c != null)
            {
                encontrada = c;
                distanciaCarta = g.distance;
                break;
            }
        }

        // 2. Revisa si algo la tapa. Los objetos que la CONTIENEN (una caja, un cajón) no cuentan;
        //    los que están en medio (una pared) sí la bloquean.
        if (encontrada != null)
        {
            foreach (RaycastHit g in golpes)
            {
                if (g.distance >= distanciaCarta) break;
                if (EsDelJugador(g)) continue;

                Bounds caja = g.collider.bounds;
                caja.Expand(0.05f);
                if (!caja.Contains(encontrada.transform.position))
                {
                    encontrada = null;
                    break;
                }
            }
        }

        if (depurar)
        {
            Debug.DrawRay(cam.transform.position, cam.transform.forward * distancia, encontrada != null ? Color.green : Color.red);
            if (PresionoInteractuar())
            {
                string lista = golpes.Length == 0 ? "(nada en " + distancia + " m)" : "";
                foreach (RaycastHit g in golpes)
                    lista += $"\n  - {g.collider.name} a {g.distance:0.00} m" +
                             (EsDelJugador(g) ? " (jugador, ignorado)" : "") +
                             (g.collider.GetComponentInParent<Carta>() != null ? " <- CARTA" : "");
                Debug.Log($"[LectorDeCartas] Carta detectada: {(encontrada != null ? encontrada.name : "ninguna")}. El rayo tocó:{lista}");
            }
        }

        cartaMirada = encontrada;
        aviso.gameObject.SetActive(encontrada != null);
        if (encontrada != null) aviso.text = encontrada.mensajeInteraccion;
    }

    // Ignora el collider del propio jugador y lo que ya estaba tocando el rayo al inicio
    private bool EsDelJugador(RaycastHit g)
    {
        if (g.distance <= 0f) return true;
        Transform t = g.collider.transform;
        return t == jugadorRaiz || t.IsChildOf(jugadorRaiz);
    }

    // ---------- Abrir / cerrar ----------

    private void AbrirCarta(Carta carta)
    {
        cartaAbierta = carta;
        aviso.gameObject.SetActive(false);
        panel.SetActive(true);

        // Títulos
        tituloPantalla.text = carta.titulo.ToUpper();
        tituloPanel.text = carta.titulo;

        // Imagen inclinada con sombra
        if (carta.imagen != null)
        {
            grupoCarta.gameObject.SetActive(true);
            float proporcion = carta.imagen.rect.width / carta.imagen.rect.height;
            float alto = AltoReferencia * altoImagen;
            float ancho = alto * proporcion;
            const float anchoMaximo = 820f;
            if (ancho > anchoMaximo)
            {
                ancho = anchoMaximo;
                alto = ancho / proporcion;
            }
            grupoCarta.sizeDelta = new Vector2(ancho, alto);
            grupoCarta.localEulerAngles = new Vector3(0f, 0f, carta.inclinacion);
            imagenCarta.sprite = carta.imagen;
            imagenSombra.sprite = carta.imagen;
            imagenSombra.gameObject.SetActive(sombra);
        }
        else
        {
            grupoCarta.gameObject.SetActive(false);
        }

        // Texto del panel
        textoUI.text = carta.texto;
        textoUI.fontSize = tamanoTexto;
        textoUI.color = colorTexto;

        Canvas.ForceUpdateCanvases();
        float altoTexto = textoUI.preferredHeight + 20f;
        textoUI.rectTransform.sizeDelta = new Vector2(0f, altoTexto);
        desplazamientoMaximo = Mathf.Max(0f, altoTexto - vista.rect.height);
        desplazamiento = 0f;
        AplicarDesplazamiento();

        indicacion.text = desplazamientoMaximo > 0f
            ? "[E] Cerrar        [Rueda / W S] Desplazar"
            : "[E] Cerrar";

        // Congela al jugador
        if (pausarJuego)
        {
            escalaTiempoAnterior = Time.timeScale;
            Time.timeScale = 0f;
        }

        estabanActivos = new bool[scriptsADesactivar.Length];
        for (int i = 0; i < scriptsADesactivar.Length; i++)
        {
            if (scriptsADesactivar[i] == null) continue;
            estabanActivos[i] = scriptsADesactivar[i].enabled;
            scriptsADesactivar[i].enabled = false;
        }

        if (carta.sonido != null)
            AudioSource.PlayClipAtPoint(carta.sonido, cam.transform.position);

        carta.MarcarLeida();
    }

    private void CerrarCarta()
    {
        panel.SetActive(false);

        if (pausarJuego)
            Time.timeScale = escalaTiempoAnterior;

        for (int i = 0; i < scriptsADesactivar.Length; i++)
        {
            if (scriptsADesactivar[i] == null) continue;
            scriptsADesactivar[i].enabled = estabanActivos == null || estabanActivos[i];
        }

        cartaAbierta = null;
    }

    // ---------- Desplazamiento del texto ----------

    private void Desplazar()
    {
        if (desplazamientoMaximo <= 0f) return;

        float rueda = LeerRueda();
        if (rueda != 0f) desplazamiento -= Mathf.Sign(rueda) * 80f;

        float teclas = 0f;
        if (TeclaAbajo()) teclas += 1f;
        if (TeclaArriba()) teclas -= 1f;
        desplazamiento += teclas * velocidadDesplazamiento * Time.unscaledDeltaTime;

        AplicarDesplazamiento();
    }

    private void AplicarDesplazamiento()
    {
        desplazamiento = Mathf.Clamp(desplazamiento, 0f, desplazamientoMaximo);
        textoUI.rectTransform.anchoredPosition = new Vector2(0f, desplazamiento);

        bool hayBarra = desplazamientoMaximo > 0f;
        barra.gameObject.SetActive(hayBarra);
        if (!hayBarra) return;

        float altoBarra = barra.rect.height;
        float altoTexto = textoUI.rectTransform.sizeDelta.y;
        float altoManija = Mathf.Max(30f, altoBarra * (vista.rect.height / altoTexto));
        manija.sizeDelta = new Vector2(0f, altoManija);
        manija.anchoredPosition = new Vector2(0f, -(altoBarra - altoManija) * (desplazamiento / desplazamientoMaximo));
    }

    // ---------- Teclas (funciona con el Input System nuevo y con el viejo) ----------

    private bool PresionoInteractuar()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.E);
#endif
    }

    private bool PresionoEscape()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.Escape);
#endif
    }

    private bool TeclaArriba()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed);
#else
        return Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow);
#endif
    }

    private bool TeclaAbajo()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed);
#else
        return Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow);
#endif
    }

    private float LeerRueda()
    {
#if ENABLE_INPUT_SYSTEM
        return Mouse.current != null ? Mouse.current.scroll.ReadValue().y : 0f;
#else
        return Input.mouseScrollDelta.y;
#endif
    }

    // ---------- Construcción de la interfaz ----------

    private void CrearInterfaz()
    {
        // Canvas
        GameObject goCanvas = new GameObject("UI_Cartas");
        Canvas canvas = goCanvas.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        CanvasScaler escalador = goCanvas.AddComponent<CanvasScaler>();
        escalador.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        escalador.referenceResolution = new Vector2(1920f, AltoReferencia);
        escalador.matchWidthOrHeight = 1f;

        // Aviso "Presiona E para leer"
        aviso = CrearTexto("Aviso", goCanvas.transform, 34, TextAnchor.MiddleCenter, Color.white, fuente);
        Colocar(aviso.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0f, 140f), new Vector2(1000f, 60f));
        aviso.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(2f, -2f);
        aviso.gameObject.SetActive(false);

        // Fondo que cubre toda la pantalla
        panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(goCanvas.transform, false);
        Estirar((RectTransform)panel.transform);
        Image fondo = panel.GetComponent<Image>();
        if (texturaFondo != null)
        {
            fondo.sprite = texturaFondo;
            float brillo = 1f - oscuridadFondo * 0.7f;
            fondo.color = new Color(brillo, brillo, brillo, 1f);
        }
        else
        {
            fondo.color = new Color(0f, 0f, 0f, oscuridadFondo);
        }

        // Título grande arriba a la izquierda
        tituloPantalla = CrearTexto("TituloPantalla", panel.transform, 54, TextAnchor.UpperLeft, colorTituloPantalla, fuenteTitulo);
        tituloPantalla.fontStyle = FontStyle.Bold;
        Colocar(tituloPantalla.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(60f, -40f), new Vector2(1100f, 160f));
        tituloPantalla.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(3f, -3f);

        // Imagen de la carta a la izquierda, con sombra
        GameObject goGrupo = new GameObject("Carta", typeof(RectTransform));
        goGrupo.transform.SetParent(panel.transform, false);
        grupoCarta = (RectTransform)goGrupo.transform;
        Colocar(grupoCarta, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(-375f, -10f), new Vector2(500f, 700f));

        imagenSombra = CrearImagen("Sombra", grupoCarta, new Color(0f, 0f, 0f, 0.55f));
        Estirar(imagenSombra.rectTransform);
        imagenSombra.rectTransform.offsetMin = new Vector2(22f, -22f);
        imagenSombra.rectTransform.offsetMax = new Vector2(22f, -22f);

        imagenCarta = CrearImagen("Imagen", grupoCarta, Color.white);
        Estirar(imagenCarta.rectTransform);

        // Panel de documento a la derecha
        Image documento = CrearImagen("Documento", panel.transform, new Color(0f, 0f, 0f, 0.6f));
        RectTransform rDoc = documento.rectTransform;
        Colocar(rDoc, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
            new Vector2(-110f, 0f), new Vector2(640f, 760f));

        Color colorBorde = new Color(1f, 1f, 1f, 0.35f);
        Colocar(CrearImagen("BordeArriba", rDoc, colorBorde).rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 2f));
        Colocar(CrearImagen("BordeAbajo", rDoc, colorBorde).rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(0f, 2f));
        Colocar(CrearImagen("BordeIzq", rDoc, colorBorde).rectTransform, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(2f, 0f));
        Colocar(CrearImagen("BordeDer", rDoc, colorBorde).rectTransform, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(2f, 0f));

        Text etiqueta = CrearTexto("Etiqueta", rDoc, 14, TextAnchor.UpperLeft, new Color(1f, 1f, 1f, 0.5f), fuente);
        etiqueta.text = "DOCUMENTOS";
        Colocar(etiqueta.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(14f, -8f), new Vector2(300f, 24f));

        tituloPanel = CrearTexto("TituloPanel", rDoc, 32, TextAnchor.MiddleCenter, Color.white, fuente);
        Colocar(tituloPanel.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -60f), new Vector2(-60f, 50f));

        Colocar(CrearImagen("Divisor", rDoc, colorBorde).rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -122f), new Vector2(-60f, 2f));

        // Zona visible del texto (recorta lo que se sale)
        GameObject goVista = new GameObject("Vista", typeof(RectTransform), typeof(RectMask2D));
        goVista.transform.SetParent(rDoc, false);
        vista = (RectTransform)goVista.transform;
        Estirar(vista);
        vista.offsetMin = new Vector2(40f, 90f);
        vista.offsetMax = new Vector2(-50f, -140f);

        textoUI = CrearTexto("Texto", vista, tamanoTexto, TextAnchor.UpperLeft, colorTexto, fuente);
        Colocar(textoUI.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
            Vector2.zero, new Vector2(0f, 100f));
        textoUI.horizontalOverflow = HorizontalWrapMode.Wrap;
        textoUI.verticalOverflow = VerticalWrapMode.Overflow;
        textoUI.lineSpacing = 1.15f;
        textoUI.supportRichText = true;

        // Barra de desplazamiento
        barra = CrearImagen("Barra", rDoc, new Color(1f, 1f, 1f, 0.12f)).rectTransform;
        Colocar(barra, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f),
            new Vector2(-22f, -25f), new Vector2(4f, -230f));
        manija = CrearImagen("Manija", barra, new Color(1f, 1f, 1f, 0.7f)).rectTransform;
        Colocar(manija, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 60f));

        // Indicaciones abajo del panel
        indicacion = CrearTexto("Indicacion", rDoc, 20, TextAnchor.MiddleCenter, new Color(1f, 1f, 1f, 0.75f), fuente);
        Colocar(indicacion.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0f, 22f), new Vector2(-40f, 40f));

        panel.SetActive(false);
    }

    private Text CrearTexto(string nombre, Transform padre, int tamano, TextAnchor alineacion, Color color, Font tipo)
    {
        GameObject go = new GameObject(nombre, typeof(RectTransform));
        go.transform.SetParent(padre, false);
        Text t = go.AddComponent<Text>();
        t.font = tipo;
        t.fontSize = tamano;
        t.alignment = alineacion;
        t.color = color;
        t.raycastTarget = false;
        return t;
    }

    private Image CrearImagen(string nombre, Transform padre, Color color)
    {
        GameObject go = new GameObject(nombre, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(padre, false);
        Image img = go.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    private static void Colocar(RectTransform rt, Vector2 anclaMin, Vector2 anclaMax, Vector2 pivote, Vector2 posicion, Vector2 tamano)
    {
        rt.anchorMin = anclaMin;
        rt.anchorMax = anclaMax;
        rt.pivot = pivote;
        rt.anchoredPosition = posicion;
        rt.sizeDelta = tamano;
    }

    private static void Estirar(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}