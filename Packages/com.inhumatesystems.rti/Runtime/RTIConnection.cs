using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Inhumate.RTI;
using Inhumate.RTI.Proto;
using Google.Protobuf;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Reflection;

namespace Inhumate.Unity.RTI {

    public class RTIConnection : MonoBehaviour {

        public const string IntegrationVersion = "0.0.1-dev-version";

        public bool autoConnect = true;

        public bool polling = true;

        [Tooltip("URL of RTI broker to connect to. Leave blank for default. May be overridden by INHUMATE_RTI_URL environment variable or command-line.")]
        public string url;

        [Tooltip("Secret to use when connecting. Leave blank for default. May be overridden by INHUMATE_RTI_SECRET environment variable or command-line.")]
        public string secret;

        public List<RTIScenario> scenarios = new List<RTIScenario>();
        public List<string> scenarioNames { get; set; } = new List<string>();
        public RTIScenario scenario { get; private set; }
        private Dictionary<string, string> scenarioParameterValues = new Dictionary<string, string>();
        public string lastScenarioName { get; private set; }

        [Tooltip("Automatically load scenario and start if other clients are running")]
        public bool lateJoin;

        public bool debugConnection;
        public bool debugChannels;
        public bool debugRuntimeControl;
        public bool debugEntities;

        public int maxPollCount = 100;

        public string timeSyncMasterClientId { get; private set; }
        private float lastTimeSyncTime;
        public bool IsTimeSyncMaster => rti != null && timeSyncMasterClientId == rti.ClientId;
        private bool inhibitTimeSyncMaster;

        private static int mainThreadId;
        public static RTIConnection Instance {
            get {
                if (_instance == null && !_quitting) {
                    _instance = new GameObject("RTI Connection").AddComponent<RTIConnection>();
                    mainThreadId = Thread.CurrentThread.ManagedThreadId;
                }
                return _instance;
            }
        }

        public RTIClient Client => rti;
        private RTIClient rti;
        public string Application => Client.Application;
        public string ClientId => Client.ClientId;

        public bool IsConnected => connected;
        private bool connected;
        public bool WasEverConnected => everConnected;
        private bool everConnected;
        public bool quitting { get; private set; }
        private static bool _quitting;
        public float time { get; private set; }
        public float timeScale { get; private set; } = 1;
        public int pollCount { get; private set; }
        public delegate void TimeScaleChanged(float timeScale);
        public event TimeScaleChanged OnTimeScaleChanged;
        public RuntimeState state {
            get { return rti != null ? rti.State : RuntimeState.Unknown; }
            set { if (rti != null) rti.State = value; }
        }

        public event Action OnStart;
        public event Action OnStop;
        public event Action CustomStop;
        public event Action OnReset;
        public event Action CustomReset;
        public event Action<RuntimeState> OnStateChanged;
        public event Action<RuntimeControl.Types.LoadScenario> OnLoadScenario;
        public event Action<RuntimeControl.Types.LoadScenario> CustomLoadScenario;
        public event Action OnConnected;
        public event Action OnConnectedOnce;
        public event Action OnDisconnected;
        public event Action<RTIEntity> OnEntityOwnershipReleased;
        public event Action<RTIEntity> OnEntityOwnershipAssumed;

        private Dictionary<string, RTIEntity> entities = new Dictionary<string, RTIEntity>();
        public IEnumerable<RTIEntity> Entities => entities.Values;
        private Dictionary<string, RTIGeometry> geometries = new Dictionary<string, RTIGeometry>();
        private Dictionary<string, RTIInjectable> injectables = new Dictionary<string, RTIInjectable>();

        public Command[] Commands => commands.Values.ToArray();
        private Dictionary<string, Command> commands = new Dictionary<string, Command>();
        public delegate CommandResponse CommandHandler(Command command, ExecuteCommand exec);
        private Dictionary<string, CommandHandler> commandHandlers = new Dictionary<string, CommandHandler>();
        private Dictionary<string, string> commandTransactionChannel = new Dictionary<string, string>();

        private bool inhibitConnect;

        public string persistentEntityOwnerClientId { get; private set; }
        public bool IsPersistentEntityOwner => rti != null && persistentEntityOwnerClientId == rti.ClientId;
        public string persistentGeometryOwnerClientId { get; private set; }
        public bool IsPersistentGeometryOwner => rti != null && persistentGeometryOwnerClientId == rti.ClientId;

        public string LastErrorChannel { get; private set; }
        public Exception LastError { get; private set; }

        private RuntimeControl.Types.LoadScenario receivedCurrentScenario;

        public void WhenConnected(Action handler) {
            if (IsConnected) handler?.Invoke();
            else OnConnected += handler;
        }

        public void WhenConnectedOnce(Action handler) {
            if (IsConnected) handler?.Invoke();
            else OnConnectedOnce += handler;
        }

        public void Publish(string channelName, string message) {
            //if (quitting) return;
            if (connected) rti.Publish(channelName, message);
        }

        public void Publish<T>(string channelName, T message) where T : IMessage<T>, new() {
            //if (quitting) return;
            if (connected) rti.Publish(channelName, message);
        }

        public void PublishJson(string channelName, object message) {
            //if (quitting) return;
            if (connected) rti.PublishJson(channelName, message);
        }

        public UntypedListener Subscribe<T>(string channelName, TypedListener<T> callback) where T : IMessage<T>, new() {
            if (quitting) return null;
            return rti.Subscribe<T>(channelName, (name, data) => {
                RunOrQueue(() => { callback(name, data); });
            });
        }

        public UntypedListener Subscribe<T>(string channelName, TypedIdListener<T> callback) where T : IMessage<T>, new() {
            if (quitting) return null;
            return rti.Subscribe<T>(channelName, (name, id, data) => {
                RunOrQueue(() => { callback(name, id, data); });
            });
        }

        public UntypedListener Subscribe(string channelName, UntypedListener callback) {
            if (quitting) return null;
            return rti.Subscribe(channelName, (name, data) => {
                RunOrQueue(() => { callback(name, data); });
            });
        }

        public UntypedListener SubscribeJson<T>(string channelName, TypedListener<T> callback) {
            if (quitting) return null;
            return rti.SubscribeJson<T>(channelName, (name, data) => {
                RunOrQueue(() => { callback(name, data); });
            });
        }

