using UnityEngine;
using UnityEngine.AI;

public class AINavigation : MonoBehaviour
{
    public Transform exitPoint; // Assign the exit GameObject's Transform in the Inspector
    private NavMeshAgent navAgent;

    void Start()
    {
        // Get the NavMeshAgent component attached to the AI character
        navAgent = GetComponent<NavMeshAgent>();

        if (exitPoint != null)
        {
            // Set the destination of the NavMeshAgent to the exit point
            navAgent.SetDestination(exitPoint.position);
        }
        else
        {
            Debug.LogError("Exit point not assigned!");
        }
    }

    void Update()
    {
        // Optionally, check if the AI has reached the destination
        if (navAgent.remainingDistance <= navAgent.stoppingDistance && !navAgent.pathPending)
        {
            Debug.Log("AI has reached the exit!");
        }
    }
}
