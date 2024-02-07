using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Inhumate.RTI.Proto;
using Inhumate.RTI.Client;

namespace Inhumate.Unity.RTI {

    public abstract class RTIInjectionBehaviour : MonoBehaviour {

        public RTIInjectable injectable {
            get {
                if (_injectable == null) _injectable = GetComponentInParent<RTIInjectable>();
                return _injectable;
            }
        }
        private RTIInjectable _injectable;

        public bool running { get; protected set; }

        public Injection injection { get; protected set; }
        protected RTIConnection RTI => RTIConnection.Instance;

        // Called when injection is injected.
        public virtual void Inject(Injection injection, RTIInjectable injectable) {
            this.injection = injection;
            this._injectable = injectable;
        }

        // Called when injection is disabled.
        // Destroy/disable/de-active triggers/conditions etc here.
        public virtual void Disable() {
        }

        // Called when injection is enabled.
        // Create/enable/active triggers/conditions etc here.
        public virtual bool Enable() {
            return true;
        }

        // Called when injection is manually started.
        public virtual void RequestStart() {
            Begin();
        }

        // Called when injection is manually stopped.
        public virtual void Stop() {
            End();
        }

        // Called when injection is canceled.
        public virtual void Cancel() {
            if (running) End();
            Disable();
        }

        // Called when injection is scheduled.
        public virtual void Schedule() { }

        // Override to implement start of injection behaviour.
        // Called by RequestStart() default implementation.
        // Should be called by triggers/conditions.
        // Should set running to true.
        public virtual void Begin() {
            running = true;
        }

        // Called by Stop() and Cancel() default implementation.
        // Should be called by implemented behaviour when it ends.
        // Should set running to false.
        public virtual void End() {
            running = false;
        }

        public string GetParameterValue(string name) {
            if (injection != null && injection.ParameterValues.ContainsKey(name)) {
                return injection.ParameterValues[name];
            } else if (injectable != null) {
                return injectable.GetParameterDefaultValue(name);
            }
            return null;
        }

        protected void Publish() {
            RTI.Publish(RTIConstants.InjectionChannel, injection);
        }

    }

}