        public void Unsubscribe(UntypedListener listener) {
            if (quitting) return;
            rti.Unsubscribe(listener);
        }

        public void Unsubscribe(string channelName) {
            if (quitting) return;
            if (debugChannels) Debug.Log($"RTI unsubscribe {channelName}");
            rti.Unsubscribe(channelName);
        }

        public void Awake() {
            quitting = false;
            if (_instance != null) {
                if (SceneManager.GetActiveScene().buildIndex != 0) {
                    Debug.LogWarning("An instance of RTIConnection already exists - it should not be added to scenario scenes - destroying this one", this);
                } else {
                    //Debug.LogWarning($"Multiple instances of RTIConnection - destroying this one", this);
                    if (scenarios.Count > 0 && _instance.scenarios.Count == 0) _instance.scenarios = scenarios;
                    if (scenarioNames.Count > 0 && _instance.scenarioNames.Count == 0) _instance.scenarioNames = scenarioNames;
                }
                Destroy(this);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(_instance.gameObject);
            Initialize();
        }

        public void OnApplicationQuit() {
            quitting = true;
            _quitting = true;
        }

        public void OnDestroy() {
            if (rti != null) {
                rti.OnConnected -= OnRtiConnected;
                rti.OnDisconnected -= OnRtiDisconnected;
                rti.OnError -= OnRtiError;
            }
            Disconnect();
        }

        private void Initialize() {
            string clientId = null;

            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++) {
                if (args[i] == "--rti" && i < args.Length - 2) {
                    url = args[i + 1];
                } else if (args[i] == "--rti-client-id" && i < args.Length - 2) {
                    clientId = args[i + 1];
                } else if (args[i] == "--rti-secret" && i < args.Length - 2) {
                    secret = args[i + 1];
                } else if (args[i] == "--no-rti") {
                    inhibitConnect = true;
                } else if (args[i] == "--rti-no-time-sync-master") {
                    inhibitTimeSyncMaster = true;
                } else if (args[i] == "--rti-debug") {
                    debugChannels = true;
                    debugConnection = true;
                    debugEntities = true;
                    debugRuntimeControl = true;
                } else if (args[i] == "--rti-debug-channels") {
                    debugChannels = true;
                } else if (args[i] == "--rti-debug-connection") {
                    debugConnection = true;
                } else if (args[i] == "--rti-debug-entities") {
                    debugEntities = true;
                } else if (args[i] == "--rti-debug-runtime-control") {
                    debugRuntimeControl = true;
                }
            }

            try {
                var frameworkVer = System.Environment.Version;
                var runtimeVer = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription;
                rti = new RTIClient(url, false) {
                    Application = UnityEngine.Application.productName,
                    ApplicationVersion = UnityEngine.Application.version,
                    IntegrationVersion = IntegrationVersion,
                    EngineVersion = $"Unity {UnityEngine.Application.unityVersion} .NET {frameworkVer} {runtimeVer}",
                    Capabilities = {
                        RTICapability.RuntimeControl,
                        RTICapability.Scenario,
                        RTICapability.TimeScale
                    }
                };
                if (!string.IsNullOrWhiteSpace(clientId)) rti.ClientId = clientId;
                if (!string.IsNullOrWhiteSpace(secret)) rti.Secret = secret;
                Subscribe<RuntimeControl>(RTIChannel.Control, OnRuntimeControl);
                Subscribe<RuntimeControl>(rti.OwnChannelPrefix + RTIChannel.Control, OnRuntimeControl);
                Subscribe<Scenarios>(RTIChannel.Scenarios, OnScenarios);
                rti.RegisterChannel(new Channel {
                    Name = RTIChannel.Entity,
                    DataType = typeof(Entity).Name,
                    State = true,
                    FirstFieldId = true
                });
                Subscribe<Entity>(RTIChannel.Entity, OnEntity);
                rti.RegisterChannel(new Channel {
                    Name = RTIChannel.EntityOperation,
                    DataType = typeof(EntityOperation).Name,
                    Ephemeral = true,
                });
                Subscribe<EntityOperation>(RTIChannel.EntityOperation, OnEntityOperation);
                rti.RegisterChannel(new Channel {
                    Name = RTIChannel.Geometry,
                    DataType = typeof(Geometry).Name,
                    State = true,
                    FirstFieldId = true
                });
                rti.RegisterChannel(new Channel {
                    Name = RTIChannel.GeometryOperation,
                    DataType = typeof(GeometryOperation).Name,
                    Ephemeral = true,
                });
                Subscribe<GeometryOperation>(RTIChannel.GeometryOperation, OnGeometryOperation);
                Subscribe<InjectableOperation>(RTIChannel.InjectableOperation, OnInjectableOperation);
                Subscribe<InjectionOperation>(RTIChannel.InjectionOperation, OnInjectionOperation);
                rti.RegisterChannel(new Channel {
                    Name = RTIChannel.Injection,
                    DataType = typeof(Injection).Name,
                    State = true,
                    FirstFieldId = true
                });
                Subscribe<Commands>(RTIChannel.Commands, OnCommands);
                Subscribe<Commands>(rti.OwnChannelPrefix + RTIChannel.Commands, OnCommands);
                rti.RegisterChannel(new Channel {
                    Name = RTIChannel.Commands,
                    DataType = typeof(Commands).Name,
                    Ephemeral = true
                });
                Subscribe(RTIChannel.ClientDisconnect, OnClientDisconnect);
                rti.OnConnected += OnRtiConnected;
                rti.OnDisconnected += OnRtiDisconnected;
                rti.OnError += OnRtiError;
                if (SceneManager.sceneCountInBuildSettings > 1 && SceneManager.GetActiveScene().buildIndex == 0) rti.State = RuntimeState.Initial;
                if (autoConnect) Connect();
            } catch (AggregateException ex) {
                if (ex.InnerExceptions.Count == 1) {
                    Debug.LogError($"RTI connection failed: {ex.InnerException.Message}");
                } else {
                    Debug.LogError($"RTI connection failed: {ex.Message}");
                }
            } catch (Exception ex) {
                Debug.LogError($"RTI initialization failed: {ex.Message}");
            }
        }

