using Inhumate.RTI.Proto;
using UnityEngine;

namespace Inhumate.UnityRTI {

    public class RTIActiveOnState : MonoBehaviour {

        public bool unknown;
        public bool initial;
        public bool loading;
        public bool ready;
        public bool running;
        public bool playback;
        public bool paused;
        public bool playbackPaused;
        public bool end;
        public bool playbackEnd;
        public bool stopped;
        public bool playbackStopped;
        public bool other;

        void Start() {
            RTIConnection.Instance.OnStateChanged += Apply;
            Apply(RTIConnection.Instance.state);
        }

        void OnDestroy() {
            RTIConnection.Instance.OnStateChanged -= Apply;
        }

        void Apply(RuntimeState state) {
            switch (state) {
                case RuntimeState.Unknown: gameObject.SetActive(unknown); break;
                case RuntimeState.Loading: gameObject.SetActive(loading); break;
                case RuntimeState.Ready: gameObject.SetActive(ready); break;
                case RuntimeState.Running: gameObject.SetActive(running); break;
                case RuntimeState.Playback: gameObject.SetActive(playback); break;
                case RuntimeState.Paused: gameObject.SetActive(paused); break;
                case RuntimeState.PlaybackPaused: gameObject.SetActive(playbackPaused); break;
                case RuntimeState.End: gameObject.SetActive(end); break;
                case RuntimeState.PlaybackEnd: gameObject.SetActive(playbackEnd); break;
                case RuntimeState.Stopped: gameObject.SetActive(stopped); break;
                case RuntimeState.PlaybackStopped: gameObject.SetActive(playbackStopped); break;
                default: gameObject.SetActive(other); break;
            }
        }

    }

}
