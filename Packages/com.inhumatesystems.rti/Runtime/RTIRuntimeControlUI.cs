using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Inhumate.RTI.Proto;
using UnityEngine.SceneManagement;

namespace Inhumate.Unity.RTI {
    public class RTIRuntimeControlUI : MonoBehaviour {

        public bool controlsEditorOnly = true;
        public bool errorEditorOnly;

        public GameObject controlsPanel;
        public Button resetButton;
        public Dropdown scenarioDropdown;
        public Button loadButton;
        public Button startButton;
        public Button playButton;
        public Button pauseButton;
        public Button stopButton;
        public Text stateText;
        public Text timeText;
        public Dropdown timeScaleDropdown;
        public GameObject connectionErrorPanel;
        public Text connectionErrorText;

        protected static RTIConnection RTI => RTIConnection.Instance;

        void Awake() {
            bool enabledFromCommandLine = false;
            bool disabledFromCommandLine = false;
            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++) {
                if (args[i] == "--rti-ui") enabledFromCommandLine = true;
                if (args[i] == "--no-rti-ui" || args[i] == "--no-rti") disabledFromCommandLine = true;
            }

            if (disabledFromCommandLine || (controlsEditorOnly && !Application.isEditor && !enabledFromCommandLine)) {
                controlsPanel.SetActive(false);
                connectionErrorPanel.GetComponent<RectTransform>().anchoredPosition = Vector2.zero;
            }
        }

        void Start() {
            if (scenarioDropdown != null) {
                scenarioDropdown.ClearOptions();
                var names = new List<string>();
                if (RTI.scenarios.Count > 0) {
                    foreach (var scenario in RTI.scenarios) {
                        if (scenario != null && !string.IsNullOrWhiteSpace(scenario.name)) names.Add(scenario.name);
                    }
                }
                foreach (var name in RTI.scenarioNames) if (!names.Contains(name)) names.Add(name);
                if (names.Count == 0) {
                    // Fallback to just listing all levels in build settings
                    for (int i = 1; i < SceneManager.sceneCountInBuildSettings; i++) {
                        var scenePath = SceneUtility.GetScenePathByBuildIndex(i);
                        if (scenePath.Contains("Additive")) continue;
                        var lastSlash = scenePath.LastIndexOf("/");
                        var sceneName = scenePath.Substring(lastSlash + 1, scenePath.LastIndexOf(".") - lastSlash - 1);
                        names.Add(sceneName);
                    }
                }
                scenarioDropdown.AddOptions(names);
                if (RTI.scenario != null) {
                    var index = names.IndexOf(RTI.scenario.name);
                    if (index >= 0) scenarioDropdown.value = index;
                } else if (RTI.lastScenarioName != null) {
                    var index = names.IndexOf(RTI.lastScenarioName);
                    if (index >= 0) scenarioDropdown.value = index;
                }
            }
            if (resetButton != null) resetButton.onClick.AddListener(RTIRuntimeControl.PublishReset);
            if (loadButton != null) loadButton.onClick.AddListener(PublishLoadScenario);
            if (startButton != null) startButton.onClick.AddListener(RTIRuntimeControl.PublishStart);
            if (playButton != null) playButton.onClick.AddListener(RTIRuntimeControl.PublishPlay);
            if (pauseButton != null) pauseButton.onClick.AddListener(RTIRuntimeControl.PublishPause);
            if (stopButton != null) stopButton.onClick.AddListener(RTIRuntimeControl.PublishStop);
            if (timeScaleDropdown != null) timeScaleDropdown.onValueChanged.AddListener(TimeScaleDropdownChanged);
            RTI.OnStateChanged += StateChanged;
            StateChanged(RTI.state);
            RTI.OnTimeScaleChanged += TimeScaleChanged;
            TimeScaleChanged(RTI.timeScale);
            RTI.OnConnected += ConnectionChanged;
            RTI.OnDisconnected += ConnectionChanged;
            ConnectionChanged();
            StartCoroutine(DelayedConnectionCheck());
        }

        void OnDestroy() {
            RTI.OnStateChanged -= StateChanged;
            RTI.OnTimeScaleChanged -= TimeScaleChanged;
            RTI.OnConnected -= ConnectionChanged;
            RTI.OnDisconnected -= ConnectionChanged;
        }

        private float lastUpdatedTime = float.NegativeInfinity;
        void Update() {
            if (timeText != null && Mathf.Abs(RTI.time - lastUpdatedTime) > 0.05f) {
                timeText.text = FormatTime(RTI.time);
                lastUpdatedTime = RTI.time;
            }
        }

