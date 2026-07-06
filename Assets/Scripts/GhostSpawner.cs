using System.Collections.Generic;
using UnityEngine;

public sealed class GhostSpawner : MonoBehaviour
{
    [System.Serializable]
    public class GhostTier
    {
        public string tierName = "Tier 1";
        public GameObject[] prefabs;
        public float health = 100f;
        [Tooltip("이 티어 유령이 한 번 공격 시 입히는 데미지")]
        public float attackDamage = 10f;
        [Tooltip("Night Profile 이 없거나 가중치 합이 0일 때만 사용되는 폴백 가중치")]
        public float spawnWeight = 3f;
        public GameObject baseCoinPrefab;
        public int baseCoinValue = 100;
        public GameObject bonusCoinPrefab;
        public int bonusCoinValue = 500;
        [Range(0f, 1f)]
        public float bonusCoinChance = 0.2f;
    }

    [System.Serializable]
    public class NightProfile
    {
        public string label = "Night";
        [Tooltip("이 밤에 스폰할 총 유령 수 (다 죽이면 새벽)")]
        public int totalGhosts = 10;
        [Tooltip("스폰 간격 (초)")]
        public float spawnInterval = 5f;
        [Tooltip("Tier 1 선택 가중치")]
        public float tier1Weight = 6f;
        [Tooltip("Tier 2 선택 가중치")]
        public float tier2Weight = 3f;
        [Tooltip("Tier 3 선택 가중치")]
        public float tier3Weight = 1f;
    }

    [Header("Spawn Points (SpawnPoint 1~3 드래그)")]
    [SerializeField] private Transform[] spawnPoints;

    [Header("Global Settings")]
    [SerializeField] private int maxGhostsAlive = 12;
    [Tooltip("이 밤의 마지막 유령이 사망하면 자동으로 낮으로 전환")]
    [SerializeField] private bool autoEndNight = true;
    [SerializeField, Min(0)] private int endlessExtraGhostsPerNight = 3;

    [Header("Night Profiles (Element 0 = Night 1)")]
    [SerializeField] private NightProfile[] nightProfiles = new NightProfile[]
    {
        new NightProfile { label = "Night 1", totalGhosts = 8,  spawnInterval = 6f,
                           tier1Weight = 10, tier2Weight = 0, tier3Weight = 0 },
        new NightProfile { label = "Night 2", totalGhosts = 12, spawnInterval = 5f,
                           tier1Weight = 8,  tier2Weight = 2, tier3Weight = 0 },
        new NightProfile { label = "Night 3", totalGhosts = 15, spawnInterval = 4.5f,
                           tier1Weight = 6,  tier2Weight = 3, tier3Weight = 0 },
        new NightProfile { label = "Night 4", totalGhosts = 18, spawnInterval = 4f,
                           tier1Weight = 5,  tier2Weight = 4, tier3Weight = 1 },
        new NightProfile { label = "Night 5", totalGhosts = 22, spawnInterval = 3.5f,
                           tier1Weight = 4,  tier2Weight = 5, tier3Weight = 2 },
        new NightProfile { label = "Night 6", totalGhosts = 26, spawnInterval = 3f,
                           tier1Weight = 3,  tier2Weight = 5, tier3Weight = 3 },
        new NightProfile { label = "Night 7 (Final)", totalGhosts = 35, spawnInterval = 2.5f,
                           tier1Weight = 2,  tier2Weight = 5, tier3Weight = 5 },
    };

    [Header("Tiers")]
    [SerializeField] private GhostTier[] tiers = new GhostTier[]
    {
        new GhostTier { tierName = "Tier 1 (HP 100)",  health = 100f,  attackDamage = 8f,  spawnWeight = 6f,
                        baseCoinValue = 100,  bonusCoinValue = 500,  bonusCoinChance = 0.20f },
        new GhostTier { tierName = "Tier 2 (HP 500)",  health = 500f,  attackDamage = 15f, spawnWeight = 3f,
                        baseCoinValue = 500,  bonusCoinValue = 1000, bonusCoinChance = 0.15f },
        new GhostTier { tierName = "Tier 3 (HP 1000)", health = 1000f, attackDamage = 25f, spawnWeight = 1f,
                        baseCoinValue = 1000, bonusCoinValue = 1000, bonusCoinChance = 0.10f }
    };