        private void OnRtiConnected() {
            connected = everConnected = true;
            if (debugConnection) Debug.Log("RTI connected");
            if (persistentEntityOwnerClientId == null) QueryPersistentEntityOwner();
            if (persistentGeometryOwnerClientId == null) QueryPersistenGeometryOwner();
            if (lateJoin && state == RuntimeState.Initial) {
                Publish(RTIChannel.Clients, new Clients { RequestClients = new Google.Protobuf.WellKnownTypes.Empty() });
                Publish(RTIChannel.Control, new RuntimeControl { RequestCurrentScenario = new Google.Protobuf.WellKnownTypes.Empty() });
            }
            RunOrQueue(() => {
                url = rti.Url;
                OnConnected?.Invoke();
                OnConnectedOnce?.Invoke();
                OnConnectedOnce = null;
            });
        }

        public void ClaimPersistentEntityOwnership() {
            persistentEntityOwnerClientId = rti.ClientId;
            PublishClaimPersistentEntityOwnership();
        }

        public void ClaimPersistentGeometryOwnership() {
            persistentGeometryOwnerClientId = rti.ClientId;
            PublishClaimPersistentGeometryOwnership();
        }

        private void QueryPersistentEntityOwner() {
            Publish(RTIChannel.EntityOperation, new EntityOperation {
                RequestPersistentOwnership = new EntityOperation.Types.ApplicationClient {
                    Application = rti.Application,
                    ClientId = rti.ClientId
                }
            });
            RunOrQueue(() => { StartCoroutine(RandomDelayClaimPersistentEntityOwnership()); });
        }

        private void QueryPersistenGeometryOwner() {
            Publish(RTIChannel.Geometry, new GeometryOperation {
                RequestPersistentOwnership = new GeometryOperation.Types.ApplicationClient {
                    Application = rti.Application,
                    ClientId = rti.ClientId
                }
            });
            RunOrQueue(() => { StartCoroutine(RandomDelayClaimPersistentGeometryOwnership()); });
        }

        private void OnRtiDisconnected() {
            if (debugConnection) Debug.Log($"RTI disconnected");
            connected = false;
            RunOrQueue(() => { OnDisconnected?.Invoke(); });
        }

        private bool warnedConnection = false;
        private void OnRtiError(string channel, Exception ex) {
            if (!quitting && (channel != "connection" || everConnected || !warnedConnection)) {
                Debug.LogWarning($"RTI error {channel} {ex}");
                if (channel == "connection") warnedConnection = true;
            }
            LastErrorChannel = channel;
            LastError = ex;
        }

        public void Connect() {
            if (inhibitConnect) {
                if (debugConnection) Debug.Log("RTI connection inhibited");
            } else {
                if (rti == null) Initialize();
                if (debugConnection) Debug.Log($"RTI connecting {rti.Application} {rti.ClientId} to {rti.Url}");
                rti.Connect();
                rti.Polling = polling;
            }
        }

