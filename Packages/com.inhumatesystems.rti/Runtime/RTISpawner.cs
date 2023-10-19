using System;
using System.Collections;
using System.Collections.Generic;
using Inhumate.RTI.Client;
using Inhumate.RTI.Proto;
using UnityEngine;

namespace Inhumate.Unity.RTI {

    public class RTISpawner : RTIBehaviour<EntityOperation> {
        public override string ChannelName => RTIConstants.EntityChannel;
        public const string SpawnAllocationChannel = "rti/spawn";

        [System.Serializable]
        public class Spawnable {
            public string type;
            public RTIEntity prefab;
        }

        [HideInInspector]
        public List<Spawnable> spawnableEntities = new List<Spawnable>();

        public GameObject unknownPrefab;
        public GameObject playerPrefab;

        private RTIEntity unknownEntity;
        private RTIEntity playerEntity;
        private GameObject player;
        public Transform[] playerSpawnPoints;
        private Dictionary<int, string> spawnPointAllocations = new Dictionary<int, string>();
        private int allocatedSpawnPointIndex = -1;
        private Transform allocatedSpawnPoint { get { return allocatedSpawnPointIndex >= 0 ? playerSpawnPoints[allocatedSpawnPointIndex] : null; } }
        private Inhumate.RTI.Client.UntypedListener spawnListener;
        private float startTime;

        public Dictionary<string, RTIEntity> entityTypes = new Dictionary<string, RTIEntity>();

        public bool requestUpdatesOnStart = true;

        public event Action<RTIEntity> OnEntityCreated;
        public event Action<GameObject> OnSpawnPlayer;

        void Awake() {
            UnityEngine.Random.InitState(RTI.ClientId.GetHashCode());
            foreach (var spawnable in spawnableEntities) {
                if (spawnable.prefab.gameObject.scene.name != null) Debug.LogWarning($"Seems like entity {spawnable.prefab.gameObject.name} type {spawnable.type} is not a prefab!", this);
                entityTypes.Add(spawnable.type, spawnable.prefab);
            }
            if (unknownPrefab != null) {
                unknownEntity = unknownPrefab.GetComponent<RTIEntity>();
                if (unknownEntity == null) Debug.LogError($"Unkown prefab {unknownPrefab.name} doesn't have an RTIEntity component", this);
            }
            if (playerPrefab != null) {
                playerEntity = playerPrefab.GetComponent<RTIEntity>();
                if (playerEntity == null) Debug.LogError($"Player prefab {playerPrefab.name} doesn't have an RTIEntity component", this);
            }
        }

        void Start() {
            RTI.OnStart += OnStart;
            if (RTI.state == RuntimeState.Unknown) OnStart();
            if (playerSpawnPoints.Length > 1) {
                spawnListener = RTI.Subscribe(SpawnAllocationChannel, OnSpawnAllocation);
                RTI.Publish(SpawnAllocationChannel, "?");
                StartCoroutine(RandomDelayAllocateSpawnPoint());
            }
        }

        void OnDestroy() {
            RTI.OnStart -= OnStart;
            if (spawnListener != null) RTI.Unsubscribe(spawnListener);
        }

        void OnStart() {
            startTime = Time.time;
            if (player == null && playerEntity != null && playerSpawnPoints.Length <= 1) {
                // Not allocating spawn point... just spawn
                var spawnPoint = this.transform;
                if (playerSpawnPoints.Length == 1) spawnPoint = playerSpawnPoints[0];
                SpawnPlayer(spawnPoint);
            }
            if (requestUpdatesOnStart) {
                RTI.WhenConnected(RequestUpdates);
            }
        }

        void Update() {
            if ((RTI.state == RuntimeState.Running || RTI.state == RuntimeState.Unknown) && player == null && playerEntity != null && playerSpawnPoints.Length > 1) {
                if (allocatedSpawnPoint == null && Time.time - startTime > 2f && spawnPointAllocations.Count < playerSpawnPoints.Length) {
                    Debug.LogError("Spawn point allocation timeout");
                    AllocateSpawnPoint();
                }
                if (allocatedSpawnPoint != null) {
                    SpawnPlayer(allocatedSpawnPoint);
                }
            }
        }

        void SpawnPlayer(Transform spawnPoint) {
            if (RTI.debugEntities) Debug.Log($"Spawning player at {spawnPoint.name}", this);
            player = Instantiate(playerEntity.gameObject, spawnPoint.position, spawnPoint.rotation);
            player.transform.parent = this.transform;
            OnSpawnPlayer?.Invoke(player);
        }

        private IEnumerator RandomDelayAllocateSpawnPoint() {
            yield return new WaitForSecondsRealtime(UnityEngine.Random.Range(0.2f, 0.5f));
            AllocateSpawnPoint();
        }

