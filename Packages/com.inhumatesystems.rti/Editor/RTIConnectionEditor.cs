using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
using Inhumate.RTI;
using NaughtyAttributes.Editor;

namespace Inhumate.Unity.RTI {

    [CustomEditor(typeof(RTIConnection))]
    public class RTIConnectionEditor : NaughtyInspector {

        bool showVersions;
        bool showCommands;

        public override void OnInspectorGUI() {
            var connection = (RTIConnection)target;
            if (connection.IsConnected && GUILayout.Button("Disconnect")) {
                connection.Disconnect();
            } else if (!connection.IsConnected && GUILayout.Button("Connect")) {
                connection.Connect();
            }
            if (connection.IsConnected) {
                EditorGUILayout.LabelField("State", connection.state.ToString());
                EditorGUILayout.TextField("Client ID", connection.ClientId);
                if (connection.IsTimeSyncMaster) {
                    EditorGUILayout.LabelField("Time sync master");
                } else if (connection.timeSyncMasterClientId != null) {
                    EditorGUILayout.TextField("Time sync master", connection.timeSyncMasterClientId);
                }
                if (connection.IsPersistentEntityOwner) {
                    EditorGUILayout.LabelField("Persistent entity owner");
                } else if (connection.persistentEntityOwnerClientId != null) {
                    EditorGUILayout.TextField("Persistent entity owner", connection.persistentEntityOwnerClientId);
                }
                if (connection.IsPersistentGeometryOwner) {
                    EditorGUILayout.LabelField("Persistent geometry owner");
                } else if (connection.persistentGeometryOwnerClientId != null) {
                    EditorGUILayout.TextField("Persistent geometry owner", connection.persistentGeometryOwnerClientId);
                }
                var numClients = connection.Client.KnownClients.Count(c => c.Application == connection.Client.Application);
                if (numClients > 1) EditorGUILayout.LabelField($"{numClients} {connection.Application} connected");
            }

            showVersions = EditorGUILayout.BeginFoldoutHeaderGroup(showVersions, "Versions");
            if (showVersions) {
                EditorGUILayout.LabelField("Integration", RTIConnection.IntegrationVersion);
                EditorGUILayout.LabelField("Client", RTIConstants.Version);
                if (connection.IsConnected && connection.Client != null && !string.IsNullOrEmpty(connection.Client.BrokerVersion)) {
                    EditorGUILayout.LabelField("Broker", connection.Client.BrokerVersion);
                }
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            showCommands = EditorGUILayout.BeginFoldoutHeaderGroup(showCommands, "Commands");
            if (showCommands) {
                foreach (var command in connection.Commands) {
                    if (command.Arguments.Count == 0 && GUILayout.Button(command.Name)) {
                        connection.ExecuteCommandInternal(command.Name);
                    } else if (command.Arguments.Count > 0) {
                        EditorGUILayout.LabelField(command.Name, $"{command.Arguments.Count} arguments");
                    }
                }
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            base.OnInspectorGUI();

            if (Application.isPlaying) {
                if (!connection.IsPersistentEntityOwner && GUILayout.Button("Claim Persistent Entity Ownership")) {
                    connection.ClaimPersistentEntityOwnership();
                }
                if (!connection.IsPersistentGeometryOwner && GUILayout.Button("Claim Persistent Geometry Ownership")) {
                    connection.ClaimPersistentGeometryOwnership();
                }
            }
        }
    }
}