    [Header("Ghost AI Stats (모든 티어 공용)")]
    [SerializeField] private float ghostMoveSpeed = 2.5f;
    [SerializeField] private float ghostAttackRange = 1.2f;
    [Tooltip("티어별 attackDamage 가 0 이하일 때만 폴백으로 사용")]
    [SerializeField] private float ghostAttackDamage = 10f;
    [SerializeField] private float ghostAttackCooldown = 1.5f;
    [SerializeField] private float ghostAggroRange = 5f;
    [SerializeField] private float ghostDeaggroRange = 8f;
    [SerializeField] private float ghostFlightHeight = 0.25f;

    [Header("Ghost Health Bar")]
    [SerializeField] private Vector3 ghostHealthBarOffset = new Vector3(0f, 1.4f, 0f);
    [SerializeField] private Vector2 ghostHealthBarPixelSize = new Vector2(60f, 7f);
    [SerializeField] private float ghostHealthBarWorldScale = 0.007f;

    private float timer;
    private bool wasNight;
    private int ghostsSpawnedThisNight;
    private int currentNightIndex;
    private bool nightEnded;
    private readonly NightProfile endlessProfile = new NightProfile();
    private readonly List<GameObject> activeGhosts = new List<GameObject>();

    public int TotalThisNight => GetCurrentProfile()?.totalGhosts ?? 0;
    public int SpawnedThisNight => ghostsSpawnedThisNight;
    public int RemainingThisNight => Mathf.Max(0, TotalThisNight - ghostsSpawnedThisNight);

    private void Start()
    {
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
    }

    private void Update()
    {
        bool isNight = DayNightManager.Instance != null
                       && DayNightManager.Instance.CurrentPhase == DayNightManager.Phase.Night;

        // 낮 → 밤 전환 감지 → 이번 밤 카운터 리셋
        if (!wasNight && isNight)
            ResetForNewNight();

        // 밤 → 낮 전환 감지 → 정리
        if (wasNight && !isNight)
            ClearGhosts();

        wasNight = isNight;

        if (!isNight || nightEnded) return;

        activeGhosts.RemoveAll(g => g == null);

        NightProfile profile = GetCurrentProfile();
        int total = profile?.totalGhosts ?? 10;

        // 이 밤 유령 다 스폰 + 다 죽음 → 새벽
        if (ghostsSpawnedThisNight >= total && activeGhosts.Count == 0)
        {
            nightEnded = true;
            if (autoEndNight)
            {
                Debug.Log($"[GhostSpawner] {profile?.label ?? "Night"} 클리어 → 낮으로");
                DayNightManager.Instance?.BeginDay();
            }
            return;
        }

        // 이미 다 스폰했으면 남은 유령 죽을 때까지 대기
        if (ghostsSpawnedThisNight >= total) return;

        timer -= Time.deltaTime;
        if (timer > 0f || activeGhosts.Count >= maxGhostsAlive) return;

        if (SpawnGhostForNight(profile))
        {
            ghostsSpawnedThisNight++;
            timer = profile?.spawnInterval ?? 5f;
        }
    }

    private void ResetForNewNight()
    {
        ghostsSpawnedThisNight = 0;
        nightEnded = false;
        int dayCount = DayNightManager.Instance?.DayCount ?? 1;
        currentNightIndex = Mathf.Max(0, dayCount - 1);
        NightProfile p = GetCurrentProfile();
        timer = p?.spawnInterval ?? 5f;

        if (p != null)
            Debug.Log($"[GhostSpawner] {p.label} 시작 — 총 {p.totalGhosts}마리, 간격 {p.spawnInterval}s");
    }

