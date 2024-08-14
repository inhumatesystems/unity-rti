using Inhumate.RTI;
using Inhumate.RTI.Proto;
using UnityEngine;

namespace Inhumate.Unity.RTI {

    [RequireComponent(typeof(RTIEntity))]
    public class RTIOwnEntityOnCollision : MonoBehaviour {

        [Tooltip("Minimum time (milliseconds) since last ownership change")]
        public float gracePeriod = 100;

        protected RTIConnection RTI => RTIConnection.Instance;
        protected RTIEntity entity;

        void Start() {
            entity = GetComponent<RTIEntity>();
        }

        void OnCollisionEnter(Collision collision) {
            if (entity == null || !entity.owned) return;
            var otherEntity = collision.gameObject.GetComponentInParent<RTIEntity>();
            if (otherEntity == null) return;
            if (otherEntity.owned) return;

            if ((Time.time - otherEntity.lastOwnershipChangeTime) * 1000f < gracePeriod) return;

            // Don't take ownership of another entity who takes ownership the same way
            var otherOwn = otherEntity.GetComponent<RTIOwnEntityOnCollision>();
            if (otherOwn != null) return;

            Debug.Log($"Assuming ownership of colliding entity {otherEntity.id}", otherEntity);
            otherEntity.AssumeOwnership();
        }
    }

}