        public void OnRuntimeControl(string channelName, RuntimeControl message) {
            switch (message.ControlCase) {
                case RuntimeControl.ControlOneofCase.LoadScenario:
                    scenarioParameterValues.Clear();
                    foreach (var pair in message.LoadScenario.ParameterValues) {
                        scenarioParameterValues[pair.Key] = pair.Value;
                    }
                    OnLoadScenario?.Invoke(message.LoadScenario);
                    if (CustomLoadScenario != null) {
                        CustomLoadScenario(message.LoadScenario);
                    } else {
                        var sceneName = message.LoadScenario.Name;
                        RTIScenario scenarioToLoad = null;
                        foreach (var scenario in scenarios) {
                            if (scenario != null && scenario.name == message.LoadScenario.Name) {
                                sceneName = scenario.sceneName;
                                scenarioToLoad = scenario;
                                break;
                            }
                        }
                        if (scenarioToLoad != null && scenario == scenarioToLoad && rti.State == RuntimeState.Playback) {
                            if (debugRuntimeControl) Debug.Log($"Scenario already loaded for playback: {scenarioToLoad.name}");
                        } else if (SceneUtility.GetBuildIndexByScenePath(sceneName) >= 0) {
                            var scenarioToLoadName = scenarioToLoad != null ? scenarioToLoad.name : "?";
                            if (debugRuntimeControl) Debug.Log($"Load scenario {scenarioToLoadName} scene {sceneName}");
                            scenario = scenarioToLoad;
                            if (SceneManager.GetActiveScene().name == sceneName && rti.State == RuntimeState.Playback) {
                                if (debugRuntimeControl) Debug.Log($"Scene already loaded for playback: {sceneName}");
                            } else {
                                var previousState = rti.State;
                                rti.State = RuntimeState.Loading;
                                Time.timeScale = 1;
                                var loadOperation = SceneManager.LoadSceneAsync(sceneName);
                                if (loadOperation != null) {
                                    loadOperation.completed += (op) => {
                                        if (debugRuntimeControl) Debug.Log("Scene loaded");
                                        time = 0;
                                        var scenarioLoader = FindObjectOfType<RTIScenarioLoader>();
                                        if (scenarioLoader != null) {
                                            if (debugRuntimeControl) Debug.Log("Scene has scenario loader");
                                        } else if (previousState == RuntimeState.Playback) {
                                            rti.State = RuntimeState.Playback;
                                            Time.timeScale = timeScale;
                                        } else {
                                            rti.State = RuntimeState.Ready;
                                            Time.timeScale = 0;
                                        }
                                    };
                                } else {
                                    var error = $"Cannot load scenario {scenarioToLoad.name} scene {sceneName}";
                                    Debug.LogError(error);
                                    rti.PublishError(error, RuntimeState.Loading);
                                }
                            }
                        } else {
                            var error = $"No such scene {sceneName} for - need to add it to build settings?";
                            Debug.LogError(error);
                            rti.PublishError(error, RuntimeState.Loading);
                        }
                    }
                    lastScenarioName = message.LoadScenario.Name;
                    break;
                case RuntimeControl.ControlOneofCase.Start:
                    if (debugRuntimeControl) Debug.Log("Start");
                    if (CustomLoadScenario == null && SceneManager.GetActiveScene().buildIndex == 0) {
                        Debug.LogWarning("Cannot start - no scene loaded");
                        rti.PublishError("No scene loaded", RuntimeState.Running);
                    } else {
                        if (rti.State != RuntimeState.Paused && rti.State != RuntimeState.PlaybackPaused) {
                            time = 0;
                            timeSyncMasterClientId = null;
                        }
                        rti.State = RuntimeState.Running;
                        Time.timeScale = timeScale;
                        OnStart?.Invoke();
                    }
                    break;
                case RuntimeControl.ControlOneofCase.Pause:
                    if (debugRuntimeControl) Debug.Log("Pause");
                    rti.State = rti.State == RuntimeState.Playback ? RuntimeState.PlaybackPaused : RuntimeState.Paused;
                    Time.timeScale = 0;
                    break;
                case RuntimeControl.ControlOneofCase.End:
                    if (rti.State == RuntimeState.Running) {
                        if (debugRuntimeControl) Debug.Log("End");
                        rti.State = RuntimeState.End;
                        Time.timeScale = 0;
                    } else if (rti.State == RuntimeState.Playback) {
                        if (debugRuntimeControl) Debug.Log("End playback");
                        rti.State = RuntimeState.PlaybackEnd;
                        Time.timeScale = 0;
                    } else {
                        if (debugRuntimeControl) Debug.Log($"Unexpected end from state {rti.State}");
                    }
                    break;
                case RuntimeControl.ControlOneofCase.Play:
                    if (debugRuntimeControl) Debug.Log("Play");
                    if (rti.State != RuntimeState.PlaybackPaused) time = 0;
                    rti.State = RuntimeState.Playback;
                    Time.timeScale = timeScale;
                    timeSyncMasterClientId = null;
                    break;
                case RuntimeControl.ControlOneofCase.SetTimeScale:
                    if (debugRuntimeControl) Debug.Log($"Time scale {message.SetTimeScale.TimeScale}");
                    timeScale = (float)message.SetTimeScale.TimeScale;
                    if (rti.State == RuntimeState.Running || rti.State == RuntimeState.Playback || rti.State == RuntimeState.Unknown) Time.timeScale = timeScale;
                    OnTimeScaleChanged?.Invoke(timeScale);
                    break;
                case RuntimeControl.ControlOneofCase.Seek:
                    if (debugRuntimeControl) Debug.Log($"Seek {message.Seek.Time}");
                    time = (float)message.Seek.Time;
                    break;
                case RuntimeControl.ControlOneofCase.Stop:
                    var stateBefore = rti.State;
                    OnStop?.Invoke();
                    if (CustomStop != null) {
                        CustomStop.Invoke();
                    } else {
                        rti.State = RuntimeState.Stopping;
                        var toRemove = new List<string>();
                        foreach (var id in entities.Keys) {
                            var entity = entities[id];
                            if (entity != null && entity.gameObject != null && !entity.persistent) {
                                Destroy(entity.gameObject);
                                toRemove.Add(id);
                            }
                        }
                        foreach (var id in toRemove) {
                            entities.Remove(id);
                        }
                        foreach (var injectable in injectables.Values) {
                            if (injectable != null && injectable.gameObject != null) Destroy(injectable.gameObject);
                        }
                        injectables.Clear();
                        Time.timeScale = 0;
                        timeSyncMasterClientId = null;
                    }
                    switch (stateBefore) {
                        case RuntimeState.Playback:
                        case RuntimeState.PlaybackPaused:
                        case RuntimeState.PlaybackEnd:
                            rti.State = RuntimeState.PlaybackStopped;
                            break;
                        default:
                            rti.State = RuntimeState.Stopped;
                            break;
                    }
                    break;
                case RuntimeControl.ControlOneofCase.Reset:
                    OnReset?.Invoke();
                    if (CustomReset != null) {
                        CustomReset.Invoke();
                        rti.State = RuntimeState.Initial;
                    } else {
                        if (debugRuntimeControl) Debug.Log($"Reset - loading home scene at index 0 - {SceneManager.GetSceneByBuildIndex(0).name}");
                        SceneManager.LoadSceneAsync(0).completed += (op) => {
                            if (debugRuntimeControl) Debug.Log("Reset");
                            rti.State = RuntimeState.Initial;
                            Time.timeScale = 1;
                            time = 0;
                            scenario = null;
                            entities.Clear();
                            geometries.Clear();
                            injectables.Clear();
                            scenarioParameterValues.Clear();
                            receivedCurrentScenario = null;
                            timeSyncMasterClientId = null;
                        };
                    }
                    break;
                case RuntimeControl.ControlOneofCase.TimeSync: {
                        var wasTimeSyncMaster = IsTimeSyncMaster;
                        timeSyncMasterClientId = message.TimeSync.MasterClientId;
                        var diff = time - (float)message.TimeSync.Time;
                        if (Math.Abs(diff) > 0.5f) {
                            if (debugRuntimeControl) Debug.LogWarning($"Time sync diff {diff}");
                            time = (float)message.TimeSync.Time;
                        }
                        if (Mathf.Abs(timeScale - (float)message.TimeSync.TimeScale) > 0.01f) {
                            if (debugRuntimeControl) Debug.LogWarning($"Sync time scale to {message.TimeSync.TimeScale}");
                            timeScale = (float)message.TimeSync.TimeScale;
                            if (rti.State == RuntimeState.Running || rti.State == RuntimeState.Playback) Time.timeScale = timeScale;
                            OnTimeScaleChanged?.Invoke(timeScale);
                        }
                        if (wasTimeSyncMaster && !IsTimeSyncMaster && debugRuntimeControl) Debug.Log($"Giving up time sync master to {timeSyncMasterClientId}");
                        break;
                    }
                case RuntimeControl.ControlOneofCase.RequestCurrentScenario: {
                        if (scenario != null) {
                            var currentScenario = new RuntimeControl.Types.LoadScenario {
                                Name = scenario.name
                            };
                            foreach (var pair in scenarioParameterValues) {
                                currentScenario.ParameterValues.Add(pair.Key, pair.Value);
                            }
                            Publish(RTIChannel.Control, new RuntimeControl {
                                CurrentScenario = currentScenario
                            });
                        }
                        break;
                    }
                case RuntimeControl.ControlOneofCase.CurrentScenario: {
                        receivedCurrentScenario = message.CurrentScenario;
                        break;
                    }
            }
            if (rti.State != lastState) OnStateChanged?.Invoke(rti.State);
            lastState = rti.State;
        }
        private RuntimeState lastState;

