using System.Collections.Generic;
using UnityEngine;

public class PlayerInventory : MonoBehaviour
{
    [Header("Slots Visuels")]
    public Transform mainHandSlot;         // Arme tenue en main (visible)
    public Transform[] holsterSlots;       // Armes visibles sur le joueur (2 slots)

    private List<GameObject> weapons = new List<GameObject>(); // Liste des modèles d'armes
    private int activeWeaponIndex = -1; // Index de l'arme active (-1 = aucune)

    void Start()
    {
        // Optionnel : désactiver toutes les armes au départ
        UpdateWeaponVisibility();
    }

    void Update()
    {
        // Exemple simple : changement d'arme avec touches 1, 2, 3
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

        GameObject weaponModel;

        if (weapons.Count == 0)
        {
            // Première arme ? main hand
            weaponModel = Instantiate(weaponPrefab, mainHandSlot);
            activeWeaponIndex = 0;
        }
        else
        {
            // Autres armes ? holsters
            int holsterIndex = weapons.Count - 1; // 1ère arme dans holsterSlots[0], 2ème dans holsterSlots[1]
            if (holsterIndex >= holsterSlots.Length)
            {
                Debug.LogWarning("Pas assez de holsters définis !");
                return false;
            }
            weaponModel = Instantiate(weaponPrefab, holsterSlots[holsterIndex]);
        }

        weaponModel.transform.localPosition = Vector3.zero;
        weaponModel.transform.localRotation = Quaternion.identity;

        DestroyComponentsForDisplay(weaponModel);

        weapons.Add(weaponModel);

        Debug.Log($"Arme ajoutée à l'inventaire ({weapons.Count - 1}) : {weaponPrefab.name}");
        PrintInventory();

        UpdateWeaponVisibility();

        return true;
    }

    private void DestroyComponentsForDisplay(GameObject obj)
    {
        foreach (var rb in obj.GetComponentsInChildren<Rigidbody>())
            Destroy(rb);

        foreach (var col in obj.GetComponentsInChildren<Collider>())
            Destroy(col);

        foreach (var pickup in obj.GetComponentsInChildren<PickupWeapon>())
            Destroy(pickup);
    }

    public void SwitchWeapon(int index)
    {
        if (index < 0 || index >= weapons.Count)
        {
            Debug.LogWarning("Index arme invalide.");
            return;
        }

        if (activeWeaponIndex == index)
            return; // Déjà active

        activeWeaponIndex = index;
        UpdateWeaponVisibility();

        Debug.Log($"Arme active changée à l'index {index}: {weapons[index].name}");
    }

    private void UpdateWeaponVisibility()
    {
        for (int i = 0; i < weapons.Count; i++)
        {
            if (i == activeWeaponIndex)
            {
                // Arme active ? position mainHandSlot
                weapons[i].transform.SetParent(mainHandSlot);
                weapons[i].transform.localPosition = Vector3.zero;
                weapons[i].transform.localRotation = Quaternion.identity;
                weapons[i].SetActive(true);
            }
            else
            {
                // Arme inactive ? holster correspondant
                int holsterIndex = i - 1;
                if (holsterIndex >= 0 && holsterIndex < holsterSlots.Length)
                {
                    weapons[i].transform.SetParent(holsterSlots[holsterIndex]);
                    weapons[i].transform.localPosition = Vector3.zero;
                    weapons[i].transform.localRotation = Quaternion.identity;
                    weapons[i].SetActive(true);
                }
                else
                {
                    // Si pas de holster dispo (cas non prévu), on cache l'arme
                    weapons[i].SetActive(false);
                }
            }
        }
    }

    public void PrintInventory()
    {
        Debug.Log("=== INVENTAIRE ===");
        for (int i = 0; i < weapons.Count; i++)
        {
            string activeMark = (i == activeWeaponIndex) ? " (Active)" : "";
            Debug.Log($"Slot {i + 1}: {weapons[i].name}{activeMark}");
        }
    }
}
