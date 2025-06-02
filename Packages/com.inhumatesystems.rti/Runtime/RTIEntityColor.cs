using UnityEngine;

namespace Inhumate.UnityRTI {

    // Sets a renderer material color according to its entity metadata

    [RequireComponent(typeof(RTIEntity))]
    public class RTIEntityColor : MonoBehaviour
    {

        public new Renderer renderer;


        void Start()
        {
            if (renderer == null) renderer = GetComponent<Renderer>();
            if (renderer != null) {
                var entity = GetComponent<RTIEntity>();
                if (entity.color.a > 1e-5 || entity.color.maxColorComponent > 1e-5) {
                    renderer.material.color = entity.color;
                }
            }
            
        }

    }

}
