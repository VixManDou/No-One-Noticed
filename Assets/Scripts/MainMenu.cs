using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenu : MonoBehaviour
{
    [Header("Paneles de la UI")]
    public GameObject panelPrincipal;
    public GameObject panelCreditos;
    public GameObject panelVolumen;

    [Header("Ajustes Audio")]
    public Slider sliderVolumen;

    void Start()
    {
        MostrarPanelPrincipal();
        
        if (sliderVolumen != null)
        {
            sliderVolumen.value = PlayerPrefs.GetFloat("VolumenAudio", 1f);
            AudioListener.volume = sliderVolumen.value;
        }
    }

    public void Jugar()
    {
        // Carga la siguiente escena en los Build Settings
        SceneManager.LoadScene("MapaPrincipal"); 
    }

    public void AbrirCreditos()
    {
        if (panelPrincipal) panelPrincipal.SetActive(false);
        if (panelCreditos) panelCreditos.SetActive(true);
    }

    public void AbrirVolumen()
    {
        if (panelPrincipal) panelPrincipal.SetActive(false);
        if (panelVolumen) panelVolumen.SetActive(true);
    }

    public void CambiarVolumen(float valor)
    {
        AudioListener.volume = valor;
        PlayerPrefs.SetFloat("VolumenAudio", valor);
    }

    public void MostrarPanelPrincipal()
    {
        if (panelPrincipal) panelPrincipal.SetActive(true);
        if (panelCreditos) panelCreditos.SetActive(false);
        if (panelVolumen) panelVolumen.SetActive(false);
    }

    public void Salir()
    {
        Debug.Log("Saliendo del juego...");
        Application.Quit();
    }
}