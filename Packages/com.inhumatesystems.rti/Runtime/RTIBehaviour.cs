using System.Collections.Generic;
using Google.Protobuf;
using UnityEngine;

namespace Inhumate.Unity.RTI {

    public abstract class RTIBehaviour<T> : MonoBehaviour where T : IMessage<T>, new() {

        public abstract string ChannelName { get; }

        protected RTIConnection RTI => RTIConnection.Instance;

        private Inhumate.RTI.Client.UntypedListener listener;

        protected virtual void OnEnable() {
            listener = RTI.Subscribe<T>(ChannelName, (channel, message) => {
                OnMessage(message);
            });
        }

        protected virtual void OnDisable() {
            RTI.Unsubscribe(listener);
        }

        protected abstract void OnMessage(T message);

        protected void Publish(T message) {
            RTI.Publish(ChannelName, message);
        }

    }

}
