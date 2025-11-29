using UnityEngine;
using UnityEngine.UI;

public class EnemyHealthBar : MonoBehaviour
{
    // Questa variabile DEVE chiamarsi "slider" (minuscolo) e essere pubblica
    // perché lo script EnemySetup la cerca per nome.
    public Slider slider; 
    
    private Camera cam;

    void Start()
    {
        cam = Camera.main;
    }

    public void SetMaxHealth(int maxHealth)
    {
        if (slider != null)
        {
            slider.maxValue = maxHealth;
            slider.value = maxHealth;
        }
        // Opzionale: Nascondi la barra se è piena per pulizia
        // gameObject.SetActive(false);
    }

    public void SetHealth(int health)
    {
        // Mostra la barra appena il nemico viene colpito
        gameObject.SetActive(true); 
        
        if (slider != null)
        {
            slider.value = health;
        }
    }

    void LateUpdate()
    {
        // Rotazione Billboard: guarda sempre la telecamera
        if (cam != null)
        {
            transform.LookAt(transform.position + cam.transform.forward);
        }
    }
}