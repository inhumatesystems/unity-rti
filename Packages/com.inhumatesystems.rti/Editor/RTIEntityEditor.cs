using UnityEngine;
using UnityEditor;
using NaughtyAttributes.Editor;

namespace Inhumate.UnityRTI {

    [CustomEditor(typeof(RTIEntity))]
    public class RTIEntityEditor : NaughtyInspector {

        bool showCommands;

        public override void OnInspectorGUI() {
            var entity = (RTIEntity)target;
            if (Application.isPlaying) {
                EditorGUILayout.TextField("ID", entity.id);
                var flags = "";
                if (entity.persistent) flags += ", Persistent";
                if (entity.owned) flags += ", Owned";
                if (entity.published) flags += ", Published";
                if (entity.publishing) flags += ", Publishing";
                if (entity.receiving) flags += ", Receiving";
                if (entity.deleted) flags += ", Deleted";
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
            
            base.OnInspectorGUI();

            if (!Application.isPlaying) {
                if (GUILayout.Button("Set dimensons from renderers")) {
                    var bounds = entity.GetBoundsFromRenderers();
                    entity.size = new Vector3(bounds.size.x, bounds.size.y, bounds.size.z);
                    entity.center = new Vector3(bounds.center.x, bounds.center.y, bounds.center.z);
                }
                if (GUILayout.Button("Set dimensions from colliders")) {
                    var bounds = entity.GetBoundsFromColliders();
                    entity.size = new Vector3(bounds.size.x, bounds.size.y, bounds.size.z);
                    entity.center = new Vector3(bounds.center.x, bounds.center.y, bounds.center.z);
                }
            } else {
                if (!entity.owned) {
                    if (GUILayout.Button("Assume Ownership")) {
                        entity.AssumeOwnership();
                    }
                }
            }
        }
    }
}
