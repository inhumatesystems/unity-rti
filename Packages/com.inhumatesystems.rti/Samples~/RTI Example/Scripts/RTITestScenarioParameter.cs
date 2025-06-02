using UnityEngine;

namespace Inhumate.UnityRTI.Example {

    public class RTITestScenarioParameter : MonoBehaviour {

        void Awake() {
            var value = RTIConnection.Instance.GetScenarioParameterValue("name") ?? "[nothing]";
            Debug.Log($"Magic scenario parameter value: {value}", this);
        }
    }

}
