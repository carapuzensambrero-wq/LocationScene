using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Single authority for zombie spawning, pooling, interest selection and the
/// limited number of simultaneous attackers.
/// </summary>
public sealed class ZombieCrowdController : MonoBehaviour
{
    [Header("Pool")]
    [SerializeField] private ZombieAgent zombiePrefab;
    [SerializeField, Min(0)] private int initialPoolSize = 24;
    [SerializeField, Min(0)] private int maximumZombieCount = 100;
    [SerializeField] private Transform zombieContainer;

    [Header("Interest events")]
    [SerializeField, Min(0.1f)] private float defaultInterestLifetime = 5f;
    [SerializeField, Min(0.1f)] private float defaultInterestStrength = 1f;
    [SerializeField, Min(0.1f)] private float repathInterval = 0.25f;
    [SerializeField, Min(0f)] private float attackRadius = 1.5f;
    [SerializeField, Min(0f)] private float holdingDistance = 4f;
    [SerializeField, Min(0f)] private float holdingRingSpacing = 2f;
    [SerializeField, Min(0f)] private float destinationSampleRadius = 3f;
    [SerializeField, Min(0)] private int maximumAttackers = 3;

    [Header("Spawning")]
    [SerializeField, Min(0f)] private float spawnRadius = 25f;
    [SerializeField, Min(0f)] private float minimumSpawnDistance = 8f;
    [SerializeField, Min(0f)] private float spawnOutsideCameraMargin = 4f;
    [SerializeField, Min(0)] private int spawnAttempts = 20;
    [SerializeField] private LayerMask spawnObstacleMask = Physics.DefaultRaycastLayers;

    private readonly List<ZombieAgent> activeZombies = new List<ZombieAgent>();
    private readonly Queue<ZombieAgent> pooledZombies = new Queue<ZombieAgent>();
    private readonly List<InterestEvent> interestEvents = new List<InterestEvent>();
    private float repathTimer;

    public IReadOnlyList<ZombieAgent> ActiveZombies => activeZombies;

    private void Awake()
    {
        if (zombieContainer == null) zombieContainer = transform;
        if (zombiePrefab == null)
        {
            Debug.LogError("ZombieCrowdController requires a ZombieAgent prefab.", this);
            enabled = false;
            return;
        }

        for (int i = 0; i < initialPoolSize; i++)
            pooledZombies.Enqueue(CreatePooledZombie());
    }

    private void Update()
    {
        repathTimer -= Time.deltaTime;
        if (repathTimer > 0f) return;
        repathTimer = repathInterval;
        UpdateInterestEvents();
        RebuildOrders();
    }

    private void Start()
    {
        if (initialPoolSize > 0 && activeZombies.Count == 0)
        {
            for (int i = 0; i < initialPoolSize; i++)
                SpawnZombie();
        }
    }

    [ContextMenu("Spawn One Zombie")]
    private void SpawnOneZombieFromInspector()
    {
        SpawnZombie();
    }

    public void AddInterestPoint(Vector3 position, float lifetime = -1f, float strength = -1f)
    {
        interestEvents.Add(new InterestEvent
        {
            Position = position,
            TimeRemaining = lifetime > 0f ? lifetime : defaultInterestLifetime,
            Strength = strength > 0f ? strength : defaultInterestStrength
        });
    }

    public void SetPlayerInterest(Transform player)
    {
        if (player != null) AddInterestPoint(player.position, 0.5f, 0.25f);
    }

    public ZombieAgent SpawnZombie()
    {
        if (maximumZombieCount > 0 && activeZombies.Count >= maximumZombieCount) return null;
        if (!TryFindSpawnPosition(out Vector3 position)) return null;

        ZombieAgent zombie = pooledZombies.Count > 0 ? pooledZombies.Dequeue() : CreatePooledZombie();
        zombie.Activate(this, position);
        activeZombies.Add(zombie);
        return zombie;
    }

    public void Despawn(ZombieAgent zombie)
    {
        if (zombie == null || !activeZombies.Remove(zombie)) return;
        zombie.Deactivate();
        pooledZombies.Enqueue(zombie);
    }

