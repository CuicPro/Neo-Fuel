using UnityEngine;

public class PickupWeapon : MonoBehaviour
{
    public GameObject weaponPrefab; // Le prefab � donner
    private bool isPlayerNearby = false;
    private PlayerInventory playerInventory;

    void Update()
    {
        if (isPlayerNearby && Input.GetKeyDown(KeyCode.E))
        {
            if (playerInventory != null)
            {
                bool success = playerInventory.AddWeapon(weaponPrefab != null ? weaponPrefab : transform.parent.gameObject);
                if (success)
                {
                    Destroy(transform.parent.gameObject); // Supprime l�arme ramass�e
                }
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerInventory = other.GetComponent<PlayerInventory>();
            isPlayerNearby = true;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerNearby = false;
            playerInventory = null;
        }
    }
}
