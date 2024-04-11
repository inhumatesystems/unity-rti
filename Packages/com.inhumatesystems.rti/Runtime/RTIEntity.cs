using System;
using System.Linq;
using Inhumate.RTI.Client;
using Inhumate.RTI.Proto;
using UnityEngine;
using NaughtyAttributes;
using System.Collections.Generic;
using System.Reflection;

namespace Inhumate.Unity.RTI {

    public class RTIEntity : MonoBehaviour {

        public string type;
        public EntityCategory category;
        public EntityDomain domain;
        public LVCCategory lvc;
        public Vector3 center;
        public Vector3 size;
        public UnityEngine.Color color;
        public bool titleFromName = true;
        [HideIf("titleFromName")]
        public string title;
        [Tooltip("Unique ID used to identify this entity. Leave blank to generate a random ID when the entity is created.")]
        public string id;

        public float updateInterval = 0f;

        public bool persistent { get; internal set; }
        public bool created { get; internal set; }
        public bool owned { get; internal set; } = true;
        public string ownerClientId { get; internal set; }
        public float lastOwnershipChangeTime { get; internal set; }

        public bool publishing {
            get {
                return owned && created && (RTI.state == RuntimeState.Running || RTI.state == RuntimeState.Unknown);
            }
        }

        public bool receiving {
            get {
                return !publishing && (!owned || (RTI.state != RuntimeState.Running && RTI.state != RuntimeState.Unknown));
            }
        }

        public Client OwnerClient {
            get {
                return ownerClientId != null ? RTI.Client.GetClient(ownerClientId) : null;
            }
        }

        public event Action<EntityOperation.Types.EntityData> OnCreated;
        public event Action<EntityOperation.Types.EntityData> OnUpdated;
        public event Action OnOwnershipChanged;

        public string CommandsChannelName => $"{RTIConstants.CommandsChannel}/{id}";
        public Command[] Commands => commands.Values.ToArray();
        private Dictionary<string, Command> commands = new Dictionary<string, Command>();
        private Dictionary<string, RTIConnection.CommandHandler> commandHandlers = new Dictionary<string, RTIConnection.CommandHandler>();
        private UntypedListener commandsListener;
        private UntypedListener ownCommandsListener;

        protected static RTIConnection RTI => RTIConnection.Instance;

