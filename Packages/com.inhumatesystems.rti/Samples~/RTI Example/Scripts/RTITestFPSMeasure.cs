using UnityEngine;
using Inhumate.RTI.Proto;
using Inhumate.UnityRTI;

namespace Inhumate.UnityRTI.Example {

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

 