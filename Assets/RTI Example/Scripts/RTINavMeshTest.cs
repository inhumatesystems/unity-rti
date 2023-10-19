using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Inhumate.Unity.RTI;

namespace Inhumate.Unity.Examples.RTI {

    [RequireComponent(typeof(RTIEntity))]
    [RequireComponent(typeof(NavMeshAgent))]
    public class RTINavMeshTest : MonoBehaviour {
        
        RTIEntity entity;
        NavMeshAgent agent;
        Vector3 center;

        void Start() {
            entity = GetComponent<RTIEntity>();
            agent = GetComponent<NavMeshAgent>();
            center = transform.position;
            StartCoroutine(RotateDestinationRoutine());
        }

        private IEnumerator RotateDestinationRoutine() {
            while (isActiveAndEnabled) {
                if (entity.publishing) {
                    var destination = center + Mathf.Sin(Time.time) * Vector3.forward * 3f + Mathf.Cos(Time.time) * Vector3.right * 3f;
                    agent.SetDestination(destination);
                }
                yield return new WaitForSeconds(0.1f);
            }
        }
    }

}