        private bool updateRequested;
        public void RequestUpdate() {
            updateRequested = true;
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
                            Debug.LogError($"Invalid entity command method {method.Name} in {behaviour.GetType().Name}: parameters");
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
                            Debug.LogError($"Invalid entity command method {method.Name} in {behaviour.GetType().Name}: parameters");
                        }
                    } else {
                        Debug.LogError($"Invalid entity command method {method.Name} in {behaviour.GetType().Name}: return type");
                    }
                }
            }
        }

        public bool RegisterCommand(Command command, RTIConnection.DefaultResponseCommandHandler handler) {
            return RegisterCommand(command, (cmd, exe) => {
                handler(cmd, exe);
                return new CommandResponse();
            });
        }

        public bool RegisterCommand(Command command, RTIConnection.CommandHandler handler) {
            var name = command.Name.ToLower();
            if (commands.ContainsKey(name) && commands[name] != command) {
                return false;
            } else {
                commands[name] = command;
                commandHandlers[name] = handler;
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
            if (string.IsNullOrEmpty(transactionId) || !RTI.IsConnected) return;
            response.TransactionId = transactionId;
            RTI.Publish(CommandsChannelName, new Commands { Response = response });
        }

        public void AssumeOwnership() {
            if (owned) return;
            RTI.Publish(RTIConstants.EntityChannel, new EntityOperation {
                Id = id,
                ClientId = RTI.ClientId,
                AssumeOwnership = new Google.Protobuf.WellKnownTypes.Empty()
            });
            owned = true;
            ownerClientId = RTI.ClientId;
            InvokeOnOwnershipChanged();
        }

        public void ReleaseOwnership(string newOwnerClientId = null) {
            if (!owned) return;
            RTI.Publish(RTIConstants.EntityChannel, new EntityOperation {
                Id = id,
                ClientId = RTI.ClientId,
                ReleaseOwnership = new Google.Protobuf.WellKnownTypes.Empty()
            });
            owned = false;
            ownerClientId = newOwnerClientId;
            InvokeOnOwnershipChanged();
        }

        void Awake() {
            if (RTI.state == RuntimeState.Loading || (RTI.time < float.Epsilon && RTI.state != RuntimeState.Running && !name.EndsWith("(Clone)"))) {
                persistent = true;
            }
            if (string.IsNullOrWhiteSpace(id)) id = GenerateId();
            ownerClientId = RTI.ClientId;
        }

        void Start() {
            if (RTI.GetEntityById(id)) {
                Debug.LogWarning($"Entity {id} has already been registered", this);
            }
            RTI.RegisterEntity(this);
            RTI.Client.RegisterChannel(new Channel {
                Name = CommandsChannelName,
                DataType = typeof(Commands).Name,
                Ephemeral = true
            });
            commandsListener = RTI.Subscribe<Commands>(CommandsChannelName, OnCommandsMessage);
            ownCommandsListener = RTI.Subscribe<Commands>(RTI.Client.OwnChannelPrefix + CommandsChannelName, OnCommandsMessage);
        }

        string GenerateId() {
            if (persistent) {
                string path = "";
                var current = transform;
                while (current != null) {
                    path = "/" + current.name.Trim().Replace(" (", "").Replace(")", "").Replace(" ", "_") + path;
                    current = current.parent;
                }
                return RTI.Application + path;
            } else {
                return Guid.NewGuid().ToString();
            }
        }

        private float lastUpdateTime;

        void Update() {
            if (owned && RTI.IsConnected && RTI.persistentEntityOwnerClientId != null && (RTI.state == RuntimeState.Running || RTI.state == RuntimeState.Unknown)) {
                if (!created) {
                    if (persistent && RTI.persistentEntityOwnerClientId != null && !RTI.IsPersistentEntityOwner) {
                        owned = false;
                        ownerClientId = RTI.persistentEntityOwnerClientId;
                        InvokeOnOwnershipChanged();
                    } else {
                        if (RTI.debugEntities) Debug.Log($"RTI publish create entity {id}", this);
                        RTI.Publish(RTIConstants.EntityChannel, new EntityOperation {
                            Id = id,
                            ClientId = RTI.ClientId,
                            Create = EntityData
                        });
                    }
                    created = true;
                } else if (updateRequested || (updateInterval > 1e-5f && Time.time - lastUpdateTime > updateInterval)) {
                    updateRequested = false;
                    PublishUpdate();
                }
            } else if (owned && RTI.state == RuntimeState.Playback) {
                if (!created && this.gameObject != null) Destroy(this.gameObject);
            }
        }

        void OnCommandsMessage(string channelName, Commands message) {
            if (!isActiveAndEnabled || !owned) return;
            switch (message.WhichCase) {
                case Inhumate.RTI.Proto.Commands.WhichOneofCase.RequestCommands:
                    foreach (var command in commands.Values) {
                        RTI.Publish(channelName, new Commands { Command = command });
                    }
                    break;
                case Inhumate.RTI.Proto.Commands.WhichOneofCase.Execute: {
                        CommandResponse response = null;
                        if (commands.TryGetValue(message.Execute.Name.ToLower(), out Command command) && commandHandlers.TryGetValue(message.Execute.Name.ToLower(), out RTIConnection.CommandHandler handler)) {
                            response = handler(command, message.Execute);
                        } else {
                            response = new CommandResponse { Failed = true, Message = $"Unknown command {message.Execute.Name}" };
                        }
                        if (response != null && !string.IsNullOrEmpty(message.Execute.TransactionId)) {
                            response.TransactionId = message.Execute.TransactionId;
                            RTI.Publish(channelName, new Commands { Response = response });
                        }
                        break;
                    }
            }
        }

        public void ExecuteCommandInternal(string name, ExecuteCommand executeCommand = null) {
            name = name.ToLower();
            if (executeCommand == null) executeCommand = new ExecuteCommand { Name = name };
            if (commands.TryGetValue(name, out Command command) && commandHandlers.TryGetValue(name, out RTIConnection.CommandHandler handler)) {
                var response = handler(command, executeCommand);
                if (response.Failed) {
                    Debug.LogWarning($"Command {name} failed: {response.Message}", this);
                } else if (!string.IsNullOrEmpty(response.Message)) {
                    Debug.Log($"Command {name}: {response.Message}", this);
                }
            } else {
                Debug.LogWarning($"Unknown entity command {name}", this);
            }
        }

        void OnEnable() {
            if (owned && created && RTI.IsConnected) PublishUpdate();
        }

        void OnDisable() {
            if (owned && created && RTI.IsConnected) PublishUpdate();
        }

        void PublishUpdate() {
            lastUpdateTime = Time.time;
            RTI.Publish(RTIConstants.EntityChannel, new EntityOperation {
                Id = id,
                ClientId = RTI.ClientId,
                Update = EntityData
            });
        }

        void OnApplicationQuit() {
            if (created && owned && !persistent) OnDestroy();
        }

        void OnDestroy() {
            bool hopefullyOtherClientTakesOver = RTI.quitting && persistent && publishing && RTI.Client.KnownClients.Count(c => c.Application == RTI.Client.Application) > 1;
            if (created && owned && RTI.IsConnected && !hopefullyOtherClientTakesOver) {
                created = false;
                if (RTI.debugEntities) Debug.Log($"RTI publish destroy {id}");
                RTI.Publish(RTIConstants.EntityChannel, new EntityOperation {
                    Id = id,
                    ClientId = RTI.ClientId,
                    Destroy = new Google.Protobuf.WellKnownTypes.Empty()
                });
            }
            if (commandsListener != null) {
                RTI.Unsubscribe(commandsListener);
                commandsListener = null;
            }
            if (ownCommandsListener != null) {
                RTI.Unsubscribe(ownCommandsListener);
                ownCommandsListener = null;
            }
            RTI.UnregisterEntity(this);
        }

        public Bounds GetBoundsFromRenderers() {
            var b = new Bounds(Vector3.zero, Vector3.zero);
            RecurseEncapsulate(transform, ref b);
            return b;

            void RecurseEncapsulate(Transform child, ref Bounds bounds) {
                var mesh = child.GetComponent<MeshFilter>();
                if (mesh && mesh.sharedMesh) {
                    var lsBounds = mesh.sharedMesh.bounds;
                    var wsMin = child.TransformPoint(lsBounds.center - lsBounds.extents);
                    var wsMax = child.TransformPoint(lsBounds.center + lsBounds.extents);
                    bounds.Encapsulate(transform.InverseTransformPoint(wsMin));
                    bounds.Encapsulate(transform.InverseTransformPoint(wsMax));
                }
                foreach (Transform grandChild in child.transform) {
                    RecurseEncapsulate(grandChild, ref bounds);
                }
            }
        }

        public Bounds GetBoundsFromColliders() {
            var b = new Bounds(Vector3.zero, Vector3.zero);
            RecurseEncapsulate(transform, ref b);
            return b;

            void RecurseEncapsulate(Transform child, ref Bounds bounds) {
                var collider = child.GetComponent<Collider>();
                if (collider) {
                    if (collider is BoxCollider) {
                        BoxCollider box = (BoxCollider)collider;
                        bounds.Encapsulate(box.center - box.size / 2);
                        bounds.Encapsulate(box.center + box.size / 2);
                    } else {
                        bounds.Encapsulate(transform.InverseTransformPoint(collider.bounds.center - collider.bounds.extents));
                        bounds.Encapsulate(transform.InverseTransformPoint(collider.bounds.center + collider.bounds.extents));
                    }
                }
                foreach (Transform grandChild in child.transform) {
                    RecurseEncapsulate(grandChild, ref bounds);
                }
            }
        }

        internal EntityOperation.Types.EntityData EntityData {
            get {
                Inhumate.RTI.Proto.Color col = null;
                if (color.a > 1e-5 || color.maxColorComponent > 1e-5) {
                    col = new Inhumate.RTI.Proto.Color {
                        Red = (int)Math.Round(color.r * 255),
                        Green = (int)Math.Round(color.g * 255),
                        Blue = (int)Math.Round(color.b * 255)
                    };
                }
                return new EntityOperation.Types.EntityData {
                    Type = type,
                    Category = category,
                    Domain = domain,
                    Lvc = lvc,
                    Dimensions = size.magnitude < 1e-5 && center.magnitude < 1e-5
                        ? null
                        : new EntityOperation.Types.Dimensions {
                            Length = size.z,
                            Width = size.x,
                            Height = size.y,
                            Center = new EntityPosition.Types.LocalPosition {
                                X = center.x,
                                Y = center.y,
                                Z = center.z
                            }
                        },
                    Color = col,
                    Title = titleFromName ? name.Replace("(Clone)", "") : !string.IsNullOrWhiteSpace(title) ? title : "",
                    Position = GetComponent<RTIPosition>() != null ? RTIPosition.PositionMessageFromTransform(this.transform) : null,
                    Disabled = !enabled || !gameObject.activeInHierarchy
                };
            }
        }

        internal void SetPropertiesFromEntityData(EntityOperation.Types.EntityData data) {
            type = data.Type;
            category = data.Category;
            domain = data.Domain;
            lvc = data.Lvc;
            if (data.Dimensions != null) {
                size = new Vector3(data.Dimensions.Width, data.Dimensions.Height, data.Dimensions.Length);
                if (data.Dimensions.Center != null) {
                    center = new Vector3(data.Dimensions.Center.X, data.Dimensions.Center.Y, data.Dimensions.Center.Z);
                }
            }
            if (data.Color != null) {
                color = new UnityEngine.Color(data.Color.Red / 255f, data.Color.Green / 255f, data.Color.Blue / 255f, 1f);
            }
            if (data.Disabled && gameObject.activeInHierarchy) gameObject.SetActive(false);
            if (!data.Disabled && !gameObject.activeInHierarchy) gameObject.SetActive(true);
        }

        internal void InvokeOnCreated(EntityOperation.Types.EntityData data) {
            OnCreated?.Invoke(data);
        }

        internal void InvokeOnUpdated(EntityOperation.Types.EntityData data) {
            OnUpdated?.Invoke(data);
        }

        internal void InvokeOnOwnershipChanged() {
            OnOwnershipChanged?.Invoke();
        }

    }

}