        private Dictionary<string, bool> mentionedScenario = new Dictionary<string, bool>();

        private void OnScenarios(string channelName, Scenarios message) {
            if (message.WhichCase == Scenarios.WhichOneofCase.RequestScenarios) {
                mentionedScenario.Clear();
                StartCoroutine(RandomDelayPublishScenarios());
            } else if (message.WhichCase == Scenarios.WhichOneofCase.Scenario) {
                mentionedScenario[message.Scenario.Name] = true;
            }
        }

        // Using a random delay to avoid multiple instances of same simulator talking in each others mouths
        private IEnumerator RandomDelayPublishScenarios() {
            yield return new WaitForSecondsRealtime(UnityEngine.Random.Range(0.01f, 0.1f));

            if (scenarios.Count > 0 || scenarioNames.Count > 0) {
                foreach (var scenario in scenarios) {
                    if (scenario != null && !mentionedScenario.ContainsKey(scenario.name)) {
                        rti.Publish(RTIChannel.Scenarios, new Scenarios {
                            Scenario = scenario.ToProto()
                        });
                        mentionedScenario[scenario.name] = true;
                    }
                }
                foreach (var scenarioName in scenarioNames) {
                    if (!mentionedScenario.ContainsKey(scenarioName)) {
                        rti.Publish(RTIChannel.Scenarios, new Scenarios {
                            Scenario = new Inhumate.RTI.Proto.Scenario { Name = scenarioName }
                        });
                        mentionedScenario[scenarioName] = true;
                    }
                }
            } else {
                // Fallback to just listing all levels in build settings
                for (int i = 1; i < SceneManager.sceneCountInBuildSettings; i++) {
                    var scenePath = SceneUtility.GetScenePathByBuildIndex(i);
                    if (scenePath.Contains("Additive")) continue;
                    var lastSlash = scenePath.LastIndexOf("/");
                    var sceneName = scenePath.Substring(lastSlash + 1, scenePath.LastIndexOf(".") - lastSlash - 1);
                    if (!mentionedScenario.ContainsKey(sceneName)) {
                        rti.Publish(RTIChannel.Scenarios, new Scenarios {
                            Scenario = new Inhumate.RTI.Proto.Scenario {
                                Name = sceneName
                            }
                        });
                        mentionedScenario[sceneName] = true;
                    }
                }
            }
        }

        private void OnEntity(string channelName, Entity message) {
            var entity = GetEntityById(message.Id);
            if (entity != null && entity.persistent && !entity.owned) {
                if (message.Deleted) {
                    if (debugEntities) Debug.Log($"Destroy deleted persistent entity {message.Id}: {entity.name}", this);
                    if (entity.gameObject != null) Destroy(entity.gameObject);
                    UnregisterEntity(entity);
                } else {
                    if (debugEntities) Debug.Log($"Update persistent entity {message.Id}: {entity.name}", this);
                    entity.SetPropertiesFromEntityData(message);
                    entity.InvokeOnUpdated(message);
                }
            }
        }

        private void OnEntityOperation(string channelName, EntityOperation message) {
            switch (message.WhichCase) {
                case EntityOperation.WhichOneofCase.RequestUpdate: {
                        foreach (var ent in entities.Values) {
                            if (ent.publishing) ent.RequestUpdate();
                        }
                        break;
                    }
                case EntityOperation.WhichOneofCase.TransferOwnership: {
                        var entity = GetEntityById(message.TransferOwnership.EntityId);
                        if (entity != null) {
                            if (entity.owned && entity.ownerClientId == ClientId && message.TransferOwnership.ClientId != ClientId) {
                                if (debugEntities) Debug.Log($"Transfer entity {entity.id} - releasing ownership");
                                entity.ReleaseOwnership(message.TransferOwnership.ClientId);
                                OnEntityOwnershipReleased?.Invoke(entity);
                            } else if (!entity.owned && entity.ownerClientId != ClientId && message.TransferOwnership.ClientId == ClientId) {
                                if (debugEntities) Debug.Log($"Transfer entity {entity.id} - assuming ownership");
                                entity.AssumeOwnership();
                                OnEntityOwnershipAssumed?.Invoke(entity);
                            } else if (entity.owned) {
                                Debug.LogWarning($"Weird ownership transfer of owned entity {entity.id} to {message.TransferOwnership.ClientId}");
                            }
                        }
                        break;
                    }
                case EntityOperation.WhichOneofCase.AssumeOwnership: {
                        var entity = GetEntityById(message.AssumeOwnership.EntityId);
                        if (entity != null) {
                            if (entity.owned && message.AssumeOwnership.ClientId != ClientId && entity.ownerClientId == ClientId) {
                                entity.ReleaseOwnership(message.AssumeOwnership.ClientId);
                                OnEntityOwnershipReleased?.Invoke(entity);
                            }
                            entity.lastOwnershipChangeTime = Time.time;
                        }
                        break;
                    }
                case EntityOperation.WhichOneofCase.ReleaseOwnership: {
                        var entity = GetEntityById(message.ReleaseOwnership.EntityId);
                        if (entity != null) {
                            if (entity.ownerClientId == message.ReleaseOwnership.ClientId) entity.ownerClientId = null;
                            entity.lastOwnershipChangeTime = Time.time;
                        }
                        break;
                    }
                case EntityOperation.WhichOneofCase.RequestPersistentOwnership:
                    if (IsPersistentEntityOwner && message.RequestPersistentOwnership.Application == rti.Application) {
                        PublishClaimPersistentEntityOwnership();
                    }
                    break;
                case EntityOperation.WhichOneofCase.ClaimPersistentOwnership:
                    if (message.ClaimPersistentOwnership.Application == rti.Application) {
                        persistentEntityOwnerClientId = message.ClaimPersistentOwnership.ClientId;
                    }
                    break;
            }
        }

