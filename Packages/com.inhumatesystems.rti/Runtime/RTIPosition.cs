using System.Collections.Generic;
using Inhumate.RTI.Proto;
using Inhumate.RTI;
using UnityEngine;
using NaughtyAttributes;

namespace Inhumate.UnityRTI {

    public class RTIPosition : RTIEntityStateBehaviour<EntityPosition> {

        public bool publish = true;
        [ShowIf("publish")]
        public float minPublishInterval = 1f;
        [ShowIf("publish")]
        public float maxPublishInterval = 10f;
        [ShowIf("publish")]
        public float positionThreshold = 0.001f;
        [ShowIf("publish")]
        public float rotationThreshold = 0.01f;
        [ShowIf("publish")]
        public float velocityThreshold = 0.1f;

        public bool receive = true;
        [ShowIf("receive")]
        public bool interpolate = true;
        [ShowIf(EConditionOperator.And, new string[] { "receive", "interpolate" })]
        public float maxInterpolateInterval = 2f;
        [Range(0.01f, 10f)]
        [ShowIf(EConditionOperator.And, new string[] { "receive", "interpolate" })]
        public float positionSmoothing = 1f;
        [Range(0.01f, 10f)]
        [ShowIf(EConditionOperator.And, new string[] { "receive", "interpolate" })]
        public float rotationSmoothing = 1f;
        [ShowIf("receive")]
        public bool setBodyKinematic;


        public override string ChannelName => RTIChannel.Position;

        public EntityPosition lastPublishedPosition { get; private set; }
        private float lastPublishTime;
        private float lastPositionTime = -1f;
        private float previousPositionTime = -1f;
        private EntityPosition lastPosition;
        private EntityPosition previousPosition;
        private Vector3? lastVelocity;
        private Vector3? lastAcceleration;
        private Vector3? lastAngularVelocity;
        public long receiveCount { get; private set; }

        private Rigidbody body;

        private static bool warnedGeodetic;

        protected override void Start() {
            base.Start();
            body = GetComponent<Rigidbody>();
            entity.OnUpdated += OnUpdated;
        }

        protected void OnUpdated(Entity data) {
            if (receiving && receiveCount == 0 && data.Position != null) {
                OnMessage(data.Position);
            }
        }

        protected override void OnMessage(EntityPosition position) {
            receiveCount++;
            if (!receive || !receiving || !enabled) return;
            previousPositionTime = lastPositionTime;
            previousPosition = lastPosition;
            lastPositionTime = Time.time;
            lastPosition = position;
            if (!interpolate || receiveCount <= 1) {
                transform.position = GetLocalPosition(position) ?? transform.position;
                transform.rotation = GetLocalRotation(position) ?? transform.rotation;
            }
            if (position.Velocity != null) {
                if (RTIToLocalVelocity != null) {
                    lastVelocity = RTIToLocalVelocity(position.Velocity);
                } else {
                    lastVelocity = new Vector3(position.Velocity.Right, position.Velocity.Up, position.Velocity.Forward);
                }
            } else {
                lastVelocity = null;
            }
            if (position.Acceleration != null) {
                if (RTIToLocalVelocity != null) {
                    lastAcceleration = RTIToLocalVelocity(position.Acceleration);
                } else {
                    lastAcceleration = new Vector3(position.Acceleration.Right, position.Acceleration.Up, position.Acceleration.Forward);
                }
            } else {
                lastAcceleration = null;
            }
            if (position.AngularVelocity != null) {
                if (RTIToLocalAngularVelocity != null) {
                    lastAngularVelocity = RTIToLocalAngularVelocity(position.AngularVelocity);
                } else {
                    lastAngularVelocity = new Vector3(-position.AngularVelocity.Pitch, position.AngularVelocity.Yaw, -position.AngularVelocity.Roll);
                }
            } else {
                lastAngularVelocity = null;
            }
        }

