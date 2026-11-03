using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Sonido de pasos del jugador. Usa sonidos de madera dentro de los interiores
/// y de concreto afuera. Ponlo en el objeto Player (el que tiene el Character Controller).
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PasosJugador : MonoBehaviour
{
    public enum Modo
    {
        [Tooltip("Cada archivo es UN paso: se reproduce uno cada cierto tiempo.")]
        PasosSueltos,
        [Tooltip("Cada archivo es una grabación larga de varios pasos: se reproduce en bucle mientras caminas.")]
        Bucle
    }

    [Header("Tipo de sonido")]
    [Tooltip("Pasos Sueltos si cada .mp3 es un solo paso. Bucle si cada .mp3 es una grabación de muchos pasos seguidos.")]
    public Modo modo = Modo.Bucle;

    [Header("Sonidos")]
    [Tooltip("Pasos sobre madera (interiores). Puedes poner uno o varios.")]
    public AudioClip[] pasosMadera;

    [Tooltip("Pasos sobre concreto (exterior). Puedes poner uno o varios.")]
    public AudioClip[] pasosConcreto;

    [Range(0f, 1f)]
    public float volumen = 0.6f;

    [Header("Dónde suena madera")]
    [Tooltip("Zonas (cubos con collider) que cubren los interiores. Dentro de cualquiera suena madera; fuera, concreto.")]
    public Collider[] zonasMadera;

    [Header("Ritmo (solo Pasos Sueltos)")]
    [Tooltip("Segundos entre pasos al caminar.")]
    public float intervaloCaminar = 0.5f;

    [Tooltip("Segundos entre pasos al correr (Shift).")]
    public float intervaloCorrer = 0.32f;

    [Tooltip("Variación aleatoria del tono, para que no suene repetitivo.")]
    [Range(0f, 0.3f)]
    public float variacionTono = 0.1f;

    [Header("Ritmo (solo Bucle)")]
    [Tooltip("Velocidad del sonido al correr (1 = normal).")]
    public float tonoAlCorrer = 1.35f;

    [Header("Detección")]
    [Tooltip("Velocidad mínima para considerar que el jugador camina.")]
    public float velocidadMinima = 0.2f;

    [Tooltip("Solo suenan los pasos si el jugador toca el suelo.")]
    public bool requerirSuelo = true;

    private CharacterController cc;
    private AudioSource fuente;
    private float temporizador;
    private int ultimoIndice = -1;
    private bool estabaEnMadera;
    private Vector3 posicionAnterior;

    private void Awake()
    {
        cc = GetComponent<CharacterController>();
        posicionAnterior = transform.position;

        fuente = gameObject.AddComponent<AudioSource>();
        fuente.playOnAwake = false;
        fuente.spatialBlend = 0f; // sonido 2D: los pasos son del propio jugador
    }

    private void Update()
    {
        bool enMadera = EnZonaMadera();
        bool caminando = EstaCaminando();
        bool corriendo = caminando && TeclaCorrer();

        if (modo == Modo.Bucle) ActualizarBucle(enMadera, caminando, corriendo);
        else ActualizarPasosSueltos(enMadera, caminando, corriendo);

        estabaEnMadera = enMadera;
    }

    // ---------- Modo bucle ----------

    private void ActualizarBucle(bool enMadera, bool caminando, bool corriendo)
    {
        if (!caminando)
        {
            if (fuente.isPlaying) fuente.Pause();
            return;
        }

        // Al cambiar de superficie (o la primera vez), cambia la grabación
        if (fuente.clip == null || enMadera != estabaEnMadera)
        {
            AudioClip clip = Elegir(enMadera ? pasosMadera : pasosConcreto);
            if (clip == null) return;
            if (fuente.clip != clip)
            {
                fuente.clip = clip;
                fuente.loop = true;
                fuente.time = 0f;
            }
        }

        fuente.volume = volumen;
        fuente.pitch = corriendo ? tonoAlCorrer : 1f;
        if (!fuente.isPlaying) fuente.Play(); // reanuda donde se quedó
    }

    // ---------- Modo pasos sueltos ----------

    private void ActualizarPasosSueltos(bool enMadera, bool caminando, bool corriendo)
    {
        if (fuente.loop) { fuente.loop = false; fuente.Stop(); }

        if (!caminando)
        {
            // Listo para dar el primer paso en cuanto empiece a moverse
            temporizador = 0f;
            return;
        }

        temporizador -= Time.deltaTime;
        if (temporizador > 0f) return;

        temporizador = corriendo ? intervaloCorrer : intervaloCaminar;

        AudioClip clip = Elegir(enMadera ? pasosMadera : pasosConcreto);
        if (clip == null) return;

        fuente.pitch = 1f + Random.Range(-variacionTono, variacionTono);
        fuente.PlayOneShot(clip, volumen);
    }

    // ---------- Utilidades ----------

    private bool EstaCaminando()
    {
        // Mide cuánto se movió realmente el jugador desde el frame anterior.
        // (cc.velocity no sirve: conserva la última velocidad cuando el script de movimiento se apaga)
        Vector3 posicion = transform.position;
        Vector3 desplazamiento = posicion - posicionAnterior;
        posicionAnterior = posicion;
        desplazamiento.y = 0f;

        if (!cc.enabled || Time.deltaTime <= 0f) return false;

        // Con un diálogo, una carta o la pausa abiertos, nunca suenan pasos
        if (EstadoJuego.Ocupado) return false;

        if (requerirSuelo && !cc.isGrounded) return false;

        float velocidad = desplazamiento.magnitude / Time.deltaTime;
        return velocidad > velocidadMinima;
    }

    private bool EnZonaMadera()
    {
        if (zonasMadera == null) return false;
        Bounds jugador = cc.bounds;
        foreach (Collider zona in zonasMadera)
        {
            if (zona == null || !zona.gameObject.activeInHierarchy) continue;
            if (zona.bounds.Intersects(jugador)) return true;
        }
        return false;
    }

    // Elige un sonido al azar sin repetir el anterior
    private AudioClip Elegir(AudioClip[] lista)
    {
        if (lista == null || lista.Length == 0) return null;
        if (lista.Length == 1) return lista[0];

        int i;
        do { i = Random.Range(0, lista.Length); } while (i == ultimoIndice);
        ultimoIndice = i;
        return lista[i];
    }

    private bool TeclaCorrer()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed;
#else
        return Input.GetKey(KeyCode.LeftShift);
#endif
    }
}