        private IEnumerator RandomDelayClaimPersistentEntityOwnership() {
            yield return new WaitForSecondsRealtime(UnityEngine.Random.Range(0.2f, 0.5f));
            if (persistentEntityOwnerClientId == null) {
                if (debugRuntimeControl) Debug.Log($"RTI claiming persistent entity ownership for {rti.Application}");
                persistentEntityOwnerClientId = rti.ClientId;
                PublishClaimPersistentEntityOwnership();
                Publish(RTIChannel.Clients, new Clients { RequestClients = new Google.Protobuf.WellKnownTypes.Empty() });
                foreach (var entity in entities.Values) {
                    if (entity.persistent && string.IsNullOrEmpty(entity.ownerClientId)) {
                        Debug.Log($"Re-assume ownership of persistent entity {entity.id}");
                        entity.owned = true;
                        entity.ownerClientId = ClientId;
                        Publish(RTIChannel.EntityOperation, new EntityOperation {
                            AssumeOwnership = new EntityOperation.Types.EntityClient {
                                EntityId = entity.id,
                                ClientId = ClientId
                            }
                        });
                        entity.InvokeOnOwnershipChanged();
                    }
                }
            }
        }

        private void PublishClaimPersistentEntityOwnership() {
            rti.Publish(RTIChannel.EntityOperation, new EntityOperation {
                ClaimPersistentOwnership = new EntityOperation.Types.ApplicationClient {
                    Application = rti.Application,
                    ClientId = rti.ClientId
                }
            });
        }

        public void RegisterEntity(RTIEntity entity) {
            entities[entity.id] = entity;
        }

        public void UnregisterEntity(RTIEntity entity) {
            entities.Remove(entity.id);
        }

        public RTIEntity GetEntityById(string id) {
            if (entities.ContainsKey(id)) return entities[id];
            return null;
        }

        public IEnumerable<RTIEntity> GetEntitiesByType(string type) {
            return entities.Values.Where(entity => entity.EntityData.Type.ToLower() == type.ToLower());
        }

        private void OnGeometryOperation(string channelName, GeometryOperation message) {
            switch (message.WhichCase) {
                case GeometryOperation.WhichOneofCase.RequestUpdate: {
                        foreach (var geometry in geometries.Values) {
                            geometry.RequestUpdate();
                        }
                        break;
                    }
                case GeometryOperation.WhichOneofCase.RequestPersistentOwnership:
                    if (IsPersistentGeometryOwner && message.RequestPersistentOwnership.Application == rti.Application) {
                        PublishClaimPersistentGeometryOwnership();
                    }
                    break;
                case GeometryOperation.WhichOneofCase.ClaimPersistentOwnership:
                    if (message.ClaimPersistentOwnership.Application == rti.Application) {
                        persistentGeometryOwnerClientId = message.ClaimPersistentOwnership.ClientId;
                    }
                    break;
            }
        }

        private IEnumerator RandomDelayClaimPersistentGeometryOwnership() {
            yield return new WaitForSecondsRealtime(UnityEngine.Random.Range(0.2f, 0.5f));
            if (persistentGeometryOwnerClientId == null) {
                if (debugRuntimeControl) Debug.Log($"RTI claiming persistent geometry ownership for {rti.Application}");
                persistentGeometryOwnerClientId = rti.ClientId;
                PublishClaimPersistentGeometryOwnership();
                Publish(RTIChannel.Clients, new Clients { RequestClients = new Google.Protobuf.WellKnownTypes.Empty() });
                foreach (var geometry in geometries.Values) {
                    if (geometry.persistent) geometry.owned = true;
                }
            }
        }

        private void PublishClaimPersistentGeometryOwnership() {
            rti.Publish(RTIChannel.GeometryOperation, new GeometryOperation {
                ClaimPersistentOwnership = new GeometryOperation.Types.ApplicationClient {
                    Application = rti.Application,
                    ClientId = rti.ClientId
                }
            });
        }

        public bool RegisterGeometry(RTIGeometry geometry) {
            if (geometries.ContainsKey(geometry.Id) && geometries[geometry.Id] != geometry) {
                return false;
            } else {
                geometries[geometry.Id] = geometry;
                return true;
            }
        }

        public void UnregisterGeometry(RTIGeometry geometry) {
            if (geometries.ContainsKey(geometry.Id) && geometries[geometry.Id] == geometry) {
                geometries.Remove(geometry.Id);
            }
        }

        public RTIGeometry GetGeometryById(string id) {
            if (geometries.ContainsKey(id)) return geometries[id];
            return null;
        }

        private void OnInjectableOperation(string channelName, InjectableOperation message) {
            if (message.WhichCase == InjectableOperation.WhichOneofCase.RequestUpdate) {
                foreach (var injectable in injectables.Values) injectable.Publish();
            }
        }

        private void OnInjectionOperation(string channelName, InjectionOperation message) {
            if (message.WhichCase == InjectionOperation.WhichOneofCase.RequestUpdate) {
                foreach (var injectable in injectables.Values) injectable.PublishInjections();
            } else if (message.WhichCase == InjectionOperation.WhichOneofCase.Inject) {
                var id = message.Inject.Injectable.ToLower();
                if (injectables.ContainsKey(id)) {
                    injectables[id].Inject(message.Inject);
                } else {
                    Debug.LogWarning($"Unknown injectable: {message.Inject.Injectable}");
                }
            } else if (message.WhichCase == InjectionOperation.WhichOneofCase.Disable) {
                if (GetInjectableAndInjection(message.Disable, out RTIInjectable injectable, out Injection injection)) {
                    injectable.DisableInjection(injection);
                }
            } else if (message.WhichCase == InjectionOperation.WhichOneofCase.Enable) {
                if (GetInjectableAndInjection(message.Enable, out RTIInjectable injectable, out Injection injection)) {
                    injectable.EnableInjection(injection);
                }
            } else if (message.WhichCase == InjectionOperation.WhichOneofCase.Start) {
                if (GetInjectableAndInjection(message.Start, out RTIInjectable injectable, out Injection injection)) {
                    injectable.StartInjection(injection);
                }
            } else if (message.WhichCase == InjectionOperation.WhichOneofCase.End) {
                if (GetInjectableAndInjection(message.End, out RTIInjectable injectable, out Injection injection)) {
                    injectable.EndInjection(injection);
                }
            } else if (message.WhichCase == InjectionOperation.WhichOneofCase.Stop) {
                if (GetInjectableAndInjection(message.Stop, out RTIInjectable injectable, out Injection injection)) {
                    injectable.StopInjection(injection);
                }
            } else if (message.WhichCase == InjectionOperation.WhichOneofCase.Cancel) {
                if (GetInjectableAndInjection(message.Cancel, out RTIInjectable injectable, out Injection injection)) {
                    injectable.CancelInjection(injection);
                }
            } else if (message.WhichCase == InjectionOperation.WhichOneofCase.Schedule) {
                if (GetInjectableAndInjection(message.Schedule.InjectionId, out RTIInjectable injectable, out Injection injection)) {
                    injectable.ScheduleInjection(message.Schedule.EnableTime, injection);
                }
            } else if (message.WhichCase == InjectionOperation.WhichOneofCase.UpdateTitle) {
                if (GetInjectableAndInjection(message.UpdateTitle.InjectionId, out RTIInjectable injectable, out Injection injection)) {
                    injectable.UpdateTitle(message.UpdateTitle.Title, injection);
                }
            }
        }