        private EntityPosition lastVelocityPosition;
        private float lastVelocityTime;

        void FixedUpdate() {
            if (receive && setBodyKinematic) {
                body.isKinematic = !publishing;
            }

            Vector3? localVelocity = null;
            if (body != null && !body.isKinematic) {
                localVelocity = transform.InverseTransformDirection(body.velocity);
            } else if (lastVelocityPosition != null && lastVelocityTime > float.Epsilon && Time.fixedTime > lastVelocityTime) {
                Vector3 localLastVelocityPosition = GetLocalPosition(lastVelocityPosition) ?? transform.position;
                Vector3 velocity = (transform.position - localLastVelocityPosition) / (Time.fixedTime - lastVelocityTime);
                localVelocity = transform.InverseTransformDirection(velocity);
            }

            Vector3 localLastPosition = GetLocalPosition(lastPosition) ?? transform.position;
            Quaternion localLastRotation = GetLocalRotation(lastPosition) ?? transform.rotation;
            Vector3 localPreviousPosition = GetLocalPosition(previousPosition) ?? localLastPosition;
            Quaternion localPreviousRotation = GetLocalRotation(lastPosition) ?? localLastRotation;

            var position = PositionMessageFromTransform(transform);
            position.Id = entity.id;
            if (publish && publishing && Time.fixedTime - lastPublishTime > minPublishInterval
                    && (Time.fixedTime - lastPublishTime > maxPublishInterval
                        || positionThreshold < float.Epsilon || rotationThreshold < float.Epsilon || velocityThreshold < float.Epsilon
                        || (transform.position - localLastPosition).magnitude > positionThreshold
                        || Quaternion.Angle(transform.rotation, localLastRotation) > rotationThreshold
                        || (localVelocity.HasValue && lastVelocity.HasValue && (localVelocity.Value - lastVelocity.Value).magnitude > velocityThreshold)
                    )) {
                lastPublishTime = Time.fixedTime;
                if (localVelocity.HasValue) {
                    if (LocalToRTIVelocity != null) {
                        position.Velocity = LocalToRTIVelocity(localVelocity.Value);
                        lastAngularVelocity = Vector3.zero;
                    } else {
                        position.Velocity = new EntityPosition.Types.VelocityVector {
                            Forward = localVelocity.Value.z,
                            Up = localVelocity.Value.y,
                            Right = localVelocity.Value.x
                        };
                    }
                    lastVelocity = localVelocity;
                }
                if (body != null && !body.isKinematic) {
                    Vector3 localAngularVelocity = transform.InverseTransformDirection(body.angularVelocity) * 180.0f / Mathf.PI;
                    if (LocalToRTIAngularVelocity != null) {
                        position.AngularVelocity = LocalToRTIAngularVelocity(localAngularVelocity);
                    } else {
                        position.AngularVelocity = new EntityPosition.Types.EulerRotation {
                            Roll = -localAngularVelocity.z,
                            Pitch = -localAngularVelocity.x,
                            Yaw = localAngularVelocity.y
                        };
                    }
                    lastAngularVelocity = localAngularVelocity;
                } else {
                    lastAngularVelocity = Vector3.zero;
                }
                Publish(position);
                lastPublishedPosition = position;
                lastPosition = position;
                lastPositionTime = Time.fixedTime;
            } else if (receive && receiving && interpolate) {
                if (lastAcceleration.HasValue && lastVelocity.HasValue) {
                    lastVelocity += lastAcceleration * Time.deltaTime;
                }
                Vector3 targetPosition = transform.position;
                Quaternion targetRotation = transform.rotation;
                if (Time.fixedTime - lastPositionTime < maxInterpolateInterval) {
                    if (lastPositionTime > 0 && lastVelocity.HasValue) {
                        // Interpolate using velocity
                        targetPosition = localLastPosition + transform.TransformDirection(lastVelocity.Value * (Time.fixedTime - lastPositionTime));
                    } else if (lastPositionTime > 0 && previousPositionTime > 0 && lastPositionTime - previousPositionTime > 1e-5f && lastPositionTime - previousPositionTime < minPublishInterval * 2.5f) {
                        // or else lerp based on last and previous position
                        targetPosition = Vector3.Lerp(localLastPosition, localLastPosition + (localLastPosition - localPreviousPosition), (Time.fixedTime - lastPositionTime) / (lastPositionTime - previousPositionTime));
                    } else if (lastPositionTime > 0) {
                        // or else just teleport
                        targetPosition = localLastPosition;
                    }
                    if (lastPositionTime > 0 && lastAngularVelocity.HasValue) {
                        // Interpolate using angular velocity
                        targetRotation = Quaternion.Euler(lastAngularVelocity.Value * (Time.time - lastPositionTime)) * localLastRotation;
                    } else if (lastPositionTime > 0 && previousPositionTime > 0 && lastPositionTime - previousPositionTime > 1e-5f && lastPositionTime - previousPositionTime < minPublishInterval * 2.5f) {
                        // or else slerp based on last and previous rotation
                        targetRotation = Quaternion.Slerp(localLastRotation, (localLastRotation * Quaternion.Inverse(localPreviousRotation)) * localLastRotation, (Time.time - lastPositionTime) / (lastPositionTime - previousPositionTime));
                    } else if (lastPositionTime > 0) {
                        // or else just set rotation
                        targetRotation = localLastRotation;
                    }
                } else {
                    targetPosition = localLastPosition;
                    targetRotation = localLastRotation;
                    lastVelocity = null;
                    lastAcceleration = null;
                    lastAngularVelocity = null;
                }
                transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * 10f / positionSmoothing);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 10f / rotationSmoothing);
            }
            if (Time.fixedTime - lastVelocityTime >= 10 * Time.fixedDeltaTime || Time.fixedTime < lastVelocityTime) {
                lastVelocityPosition = position;
                lastVelocityTime = Time.fixedTime;
            }
        }

        public static Vector3? GetLocalPosition(EntityPosition position) {
            if (position == null) return null;
            if (position.Local != null && UseLocalCoordinates) {
                if (RTIToLocalPosition != null) {
                    return RTIToLocalPosition(position.Local);
                } else {
                    return new Vector3(position.Local.X, position.Local.Y, position.Local.Z);
                }
            } else if (position.Geodetic != null) {
                if (GeodeticToLocal != null) {
                    return GeodeticToLocal(position.Geodetic);
                } else if (!warnedGeodetic) {
                    Debug.LogWarning("Cannot use geodetic position, RTIPosition.GeodeticToLocal not set");
                    warnedGeodetic = true;
                }
            }
            return null;
        }

        public static Quaternion? GetLocalRotation(EntityPosition position) {
            if (position == null) return null;
            if (position.LocalRotation != null && UseLocalCoordinates) {
                return new Quaternion(position.LocalRotation.X, position.LocalRotation.Y, position.LocalRotation.Z, position.LocalRotation.W);
            } else if (position.EulerRotation != null) {
                if (RTIToLocalEuler != null) {
                    return RTIToLocalEuler(position.EulerRotation);
                } else {
                    return Quaternion.Euler(-position.EulerRotation.Pitch, position.EulerRotation.Yaw, -position.EulerRotation.Roll);
                }
            }
            return null;
        }

        public static EntityPosition PositionMessageFromTransform(Transform transform) {
            var euler = transform.eulerAngles;
            var position = new EntityPosition();
            if (LocalToRTIPosition != null) {
                position.Local = LocalToRTIPosition(transform.position);
            } else if (UseLocalCoordinates) {
                position.Local = new EntityPosition.Types.LocalPosition {
                    X = transform.position.x,
                    Y = transform.position.y,
                    Z = transform.position.z
                };
            }
            if (LocalToRTIEuler != null) {
                position.EulerRotation = LocalToRTIEuler(transform.rotation);
            } else {
                if (UseLocalCoordinates) {
                    position.LocalRotation = new EntityPosition.Types.LocalRotation {
                        X = transform.rotation.x,
                        Y = transform.rotation.y,
                        Z = transform.rotation.z,
                        W = transform.rotation.w
                    };
                }
                position.EulerRotation = new EntityPosition.Types.EulerRotation {
                    Roll = -euler.z,
                    Pitch = -euler.x,
                    Yaw = euler.y
                };
            }
            if (LocalToGeodetic != null) {
                position.Geodetic = LocalToGeodetic(transform.position);
            }
            return position;
        }

        public static void ApplyPositionMessageToTransform(EntityPosition position, Transform transform) {
            if (position.Local != null) {
                if (RTIToLocalPosition != null) {
                    transform.position = RTIToLocalPosition(position.Local);
                } else {
                    transform.position = new Vector3(position.Local.X, position.Local.Y, position.Local.Z);
                }
            } else if (position.Geodetic != null) {
                if (GeodeticToLocal != null) {
                    transform.position = GeodeticToLocal(position.Geodetic);
                } else if (!warnedGeodetic) {
                    Debug.LogWarning("Cannot use geodetic position, RTIPosition.GeodeticToLocal not set");
                    warnedGeodetic = true;
                }
            }
            if (position.EulerRotation != null && RTIToLocalEuler != null) {
                transform.rotation = RTIToLocalEuler(position.EulerRotation);
            } else if (position.LocalRotation != null) {
                transform.rotation = new Quaternion(position.LocalRotation.X, position.LocalRotation.Y, position.LocalRotation.Z, position.LocalRotation.W);
            } else if (position.EulerRotation != null) {
                transform.rotation = Quaternion.Euler(-position.EulerRotation.Pitch, position.EulerRotation.Yaw, -position.EulerRotation.Roll);
            }
        }

        public delegate EntityPosition.Types.GeodeticPosition LocalToGeodeticConversion(Vector3 local);
        public static LocalToGeodeticConversion LocalToGeodetic;

        public delegate Vector3 GeodeticToLocalConversion(EntityPosition.Types.GeodeticPosition position);
        public static GeodeticToLocalConversion GeodeticToLocal;

        public delegate EntityPosition.Types.LocalPosition LocalToRTIConversion(Vector3 local);
        public static LocalToRTIConversion LocalToRTIPosition;

        public delegate Vector3 RTIToLocalConversion(EntityPosition.Types.LocalPosition rti);
        public static RTIToLocalConversion RTIToLocalPosition;

        public delegate EntityPosition.Types.EulerRotation LocalToRTIEulerConversion(Quaternion local);
        public static LocalToRTIEulerConversion LocalToRTIEuler;

        public delegate Quaternion RTIEulerToLocalConversion(EntityPosition.Types.EulerRotation euler);
        public static RTIEulerToLocalConversion RTIToLocalEuler;

        public delegate EntityPosition.Types.VelocityVector LocalToRTIVelocityConversion(Vector3 local);
        public static LocalToRTIVelocityConversion LocalToRTIVelocity;

        public delegate Vector3 RTIToLocalVelocityConversion(EntityPosition.Types.VelocityVector rti);
        public static RTIToLocalVelocityConversion RTIToLocalVelocity;

        public delegate EntityPosition.Types.EulerRotation LocalToRTIAngularVelocityConversion(Vector3 local);
        public static LocalToRTIAngularVelocityConversion LocalToRTIAngularVelocity;

        public delegate Vector3 RTIToLocalAngularVelocityConversion(EntityPosition.Types.EulerRotation angularEuler);
        public static RTIToLocalAngularVelocityConversion RTIToLocalAngularVelocity;

        public static bool UseLocalCoordinates = true;

    }

}