        private void AllocateSpawnPoint() {
            for (int i = 0; i < playerSpawnPoints.Length; i++) {
                if (!spawnPointAllocations.ContainsKey(i)) {
                    RTI.Publish(SpawnAllocationChannel, $"{i} {RTI.ClientId}");
                    if (RTI.debugEntities) Debug.Log($"Allocating spawn point {i}");
                    allocatedSpawnPointIndex = i;
                    break;
                }
            }
        }

        private void RequestUpdates() {
            RTI.Publish(RTIConstants.EntityChannel, new EntityOperation {
                RequestUpdate = new Google.Protobuf.WellKnownTypes.Empty()
            });
        }

        protected void OnSpawnAllocation(string channel, object message) {
            if (message.ToString() == "?") {
                if (allocatedSpawnPoint != null) RTI.Publish(SpawnAllocationChannel, $"{allocatedSpawnPointIndex} {RTI.ClientId}");
            } else {
                var parts = message.ToString().Split(' ');
                var index = int.Parse(parts[0]);
                var clientId = parts[1];
                if (index == allocatedSpawnPointIndex && clientId != RTI.ClientId) {
                    Debug.LogWarning($"Spawn point conflict with {clientId}");
                    if (clientId.GetHashCode() < RTI.ClientId.GetHashCode()) StartCoroutine(RandomDelayAllocateSpawnPoint());
                }
                if (RTI.debugEntities && clientId != RTI.ClientId) Debug.Log($"Received spawn point allocation {index} {clientId}");
                spawnPointAllocations[index] = clientId;
                if (allocatedSpawnPoint == null && spawnPointAllocations.Count == playerSpawnPoints.Length) {
                    Debug.LogWarning("All spawn points are allocated");
                }
            }
        }

        protected override void OnMessage(EntityOperation message) {
            var id = message.Id;
            var entity = RTI.GetEntityById(id);
            switch (message.OperationCase) {
                case EntityOperation.OperationOneofCase.Create:
                    if (entity != null) {
                        if (!entity.owned && !entity.persistent && RTI.debugEntities) Debug.Log($"Already created entity id {id}: {entity.name}", this);
                    } else {
                        CreateEntity(message);
                    }
                    break;
                case EntityOperation.OperationOneofCase.Destroy:
                    if (entity != null && !entity.persistent) {
                        if (RTI.debugEntities) Debug.Log($"Destroy entity {id}: {entity.name}", this);
                        entity.created = false;
                        if (entity.gameObject != null) Destroy(entity.gameObject);
                        RTI.UnregisterEntity(entity);
                    }
                    break;
                case EntityOperation.OperationOneofCase.Update:
                    if (entity != null) {
                        if (!entity.persistent) {
                            if (RTI.debugEntities) Debug.Log($"Update entity {id}: {entity.name}", this);
                            entity.SetPropertiesFromEntityData(message.Update);
                            entity.InvokeOnUpdated(message.Update);
                        }
                    } else {
                        CreateEntity(message);
                    }
                    break;
            }
        }

        protected void CreateEntity(EntityOperation message) {
            var data = message.Create ?? message.Update;
            GameObject prefab = null;
            if (entityTypes.ContainsKey(data.Type)) {
                if (RTI.debugEntities) Debug.Log($"Create entity id {message.Id} type {data.Type}", this);
                prefab = entityTypes[data.Type].transform.root.gameObject;
            }
            if (prefab == null) {
                foreach (var key in entityTypes.Keys) {
                    if (key.Contains("*") && key.Substring(0, key.IndexOf("*")) == data.Type.Substring(0, key.IndexOf("*"))) {
                        if (RTI.debugEntities) Debug.Log($"Create entity id {message.Id} type {data.Type} (matching {key})");
                        prefab = entityTypes[key].transform.root.gameObject;
                    }
                }
            }
            if (prefab == null && unknownEntity != null) {
                Debug.LogWarning($"Create entity id {message.Id} unknown type {data.Type}", this);
                prefab = unknownEntity.transform.root.gameObject;
            }
            if (prefab == null) {
                Debug.LogWarning($"Can't create entity id {message.Id} unknown type {data.Type}", this);
                return;
            }
            var go = Instantiate(prefab, transform.position, transform.rotation);
            var entity = go.GetComponentInChildren<RTIEntity>();
            if (entity != null) {
                entity.id = message.Id;
                RTI.RegisterEntity(entity);
                entity.SetPropertiesFromEntityData(data);
                entity.created = true;
                entity.owned = message.ClientId == RTI.ClientId;
                entity.ownerClientId = message.ClientId;
                go.name = $"{entity.type} {entity.id}";
                go.transform.parent = this.transform;
                if (data.Position != null) RTIPosition.ApplyPositionMessageToTransform(data.Position, go.transform);
                entity.InvokeOnCreated(data);
                OnEntityCreated?.Invoke(entity);
            } else {
                Debug.LogWarning($"No entity component in prefab {prefab.name}", this);
            }
        }
    }

}