        public bool GetInjectableAndInjection(string injectionId, out RTIInjectable injectable, out Injection injection) {
            injectable = null;
            injection = null;
            foreach (var able in injectables.Values) {
                var on = able.GetInjection(injectionId);
                if (on != null) {
                    injectable = able;
                    injection = on;
                    return true;
                }
            }
            return false;
        }

        public bool RegisterInjectable(RTIInjectable injectable) {
            var id = injectable.name.ToLower();
            if (injectables.ContainsKey(id) && injectables[id] != injectable) {
                return false;
            } else {
                injectables[id] = injectable;
                return true;
            }
        }

        public void UnregisterInjectable(RTIInjectable injectable) {
            var id = injectable.name.ToLower();
            if (injectables.ContainsKey(id) && injectables[id] == injectable) {
                injectables.Remove(id);
            }
        }

        private void OnCommands(string channelName, Commands message) {
            switch (message.WhichCase) {
                case Inhumate.RTI.Proto.Commands.WhichOneofCase.RequestCommands:
                    foreach (var command in commands.Values) {
                        Publish(channelName, new Commands { Command = command });
                    }
                    break;
                case Inhumate.RTI.Proto.Commands.WhichOneofCase.Execute: {
                        CommandResponse response = null;
                        var name = message.Execute.Name.ToLower();
                        var specific = false;
                        // Allow command names with prefixed application name, e.g. mysim/dostuff
                        if (name.StartsWith(Client.Application.ToLower() + "/")) {
                            name = name.Substring(Client.Application.Length + 1);
                            specific = true;
                        }
                        if (commands.TryGetValue(name, out Command command) && commandHandlers.TryGetValue(name, out CommandHandler handler)) {
                            response = handler(command, message.Execute);
                        } else if (specific) {
                            response = new CommandResponse { Failed = true, Message = $"Unknown command {name}" };
                        }
                        if (!string.IsNullOrEmpty(message.Execute.TransactionId)) {
                            if (response != null) {
                                response.TransactionId = message.Execute.TransactionId;
                                Publish(channelName, new Commands { Response = response });
                            } else {
                                commandTransactionChannel[message.Execute.TransactionId] = channelName;
                            }
                        }
                        break;
                    }
            }
        }

        public void ExecuteCommandInternal(string name, ExecuteCommand executeCommand = null) {
            name = name.ToLower();
            if (executeCommand == null) executeCommand = new ExecuteCommand { Name = name };
            if (commands.TryGetValue(name, out Command command) && commandHandlers.TryGetValue(name, out CommandHandler handler)) {
                var response = handler(command, executeCommand);
                if (response.Failed) {
                    Debug.LogWarning($"Command {name} failed: {response.Message}", this);
                } else if (!string.IsNullOrEmpty(response.Message)) {
                    Debug.Log($"Command {name}: {response.Message}", this);
                }
            } else {
                Debug.LogWarning($"Unknown command {name}", this);
            }
        }

        public void RegisterCommands(MonoBehaviour behaviour) {
            var methods = behaviour.GetType().GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            foreach (var method in methods) {
                var commandAttribute = method.GetCustomAttribute<RTICommandAttribute>();
                if (commandAttribute != null) {
                    var command = new Command { Name = commandAttribute.Name };
                    if (string.IsNullOrWhiteSpace(command.Name)) command.Name = method.Name;
                    var argumentAttributes = method.GetCustomAttributes<RTICommandArgumentAttribute>();
                    foreach (var argumentAttribute in argumentAttributes) {
                        argumentAttribute.AddToCommand(command);
                    }
                    if (typeof(CommandResponse).IsAssignableFrom(method.ReturnType)) {
                        if (method.GetParameters().Length == 2) {
                            RegisterCommand(command, (cmd, exe) => {
                                return (CommandResponse)method.Invoke(behaviour, new object[] { cmd, exe });
                            });
                        } else if (method.GetParameters().Length == 0) {
                            RegisterCommand(command, (cmd, exe) => {
                                return (CommandResponse)method.Invoke(behaviour, new object[] { });
                            });
                        } else {
                            Debug.LogError($"Invalid command method {method.Name} in {behaviour.GetType().Name}: parameters");
                        }
                    } else if (method.ReturnType == typeof(void)) {
                        if (method.GetParameters().Length == 2) {
                            RegisterCommand(command, (cmd, exe) => {
                                method.Invoke(behaviour, new object[] { cmd, exe });
                                return new CommandResponse();
                            });
                        } else if (method.GetParameters().Length == 0) {
                            RegisterCommand(command, (cmd, exe) => {
                                method.Invoke(behaviour, new object[] { });
                                return new CommandResponse();
                            });
                        } else {
                            Debug.LogError($"Invalid command method {method.Name} in {behaviour.GetType().Name}: parameters");
                        }
                    } else {
                        Debug.LogError($"Invalid command method {method.Name} in {behaviour.GetType().Name}: return type");
                    }
                }
            }
        }

        public delegate void DefaultResponseCommandHandler(Command command, ExecuteCommand exec);
        public bool RegisterCommand(Command command, DefaultResponseCommandHandler handler) {
            return RegisterCommand(command, (cmd, exe) => {
                handler(cmd, exe);
                return new CommandResponse();
            });
        }

