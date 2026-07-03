using System.Collections.Generic;
using UnityEngine;

public sealed class GhostSpawner : MonoBehaviour
{
    [Header("Ghost Prefabs")]
    [SerializeField] private GameObject[] ghostPrefabs;

    [Header("Spawn Points (SpawnPoint 1~3 드래그)")]
    [SerializeField] private Transform[] spawnPoints;

    [Header("Settings")]
    [SerializeField] private float spawnInterval = 5f;
    [SerializeField] private int maxGhostsAlive = 10;

    private float timer;
    private bool wasNight;
    private readonly List<GameObject> activeGhosts = new List<GameObject>();

    private void Start()
    {
        // spawnPoints가 비어 있으면 GhostSpawn 자식에서 자동 탐색
        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            GameObject parent = GameObject.Find("GhostSpawn");
            if (parent != null)
            {
                spawnPoints = new Transform[parent.transform.childCount];
                for (int i = 0; i < parent.transform.childCount; i++)
                    spawnPoints[i] = parent.transform.GetChild(i);
            }
        }

        timer = spawnInterval;
    }

    private void Update()
    {
        bool isNight = DayNightManager.Instance != null
                       && DayNightManager.Instance.CurrentPhase == DayNightManager.Phase.Night;

        // 낮으로 전환되면 유령 전부 제거
        if (wasNight && !isNight)
            ClearGhosts();

        wasNight = isNight;

        if (!isNight) return;

        // 죽은 유령 정리
        activeGhosts.RemoveAll(g => g == null);

        timer -= Time.deltaTime;
        if (timer > 0f || activeGhosts.Count >= maxGhostsAlive) return;

        SpawnGhost();
        timer = spawnInterval;
    }

    private void SpawnGhost()
    {
        if (ghostPrefabs == null || ghostPrefabs.Length == 0) return;
        if (spawnPoints == null || spawnPoints.Length == 0) return;

        Transform sp = spawnPoints[Random.Range(0, spawnPoints.Length)];
        GameObject prefab = ghostPrefabs[Random.Range(0, ghostPrefabs.Length)];
        if (prefab == null) return;

        GameObject ghost = Instantiate(prefab, sp.position, sp.rotation);
        if (ghost.GetComponent<GhostAI>() == null)
            ghost.AddComponent<GhostAI>();

        activeGhosts.Add(ghost);
    }

    private void ClearGhosts()
    {
        foreach (GameObject g in activeGhosts)
            if (g != null) Destroy(g);
        activeGhosts.Clear();
    }

    // DayNightManager에서 직접 호출도 지원
    public void StartSpawning() { timer = 0f; }
    public void StopSpawning() { ClearGhosts(); }
}
