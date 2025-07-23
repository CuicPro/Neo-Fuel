using System.Collections.Generic;
using UnityEngine;

public class PlayerInventory : MonoBehaviour
{
    [Header("Emplacements visuels")]
    public Transform mainHandSlot;         // Emplacement dans la main
    public Transform[] holsterSlots;       // Emplacements sur le corps

    private List<GameObject> weapons = new();  // Toutes les armes en jeu
    private int activeWeaponIndex = -1;

    void Start()
    {
        UpdateWeaponVisibility();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1)) SwitchWeapon(0);
        if (Input.GetKeyDown(KeyCode.Alpha2)) SwitchWeapon(1);
        if (Input.GetKeyDown(KeyCode.Alpha3)) SwitchWeapon(2);
    }

    public bool AddWeapon(GameObject weaponPrefab)
    {
        if (weapons.Count >= 3)
        {
            Debug.Log("Inventaire plein !");
            return false;
        }

        GameObject weaponInstance;
        Transform targetSlot;

        if (weapons.Count == 0)
        {
            // Arme en main
            targetSlot = mainHandSlot;
            activeWeaponIndex = 0;
        }
        else
        {
            // Holster
            int holsterIndex = weapons.Count - 1;
            if (holsterIndex >= holsterSlots.Length)
            {
                Debug.LogWarning("Pas assez de slots de holster !");
                return false;
            }
            targetSlot = holsterSlots[holsterIndex];
        }

        weaponInstance = Instantiate(weaponPrefab, targetSlot);
        weaponInstance.SetActive(true);
        weaponInstance.transform.localPosition = Vector3.zero;
        weaponInstance.transform.localRotation = Quaternion.identity;

        RemoveUnnecessaryComponents(weaponInstance);

        // Assigner owner si script Weapon
        Weapon weaponScript = weaponInstance.GetComponent<Weapon>();
        if (weaponScript != null)
        {
            weaponScript.owner = gameObject;

            // Lier dynamiquement l'Animator du joueur
            Animator anim = GetComponent<Animator>();
            if (anim != null)
            {
                weaponScript.playerAnimator = anim;
            }
        }


        weapons.Add(weaponInstance);
        Debug.Log($"Arme ajoutée : {weaponPrefab.name}");
        PrintInventory();

        UpdateWeaponVisibility();
        return true;
    }

    void RemoveUnnecessaryComponents(GameObject weapon)
    {
        foreach (var rb in weapon.GetComponentsInChildren<Rigidbody>())
            Destroy(rb);
        foreach (var col in weapon.GetComponentsInChildren<Collider>())
            Destroy(col);
        foreach (var pickup in weapon.GetComponentsInChildren<PickupWeapon>())
            Destroy(pickup);
    }

    public void SwitchWeapon(int index)
    {
        if (index < 0 || index >= weapons.Count) return;
        if (activeWeaponIndex == index) return;

        activeWeaponIndex = index;
        UpdateWeaponVisibility();
        Debug.Log($"Changement vers l’arme {index + 1} : {weapons[index].name}");
    }

    void UpdateWeaponVisibility()
    {
        for (int i = 0; i < weapons.Count; i++)
        {
            GameObject weapon = weapons[i];
            bool isActive = (i == activeWeaponIndex);

            // Change de parent (main ou holster)
            if (isActive)
            {
                weapon.transform.SetParent(mainHandSlot);
            }
            else
            {
                int holsterIndex = i - 1;
                if (holsterIndex >= 0 && holsterIndex < holsterSlots.Length)
                {
                    weapon.transform.SetParent(holsterSlots[holsterIndex]);
                }
            }

            // Réinitialise position/rotation locale
            weapon.transform.localPosition = Vector3.zero;
            weapon.transform.localRotation = Quaternion.identity;

            // Active l'objet
            weapon.SetActive(true);

            //  Met à jour isEquipped
            Weapon weaponScript = weapon.GetComponent<Weapon>();
            if (weaponScript != null)
            {
                weaponScript.isEquipped = isActive;
            }
        }
    }


    public GameObject GetActiveWeapon()
    {
        if (activeWeaponIndex >= 0 && activeWeaponIndex < weapons.Count)
            return weapons[activeWeaponIndex];
        return null;
    }

    public void PrintInventory()
    {
        Debug.Log("=== INVENTAIRE ===");
        for (int i = 0; i < weapons.Count; i++)
        {
            string active = (i == activeWeaponIndex) ? " (Active)" : "";
            Debug.Log($"Slot {i + 1}: {weapons[i].name}{active}");
        }
    }
}
