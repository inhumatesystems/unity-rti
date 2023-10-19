using System.Collections;
using System.Collections.Generic;
using Inhumate.RTI.Proto;
using UnityEngine;

namespace Inhumate.Unity.RTI {

    // Trigger (using a collider with isTrigger=true) an injectable to start or end
    public class RTIInjectableTrigger : RTIInjectionBehaviour {

        public enum TriggerMode {
            StartOnEnterEndOnExit,
            StartOnEnter,
            EndOnEnter,
            //ResetOnEnter
        }
        public TriggerMode mode;

        void Start() {
            Disable();
        }

        public override void Disable() {
            foreach (var collider in GetComponentsInChildren<Collider>()) {
                if (collider.isTrigger) collider.enabled = false;
            }
        }

        public override bool Enable() {
            var colliders = GetComponentsInChildren<Collider>();
            foreach (var collider in colliders) {
                if (collider.isTrigger) collider.enabled = true;
            }
            return colliders.Length > 0;
        }

        public override void Begin() {
            if (injectable != null) {
                var behaviours = injectable.GetComponentsInChildren<RTIInjectionBehaviour>();
                foreach (var behaviour in behaviours) {
                    if (behaviour != this && !(behaviour is RTIInjectableTrigger)) behaviour.Begin();
                }
            }
            base.Begin();
        }

        public override void End() {
            Disable();
            if (injectable != null) {
                var behaviours = injectable.GetComponentsInChildren<RTIInjectionBehaviour>();
                foreach (var behaviour in behaviours) {
                    if (behaviour != this && !(behaviour is RTIInjectableTrigger)) behaviour.End();
                }
            }
            base.End();
        }

        void OnTriggerEnter(Collider other) {
            switch (mode) {
                case TriggerMode.StartOnEnterEndOnExit:
                case TriggerMode.StartOnEnter:
                    var entity = other.GetComponentInParent<RTIEntity>();
                    if (entity != null) {
                        if (!string.IsNullOrEmpty(entity.title)) injectable.injection.Title = entity.title;
                        else if (entity.titleFromName) injectable.injection.Title = entity.name;
                    }
                    Begin();
                    break;
                case TriggerMode.EndOnEnter:
                    End();
                    break;
                    // TODO Reset
            }
        }

        void OnTriggerExit(Collider other) {
            if (injectable == null || injectable.injection == null) return;
            switch (mode) {
                case TriggerMode.StartOnEnterEndOnExit: End(); break;
            }
        }

    }
}
