using UnityEngine;
using UnityEngine.UI;

public class EnemyHealthBar : MonoBehaviour
{
    [Header("Riferimenti UI")]
    public Slider healthSlider; // Il componente Slider di Unity
    public Image fillImage;     // L'immagine che si colora (per cambiarla se serve)

    [Header("Settings")]
    public Gradient healthGradient; // Opzionale: Verde -> Giallo -> Rosso

    private Camera mainCamera;

    void Start()
    {
        mainCamera = Camera.main;
        
        // Nascondi la barra se il nemico è al massimo della vita (opzionale)
        // gameObject.SetActive(false); 
    }

    public void SetMaxHealth(int maxHealth)
    {
        healthSlider.maxValue = maxHealth;
        healthSlider.value = maxHealth;
        
        if(healthGradient != null)
            fillImage.color = healthGradient.Evaluate(1f);
    }

    public void SetHealth(int currentHealth)
    {
        // Mostra la barra appena prende danno
        if (!gameObject.activeSelf) gameObject.SetActive(true);

        healthSlider.value = currentHealth;

        if (healthGradient != null)
            fillImage.color = healthGradient.Evaluate(healthSlider.normalizedValue);
    }

    void LateUpdate()
    {
        // BILLBOARD: Fai in modo che la barra guardi sempre la telecamera
        if (mainCamera != null)
        {
            transform.LookAt(transform.position + mainCamera.transform.forward);
        }
    }
}