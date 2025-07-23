using Unity.Netcode;
using UnityEngine;

public class PlayerHealth : NetworkBehaviour
{
    public NetworkVariable<float> health = new NetworkVariable<float>(100f);

    public void ApplyDamage(float damage)
    {
        if (!IsServer) return;

        health.Value -= damage;
        Debug.Log($"{OwnerClientId} a été touché ! HP restant : {health.Value}");

        if (health.Value <= 0)
        {
            Die();
        }
    }

    void Die()
    {
        Debug.Log($"Le joueur {OwnerClientId} est mort !");
        // Exemple : désactiver joueur ou le respawn
    }
}
