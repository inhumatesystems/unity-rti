using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using Inhumate.RTI.Proto;
using System;
using Inhumate.RTI;
using System.Linq;

namespace Inhumate.UnityRTI {
    public class RTIInjectable : MonoBehaviour {

        public Injectable.Types.ControlMode startMode;
        public Injectable.Types.ControlMode endMode;

        public GameObject injectPrefab;

        public bool concurrent => injectPrefab != null;

        public enum AutoInjection {
            OnDemand,
            StartDisabled,
            StartEnabled
        }
        public AutoInjection autoInjection;

        [TextArea(3, 10)]
        public string description;

        public RTIParameter[] parameters = new RTIParameter[] { };

        public Injection injection => concurrent || injections.Count == 0 ? null : injections[injections.Count - 1];

        public event Action<Injection.Types.State> OnStateUpdated;

        protected List<Injection> injections = new List<Injection>();
        protected RTIInjectionBehaviour[] behaviours = new RTIInjectionBehaviour[] { };
        protected Dictionary<string, RTIInjectionBehaviour[]> injectionBehaviours = new Dictionary<string, RTIInjectionBehaviour[]>();
        protected RTIConnection RTI => RTIConnection.Instance;

        protected virtual void Awake() {
            behaviours = GetComponentsInChildren<RTIInjectionBehaviour>();
            if (injectPrefab != null && behaviours.Length > 0) {
                Debug.LogWarning($"Injectable has both inject prefab and behaviours in hierarchy", this);
            }
            RTI.RegisterInjectable(this);
            RTI.WhenConnectedOnce(() => {
                Publish();
                PublishClearInjections();
                // We delay the autoinject to get some time after PublishClearInjections().
                // Typically a lot of messages being pushed around at start...
                StartCoroutine(DelayedAutoInject());
            });
        }

        protected virtual void Start() {
        }

        IEnumerator DelayedAutoInject() {
            var time = 0f;
            while (time < 0.1f) {
                yield return null;
                time += Time.unscaledDeltaTime;
            }
            AutoInject();
        }

        private void AutoInject() {
            if (injection == null && autoInjection > AutoInjection.OnDemand) Inject(null);
        }

        private float lastRtiTime;
        protected virtual void Update() {
            foreach (var injection in injections) {
                if (injection.State == Injection.Types.State.Disabled && injection.EnableTime > float.Epsilon && RTI.time >= injection.EnableTime && lastRtiTime < injection.EnableTime) {
                    EnableInjection(injection);
                }
                var behaviours = GetBehaviours(injection);
                if (behaviours.Count() > 0) {
                    bool anyRunning = false;
                    foreach (var behaviour in behaviours) {
                        if (behaviour.running) {
                            anyRunning = true;
                            if (injection.State < Injection.Types.State.Running) {
                                injection.StartTime = RTI.time;
                                UpdateState(injection, Injection.Types.State.Running);
                            }
                        }
                    }
                    if (!anyRunning && injection.State == Injection.Types.State.Running) {
                        injection.EndTime = RTI.time;
                        UpdateState(injection, Injection.Types.State.End);
                    }
                }
            }
            lastRtiTime = RTI.time;
        }

        protected virtual void OnDestroy() {
            foreach (var injection in injections) {
                if (injection.State > Injection.Types.State.Disabled && injection.State < Injection.Types.State.End) {
                    CancelInjection(injection);
                }
            }
            RTI.UnregisterInjectable(this);
        }

        public void Inject(InjectionOperation.Types.Inject inject) {
            if (RTI.state != RuntimeState.Unknown && RTI.state != RuntimeState.Loading && RTI.state != RuntimeState.Ready && RTI.state != RuntimeState.Running && RTI.state != RuntimeState.Paused) {
                Debug.LogWarning($"Not injecting {name} while {RTI.state}");
                return;
            }
            if (!concurrent && injections.Count > 0 && injections[injections.Count - 1].State <= Injection.Types.State.Running) {
                Debug.LogError($"Already injected: {name} {injections[injections.Count - 1].Id}", this);
                return;
            }
            var injection = new Injection {
                Id = Guid.NewGuid().ToString(),
                Injectable = name
            };
            if (inject != null) {
                injection.EnableTime = inject.EnableTime;
                foreach (var pair in inject.ParameterValues) {
                    injection.ParameterValues.Add(pair.Key, pair.Value);
                    Debug.Log($"Injection parameter {pair.Key} = {pair.Value}");
                }
            }
            if (injectPrefab != null) {
                var injectObject = GameObject.Instantiate(injectPrefab);
                injectObject.transform.parent = transform;
                var injectBehaviours = injectObject.GetComponentsInChildren<RTIInjectionBehaviour>();
                injectionBehaviours[injection.Id] = injectBehaviours;
                foreach (var behaviour in injectBehaviours) {
                    behaviour.Inject(injection, this);
                }
            } else {
                foreach (var behaviour in behaviours) behaviour.Inject(injection, this);
            }
            if ((inject != null && inject.Disabled) || (inject == null && autoInjection < AutoInjection.StartEnabled) || !EnableInjection(injection)) {
                injection.State = Injection.Types.State.Disabled;
                Publish(injection);
            }
            injections.Add(injection);
        }

