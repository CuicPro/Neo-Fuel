using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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
}

[System.Serializable]
public class Biome
{
    public string name;
    public Material material;

    [Header("Terrain")]
    [Min(0.0001f)] public float noiseScale = 0.01f;
    [Min(0.0001f)] public float heightScale = 10f;
    [Range(0.0001f, 1f)] public float heightThreshold = 1f;

    [Header("Object Spawning")]
    public List<SpawnGroup> spawnGroups = new();

    public void OnValidate()
    {
        noiseScale = Mathf.Max(noiseScale, 0.0001f);
        heightScale = Mathf.Max(heightScale, 0.0001f);
        heightThreshold = Mathf.Clamp(heightThreshold, 0.0001f, 1f);

        if (spawnGroups != null)
        {
            foreach (var group in spawnGroups)
            {
                group.spawnDensity = Mathf.Clamp(group.spawnDensity, 0.0001f, 1f);
            }
        }
    }

}


public class ProceduralWorld : MonoBehaviour
{
    [Header("Seed Settings")]
    public int seed = 42;
    public bool randomizeSeed = true;

    [Header("Chunk Settings")]
    public int chunkSize = 64;
    public int viewRadiusX = 5;
    public int viewRadiusY = 4;

    [Header("Biomes Settings")]
    public BiomesSettings biomesSettings;

    [Header("Player Settings")]
    public Transform player;

    private Dictionary<Vector2Int, GameObject> activeChunks = new();
    private HashSet<Vector2Int> chunksToKeep = new();
    private System.Random rng;
    private bool worldInitialized = false;

    private void Start()
    {
        if (randomizeSeed)
            seed = Random.Range(0, int.MaxValue);

        rng = new System.Random(seed);

        if (player == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
                player = playerObj.transform;
            else
            {
                Debug.LogError("Aucun objet avec le tag 'Player' trouvé !");
                return;
            }
        }
        if (biomesSettings == null)
        {
            Debug.LogError("BiomesSettings n'est pas assigné !");
            return;
        }

        StartCoroutine(GenerateWorldAndSpawnPlayer());
    }

    IEnumerator GenerateWorldAndSpawnPlayer()
    {
        Vector2Int centerChunk = new Vector2Int(0, 0);

        for (int y = -viewRadiusY; y <= viewRadiusY; y++)
        {
            for (int x = -viewRadiusX; x <= viewRadiusX; x++)
            {
                float ellipseCheck = (x * x) / (float)(viewRadiusX * viewRadiusX) + (y * y) / (float)(viewRadiusY * viewRadiusY);
                if (ellipseCheck <= 1f)
                {
                    Vector2Int coord = new Vector2Int(centerChunk.x + x, centerChunk.y + y);
                    if (!activeChunks.ContainsKey(coord))
                    {
                        GameObject chunk = GenerateChunk(coord);
                        activeChunks.Add(coord, chunk);
                    }
                }
            }
        }

        yield return new WaitForEndOfFrame();

        float centerX = chunkSize / 2f;
        float centerZ = chunkSize / 2f;
        float avgHeight = GetAverageHeight(new Vector2Int(0, 0));
        Biome spawnBiome = GetBiomeFromHeight(avgHeight);
        float height = avgHeight * spawnBiome.heightScale;

        // Vérifie que le résultat est valide
        if (float.IsNaN(height))
        {
            Debug.LogError("La hauteur de spawn est NaN ! Vérifie les paramètres de biome.");
        }
        else
        {
            player.position = new Vector3(centerX, height + 2f, centerZ);
        }


        worldInitialized = true;
    }

    private void Update()
    {
        if (worldInitialized)
            UpdateChunks();
    }

