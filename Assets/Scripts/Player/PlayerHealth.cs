using Unity.Netcode;
using UnityEngine;

public class PlayerHealth : NetworkBehaviour
{
    public NetworkVariable<float> health = new NetworkVariable<float>(100f);

    public void ApplyDamage(float damage)
    {
        if (!IsServer) return;

        health.Value -= damage;
        Debug.Log($"{OwnerClientId} a �t� touch� ! HP restant : {health.Value}");

        if (health.Value <= 0)
        {
            Die();
        }
    }

    void Die()
    {
        Debug.Log($"Le joueur {OwnerClientId} est mort !");
        // Exemple : d�sactiver joueur ou le respawn
    }
}
