using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Estado compartido entre el menú de pausa, las cartas y los diálogos, para que no se encimen.
/// No se pone en ningún objeto: basta con que esté en la carpeta Scripts.
/// </summary>
public static class EstadoJuego
{
    public static bool CartaAbierta;
    public static bool DialogoAbierto;
    public static bool Pausado;

    private static int frameUltimoCierre = -1;

    /// <summary>True si hay una carta, un diálogo o la pausa en pantalla.</summary>
    public static bool Ocupado => CartaAbierta || DialogoAbierto || Pausado;

    /// <summary>Se llama al cerrar una carta, un diálogo o la pausa, para que la misma tecla no abra otra cosa en ese frame.</summary>
    public static void MarcarCierre()
    {
        frameUltimoCierre = Time.frameCount;
    }

    public static bool SeCerroEsteFrame => frameUltimoCierre == Time.frameCount;

    // Reinicia todo al dar Play (aunque la recarga de dominio esté desactivada)
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reiniciar()
    {
        CartaAbierta = false;
        DialogoAbierto = false;
        Pausado = false;
        frameUltimoCierre = -1;
    }

    // ---------- Teclas (funciona con el Input System nuevo y con el viejo) ----------

    public static bool TeclaInteractuar()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.E);
#endif
    }

    public static bool TeclaEscape()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.Escape);
#endif
    }

    /// <summary>E, Espacio, Enter o clic izquierdo: para avanzar en los diálogos.</summary>
    public static bool TeclaAvanzar()
    {
#if ENABLE_INPUT_SYSTEM
        bool teclado = Keyboard.current != null &&
            (Keyboard.current.eKey.wasPressedThisFrame ||
             Keyboard.current.spaceKey.wasPressedThisFrame ||
             Keyboard.current.enterKey.wasPressedThisFrame);
        bool mouse = Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
        return teclado || mouse;
#else
        return Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Space) ||
               Input.GetKeyDown(KeyCode.Return) || Input.GetMouseButtonDown(0);
#endif
    }
}