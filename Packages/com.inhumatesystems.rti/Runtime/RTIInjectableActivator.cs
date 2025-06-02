using System.Collections;
using System.Collections.Generic;
using Inhumate.RTI.Proto;
using UnityEngine;

namespace Inhumate.UnityRTI {
    public class RTIInjectableActivator : RTIInjectionBehaviour {

        public GameObject[] objects;

        public enum EventMode {
            Never,
            Begin,
            End,
            Stop,
            Cancel,
            StopOrCancel
        }

        public EventMode activateOn = EventMode.Begin;
        public EventMode deactivateOn = EventMode.End;

        public override void Begin() {
            if (activateOn == EventMode.Begin) Activate();
            if (deactivateOn == EventMode.Begin) Deactivate();
            base.Begin();
        }

        public override void End() {
            if (activateOn == EventMode.End) Activate();
            if (deactivateOn == EventMode.End) Deactivate();
            base.End();
        }

        public override void Stop() {
            if (activateOn == EventMode.Stop || activateOn == EventMode.StopOrCancel) Activate();
            if (deactivateOn == EventMode.Stop || deactivateOn == EventMode.StopOrCancel) Deactivate();
            base.Stop();
        }

        public override void Cancel() {
            if (activateOn == EventMode.Cancel || activateOn == EventMode.StopOrCancel) Activate();
            if (deactivateOn == EventMode.Cancel || deactivateOn == EventMode.StopOrCancel) Deactivate();
            base.Cancel();
        }

        private void Activate() {
            foreach (var o in objects) if (o != null) o.SetActive(true);
        }

        private void Deactivate() {
            foreach (var o in objects) if (o != null) o.SetActive(false);
        }

    }
}
