using System.Collections;
using System.Collections.Generic;
using Inhumate.RTI.Proto;
using UnityEngine;

namespace Inhumate.Unity.RTI {

    public class RTIScenarioLoader : MonoBehaviour {

        public RuntimeState previousState { get; set; }

        public void Done() {
            if (RTIConnection.Instance.Client.State == RuntimeState.Loading) {
                if (previousState == RuntimeState.Playback) {
                    RTIConnection.Instance.Client.State = RuntimeState.Playback;
                    Time.timeScale = RTIConnection.Instance.timeScale;
                } else {
                    RTIConnection.Instance.Client.State = RuntimeState.Ready;
                    Time.timeScale = 0;
                }
            }
        }

    }

}
