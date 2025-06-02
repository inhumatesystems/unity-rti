using Inhumate.RTI.Proto;
using UnityEngine;

namespace Inhumate.UnityRTI {

    public class RTIMeasure : MonoBehaviour {

        public string id;
        public string title;
        public string unit;
        public string channel;
        public float interval;

        private Measure _measure;
        public Measure measure {
            get {
                if (_measure == null) {
                    _measure = new Measure {
                        Id = id,
                        Title = title,
                        Unit = unit,
                        Channel = channel,
                        Interval = interval
                    };
                }
                return _measure;
            }
        }

        public void Measure(float value) {
            if (RTIConnection.Instance == null || RTIConnection.Instance.Client == null) return;
            var state = RTIConnection.Instance.state;
            switch (state) {
                case RuntimeState.Initial:
                case RuntimeState.Unknown:
                case RuntimeState.Running:
                    break;
                default:
                    return;
            }
            RTIConnection.Instance.Client.Measure(measure, value);
        }
    }
}
