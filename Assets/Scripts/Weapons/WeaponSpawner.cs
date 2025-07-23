using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

[System.Serializable]
public class WeaponData
{
    public GameObject prefab;
    [Range(0f, 1f)] public float spawnChance;
}

public class WeaponSpawner : MonoBehaviour
{
    public List<WeaponData> weapons;
    public float spawnRadius = 20f;
    public int maxWeapons = 10;
    public float spawnHeight = 60f;

    private bool gameStarted = false;

    void Update()
    {
        if (!gameStarted || !NetworkManager.Singleton.IsServer) return;

        int currentWeapons = GameObject.FindGameObjectsWithTag("Weapon").Length;
        if (currentWeapons < maxWeapons)
        {
            SpawnWeapon();
        }
    }

    public void OnGameStart()
    {
        gameStarted = true;
    }

    private void SpawnWeapon()
    {
        GameObject prefab = GetRandomWeaponPrefab();
        if (prefab == null)
        {
            Debug.LogWarning("Aucun prefab sélectionné pour le spawn.");
            return;
        }

        Vector3 spawnPos = GetRandomSpawnPosition();
        Quaternion rotation = Quaternion.identity;

        // === Instanciation Réseau ===
        GameObject weaponObj = Instantiate(prefab, spawnPos, rotation);

        NetworkObject netObj = weaponObj.GetComponent<NetworkObject>();
        if (netObj != null)
        {
            netObj.Spawn(true);
        }
        else
        {
            Debug.LogWarning($"Le prefab {prefab.name} n'a pas de NetworkObject !");
        }

        // Rigidbody config
        Rigidbody rb = weaponObj.GetComponent<Rigidbody>();
        if (rb == null) rb = weaponObj.AddComponent<Rigidbody>();

        rb.useGravity = true;
        rb.mass = 0.2f;
        rb.linearDamping = 1f;
        rb.angularDamping = 0.5f;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        // Collider solide
        if (!weaponObj.GetComponent<Collider>())
        {
            MeshCollider meshCol = weaponObj.AddComponent<MeshCollider>();
            meshCol.convex = true;
        }

        // === Ajout du trigger pickup ===
        GameObject triggerObj = new GameObject("PickupTrigger");
        triggerObj.transform.SetParent(weaponObj.transform);
        triggerObj.transform.localPosition = Vector3.zero;

        SphereCollider trigger = triggerObj.AddComponent<SphereCollider>();
        trigger.isTrigger = true;
        trigger.radius = 1f;

        PickupWeapon pickupScript = triggerObj.AddComponent<PickupWeapon>();
        pickupScript.weaponPrefab = prefab;

        weaponObj.tag = "Weapon";
    }

    private GameObject GetRandomWeaponPrefab()
    {
        float total = 0f;
        foreach (var w in weapons) total += w.spawnChance;

        float rand = Random.value * total;
        float current = 0f;

        foreach (var w in weapons)
        {
            current += w.spawnChance;
            if (rand <= current)
                return w.prefab;
        }

        return null;
    }

    private Vector3 GetRandomSpawnPosition()
    {
        Vector2 circle = Random.insideUnitCircle * spawnRadius;
        Vector3 origin = new Vector3(circle.x, spawnHeight, circle.y);

        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 100f))
            return hit.point + Vector3.up * 0.5f;

        return new Vector3(0, 1, 0);
    }
}
