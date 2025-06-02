using Inhumate.UnityRTI;
using UnityEngine;

namespace Inhumate.UnityRTI.Example {

    public class RTITestCustomScenarioLoading : MonoBehaviour {

        public GameObject cube, sphere;

        protected RTIConnection RTI => RTIConnection.Instance;

        void Awake() {
            // Specify scenarios that can be loaded
            RTI.scenarioNames.Add("cube");
            RTI.scenarioNames.Add("sphere");
        }

        void Start() {
            // Override default scene loading
            RTI.CustomLoadScenario += message => {
                cube.SetActive(message.Name == "cube");
                sphere.SetActive(message.Name == "sphere");
            };

            // Override loading home scene on reset
            RTI.CustomReset += delegate {
                cube.SetActive(false);
                sphere.SetActive(false);
            };

            cube.SetActive(false);
            sphere.SetActive(false);
        }

    }

}
