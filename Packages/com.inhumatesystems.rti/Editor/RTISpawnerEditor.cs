using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace Inhumate.Unity.RTI {

    [CustomEditor(typeof(RTISpawner))]
    public class RTISpawnerEditor : Editor {
        public override void OnInspectorGUI() {
            RTISpawner spawner = (RTISpawner)target;
            GUILayout.Label("Spawnable Entities");
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Type", new GUILayoutOption[] { GUILayout.Width(100) });
            GUILayout.Label("Prefab");
            EditorGUILayout.EndHorizontal();
            var toRemove = new List<RTISpawner.Spawnable>();
            foreach (var entry in spawner.spawnableEntities) {
                EditorGUILayout.BeginHorizontal();
                entry.type = GUILayout.TextField(entry.type, new GUILayoutOption[] { GUILayout.Width(100) });
                var obj = EditorGUILayout.ObjectField(entry.prefab?.gameObject, typeof(GameObject), false) as GameObject;
                entry.prefab = obj ? obj.GetComponent<RTIEntity>() : null;
                if (GUILayout.Button("X", new GUILayoutOption[] { GUILayout.Width(25) })) {
                    toRemove.Add(entry);
                }
                EditorGUILayout.EndHorizontal();
            }
            foreach (var spawnable in toRemove) spawner.spawnableEntities.Remove(spawnable);

            Rect dropArea = GUILayoutUtility.GetRect(0.0f, 25.0f, GUILayout.ExpandWidth(true));
            if (GUI.Button(dropArea, "Add (drag prefab here)")) {
                spawner.spawnableEntities.Add(new RTISpawner.Spawnable());
            }

            switch (Event.current.type) {
                case EventType.DragUpdated:
                case EventType.DragPerform:
                    if (!dropArea.Contains(Event.current.mousePosition))
                        return;

                    DragAndDrop.visualMode = DragAndDropVisualMode.Copy;

                    if (Event.current.type == EventType.DragPerform) {
                        DragAndDrop.AcceptDrag();

                        foreach (Object obj in DragAndDrop.objectReferences) {
                            var go = obj as GameObject;
                            if (go != null && go.GetComponent<RTIEntity>() != null) {
                                spawner.spawnableEntities.Add(new RTISpawner.Spawnable {
                                    type = go.GetComponent<RTIEntity>().type,
                                    prefab = go.GetComponent<RTIEntity>()
                                });
                                DragAndDrop.AcceptDrag();
                            }
                        }
                    }
                    break;
            }

            this.DrawDefaultInspector();
        }

    }
}
