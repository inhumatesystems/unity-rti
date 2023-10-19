using System.Collections;
using System.Collections.Generic;
using Inhumate.RTI.Proto;
using UnityEngine;

namespace Inhumate.Unity.RTI {
    public class RTIInjectableActivator : RTIInjectionBehaviour {

        public GameObject[] objects;

        public override void Begin() {
            foreach (var o in objects) o.SetActive(true);
            base.Begin();
        }

        public override void End() {
            foreach (var o in objects) o.SetActive(false);
            base.End();
        }

    }
}
