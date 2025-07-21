using UnityEngine;
using System.Collections;

public class PlayerSpawner : MonoBehaviour
{
    public GameObject playerPrefab;
    public int chunkSize = 250;
    private bool hasSpawned = false;

    void Start()
    {
        StartCoroutine(SpawnWhenReady());
    }

    IEnumerator SpawnWhenReady()
    {
        while (Terrain.activeTerrain == null || Terrain.activeTerrain.terrainData == null)
        {
            yield return null;
        }

        if (hasSpawned) yield break;
        hasSpawned = true;

        Vector3 spawnPosition = new Vector3(
            Random.Range(0, chunkSize),
            0,
            Random.Range(0, chunkSize)
        );

        spawnPosition.y = Terrain.activeTerrain.SampleHeight(spawnPosition) + 2f;
        Instantiate(playerPrefab, spawnPosition, Quaternion.identity);
    }
}