        public virtual bool EnableInjection(Injection injection = null) {
            if (injection == null) injection = this.injection;
            if (injection.State < Injection.Types.State.Enabled) {
                var behavioursEnabled = true;
                foreach (var behaviour in GetBehaviours(injection)) {
                    if (!behaviour.Enable()) {
                        behavioursEnabled = false;
                    }
                }
                if (!behavioursEnabled) {
                    foreach (var behaviour in GetBehaviours(injection)) behaviour.Disable();
                    return false;
                }
                injection.EnableTime = RTI.time;
                if (startMode == Injectable.Types.ControlMode.Immediate) {
                    foreach (var behaviour in GetBehaviours(injection)) behaviour.Begin();
                    injection.StartTime = RTI.time;
                    if (endMode == Injectable.Types.ControlMode.Immediate) {
                        foreach (var behaviour in GetBehaviours(injection)) behaviour.End();
                        injection.EndTime = RTI.time;
                        UpdateState(injection, Injection.Types.State.End);
                    } else {
                        UpdateState(injection, Injection.Types.State.Running);
                    }
                } else {
                    UpdateState(injection, Injection.Types.State.Enabled);
                }
            }
            return true;
        }

        public virtual void DisableInjection(Injection injection = null) {
            if (injection == null) injection = this.injection;
            foreach (var behaviour in GetBehaviours(injection)) behaviour.Disable();
            UpdateState(injection, Injection.Types.State.Disabled);
        }

        public virtual void StartInjection(Injection injection = null) {
            if (injection == null) {
                if (this.injection != null && this.injection.State > Injection.Types.State.Running) Inject(null);
                injection = this.injection;
            }
            var behaviours = GetBehaviours(injection);
            if (behaviours.Count() == 0) {
                injection.StartTime = RTI.time;
                if (endMode == Injectable.Types.ControlMode.Immediate) {
                    injection.EndTime = RTI.time;
                    UpdateState(injection, Injection.Types.State.End);
                } else {
                    UpdateState(injection, Injection.Types.State.Running);
                }
            } else {
                foreach (var behaviour in behaviours) behaviour.RequestStart();
                if (endMode == Injectable.Types.ControlMode.Immediate) {
                    injection.EndTime = RTI.time;
                    UpdateState(injection, Injection.Types.State.End);
                }
            }
        }

        public virtual void EndInjection(Injection injection = null) {
            if (injection == null) injection = this.injection;
            var behaviours = GetBehaviours(injection);
            if (behaviours.Count() == 0) {
                injection.EndTime = RTI.time;
                UpdateState(injection, Injection.Types.State.End);
            } else {
                foreach (var behaviour in behaviours) behaviour.End();
            }
        }

        public virtual void StopInjection(Injection injection = null) {
            if (injection == null) injection = this.injection;
            foreach (var behaviour in GetBehaviours(injection)) behaviour.Stop();
            injection.EndTime = RTI.time;
            UpdateState(injection, Injection.Types.State.Stopped);
        }

        public virtual void CancelInjection(Injection injection = null) {
            if (injection == null) injection = this.injection;
            foreach (var behaviour in GetBehaviours(injection)) behaviour.Cancel();
            injection.EndTime = RTI.time;
            UpdateState(injection, Injection.Types.State.Canceled);
        }

        public virtual void ScheduleInjection(double enableTime, Injection injection = null) {
            if (injection == null) injection = this.injection;
            injection.EnableTime = enableTime;
            foreach (var behaviour in GetBehaviours(injection)) behaviour.Schedule();
            Publish(injection);
        }

        public virtual void UpdateTitle(string title, Injection injection = null) {
            if (injection == null) injection = this.injection;
            injection.Title = title;
            Publish(injection);
        }

        protected void PublishClearInjections() {
            RTI.Publish(RTIChannel.InjectionOperation, new InjectionOperation {
                Clear = name
            });
        }

        protected void UpdateState(Injection injection, Injection.Types.State state) {
            injection.State = state;
            OnStateUpdated?.Invoke(state);
            Publish(injection);
        }

        public void Publish() {
            var injectable = new Injectable {
                Name = name,
                Description = description,
                Concurrent = concurrent,
                StartMode = startMode,
                EndMode = endMode
            };
            foreach (var parameter in parameters) injectable.Parameters.Add(parameter.ToProto());
            RTI.Publish(RTIChannel.Injectable, injectable);
        }

        public void PublishInjections() {
            foreach (var injection in injections) Publish(injection);
        }

        protected void Publish(Injection injection) {
            RTI.Publish(RTIChannel.Injection, injection);
        }

        public Injection GetInjection(string id) {
            return injections.FirstOrDefault(i => i.Id == id);
        }

        public RTIInjectionBehaviour[] GetBehaviours(Injection injection) {
            if (injectionBehaviours.ContainsKey(injection.Id)) {
                return injectionBehaviours[injection.Id];
            } else if (injectPrefab == null) {
                return behaviours;
            } else {
                return new RTIInjectionBehaviour[] { };
            }
        }

        public RTIParameter GetParameter(string name) {
            return parameters.Where(p => p.name == name).FirstOrDefault();
        }

        public string GetParameterDefaultValue(string name) {
            var parameter = GetParameter(name);
            if (parameter != null) return parameter.defaultValue;
            return null;
        }

    }
}
