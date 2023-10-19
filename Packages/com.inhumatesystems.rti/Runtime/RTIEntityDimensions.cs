using UnityEngine;

namespace Inhumate.Unity.RTI {

    // Scales an entity according to its metadata dimensions

    [RequireComponent(typeof(RTIEntity))]
    public class RTIEntityDimensions : MonoBehaviour
    {

        public Transform target;
        public bool adjustScale;
        public bool adjustCenter;


        void Start()
        {
            var entity = GetComponent<RTIEntity>();
            if (target != null) {
                if (adjustScale && entity.size.magnitude > 1e-5) {
                    target.localScale = new Vector3(
                        target.localScale.x * entity.size.x,
                        target.localScale.y * entity.size.y,
                        target.localScale.z * entity.size.z
                    );
                }
                if (adjustCenter && entity.center.magnitude > 1e-5) {
                    target.localPosition += entity.center;
                }
            }
            
        }

    }

}
