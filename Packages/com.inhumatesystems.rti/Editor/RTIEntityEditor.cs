using UnityEngine;
using UnityEditor;

namespace Inhumate.Unity.RTI {

    [CustomEditor(typeof(RTIEntity))]
    public class RTIEntityEditor : Editor {

        bool showCommands;

        public override void OnInspectorGUI() {
            var entity = (RTIEntity)target;
            if (Application.isPlaying) {
                EditorGUILayout.TextField("ID", entity.id);
                var flags = "";
                if (entity.persistent) flags += ", Persistent";
                if (entity.owned) flags += ", Owned";
                if (entity.created) flags += ", Created";
                if (entity.publishing) flags += ", Publishing";
                if (entity.receiving) flags += ", Receiving";
                if (flags.Length > 0) {
                    EditorGUILayout.LabelField(flags.Substring(2).Trim());
                }
                if (entity.ownerClientId != RTIConnection.Instance.ClientId) {
                    if (entity.OwnerClient != null) {
                        EditorGUILayout.LabelField("Owner Application", entity.OwnerClient.Application);
                    }
                    EditorGUILayout.TextField("Owner Client ID", entity.ownerClientId);
                }

                showCommands = EditorGUILayout.BeginFoldoutHeaderGroup(showCommands, "Commands");
                if (showCommands) {
                    foreach (var command in entity.Commands) {
                        if (command.Arguments.Count == 0 && GUILayout.Button(command.Name)) {
                            entity.ExecuteCommandInternal(command.Name);
                        } else if (command.Arguments.Count > 0) {
                            EditorGUILayout.LabelField(command.Name, $"{command.Arguments.Count} arguments");
                        }
                    }
                }
                EditorGUILayout.EndFoldoutHeaderGroup();

            }
            this.DrawDefaultInspector();
        }
    }
}
