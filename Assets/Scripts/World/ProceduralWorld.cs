using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using System;

[CreateAssetMenu(fileName = "BiomesSettings", menuName = "Procedural Generation/Biomes Settings")]
public class BiomesSettings : ScriptableObject
{
    public List<Biome> biomes = new();
}

[System.Serializable]
public class SpawnGroup
{
    public string groupName;
    [Range(0f, 1f)] public float spawnDensity = 0.01f;
    public List<GameObject> prefabs = new();

    [Header("Network Settings")]
    public bool networkObject = false;
}

[System.Serializable]
public class Biome
{
    public string name;
    public Material material;

    [Header("Terrain")]
    [Min(0.0001f)] public float noiseScale = 0.01f;
    [Min(0.0001f)] public float heightScale = 10f;
    [Range(0f, 1f)] public float heightThreshold = 1f;

    [Header("Object Spawning")]
    public List<SpawnGroup> spawnGroups = new();

    public void OnValidate()
    {
        noiseScale = Mathf.Max(noiseScale, 0.0001f);
        heightScale = Mathf.Max(heightScale, 0.0001f);
        heightThreshold = Mathf.Clamp(heightThreshold, 0f, 1f);

        if (spawnGroups != null)
        {
            foreach (var group in spawnGroups)
            {
                group.spawnDensity = Mathf.Clamp(group.spawnDensity, 0f, 1f);
            }
        }
    }
}

// Fixed ObjectInteractionData struct that implements IEquatable
[System.Serializable]
public struct ObjectInteractionData : INetworkSerializable, System.IEquatable<ObjectInteractionData>
{
    public Vector3 position;
    public int chunkX;
    public int chunkY;
    public int objectId;
    public bool isDestroyed;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref position);
        serializer.SerializeValue(ref chunkX);
        serializer.SerializeValue(ref chunkY);
        serializer.SerializeValue(ref objectId);
        serializer.SerializeValue(ref isDestroyed);
    }

    public bool Equals(ObjectInteractionData other)
    {
        return position.Equals(other.position) &&
               chunkX == other.chunkX &&
               chunkY == other.chunkY &&
               objectId == other.objectId &&
               isDestroyed == other.isDestroyed;
    }

    public override bool Equals(object obj)
    {
        return obj is ObjectInteractionData other && Equals(other);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = position.GetHashCode();
            hash = (hash * 397) ^ chunkX;
            hash = (hash * 397) ^ chunkY;
            hash = (hash * 397) ^ objectId;
            hash = (hash * 397) ^ isDestroyed.GetHashCode();
            return hash;
        }
    }

    public static bool operator ==(ObjectInteractionData left, ObjectInteractionData right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(ObjectInteractionData left, ObjectInteractionData right)
    {
        return !left.Equals(right);
    }
}

public class ProceduralWorld : NetworkBehaviour
{
    public event Action WorldGenerated;
    public bool IsWorldInitialized()
    {
        return worldInitialized;
    }

    [Header("Seed Settings")]
    public int seed = 42;
    public bool randomizeSeed = true;

    [Header("Chunk Settings")]
    public int chunkSize = 64;
    public int viewRadiusX = 5;
    public int viewRadiusY = 4;

    [Header("Biomes Settings")]
    public BiomesSettings biomesSettings;

    [Header("Debug")]
    public bool enableChunkLoading = true;
    public float chunkUpdateInterval = 1f; // Délai entre les updates de chunks

    // Dictionnaire pour les chunks générés localement
    private Dictionary<Vector2Int, GameObject> localChunks = new();
    private HashSet<Vector2Int> chunksToKeep = new();
    private HashSet<Vector2Int> previousChunksToKeep = new();

    // Seed synchronisée entre tous les clients
    private NetworkVariable<int> worldSeed = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    // Dictionnaire pour stocker les interactions sur les objets du monde
    private NetworkList<ObjectInteractionData> worldInteractions;
    private Dictionary<string, GameObject> spawnedObjects = new();

    private bool worldInitialized = false;
    private System.Random worldRng;

    // Liste des joueurs pour le chunk loading
    private List<Transform> trackedPlayers = new();
    private float lastChunkUpdateTime = 0f;

