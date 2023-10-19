using System.Collections.Generic;
using Google.Protobuf;
using UnityEngine;

namespace Inhumate.Unity.RTI {

    public abstract class RTITextBehaviour : MonoBehaviour {

        public abstract string ChannelName { get; }

        protected RTIConnection RTI => RTIConnection.Instance;

        private Inhumate.RTI.Client.UntypedListener listener;

        protected virtual void OnEnable() {
            listener = RTI.Subscribe(ChannelName, (channel, message) => {
                OnMessage(message != null ? message.ToString() : null);
            });
        }

        protected virtual void OnDisable() {
            RTI.Unsubscribe(listener);
        }

        protected abstract void OnMessage(string message);

        protected void Publish(string message) {
            RTI.Publish(ChannelName, message);
        }

    }

}
