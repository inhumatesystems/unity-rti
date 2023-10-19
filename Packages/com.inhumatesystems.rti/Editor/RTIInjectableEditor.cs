using UnityEngine;
using UnityEditor;
using Inhumate.RTI.Proto;

namespace Inhumate.Unity.RTI {

    [CustomEditor(typeof(RTIInjectable))]
    public class RTIInjectableEditor : Editor {

        public override void OnInspectorGUI() {
            var injectable = (RTIInjectable)target;
            if (Application.isPlaying) {
                if (injectable.injection == null || injectable.concurrent) {
                    if (GUILayout.Button("Inject")) {
                        injectable.Inject(new InjectionOperation.Types.Inject { Disabled = true });
                    }
                }
                if (injectable.injection != null) {
                    var injection = injectable.injection;
                    EditorGUILayout.LabelField("State", $"{injection.State}");
                    if (injection.State == Injection.Types.State.Disabled
                            && injectable.startMode != Injectable.Types.ControlMode.Immediate
                            && GUILayout.Button("Enable")) {
                        injectable.EnableInjection(injection);
                    }
                    if (injection.State == Injection.Types.State.Enabled && GUILayout.Button("Disable")) {
                        injectable.DisableInjection(injection);
                    }
                    if (((injection.State == Injection.Types.State.Enabled
                            && (injectable.startMode == Injectable.Types.ControlMode.Manual || injectable.startMode == Injectable.Types.ControlMode.AutoOrManual))
                            || (injection.State == Injection.Types.State.Disabled
                            && injectable.startMode == Injectable.Types.ControlMode.Immediate)
                        ) && GUILayout.Button(
                            injectable.endMode == Injectable.Types.ControlMode.Immediate ? "Go" : "Start"
                            )) {
                        injectable.StartInjection(injection);
                    }
                    if (injection.State < Injection.Types.State.End && GUILayout.Button("Cancel")) {
                        injectable.CancelInjection(injection);
                    }
                    if (!injectable.concurrent && injection.State >= Injection.Types.State.End && GUILayout.Button("Reset")) {
                        injectable.Inject(new InjectionOperation.Types.Inject { Disabled = true });
                    }
                }
            }
            this.DrawDefaultInspector();
        }
    }
}