    private void RebuildOrders()
    {
        List<Transform> players = GlobalConfig.Instance != null ? new List<Transform>(GlobalConfig.Instance.Players) : new List<Transform>();
        players.RemoveAll(player => player == null);
        if (players.Count == 0 && interestEvents.Count == 0) return;

        int attackers = 0;
        for (int i = 0; i < activeZombies.Count; i++)
        {
            ZombieAgent zombie = activeZombies[i];
            Transform player = FindNearestPlayer(zombie.transform.position, players);
            Vector3 targetPosition = player != null ? player.position : FindBestInterest(zombie.transform.position);
            if (player == null && targetPosition == default) continue;

            float distance = Vector3.Distance(zombie.transform.position, targetPosition);
            bool attack = attackers < maximumAttackers && distance <= attackRadius;
            Vector3 destination = attack
                ? targetPosition
                : GetHoldingDestination(targetPosition, i, zombie.transform.position);

            if (NavMesh.SamplePosition(destination, out NavMeshHit hit, destinationSampleRadius, NavMesh.AllAreas))
                zombie.AssignDestination(player, hit.position, attack);

            if (attack) attackers++;
        }
    }

    private Vector3 FindBestInterest(Vector3 position)
    {
        Vector3 best = default;
        float bestScore = float.MinValue;
        for (int i = 0; i < interestEvents.Count; i++)
        {
            InterestEvent point = interestEvents[i];
            float distance = Vector3.Distance(position, point.Position);
            float score = point.Strength / Mathf.Max(1f, distance);
            if (score > bestScore)
            {
                bestScore = score;
                best = point.Position;
            }
        }
        return best;
    }

    private Vector3 GetHoldingDestination(Vector3 target, int index, Vector3 zombiePosition)
    {
        float angle = Mathf.Atan2(zombiePosition.z - target.z, zombiePosition.x - target.x) + ((index % 5) - 2) * 0.35f;
        float radius = holdingDistance + (index % 3) * holdingRingSpacing;
        return target + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
    }

    private Transform FindNearestPlayer(Vector3 position, List<Transform> players)
    {
        Transform nearest = null;
        float nearestDistance = float.MaxValue;
        for (int i = 0; i < players.Count; i++)
        {
            float distance = (players[i].position - position).sqrMagnitude;
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearest = players[i];
            }
        }
        return nearest;
    }

    private void UpdateInterestEvents()
    {
        for (int i = interestEvents.Count - 1; i >= 0; i--)
        {
            InterestEvent interest = interestEvents[i];
            interest.TimeRemaining -= repathInterval;
            if (interest.TimeRemaining <= 0f) interestEvents.RemoveAt(i);
            else interestEvents[i] = interest;
        }
    }

    private struct InterestEvent
    {
        public Vector3 Position;
        public float TimeRemaining;
        public float Strength;
    }

    private bool TryFindSpawnPosition(out Vector3 position)
    {
        Camera[] cameras = Camera.allCameras;
        for (int attempt = 0; attempt < Mathf.Max(1, spawnAttempts); attempt++)
        {
            Vector2 offset = Random.insideUnitCircle;
            if (offset.sqrMagnitude < 0.001f) continue;
            offset = offset.normalized * Random.Range(minimumSpawnDistance, Mathf.Max(minimumSpawnDistance, spawnRadius));
            Vector3 candidate = transform.position + new Vector3(offset.x, 0f, offset.y);
            if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, destinationSampleRadius, NavMesh.AllAreas)) continue;
            if (Physics.CheckSphere(hit.position, 0.35f, spawnObstacleMask, QueryTriggerInteraction.Ignore)) continue;

            bool visible = false;
            for (int i = 0; i < cameras.Length; i++)
            {
                if (IsVisible(cameras[i], hit.position))
                {
                    visible = true;
                    break;
                }
            }

            if (!visible)
            {
                position = hit.position;
                return true;
            }
        }

        position = default;
        Debug.LogWarning("Zombie spawn failed. Check NavMesh, spawn radius, camera visibility and spawnObstacleMask.", this);
        return false;
    }

    private bool IsVisible(Camera camera, Vector3 position)
    {
        if (camera == null) return false;

        Vector3 viewport = camera.WorldToViewportPoint(position);
        if (viewport.z <= 0f || viewport.x < 0f || viewport.x > 1f || viewport.y < 0f || viewport.y > 1f)
            return false;

        Vector3 direction = position - camera.transform.position;
        return !Physics.Raycast(camera.transform.position, direction.normalized, direction.magnitude, spawnObstacleMask, QueryTriggerInteraction.Ignore)
            && direction.magnitude > spawnOutsideCameraMargin;
    }

    private ZombieAgent CreatePooledZombie()
    {
        ZombieAgent zombie = Instantiate(zombiePrefab, zombieContainer);
        zombie.Deactivate();
        return zombie;
    }
}
