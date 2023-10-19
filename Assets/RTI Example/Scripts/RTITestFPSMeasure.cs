using UnityEngine;
using Inhumate.RTI.Proto;
using Inhumate.Unity.RTI;

namespace Inhumate.Unity.Examples.RTI {

    public class RTITestFPSMeasure : RTIMeasure {

        public RTITestFPSMeasure() {
            id = "FPS";
            title = "Frames per second";
            unit = "Hz";
            interval = 3;
        }

        void Update() {
            Measure(1f / Time.unscaledDeltaTime);
        }

    }
}

 