        public static string FormatTime(float rtiTime) {
            var time = System.TimeSpan.FromSeconds(rtiTime);
            return Mathf.Abs(rtiTime) < 1e-5f
                    ? "--:--"
                    : rtiTime > 3600
                    ? $"{time:hh\\:mm\\:ss}"
                    : RTI.timeScale < 0.99f
                    ? $"{time:mm\\:ss\\.ff}"
                    : $"{time:mm\\:ss}";
        }

        IEnumerator DelayedConnectionCheck() {
            yield return new WaitForSecondsRealtime(0.5f);
            ConnectionChanged();
        }

        void ConnectionChanged() {
            if (scenarioDropdown != null) scenarioDropdown.gameObject.SetActive(scenarioDropdown.options.Count > 0);
            if (loadButton != null) loadButton.gameObject.SetActive(scenarioDropdown != null && scenarioDropdown.gameObject.activeSelf);
            if (connectionErrorPanel != null) connectionErrorPanel.SetActive(!RTI.IsConnected && Time.unscaledTime >= 0.5f);
            if (connectionErrorText != null) {
                connectionErrorText.enabled = !RTI.IsConnected && Time.unscaledTime >= 0.5f;
                if (connectionErrorText.enabled) {
                    connectionErrorText.text = RTI.WasEverConnected ? "<b>DISCONNECTED</b>" : "<b>NOT CONNECTED</b>";
                    connectionErrorText.text += $" - {RTI.Client.Url}";
                    if (RTI.LastErrorChannel == "connection" && RTI.LastError != null) {
                        connectionErrorText.text += $" - {RTI.LastError.Message}";
                    }
                }
            }
            if (stateText != null) stateText.enabled = RTI.state != RuntimeState.Unknown && RTI.state != RuntimeState.Initial;
            if (timeText != null) timeText.enabled = RTI.time > 1e-5f;
        }

        void StateChanged(RuntimeState state) {
            if (stateText != null) stateText.text = state.ToString();
            if (resetButton != null) resetButton.interactable = RTI.state != RuntimeState.Running && RTI.state != RuntimeState.Playback;
            if (scenarioDropdown != null) {
                scenarioDropdown.interactable = RTI.state != RuntimeState.Loading && RTI.state != RuntimeState.Running && RTI.state != RuntimeState.Paused && RTI.state != RuntimeState.Playback && RTI.state != RuntimeState.PlaybackPaused;
                scenarioDropdown.gameObject.SetActive(scenarioDropdown.options.Count > 0);
            }
            if (loadButton != null) {
                loadButton.interactable = scenarioDropdown != null && scenarioDropdown.interactable;
                loadButton.gameObject.SetActive(scenarioDropdown != null && scenarioDropdown.gameObject.activeSelf);
            }
            if (startButton != null) startButton.interactable = RTI.state == RuntimeState.Ready || RTI.state == RuntimeState.Paused || RTI.state == RuntimeState.PlaybackPaused || RTI.state == RuntimeState.Unknown || RTI.state == RuntimeState.Stopped || RTI.state == RuntimeState.PlaybackStopped || RTI.state == RuntimeState.End || RTI.state == RuntimeState.PlaybackEnd;
            if (playButton != null) playButton.interactable = RTI.state != RuntimeState.Playback && RTI.state != RuntimeState.Running && RTI.state != RuntimeState.Paused;
            if (pauseButton != null) pauseButton.interactable = RTI.state == RuntimeState.Running || RTI.state == RuntimeState.Playback || RTI.state == RuntimeState.Unknown;
            if (stopButton != null) stopButton.interactable = RTI.state != RuntimeState.Stopped && RTI.state != RuntimeState.PlaybackStopped && RTI.state != RuntimeState.Initial && RTI.state != RuntimeState.Ready;
        }

        void TimeScaleChanged(float timeScale) {
            foreach (var option in timeScaleDropdown.options) {
                if (float.TryParse(option.text.Replace("x", ""), out float optionTimeScale)) {
                    if (Mathf.Abs(timeScale - optionTimeScale) < 1e-5f) {
                        timeScaleDropdown.value = timeScaleDropdown.options.IndexOf(option);
                        return;
                    }
                }
            }
            Debug.LogWarning($"Weird time scale {timeScale}");
            timeScaleDropdown.options.Add(new Dropdown.OptionData {
                text = $"{timeScale}x"
            });
            timeScaleDropdown.value = timeScaleDropdown.options.Count - 1;
        }

        void TimeScaleDropdownChanged(int index) {
            if (float.TryParse(timeScaleDropdown.options[index].text.Replace("x", ""), out float newTimeScale)) {
                if (newTimeScale != RTI.timeScale) {
                    RTIRuntimeControl.PublishTimeScale(newTimeScale);
                }
            }
        }

        public void PublishLoadScenario() {
            RTIRuntimeControl.PublishLoadScenario(scenarioDropdown.options[scenarioDropdown.value].text);
        }
    }
}