    private NightProfile GetCurrentProfile()
    {
        if (nightProfiles == null || nightProfiles.Length == 0) return null;

        int finalIndex = nightProfiles.Length - 1;
        if (currentNightIndex <= finalIndex)
        {
            return nightProfiles[Mathf.Clamp(currentNightIndex, 0, finalIndex)];
        }

        NightProfile finalProfile = nightProfiles[finalIndex];
        if (finalProfile == null) return null;

        int nightsAfterFinal = currentNightIndex - finalIndex;
        endlessProfile.label = $"Night {currentNightIndex + 1}";
        endlessProfile.totalGhosts = finalProfile.totalGhosts + endlessExtraGhostsPerNight * nightsAfterFinal;
        endlessProfile.spawnInterval = finalProfile.spawnInterval;
        endlessProfile.tier1Weight = finalProfile.tier1Weight;
        endlessProfile.tier2Weight = finalProfile.tier2Weight;
        endlessProfile.tier3Weight = finalProfile.tier3Weight;
        return endlessProfile;
    }

    private bool SpawnGhostForNight(NightProfile profile)
    {
        if (spawnPoints == null || spawnPoints.Length == 0) return false;

        GhostTier tier = PickTierForProfile(profile);
        if (tier == null || tier.prefabs == null || tier.prefabs.Length == 0) return false;

        GameObject prefab = tier.prefabs[Random.Range(0, tier.prefabs.Length)];
        if (prefab == null) return false;

        Transform sp = spawnPoints[Random.Range(0, spawnPoints.Length)];
        GameObject ghost = Instantiate(prefab, sp.position, sp.rotation);

        Damageable dmg = ghost.GetComponent<Damageable>();
        if (dmg == null) dmg = ghost.AddComponent<Damageable>();
        dmg.SetMaxHealth(tier.health);

        GhostAI ai = ghost.GetComponent<GhostAI>();
        if (ai == null) ai = ghost.AddComponent<GhostAI>();
        // 티어별 공격력 사용 (0 이하면 폴백으로 전역 값)
        float dmgForTier = tier.attackDamage > 0f ? tier.attackDamage : ghostAttackDamage;
        ai.ConfigureStats(
            moveSpeed: ghostMoveSpeed,
            attackRange: ghostAttackRange,
            attackDamage: dmgForTier,
            attackCooldown: ghostAttackCooldown,
            aggroRange: ghostAggroRange,
            deaggroRange: ghostDeaggroRange,
            flightHeight: ghostFlightHeight);
        ai.SetCoinDrops(tier.baseCoinPrefab, tier.baseCoinValue,
                        tier.bonusCoinPrefab, tier.bonusCoinValue, tier.bonusCoinChance);

        if (ghost.GetComponent<HealthBar>() == null)
        {
            HealthBar hb = ghost.AddComponent<HealthBar>();
            hb.Configure(ghostHealthBarOffset, ghostHealthBarPixelSize, ghostHealthBarWorldScale);
        }

        activeGhosts.Add(ghost);
        return true;
    }

    private GhostTier PickTierForProfile(NightProfile profile)
    {
        if (tiers == null || tiers.Length == 0) return null;

        // Night Profile 가중치 우선
        if (profile != null)
        {
            float t1 = Mathf.Max(0f, profile.tier1Weight);
            float t2 = Mathf.Max(0f, profile.tier2Weight);
            float t3 = Mathf.Max(0f, profile.tier3Weight);
            float total = t1 + t2 + t3;
            if (total > 0f)
            {
                float roll = Random.value * total;
                if (roll < t1 && tiers.Length > 0) return tiers[0];
                if (roll < t1 + t2 && tiers.Length > 1) return tiers[1];
                if (tiers.Length > 2) return tiers[2];
                return tiers[tiers.Length - 1];
            }
        }

        // 폴백: 티어의 spawnWeight 사용
        float sum = 0f;
        foreach (GhostTier t in tiers) if (t != null) sum += Mathf.Max(0f, t.spawnWeight);
        if (sum <= 0f) return tiers[0];
        float r = Random.value * sum;
        float acc = 0f;
        foreach (GhostTier t in tiers)
        {
            if (t == null) continue;
            acc += Mathf.Max(0f, t.spawnWeight);
            if (r <= acc) return t;
        }
        return tiers[tiers.Length - 1];
    }

    private void ClearGhosts()
    {
        foreach (GameObject g in activeGhosts)
            if (g != null) Destroy(g);
        activeGhosts.Clear();
    }

    public void StartSpawning()
    {
        ResetForNewNight();
    }

    public void StopSpawning()
    {
        ClearGhosts();
        nightEnded = true;
    }
}
