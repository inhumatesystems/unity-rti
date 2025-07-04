using System.Collections.Generic;
using Google.Protobuf;
using UnityEngine;

namespace Inhumate.UnityRTI {

    public abstract class RTIBehaviour<T> : MonoBehaviour where T : IMessage<T>, new() {

        public abstract string ChannelName { get; }

        protected RTIConnection RTI => RTIConnection.Instance;

        private Inhumate.RTI.UntypedListener listener;

        protected virtual void OnEnable() {
            listener = RTI.Subscribe<T>(ChannelName, (channel, message) => {
                OnEntity(message);
            });
        }

        protected virtual void OnDisable() {
            RTI.Unsubscribe(listener);
        }

        protected abstract void OnEntity(T message);

        protected void Publish(T message) {
            RTI.Publish(ChannelName, message);
        }

    }

}
