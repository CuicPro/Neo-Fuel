using UnityEngine;
using Unity.Netcode;

public class PlayerSpawner : NetworkBehaviour
{
    [Header("Player Settings")]
    public GameObject playerPrefab;

    [Header("Spawn Settings")]
    public bool spawnOnConnection = true;
    public Vector3 defaultSpawnPosition = Vector3.zero;
    public float spawnHeight = 2f;

    private ProceduralWorld proceduralWorld;

    private void Start()
    {
        proceduralWorld = FindObjectOfType<ProceduralWorld>();

        if (proceduralWorld == null)
        {
            Debug.LogWarning("ProceduralWorld non trouvé dans la scène");
        }
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer && spawnOnConnection)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;

            foreach (var clientId in NetworkManager.Singleton.ConnectedClientsIds)
            {
                SpawnPlayerForClient(clientId);
            }
        }
    }

    private void OnClientConnected(ulong clientId)
    {
        if (IsServer)
        {
            SpawnPlayerForClient(clientId);
        }
    }

    private void SpawnPlayerForClient(ulong clientId)
    {
        if (playerPrefab == null)
        {
            Debug.LogError("PlayerPrefab n'est pas assigné dans PlayerSpawner!");
            return;
        }

        if (NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client) &&
            client.PlayerObject != null)
        {
            Debug.LogWarning($"Le client {clientId} a déjà un PlayerObject. Spawn ignoré.");
            return;
        }

        Vector3 spawnPosition = GetValidSpawnPosition();

        GameObject playerInstance = Instantiate(playerPrefab, spawnPosition, Quaternion.identity);

        NetworkObject playerNetObj = playerInstance.GetComponent<NetworkObject>();
        if (playerNetObj == null)
        {
            Debug.LogError("Le prefab Player doit avoir un composant NetworkObject!");
            Destroy(playerInstance);
            return;
        }

        playerNetObj.SpawnAsPlayerObject(clientId);
        Debug.Log($"Joueur spawné pour le client {clientId} à la position {spawnPosition}");
    }

    private Vector3 GetValidSpawnPosition()
    {
        if (proceduralWorld != null)
        {
            return proceduralWorld.GetValidSpawnPosition();
        }

        return defaultSpawnPosition + Vector3.up * spawnHeight;
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer && NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void RequestPlayerSpawnServerRpc(ServerRpcParams serverRpcParams = default)
    {
        ulong clientId = serverRpcParams.Receive.SenderClientId;
        SpawnPlayerForClient(clientId);
    }
}
