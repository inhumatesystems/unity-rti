using System.Collections.Generic;
using Google.Protobuf;
using UnityEngine;

namespace Inhumate.UnityRTI {

    public abstract class RTIJsonBehaviour<T> : MonoBehaviour {

        public abstract string ChannelName { get; }

        protected RTIConnection RTI => RTIConnection.Instance;

        private Inhumate.RTI.UntypedListener listener;

        protected virtual void OnEnable() {
            listener = RTI.SubscribeJson<T>(ChannelName, (channel, message) => {
                OnMessage(message);
            });
        }

        protected virtual void OnDisable() {
            RTI.Unsubscribe(listener);
        }

        protected abstract void OnMessage(T message);

        protected void Publish(T message) {
            RTI.PublishJson(ChannelName, message);
        }

    }

}
