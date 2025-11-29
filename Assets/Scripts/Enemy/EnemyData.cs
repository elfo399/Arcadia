using UnityEngine;

[CreateAssetMenu(fileName = "NewEnemy", menuName = "Dungeon/Enemy Data")]
public class EnemyData : ScriptableObject
{
    [Header("Identità")]
    public string enemyName = "Zombie";
    public GameObject prefab; // Il modello 3D base (con script AI e Health)

    [Header("Statistiche")]
    public int maxHealth = 30;
    public int damage = 10;
    public float moveSpeed = 3.5f;

    [Header("Rarità")]
    [Range(1, 100)] 
    public int spawnWeight = 10; // Più è alto, più spesso appare
}