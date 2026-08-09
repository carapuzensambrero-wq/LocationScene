using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Pooled zombie body. It contains no target-selection logic; the crowd
/// controller owns that decision and assigns a destination/attack slot.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public sealed class ZombieAgent : MonoBehaviour
{
    [SerializeField] private NavMeshAgent agent;
    [SerializeField, Min(0f)] private float movementSpeed = 2f;
    [SerializeField, Min(0f)] private float attackDistance = 1.4f;
    [SerializeField, Min(0f)] private float attackDuration = 0.75f;

    private ZombieCrowdController owner;
    private Transform interestTarget;
    private Vector3 interestPosition;
    private bool attackAssigned;
    private bool attackReported;
    private float attackTimer;

    public bool IsAttackAssigned => attackAssigned;
    public bool IsAvailable => isActiveAndEnabled && owner != null && !attackAssigned;
    public NavMeshAgent Agent => agent;

    private void Awake()
    {
        if (agent == null) agent = GetComponent<NavMeshAgent>();
        agent.speed = movementSpeed;
    }

    private void Update()
    {
        if (!attackAssigned) return;

        Vector3 targetPosition = interestTarget != null ? interestTarget.position : interestPosition;
        if (!attackReported && Vector3.SqrMagnitude(transform.position - targetPosition) <= attackDistance * attackDistance)
        {
            attackReported = true;
            Debug.Log($"Zombie {name} attacks {(interestTarget != null ? interestTarget.name : "interest event")}.", this);
            attackTimer = attackDuration;
            agent.isStopped = true;
        }

        if (!attackReported) return;

        attackTimer -= Time.deltaTime;
        if (attackTimer <= 0f)
            owner.Despawn(this);
    }

    public void Activate(ZombieCrowdController crowdController, Vector3 position)
    {
        owner = crowdController;
        interestTarget = null;
        interestPosition = position;
        attackAssigned = false;
        attackReported = false;
        attackTimer = 0f;
        transform.position = position;
        gameObject.SetActive(true);

        agent.enabled = true;
        agent.isStopped = false;
        agent.ResetPath();
    }

    public void AssignDestination(Transform target, Vector3 destination, bool attack)
    {
        interestTarget = target;
        interestPosition = destination;
        attackAssigned = attack;
        attackReported = false;
        attackTimer = 0f;
        agent.isStopped = false;
        agent.SetDestination(destination);
    }

    public void Deactivate()
    {
        attackAssigned = false;
        interestTarget = null;
        if (agent != null && agent.enabled)
        {
            agent.isStopped = true;
            agent.ResetPath();
        }
        gameObject.SetActive(false);
    }
}
