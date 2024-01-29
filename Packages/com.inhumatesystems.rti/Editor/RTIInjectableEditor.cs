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
                    var injectButtonTitle = 
                        injectable.startMode == Injectable.Types.ControlMode.Immediate && injectable.endMode == Injectable.Types.ControlMode.Immediate ? "Inject & Go"
                        : injectable.startMode == Injectable.Types.ControlMode.Immediate ? "Inject & Start"
                        : "Inject";
                    if (GUILayout.Button(injectButtonTitle)) {
                        injectable.Inject(new InjectionOperation.Types.Inject { });
                    }
                }
                if (injectable.injection != null) {
                    injectable.OnStateUpdated += (state) => Repaint();
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
                        ) && GUILayout.Button( "Start")) {
                        injectable.StartInjection(injection);
                    }
                    if (injection.State == Injection.Types.State.Running 
                            && (injectable.endMode == Injectable.Types.ControlMode.Manual || injectable.endMode == Injectable.Types.ControlMode.AutoOrManual) 
                            && GUILayout.Button("Stop")) {
                        injectable.StopInjection(injection);
                    }
                    if (injection.State < Injection.Types.State.End
                            && (injectable.endMode == Injectable.Types.ControlMode.Manual || injectable.endMode == Injectable.Types.ControlMode.AutoOrManual || injection.State < Injection.Types.State.Running)
                            && GUILayout.Button("Cancel")) {
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
