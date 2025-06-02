using System;
using System.Collections;
using System.Collections.Generic;
using Inhumate.RTI.Proto;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Inhumate.UnityRTI {
    public class RTIRuntimeControlBackupUI : MonoBehaviour {

        public bool editorOnly = true;
        private bool hidden;
        private bool enabledFromCommandLine;
        private bool disabledFromCommandLine;

        public GUIStyle buttonStyle;
        public GUIStyle labelStyle;

        protected static RTIConnection RTI => RTIConnection.Instance;

        void Awake() {
            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++) {
                if (args[i] == "--rti-ui") enabledFromCommandLine = true;
                if (args[i] == "--no-rti-ui" || args[i] == "--no-rti") disabledFromCommandLine = true;
            }

            if (_instance != null) {
                if (SceneManager.GetActiveScene().buildIndex != 0) {
                    Debug.LogWarning("An instance of RTIRuntimeControlBackupUI already exists - it should not be added to scenario scenes - destroying this one", this);
                }
                Destroy(this);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(_instance.gameObject);
        }

        void Start() {
            CheckVisibility();
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode) {
            CheckVisibility();
        }

        void CheckVisibility() {
            var ui = FindObjectOfType<RTIRuntimeControlUI>();
            var show = ui == null && !disabledFromCommandLine && (!editorOnly || Application.isEditor || enabledFromCommandLine);
            if (!hidden && !show) {
                hidden = true;
            } else if (show && hidden) {
                hidden = false;
            }
        }

        void OnGUI() {
            if (hidden) return;
            GUI.skin.button = buttonStyle;
            GUI.skin.label = labelStyle;
            GUI.enabled = RTI.state != RuntimeState.Running && RTI.state != RuntimeState.Playback;
            if (GUI.Button(new Rect(10, 10, 50, 20), "Reset")) {
                RTIRuntimeControl.PublishReset();
            }
            GUI.enabled = RTI.state == RuntimeState.Ready || RTI.state == RuntimeState.Paused || RTI.state == RuntimeState.PlaybackPaused || RTI.state == RuntimeState.Unknown || RTI.state == RuntimeState.Stopped || RTI.state == RuntimeState.PlaybackStopped || RTI.state == RuntimeState.End || RTI.state == RuntimeState.PlaybackEnd;
            if (GUI.Button(new Rect(70, 10, 50, 20), "Start")) {
                RTIRuntimeControl.PublishStart();
            }
            GUI.enabled = RTI.state != RuntimeState.Playback && RTI.state != RuntimeState.Running && RTI.state != RuntimeState.Paused;
            if (GUI.Button(new Rect(130, 10, 50, 20), "Play")) {
                RTIRuntimeControl.PublishPlay();
            }
            GUI.enabled = RTI.state == RuntimeState.Running || RTI.state == RuntimeState.Playback || RTI.state == RuntimeState.Unknown;
            if (GUI.Button(new Rect(190, 10, 50, 20), "Pause")) {
                RTIRuntimeControl.PublishPause();
            }
            GUI.enabled = RTI.state != RuntimeState.Stopped && RTI.state != RuntimeState.PlaybackStopped && RTI.state != RuntimeState.Initial && RTI.state != RuntimeState.Ready;
            if (GUI.Button(new Rect(250, 10, 50, 20), "Stop")) {
                RTIRuntimeControl.PublishStop();
            }

            GUI.enabled = true;

            if (!RTI.IsConnected && Time.unscaledTime > 0.5f) {
                GUI.Label(new Rect(320, 10, 100, 20), "NOT CONNECTED");
            }
            if (GUI.Button(new Rect(Screen.width - 40, 10, 30, 20), "1x")) {
                RTIRuntimeControl.PublishTimeScale(1);
            }

            GUI.Label(new Rect(Screen.width - 210, 10, 110, 20), $"{RTI.state}");
            GUI.Label(new Rect(Screen.width - 100, 10, 60, 20), $"{RTIRuntimeControlUI.FormatTime(RTI.time)}");

        }

        static RTIRuntimeControlBackupUI _instance;
    }
}
