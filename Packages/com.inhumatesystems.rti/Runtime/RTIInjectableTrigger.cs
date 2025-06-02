using System.Collections;
using System.Collections.Generic;
using Inhumate.RTI.Proto;
using UnityEngine;

namespace Inhumate.UnityRTI {

    // Trigger (using a collider with isTrigger=true) an injectable to start or end
    public class RTIInjectableTrigger : RTIInjectionBehaviour {

        public enum TriggerMode {
            StartOnEnterEndOnExit,
            StartOnEnter,
            EndOnEnter,
            EndOnExit,
            EndOnEnterAny,
            EndOnExitAny,
            //ResetOnEnter
        }
        public TriggerMode mode;

        public string triggerTag;
        public LayerMask triggerLayers = -1;

        public Collider triggeredCollider { get; private set; }

        void Start() {
            Disable();
        }

        public override void Disable() {
            foreach (var collider in GetComponentsInChildren<Collider>()) {
                if (collider.isTrigger) collider.enabled = false;
            }
            triggeredCollider = null;
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
                    if (behaviour != this) {
                        if (behaviour is RTIInjectableTrigger) ((RTIInjectableTrigger)behaviour).running = true;
                        else behaviour.Begin();
                    }
                }
            }
            base.Begin();
        }

        public override void End() {
            Disable();
            if (injectable != null) {
                var behaviours = injectable.GetComponentsInChildren<RTIInjectionBehaviour>();
                foreach (var behaviour in behaviours) {
                    if (behaviour != this) {
                        if (behaviour is RTIInjectableTrigger) {
                            ((RTIInjectableTrigger)behaviour).running = false;
                            behaviour.Disable();
                        } else behaviour.End();
                    }
                }
            }
            base.End();
        }

        void OnTriggerEnter(Collider other) {
            if (!string.IsNullOrEmpty(triggerTag) && !other.CompareTag(triggerTag)) return;
            if (triggerLayers != (triggerLayers | (1 << other.gameObject.layer))) return;
            switch (mode) {
                case TriggerMode.StartOnEnter:
                case TriggerMode.StartOnEnterEndOnExit:
                    var entity = other.GetComponentInParent<RTIEntity>();
                    if (entity != null) {
                        if (!string.IsNullOrEmpty(entity.title)) injectable.injection.Title = entity.title;
                        else if (entity.titleFromName) injectable.injection.Title = entity.name;
                    }
                    foreach (var behaviour in injectable.GetComponentsInChildren<RTIInjectableTrigger>()) {
                        behaviour.triggeredCollider = other;
                    }
                    if (mode != TriggerMode.StartOnEnterEndOnExit) {
                        foreach (var collider in GetComponentsInChildren<Collider>()) {
                            if (collider.isTrigger) collider.enabled = false;
                        }
                    }
                    Begin();
                    break;
                case TriggerMode.EndOnEnter:
                    if (other == triggeredCollider) End();
                    break;
                case TriggerMode.EndOnEnterAny:
                    End();
                    break;
                    // TODO Reset
            }
        }

        void OnTriggerExit(Collider other) {
            if (injectable == null || injectable.injection == null || !running) return;
            switch (mode) {
                case TriggerMode.StartOnEnterEndOnExit:
                case TriggerMode.EndOnExit:
                    if (other == triggeredCollider) End();
                    break;
                case TriggerMode.EndOnExitAny:
                    End();
                    break;
            }
        }

    }
}