    void UpdateChunks()
    {
        Vector2Int currentChunk = GetChunkCoord(player.position);
        chunksToKeep.Clear();

        for (int y = -viewRadiusY; y <= viewRadiusY; y++)
        {
            for (int x = -viewRadiusX; x <= viewRadiusX; x++)
            {
                float ellipseCheck = (x * x) / (float)(viewRadiusX * viewRadiusX) + (y * y) / (float)(viewRadiusY * viewRadiusY);
                if (ellipseCheck <= 1f)
                {
                    Vector2Int coord = new Vector2Int(currentChunk.x + x, currentChunk.y + y);
                    chunksToKeep.Add(coord);

                    if (!activeChunks.ContainsKey(coord))
                    {
                        GameObject chunk = GenerateChunk(coord);
                        activeChunks.Add(coord, chunk);
                    }
                }
            }
        }

        List<Vector2Int> toRemove = new();
        foreach (var kvp in activeChunks)
        {
            if (!chunksToKeep.Contains(kvp.Key))
            {
                Destroy(kvp.Value);
                toRemove.Add(kvp.Key);
            }
        }
        foreach (var key in toRemove)
            activeChunks.Remove(key);
    }

    GameObject GenerateChunk(Vector2Int coord)
    {
        GameObject chunk = new GameObject($"Chunk_{coord.x}_{coord.y}");
        chunk.transform.position = new Vector3(coord.x * chunkSize, 0, coord.y * chunkSize);
        chunk.transform.parent = transform;

        MeshFilter mf = chunk.AddComponent<MeshFilter>();
        MeshRenderer mr = chunk.AddComponent<MeshRenderer>();
        MeshCollider mc = chunk.AddComponent<MeshCollider>();

        float avgHeight = GetAverageHeight(coord);
        Biome biome = GetBiomeFromHeight(avgHeight);

        Mesh mesh = GenerateTerrainMesh(coord, biome);
        mf.mesh = mesh;
        mc.sharedMesh = mesh;

        mr.material = biome.material;
        SpawnObjectsInChunk(coord, chunk.transform, biome);

        return chunk;
    }

