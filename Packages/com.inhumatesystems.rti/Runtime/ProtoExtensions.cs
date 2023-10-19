using UnityEngine;

namespace Inhumate.Unity.RTI {

    public static class ProtoExtensions {
        public static Proto.Vector3 ToProto(this Vector3 vector) {
            return new Proto.Vector3 {
                X = vector.x,
                Y = vector.y,
                Z = vector.z
            };
        }

        public static Vector3 ToVector3(this Proto.Vector3 vector) {
            return new Vector3(vector.X, vector.Y, vector.Z);
        }
    }

}
