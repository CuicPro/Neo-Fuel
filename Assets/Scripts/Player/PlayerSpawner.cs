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
            Debug.LogWarning("ProceduralWorld non trouv� dans la sc�ne");
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
            Debug.LogError("PlayerPrefab n'est pas assign� dans PlayerSpawner!");
            return;
        }

        if (NetworkManager.Singleton.ConnectedClients.ContainsKey(clientId) &&
            NetworkManager.Singleton.ConnectedClients[clientId].PlayerObject != null)
        {
            Debug.LogWarning($"Le client {clientId} a d�j� un PlayerObject. Spawn ignor�.");
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

        Debug.Log($"Joueur spawn� pour le client {clientId} � la position {spawnPosition}");

        if (clientId == NetworkManager.Singleton.LocalClientId)
        {
            // Active la cam�ra physique du joueur (et tout son rig)
            Camera playerCam = playerInstance.GetComponentInChildren<Camera>(true);
            if (playerCam != null)
            {
                playerCam.gameObject.SetActive(true);
                Debug.Log("Cam�ra physique activ�e pour le joueur local");
            }

            // D�sactive la cam�ra par d�faut (de la sc�ne)
            GameObject defaultCam = GameObject.Find("DefaultCamera");
            if (defaultCam != null)
            {
                defaultCam.SetActive(false);
            }
        }
        else
        {
            // Pour les autres joueurs, on laisse cam�ra d�sactiv�e
            Camera playerCam = playerInstance.GetComponentInChildren<Camera>(true);
            if (playerCam != null)
                playerCam.gameObject.SetActive(false);
        }

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