    Mesh GenerateTerrainMesh(Vector2Int coord, Biome biome)
    {
        Vector3[] vertices = new Vector3[(chunkSize + 1) * (chunkSize + 1)];
        int[] triangles = new int[chunkSize * chunkSize * 6];

        for (int z = 0; z <= chunkSize; z++)
        {
            for (int x = 0; x <= chunkSize; x++)
            {
                float worldX = coord.x * chunkSize + x;
                float worldZ = coord.y * chunkSize + z;
                float height = GetHeight(worldX, worldZ, biome);
                vertices[z * (chunkSize + 1) + x] = new Vector3(x, height, z);
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
        mesh.RecalculateNormals();

        return mesh;
    }

    float GetAverageHeight(Vector2Int coord)
    {
        if (biomesSettings == null)
        {
            Debug.LogError("BiomesSettings n'est pas assigné dans le composant ProceduralWorld !");
            return 0f;
        }

        if (biomesSettings.biomes == null || biomesSettings.biomes.Count == 0)
        {
            Debug.LogError("La liste de biomes est vide ou nulle dans BiomesSettings !");
            return 0f;
        }

        float total = 0f;
        int sampleCount = 5;
        Biome referenceBiome = biomesSettings.biomes[0];

        // Vérification du heightScale pour éviter la division par zéro
        if (referenceBiome.heightScale <= 0f)
        {
            Debug.LogError($"Le heightScale du biome '{referenceBiome.name}' est invalide ({referenceBiome.heightScale}). Il doit être supérieur à 0.");
            return 0f;
        }

        for (int i = 0; i < sampleCount; i++)
        {
            float x = (coord.x * chunkSize + rng.Next(0, chunkSize));
            float z = (coord.y * chunkSize + rng.Next(0, chunkSize));
            float height = GetHeight(x, z, referenceBiome);

            // Vérification supplémentaire pour s'assurer que height n'est pas NaN
            if (float.IsNaN(height))
            {
                Debug.LogError($"GetHeight a retourné NaN pour les coordonnées ({x}, {z})");
                height = 0f;
            }

            total += height / referenceBiome.heightScale;
        }

        float averageHeight = total / sampleCount;

        // Vérification finale du résultat
        if (float.IsNaN(averageHeight))
        {
            Debug.LogError("GetAverageHeight a calculé un NaN !");
            return 0f;
        }

        return averageHeight;
    }

    float GetHeight(float x, float z, Biome biome)
    {
        // Vérification des paramètres du biome
        if (biome.noiseScale <= 0f)
        {
            Debug.LogError($"Le noiseScale du biome '{biome.name}' est invalide ({biome.noiseScale}). Il doit être supérieur à 0.");
            return 0f;
        }

        float seedOffset = seed * 0.01f;
        float noise = 0f;

        // Calcul du bruit avec vérifications
        float noise1 = Mathf.PerlinNoise((x + seedOffset) * biome.noiseScale, (z + seedOffset) * biome.noiseScale);
        float noise2 = Mathf.PerlinNoise((x + seedOffset) * biome.noiseScale * 2f, (z + seedOffset) * biome.noiseScale * 2f);
        float noise3 = Mathf.PerlinNoise((x + seedOffset) * biome.noiseScale * 4f, (z + seedOffset) * biome.noiseScale * 4f);

        // Vérification que les valeurs de bruit sont valides
        if (float.IsNaN(noise1) || float.IsNaN(noise2) || float.IsNaN(noise3))
        {
            Debug.LogError($"Valeur de bruit NaN détectée pour les coordonnées ({x}, {z})");
            return 0f;
        }

        noise = noise1 * 0.5f + noise2 * 0.3f + noise3 * 0.2f;

        float finalHeight = noise * biome.heightScale;

        // Vérification finale
        if (float.IsNaN(finalHeight))
        {
            Debug.LogError($"Hauteur finale NaN pour le biome '{biome.name}' aux coordonnées ({x}, {z})");
            return 0f;
        }

        return finalHeight;
    }


    Biome GetBiomeFromHeight(float height)
    {
        foreach (var biome in biomesSettings.biomes)
        {
            if (height < biome.heightThreshold)
                return biome;
        }
        return biomesSettings.biomes[^1];
    }

    void SpawnObjectsInChunk(Vector2Int coord, Transform parent, Biome biome)
    {
        if (biome.spawnGroups == null || biome.spawnGroups.Count == 0) return;

        int spacing = 4; // espacement entre points de spawn possibles (ajuste selon besoin)

        foreach (var group in biome.spawnGroups)
        {
            if (group.prefabs == null || group.prefabs.Count == 0 || group.spawnDensity <= 0f) continue;

            for (int x = 0; x < chunkSize; x += spacing)
            {
                for (int z = 0; z < chunkSize; z += spacing)
                {
                    // spawnDensity est la probabilité qu'un objet spawn ici
                    if (rng.NextDouble() < group.spawnDensity)
                    {
                        // Petite variation pour ne pas avoir des objets parfaitement alignés
                        float offsetX = (float)(rng.NextDouble() - 0.5) * spacing;
                        float offsetZ = (float)(rng.NextDouble() - 0.5) * spacing;

                        float worldX = coord.x * chunkSize + x + offsetX;
                        float worldZ = coord.y * chunkSize + z + offsetZ;
                        float height = GetHeight(worldX, worldZ, biome);

                        Vector3 pos = new Vector3(worldX, height, worldZ);
                        GameObject prefab = group.prefabs[rng.Next(group.prefabs.Count)];
                        float randomYRotation = (float)rng.NextDouble() * 360f;
                        Quaternion rotation = Quaternion.Euler(0f, randomYRotation, 0f);
                        Instantiate(prefab, pos, rotation, parent);
                    }
                }
            }
        }
    }



    Vector2Int GetChunkCoord(Vector3 position)
    {
        return new Vector2Int(
            Mathf.FloorToInt(position.x / chunkSize),
            Mathf.FloorToInt(position.z / chunkSize)
        );
    }
}