        public bool RegisterCommand(Command command, CommandHandler handler) {
            var name = command.Name.ToLower();
            if (commands.ContainsKey(name) && commands[name] != command) {
                return false;
            } else {
                commands[name] = command;
                commandHandlers[name] = handler;
                WhenConnectedOnce(() => Publish(RTIChannel.Commands, new Commands { Command = command }));
                return true;
            }
        }

        public void UnregisterCommand(Command command) {
            UnregisterCommand(command.Name);
        }

        public void UnregisterCommand(string name) {
            commands.Remove(name.ToLower());
            commandHandlers.Remove(name.ToLower());
        }

        public void PublishCommandResponse(ExecuteCommand exec, CommandResponse response) {
            PublishCommandResponse(exec.TransactionId, response);
        }

        public void PublishCommandResponse(string transactionId, CommandResponse response) {
            if (string.IsNullOrEmpty(transactionId) || !IsConnected) return;
            response.TransactionId = transactionId;
            var channel = RTIChannel.Commands;
            commandTransactionChannel.TryGetValue(transactionId, out channel);
            commandTransactionChannel.Remove(transactionId);
            Publish(channel, new Commands { Response = response });
        }

        public string GetScenarioParameterValue(string name) {
            if (scenarioParameterValues.ContainsKey(name)) return scenarioParameterValues[name];
            if (scenario != null) {
                var parameter = scenario.parameters.Where(p => p.name == name).FirstOrDefault();
                if (parameter != null) return parameter.defaultValue;
            }
            return null;
        }

        public void OnClientDisconnect(string channel, object clientId) {
            if (clientId == null) return;
            if (timeSyncMasterClientId == clientId.ToString()) {
                Debug.Log("Time sync master disconnected");
                timeSyncMasterClientId = null;
            }
            if (persistentEntityOwnerClientId == clientId.ToString()) {
                Debug.Log("Persistent entity owner disconnected");
                foreach (var entity in entities.Values) {
                    if (entity.persistent && entity.ownerClientId == clientId.ToString()) {
                        entity.ownerClientId = null;
                    }
                }
                persistentEntityOwnerClientId = null;
                QueryPersistentEntityOwner();
            }
            if (persistentGeometryOwnerClientId == clientId.ToString()) {
                Debug.Log("Persistent geometry owner disconnected");
                persistentGeometryOwnerClientId = null;
                QueryPersistenGeometryOwner();
            }
        }

        public void Disconnect() {
            if (connected && debugConnection) Debug.Log($"RTI Disconnect");
            connected = false;
            if (rti != null) {
                rti.Disconnect();
            }
            if (LastErrorChannel == "connection") {
                LastError = null;
                LastErrorChannel = null;
            }
            OnDisconnected?.Invoke();
        }

        void Update() {
            if (rti != null) {
                pollCount = rti.Poll(maxPollCount);
            }
            if (rti != null && (rti.State == RuntimeState.Running || rti.State == RuntimeState.Playback || rti.State == RuntimeState.Unknown)) {
                time += Time.deltaTime;
            }
            if (_queued) {
                lock (_backlog) {
                    var tmp = _actions;
                    _actions = _backlog;
                    _backlog = tmp;
                    _queued = false;
                }

                foreach (var action in _actions)
                    action?.Invoke();

                _actions.Clear();
            }
            if (!inhibitTimeSyncMaster && rti != null && rti.State == RuntimeState.Running
                    && ((timeSyncMasterClientId == null && time >= 2f && Mathf.Abs(time - lastTimeSyncTime) >= 1f + UnityEngine.Random.Range(0.5f, 1.5f))
                    || (timeSyncMasterClientId == rti.ClientId && Mathf.Abs(time - lastTimeSyncTime) >= 1f))) {
                lastTimeSyncTime = time;
                if (rti.IsConnected) {
                    rti.Publish(RTIChannel.Control, new RuntimeControl {
                        TimeSync = new RuntimeControl.Types.TimeSync {
                            Time = time,
                            TimeScale = timeScale,
                            MasterClientId = rti.ClientId
                        }
                    });
                    if (debugRuntimeControl && timeSyncMasterClientId != rti.ClientId) Debug.Log($"Claiming time sync master");
                }
            }
            if (lateJoin && state == RuntimeState.Initial && rti != null && receivedCurrentScenario != null) {
                var siblings = rti.KnownClients.Where(c => c.Application == rti.Application && c.Id != rti.ClientId);
                foreach (var sibling in siblings) {
                    if (sibling.State >= RuntimeState.Loading) {
                        if (debugRuntimeControl) Debug.Log($"Late join load scenario");
                        OnRuntimeControl("internal", new RuntimeControl {
                            LoadScenario = receivedCurrentScenario
                        });
                        break;
                    }
                }
            }
            if (lateJoin && state == RuntimeState.Ready) {
                var siblings = rti.KnownClients.Where(c => c.Application == rti.Application && c.Id != rti.ClientId);
                foreach (var sibling in siblings) {
                    if (sibling.State == RuntimeState.Running) {
                        if (debugRuntimeControl) Debug.Log($"Late join start");
                        OnRuntimeControl("internal", new RuntimeControl {
                            Start = new Google.Protobuf.WellKnownTypes.Empty()
                        });
                    }
                    break;
                }
            }
        }

        public void RunOrQueue(Action action) {
            if (rti.Polling && Thread.CurrentThread.ManagedThreadId == mainThreadId) action?.Invoke();
            else QueueOnMainThread(action);
        }

        // as inspired from ThreadDispatcher.cs from common

        public static void QueueOnMainThread(Action action) {
            lock (_backlog) {
                _backlog.Add(action);
                _queued = true;
            }
        }

        static volatile bool _queued = false;
        static List<Action> _backlog = new List<Action>(8);
        static List<Action> _actions = new List<Action>(8);

        /*
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            if (_instance == null)
            {
                _instance = new GameObject("RTI Connection").AddComponent<RTIConnection>();
                DontDestroyOnLoad(_instance.gameObject);
            }
        }
        */

        static RTIConnection _instance;
    }

}
