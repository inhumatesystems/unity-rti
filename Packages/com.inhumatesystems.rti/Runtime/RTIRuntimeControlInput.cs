using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Inhumate.Unity.RTI {
    public class RTIRuntimeControlInput : MonoBehaviour {
        void Update() {
            if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) {
                if (Input.GetKeyDown(KeyCode.F8)) {
                    RTIRuntimeControl.PublishReset();
                }
                if (Input.GetKeyDown(KeyCode.F9)) {
                    RTIRuntimeControl.PublishStart();
                }
                if (Input.GetKeyDown(KeyCode.F10)) {
                    RTIRuntimeControl.PublishPlay();
                }
                if (Input.GetKeyDown(KeyCode.F11)) {
                    RTIRuntimeControl.PublishPause();
                }
                if (Input.GetKeyDown(KeyCode.F12)) {
                    RTIRuntimeControl.PublishStop();
                }
            }
        }
    }
}