    private void Awake()
    {
        worldInteractions = new NetworkList<ObjectInteractionData>();
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            if (randomizeSeed)
                seed = UnityEngine.Random.Range(0, int.MaxValue);

            worldSeed.Value = seed;
            Debug.Log($"[Serveur] Seed du monde: {seed}");
        }

        worldSeed.OnValueChanged += OnWorldSeedChanged;

        if (worldSeed.Value != 0)
        {
            InitializeWorldGeneration(worldSeed.Value);
        }

        worldInteractions.OnListChanged += OnWorldInteractionsChanged;
    }

    private void OnWorldSeedChanged(int previousValue, int newValue)
    {
        if (newValue != 0)
        {
            InitializeWorldGeneration(newValue);
        }
    }

    private void InitializeWorldGeneration(int seedValue)
    {
        seed = seedValue;
        worldRng = new System.Random(seed);

        if (biomesSettings == null)
        {
            Debug.LogError("BiomesSettings n'est pas assigné !");
            return;
        }

        Debug.Log($"[{(IsServer ? "Serveur" : "Client")}] Initialisation de la génération avec seed: {seed}");
        StartCoroutine(GenerateInitialWorld());
    }

    private void OnWorldInteractionsChanged(NetworkListEvent<ObjectInteractionData> changeEvent)
    {
        if (changeEvent.Type == NetworkListEvent<ObjectInteractionData>.EventType.Add)
        {
            ApplyInteractionLocally(changeEvent.Value);
        }
    }

    private void ApplyInteractionLocally(ObjectInteractionData interaction)
    {
        string objectId = $"{interaction.chunkX}_{interaction.chunkY}_{interaction.objectId}";

        if (spawnedObjects.TryGetValue(objectId, out GameObject obj) && obj != null)
        {
            if (interaction.isDestroyed)
            {
                Debug.Log($"Destruction de l'objet {objectId} suite à une interaction réseau");
                Destroy(obj);
                spawnedObjects.Remove(objectId);
            }
        }
    }

    IEnumerator GenerateInitialWorld()
    {
        Vector2Int centerChunk = new Vector2Int(0, 0);

        // Générer les chunks initiaux autour du centre
        for (int y = -viewRadiusY; y <= viewRadiusY; y++)
        {
            for (int x = -viewRadiusX; x <= viewRadiusX; x++)
            {
                float ellipseCheck = (x * x) / (float)(viewRadiusX * viewRadiusX) + (y * y) / (float)(viewRadiusY * viewRadiusY);
                if (ellipseCheck <= 1f)
                {
                    Vector2Int coord = new Vector2Int(centerChunk.x + x, centerChunk.y + y);
                    if (!localChunks.ContainsKey(coord))
                    {
                        GameObject chunk = GenerateChunkLocally(coord);
                        localChunks.Add(coord, chunk);
                        chunksToKeep.Add(coord); // Important: ajouter aux chunks à garder
                    }
                }
            }
            yield return null;
        }

        // Copier les chunks initiaux dans previousChunksToKeep
        previousChunksToKeep = new HashSet<Vector2Int>(chunksToKeep);

        worldInitialized = true;
        WorldGenerated?.Invoke();
        WeaponSpawner weaponSpawnerInstance = FindObjectOfType<WeaponSpawner>();
        if (weaponSpawnerInstance != null)
        {
            weaponSpawnerInstance.OnGameStart();
        }
        else
        {
            Debug.LogWarning("WeaponSpawner non trouvé !");
        }
        Debug.Log(">>> WorldGenerated event déclenché !");
        Debug.Log($"[{(IsServer ? "Serveur" : "Client")}] Monde initial généré avec {localChunks.Count} chunks");
    }

    private void Update()
    {
        if (worldInitialized && enableChunkLoading)
        {
            // Limiter la fréquence des updates de chunks
            if (Time.time - lastChunkUpdateTime >= chunkUpdateInterval)
            {
                UpdateChunkLoading();
                lastChunkUpdateTime = Time.time;
            }
        }
    }

    private void UpdateChunkLoading()
    {
        // Auto-détection des joueurs si nécessaire
        if (trackedPlayers.Count == 0)
        {
            FindAllPlayers();
            // Si aucun joueur n'est trouvé, ne pas faire d'update
            if (trackedPlayers.Count == 0)
            {
                return;
            }
        }

        // Nettoyer les joueurs supprimés
        trackedPlayers.RemoveAll(player => player == null);
        if (trackedPlayers.Count == 0)
        {
            return;
        }

        chunksToKeep.Clear();

        // Pour chaque joueur, calculer les chunks nécessaires
        foreach (Transform player in trackedPlayers)
        {
            if (player != null)
            {
                Vector2Int playerChunk = GetChunkCoordFromWorldPos(player.position);
                AddChunksAroundPosition(playerChunk);
            }
        }

        // Vérifier s'il y a eu des changements
        bool hasChanges = !chunksToKeep.SetEquals(previousChunksToKeep);

        if (!hasChanges)
        {
            return; // Pas de changements, pas besoin de faire quoi que ce soit
        }

        Debug.Log($"Mise à jour des chunks: {chunksToKeep.Count} chunks nécessaires, {localChunks.Count} chunks actuels");

        // Générer les nouveaux chunks nécessaires
        List<Vector2Int> newChunks = new List<Vector2Int>();
        foreach (Vector2Int coord in chunksToKeep)
        {
            if (!localChunks.ContainsKey(coord))
            {
                newChunks.Add(coord);
            }
        }

        // Supprimer les chunks trop éloignés
        List<Vector2Int> chunksToRemove = new List<Vector2Int>();
        foreach (var kvp in localChunks)
        {
            if (!chunksToKeep.Contains(kvp.Key))
            {
                chunksToRemove.Add(kvp.Key);
            }
        }

        // Supprimer d'abord
        foreach (Vector2Int coord in chunksToRemove)
        {
            if (localChunks.TryGetValue(coord, out GameObject chunk))
            {
                RemoveSpawnedObjectsFromChunk(coord);
                Destroy(chunk);
                localChunks.Remove(coord);
                //Debug.Log($"Chunk {coord} supprimé");
            }
        }

        // Puis générer les nouveaux
        foreach (Vector2Int coord in newChunks)
        {
            GameObject chunk = GenerateChunkLocally(coord);
            localChunks.Add(coord, chunk);
            //Debug.Log($"Chunk {coord} généré");
        }

        // Mettre à jour les chunks précédents
        previousChunksToKeep = new HashSet<Vector2Int>(chunksToKeep);

        //Debug.Log($"Update terminé: {localChunks.Count} chunks actifs");
    }

    private void AddChunksAroundPosition(Vector2Int centerChunk)
    {
        for (int y = -viewRadiusY; y <= viewRadiusY; y++)
        {
            for (int x = -viewRadiusX; x <= viewRadiusX; x++)
            {
                float ellipseCheck = (x * x) / (float)(viewRadiusX * viewRadiusX) + (y * y) / (float)(viewRadiusY * viewRadiusY);
                if (ellipseCheck <= 1f)
                {
                    Vector2Int coord = new Vector2Int(centerChunk.x + x, centerChunk.y + y);
                    chunksToKeep.Add(coord);
                }
            }
        }
    }

    private void RemoveSpawnedObjectsFromChunk(Vector2Int chunkCoord)
    {
        List<string> objectsToRemove = new();

        foreach (var kvp in spawnedObjects)
        {
            if (kvp.Key.StartsWith($"{chunkCoord.x}_{chunkCoord.y}_"))
            {
                objectsToRemove.Add(kvp.Key);
            }
        }

        foreach (string objectId in objectsToRemove)
        {
            spawnedObjects.Remove(objectId);
        }
    }

    private void FindAllPlayers()
    {
        // Essayer de trouver les joueurs avec différentes méthodes
        trackedPlayers.Clear();

        // Méthode 1: Par tag
        GameObject[] playerObjects = GameObject.FindGameObjectsWithTag("Player");
        foreach (GameObject playerObj in playerObjects)
        {
            trackedPlayers.Add(playerObj.transform);
        }

        // Méthode 2: Si pas de tag Player, chercher les NetworkObjects avec un component spécifique
        if (trackedPlayers.Count == 0)
        {
            NetworkObject[] networkObjects = FindObjectsOfType<NetworkObject>();
            foreach (NetworkObject netObj in networkObjects)
            {
                // Vous pouvez ajuster cette condition selon votre structure de joueur
                if (netObj.IsOwner || (netObj.gameObject.name.Contains("Player") && netObj.IsSpawned))
                {
                    trackedPlayers.Add(netObj.transform);
                }
            }
        }

        // Méthode 3: Utiliser la position de ce NetworkBehaviour si c'est un joueur
        if (trackedPlayers.Count == 0 && this.IsOwner)
        {
            trackedPlayers.Add(this.transform);
        }

        Debug.Log($"Trouvé {trackedPlayers.Count} joueurs à tracker pour le chunk loading");
    }

    GameObject GenerateChunkLocally(Vector2Int coord)
    {
        GameObject chunk = new GameObject($"Chunk_{coord.x}_{coord.y}");
        chunk.transform.position = new Vector3(coord.x * chunkSize, 0, coord.y * chunkSize);
        chunk.transform.parent = transform;

        MeshFilter mf = chunk.AddComponent<MeshFilter>();
        MeshRenderer mr = chunk.AddComponent<MeshRenderer>();
        MeshCollider mc = chunk.AddComponent<MeshCollider>();

        Mesh mesh = GenerateTerrainMesh(coord, out Color[] biomeColors);
        mf.mesh = mesh;
        mc.sharedMesh = mesh;

        if (biomesSettings.biomes.Count > 0)
            mr.material = biomesSettings.biomes[0].material;

        GenerateObjectsInChunk(coord, chunk.transform);

        return chunk;
    }

    private void GenerateObjectsInChunk(Vector2Int coord, Transform parent)
    {
        int chunkSeed = seed + coord.x * 10000 + coord.y;
        System.Random chunkRng = new System.Random(chunkSeed);

        int spacing = 4;
        int objectIdCounter = 0;

        for (int x = 0; x < chunkSize; x += spacing)
        {
            for (int z = 0; z < chunkSize; z += spacing)
            {
                float worldX = coord.x * chunkSize + x;
                float worldZ = coord.y * chunkSize + z;

                float[] biomeWeights = GetBiomeWeights(worldX, worldZ);

                for (int i = 0; i < biomesSettings.biomes.Count; i++)
                {
                    Biome biome = biomesSettings.biomes[i];
                    float weight = biomeWeights[i];

                    if (weight > 0.1f)
                    {
                        foreach (var group in biome.spawnGroups)
                        {
                            if (chunkRng.NextDouble() < group.spawnDensity * weight)
                            {
                                if (group.prefabs.Count > 0)
                                {
                                    int prefabIndex = chunkRng.Next(0, group.prefabs.Count);
                                    GameObject prefab = group.prefabs[prefabIndex];

                                    Vector3 localPos = new Vector3(x, GetHeightWeighted(worldX, worldZ), z);
                                    Vector3 worldPos = localPos + parent.position;

                                    string objectId = $"{coord.x}_{coord.y}_{objectIdCounter}";

                                    if (!IsObjectDestroyed(coord, objectIdCounter))
                                    {
                                        GameObject spawnedObj;

                                        if (group.networkObject && IsServer)
                                        {
                                            spawnedObj = Instantiate(prefab, worldPos, Quaternion.identity, parent);
                                            NetworkObject netObj = spawnedObj.GetComponent<NetworkObject>();
                                            if (netObj != null)
                                            {
                                                netObj.Spawn();
                                            }
                                        }
                                        else if (!group.networkObject)
                                        {
                                            spawnedObj = Instantiate(prefab, worldPos, Quaternion.identity, parent);

                                            WorldObject worldObjComponent = spawnedObj.GetComponent<WorldObject>();
                                            if (worldObjComponent == null)
                                            {
                                                worldObjComponent = spawnedObj.AddComponent<WorldObject>();
                                            }
                                            worldObjComponent.Initialize(coord, objectIdCounter, this);
                                        }
                                        else
                                        {
                                            spawnedObj = null;
                                        }

                                        if (spawnedObj != null)
                                        {
                                            spawnedObj.name = $"{biome.name}_{group.groupName}_{objectId}";
                                            spawnedObjects[objectId] = spawnedObj;
                                        }
                                    }

                                    objectIdCounter++;
                                }
                            }
                        }
                    }
                }
            }
        }
    }

    private bool IsObjectDestroyed(Vector2Int chunkCoord, int objectId)
    {
        foreach (var interaction in worldInteractions)
        {
            if (interaction.chunkX == chunkCoord.x &&
                interaction.chunkY == chunkCoord.y &&
                interaction.objectId == objectId &&
                interaction.isDestroyed)
            {
                return true;
            }
        }
        return false;
    }

    public void ReportObjectInteraction(Vector2Int chunkCoord, int objectId, Vector3 position, bool destroyed = false)
    {
        if (IsServer)
        {
            ObjectInteractionData interaction = new ObjectInteractionData
            {
                chunkX = chunkCoord.x,
                chunkY = chunkCoord.y,
                objectId = objectId,
                position = position,
                isDestroyed = destroyed
            };

            worldInteractions.Add(interaction);
            Debug.Log($"Interaction enregistrée sur objet {objectId} dans chunk {chunkCoord}");
        }
        else
        {
            ReportInteractionServerRpc(chunkCoord, objectId, position, destroyed);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void ReportInteractionServerRpc(Vector2Int chunkCoord, int objectId, Vector3 position, bool destroyed)
    {
        ReportObjectInteraction(chunkCoord, objectId, position, destroyed);
    }

    public Vector3 GetValidSpawnPosition()
    {
        float halfChunk = chunkSize / 2f;

        for (int attempts = 0; attempts < 20; attempts++)
        {
            float x = UnityEngine.Random.Range(-halfChunk + 5f, halfChunk - 5f);
            float z = UnityEngine.Random.Range(-halfChunk + 5f, halfChunk - 5f);
            float height = GetHeightWeighted(x, z);

            Vector3 spawnPos = new Vector3(x, height + 3f, z);
            return spawnPos;
        }

        float centerHeight = GetHeightWeighted(0, 0);
        return new Vector3(0, centerHeight + 3f, 0);
    }

    // === Méthodes de génération du terrain (identiques à avant) ===

    Mesh GenerateTerrainMesh(Vector2Int coord, out Color[] biomeColors)
    {
        Vector3[] vertices = new Vector3[(chunkSize + 1) * (chunkSize + 1)];
        int[] triangles = new int[chunkSize * chunkSize * 6];
        biomeColors = new Color[vertices.Length];

        for (int z = 0; z <= chunkSize; z++)
        {
            for (int x = 0; x <= chunkSize; x++)
            {
                float worldX = coord.x * chunkSize + x;
                float worldZ = coord.y * chunkSize + z;

                float height = GetHeightWeighted(worldX, worldZ);
                vertices[z * (chunkSize + 1) + x] = new Vector3(x, height, z);

                float[] weights = GetBiomeWeights(worldX, worldZ);
                biomeColors[z * (chunkSize + 1) + x] = GetColorFromBiomeWeights(weights);
            }
        }

        int triIndex = 0;
        for (int z = 0; z < chunkSize; z++)
        {
            for (int x = 0; x < chunkSize; x++)
            {
                int i = z * (chunkSize + 1) + x;
                triangles[triIndex++] = i;
                triangles[triIndex++] = i + chunkSize + 1;
                triangles[triIndex++] = i + 1;
                triangles[triIndex++] = i + 1;
                triangles[triIndex++] = i + chunkSize + 1;
                triangles[triIndex++] = i + chunkSize + 2;
            }
        }

        Mesh mesh = new();
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.colors = biomeColors;
        mesh.RecalculateNormals();

        return mesh;
    }

    float[] GetBiomeWeights(float x, float z)
    {
        float heightNormalized = GetHeightNormalized(x, z);
        float[] weights = new float[biomesSettings.biomes.Count];

        for (int i = 0; i < biomesSettings.biomes.Count; i++)
        {
            float lower = (i == 0) ? 0f : biomesSettings.biomes[i - 1].heightThreshold;
            float upper = biomesSettings.biomes[i].heightThreshold;

            if (heightNormalized <= upper)
            {
                if (i == 0)
                {
                    weights[i] = 1f;
                    break;
                }
                else
                {
                    float t = Mathf.InverseLerp(lower, upper, heightNormalized);
                    weights[i] = t;
                    weights[i - 1] = 1f - t;
                    break;
                }
            }
            else if (i == biomesSettings.biomes.Count - 1)
            {
                weights[i] = 1f;
            }
        }

        float sum = 0f;
        foreach (float w in weights)
            sum += w;
        if (sum > 0)
        {
            for (int i = 0; i < weights.Length; i++)
                weights[i] /= sum;
        }

        return weights;
    }

    float GetHeightNormalized(float x, float z)
    {
        if (biomesSettings.biomes.Count == 0) return 0f;

        Biome refBiome = biomesSettings.biomes[0];
        float noiseValue = Mathf.PerlinNoise((x + seed * 0.01f) * refBiome.noiseScale, (z + seed * 0.01f) * refBiome.noiseScale);
        return Mathf.Clamp01(noiseValue);
    }

    public float GetHeightWeighted(float x, float z)
    {
        if (biomesSettings.biomes.Count == 0) return 0f;

        float[] weights = GetBiomeWeights(x, z);
        float height = 0f;

        for (int i = 0; i < biomesSettings.biomes.Count; i++)
        {
            Biome biome = biomesSettings.biomes[i];
            float noise = CalculateBiomeNoise(x, z, biome);
            height += noise * weights[i];
        }

        return height;
    }

    float CalculateBiomeNoise(float x, float z, Biome biome)
    {
        float offset = seed * 0.01f;

        float n1 = Mathf.PerlinNoise((x + offset) * biome.noiseScale, (z + offset) * biome.noiseScale);
        float n2 = Mathf.PerlinNoise((x + offset) * biome.noiseScale * 2f, (z + offset) * biome.noiseScale * 2f);
        float n3 = Mathf.PerlinNoise((x + offset) * biome.noiseScale * 4f, (z + offset) * biome.noiseScale * 4f);

        float noiseValue = n1 * 0.5f + n2 * 0.3f + n3 * 0.2f;
        return noiseValue * biome.heightScale;
    }

    Color GetColorFromBiomeWeights(float[] weights)
    {
        Color[] biomeColors = new Color[]
        {
            Color.green,
            Color.yellow,
            Color.gray,
            Color.blue,
        };

        Color result = Color.black;
        for (int i = 0; i < weights.Length; i++)
        {
            Color c = (i < biomeColors.Length) ? biomeColors[i] : Color.white;
            result += c * weights[i];
        }

        return result;
    }

    Vector2Int GetChunkCoordFromWorldPos(Vector3 position)
    {
        int x = Mathf.FloorToInt(position.x / chunkSize);
        int y = Mathf.FloorToInt(position.z / chunkSize);
        return new Vector2Int(x, y);
    }

    public override void OnDestroy()
    {
        if (worldSeed != null)
        {
            worldSeed.OnValueChanged -= OnWorldSeedChanged;
        }

        if (worldInteractions != null)
        {
            worldInteractions.OnListChanged -= OnWorldInteractionsChanged;
        }

        base.OnDestroy();
    }
}

// Component à ajouter sur les objets du monde pour gérer les interactions
public class WorldObject : MonoBehaviour
{
    private Vector2Int chunkCoord;
    private int objectId;
    private ProceduralWorld worldManager;

    public void Initialize(Vector2Int chunk, int id, ProceduralWorld manager)
    {
        chunkCoord = chunk;
        objectId = id;
        worldManager = manager;
    }

    public void DestroyObject()
    {
        if (worldManager != null)
        {
            worldManager.ReportObjectInteraction(chunkCoord, objectId, transform.position, true);
            Destroy(gameObject);
        }
    }

    public void OnInteract()
    {
        if (worldManager != null)
        {
            worldManager.ReportObjectInteraction(chunkCoord, objectId, transform.position, false);
        }
    }
}