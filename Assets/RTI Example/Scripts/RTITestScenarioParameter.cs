using UnityEngine;

namespace Inhumate.Unity.RTI {

public class RTITestScenarioParameter : MonoBehaviour {

    void Awake() {
        var value = RTIConnection.Instance.GetScenarioParameterValue("name") ?? "[nothing]";
        Debug.Log($"Magic scenario parameter value: {value}", this);
    }
}

}
