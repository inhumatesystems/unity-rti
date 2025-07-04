using System.Collections.Generic;
using Google.Protobuf;
using UnityEngine;
using Inhumate.RTI.Proto;

namespace Inhumate.UnityRTI {

    [RequireComponent(typeof(RTIEntity))]
    public abstract class RTIEntityStateBehaviour<T> : MonoBehaviour where T : IMessage<T>, new() {

        public abstract string ChannelName { get; }

        public bool publishing {
            get {
                return entity != null && entity.publishing;
            }
        }

        public bool receiving {
            get {
                return entity != null && entity.receiving;
            }
        }

        protected RTIConnection RTI => RTIConnection.Instance;

        protected RTIEntity entity;

        // Note that these are not shared between derived classes, because this is a generic class.
        // Unlike the Unreal client equivalent RTIEntityStateComponent.
        private static Dictionary<string, List<RTIEntityStateBehaviour<T>>> instances = new Dictionary<string, List<RTIEntityStateBehaviour<T>>>();
        private static bool subscribed;
        private static Inhumate.RTI.UntypedListener listener;
        private static bool registeredChannel;

        private bool warnedIdNotFound;

        protected virtual void Start() {
            entity = GetComponent<RTIEntity>();
            entity.RegisterCommands(this);
            if (!instances.ContainsKey(entity.id)) instances[entity.id] = new List<RTIEntityStateBehaviour<T>>();
            if (!instances[entity.id].Contains(this)) instances[entity.id].Add(this);
            if (!subscribed) {
                if (!registeredChannel) RegisterChannel();
                listener = RTI.Subscribe<T>(ChannelName, (name, id, message) => {
                    if (instances.ContainsKey(id)) {
                        foreach (var instance in instances[id]) instance.OnMessage(message);
                    } else {
                        // Allow delaying processing one frame (happens at entity creation sometimes)
                        RTIConnection.QueueOnMainThread(() => {
                            if (instances.ContainsKey(id)) {
                                foreach (var instance in instances[id]) instance.OnMessage(message);
                            } else if (!warnedIdNotFound) {
                                Debug.LogWarning($"{this.GetType().Name} {id} not found", this);
                                warnedIdNotFound = true;
                            }
                        });
                    }
                });
                subscribed = true;
            }
        }

        protected virtual void OnDestroy() {
            if (entity != null && instances.ContainsKey(entity.id)) {
                instances[entity.id].Remove(this);
                if (instances[entity.id].Count == 0) instances.Remove(entity.id);
            }
            if (subscribed && instances.Count == 0) {
                RTI.Unsubscribe(listener);
                subscribed = false;
            }
        }

        protected abstract void OnMessage(T message);

        protected void Publish(T message) {
            if (!registeredChannel) RegisterChannel();
            RTI.Publish(ChannelName, message);
        }

        private void RegisterChannel() {
            RTI.Client.RegisterChannel(new Channel {
                Name = ChannelName,
                DataType = typeof(T).Name,
                State = true,
                FirstFieldId = true
            });
            registeredChannel = true;
        }

    }

}
