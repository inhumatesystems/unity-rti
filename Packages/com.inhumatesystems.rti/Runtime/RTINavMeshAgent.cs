using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Inhumate.RTI;

namespace Inhumate.Unity.RTI {

    [RequireComponent(typeof(NavMeshAgent))]
    public class RTINavMeshAgent : RTIEntityStateBehaviour<Proto.NavMeshAgentState> {

        public override string ChannelName => RTIChannel.InternalPrefix + "navmeshagent";

        public float updateInterval = 1f;
        public float destinationThreshold = 0.1f;
        public float speedThreshold = 0.1f;
        public float angularSpeedThreshold = 0.1f;

        private NavMeshAgent agent;
        private Proto.NavMeshAgentState current;
        private float lastPublishTime;

        protected override void Start() {
            base.Start();
            agent = GetComponent<NavMeshAgent>();
        }

        protected override void OnMessage(Proto.NavMeshAgentState message) {
            if (receiving && enabled) {
                current = message;
                agent.destination = current.Destination.ToVector3();
                agent.speed = current.Speed;
                agent.angularSpeed = current.AngularSpeed;
            }
        }

        void Update() {
            if (entity.published && publishing && Time.time - lastPublishTime > updateInterval
                    && (current == null || (agent.destination - current.Destination.ToVector3()).magnitude > destinationThreshold
                    || Mathf.Abs(agent.speed - current.Speed) > speedThreshold || Mathf.Abs(agent.angularSpeed - current.AngularSpeed) > angularSpeedThreshold)) {
                lastPublishTime = Time.time;
                current = new Proto.NavMeshAgentState {
                    Id = entity.id,
                    Destination = agent.destination.ToProto(),
                    Speed = agent.speed,
                    AngularSpeed = agent.angularSpeed
                };
                Publish(current);
            }
        }

    }

}
