using System.Collections.Generic;
using Inhumate.RTI.Proto;
using Inhumate.RTI.Client;
using UnityEngine;
using NaughtyAttributes;

namespace Inhumate.Unity.RTI {

    public class RTIPosition : RTIEntityBehaviour<EntityPosition> {

        public bool publish = true;
        [ShowIf("publish")]
        public float updateInterval = 1f;
        [ShowIf("publish")]
        public float minPublishInterval = 10f;
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


        public override string ChannelName => RTIConstants.PositionChannel;
        public override bool Stateless => true;

        private float lastPublishTime;
        private float lastPositionTime = -1f;
        private float previousPositionTime = -1f;
        private Vector3 lastPosition;
        private Vector3 previousPosition;
        private Vector3? lastVelocity;
        private Vector3? lastAcceleration;
        private float lastRotationTime = -1f;
        private float previousRotationTime = -1f;
        private Quaternion lastRotation;
        private Quaternion previousRotation;
        private Vector3? lastAngularVelocity;
        public long receiveCount { get; private set; }

        private Rigidbody body;

        private static bool warnedGeodetic;

        protected override void Start() {
            base.Start();
            body = GetComponent<Rigidbody>();
            lastPosition = transform.position;
            lastRotation = transform.rotation;
            lastPositionTime = Time.time;
            lastRotationTime = Time.time;
            entity.OnUpdated += OnUpdated;
        }

        protected void OnUpdated(EntityOperation.Types.EntityData data) {
            if (receiving && receiveCount == 0) {
                OnMessage(data.Position);
            }
        }

        protected override void OnMessage(EntityPosition position) {
            receiveCount++;
            if (!receive || !receiving || !enabled) return;
            if (position.Local != null) {
                previousPositionTime = lastPositionTime;
                previousPosition = lastPosition;
                lastPositionTime = Time.time;
                if (RTIToLocalPosition != null) {
                    lastPosition = RTIToLocalPosition(position.Local);
                } else {
                    lastPosition = new Vector3(position.Local.X, position.Local.Y, position.Local.Z);
                }
                if (!interpolate || receiveCount == 1) transform.position = previousPosition = lastPosition;
            } else if (position.Geodetic != null) {
                if (GeodeticToLocal != null) {
                    previousPositionTime = lastPositionTime;
                    previousPosition = lastPosition;
                    lastPositionTime = Time.time;
                    lastPosition = GeodeticToLocal(position.Geodetic);
                    if (!interpolate || receiveCount == 1) transform.position = previousPosition = lastPosition;
                } else if (!warnedGeodetic) {
                    Debug.LogWarning("Cannot use geodetic position, RTIPosition.GeodeticToLocal not set");
                    warnedGeodetic = true;
                }
            }
            if (position.LocalRotation != null) {
                previousRotationTime = lastRotationTime;
                previousRotation = lastRotation;
                lastRotationTime = Time.time;
                lastRotation = new Quaternion(position.LocalRotation.X, position.LocalRotation.Y, position.LocalRotation.Z, position.LocalRotation.W);
                if (!interpolate || receiveCount == 1) transform.rotation = previousRotation = lastRotation;
            } else if (position.EulerRotation != null) {
                previousRotationTime = lastRotationTime;
                previousRotation = lastRotation;
                lastRotationTime = Time.time;
                if (RTIToLocalEuler != null) {
                    lastRotation = RTIToLocalEuler(position.EulerRotation);
                } else {
                    lastRotation = Quaternion.Euler(-position.EulerRotation.Pitch, position.EulerRotation.Yaw, -position.EulerRotation.Roll);
                }
                if (!interpolate || receiveCount == 1) transform.rotation = previousRotation = lastRotation;
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

        private Vector3 lastVelocityPosition;
        private float lastVelocityTime;

        void FixedUpdate() {
            if (receive && setBodyKinematic) {
                body.isKinematic = !publishing;
            }

            Vector3? localVelocity = null;
            if (body != null && !body.isKinematic) {
                localVelocity = transform.InverseTransformDirection(body.velocity);
            } else if (lastVelocityPosition.sqrMagnitude > float.Epsilon && lastVelocityTime > float.Epsilon && Time.fixedTime > lastVelocityTime) {
                Vector3 velocity = (transform.position - lastVelocityPosition) / (Time.fixedTime - lastVelocityTime);
                localVelocity = transform.InverseTransformDirection(velocity);
            }

            if (publish && publishing && Time.fixedTime - lastPublishTime > updateInterval
                    && (Time.fixedTime - lastPublishTime > minPublishInterval 
                        || positionThreshold < float.Epsilon || rotationThreshold < float.Epsilon || velocityThreshold < float.Epsilon
                        || (transform.position - lastPosition).magnitude > positionThreshold
                        || Quaternion.Angle(transform.rotation, lastRotation) > rotationThreshold
                        || (localVelocity.HasValue && lastVelocity.HasValue && (localVelocity.Value - lastVelocity.Value).magnitude > velocityThreshold)
                    )) {
                lastPublishTime = Time.fixedTime;
                var position = PositionMessageFromTransform(transform);
                position.Id = entity.id;
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
                lastPosition = transform.position;
                lastPositionTime = Time.fixedTime;
                lastRotation = transform.rotation;
                lastRotationTime = Time.fixedTime;
            } else if (receive && receiving && interpolate) {
                if (lastAcceleration.HasValue && lastVelocity.HasValue) {
                    lastVelocity += lastAcceleration * Time.deltaTime;
                }
                Vector3 targetPosition = transform.position;
                if (Time.fixedTime - lastPositionTime < maxInterpolateInterval) {
                    if (lastPositionTime > 0 && lastVelocity.HasValue) {
                        // Interpolate using velocity
                        targetPosition = lastPosition + transform.TransformDirection(lastVelocity.Value * (Time.fixedTime - lastPositionTime));
                    } else if (lastPositionTime > 0 && previousPositionTime > 0 && lastPositionTime - previousPositionTime > 1e-5f && lastPositionTime - previousPositionTime < updateInterval * 2.5f) {
                        // or else lerp based on last and previous position
                        targetPosition = Vector3.Lerp(lastPosition, lastPosition + (lastPosition - previousPosition), (Time.fixedTime - lastPositionTime) / (lastPositionTime - previousPositionTime));
                    } else if (lastPositionTime > 0) {
                        // or else just teleport
                        targetPosition = lastPosition;
                    }
                } else {
                    targetPosition = lastPosition;
                    lastVelocity = null;
                    lastAcceleration = null;
                    lastAngularVelocity = null;
                }
                transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * 10f / positionSmoothing);

                Quaternion targetRotation = transform.rotation;
                if (Time.fixedTime - lastRotationTime < maxInterpolateInterval) {
                    if (lastRotationTime > 0 && lastAngularVelocity.HasValue) {
                        // Interpolate using angular velocity
                        targetRotation = Quaternion.Euler(lastAngularVelocity.Value * (Time.time - lastRotationTime)) * lastRotation;
                    } else if (lastRotationTime > 0 && previousRotationTime > 0 && lastRotationTime - previousRotationTime > 1e-5f && lastRotationTime - previousRotationTime < updateInterval * 2.5f) {
                        // or else slerp based on last and previous rotation
                        targetRotation = Quaternion.Slerp(lastRotation, (lastRotation * Quaternion.Inverse(previousRotation)) * lastRotation, (Time.time - lastRotationTime) / (lastRotationTime - previousRotationTime));
                    } else if (lastRotationTime > 0) {
                        // or else just set rotation
                        targetRotation = lastRotation;
                    }
                } else {
                    targetRotation = lastRotation;
                    lastVelocity = null;
                    lastAcceleration = null;
                    lastAngularVelocity = null;
                }
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 10f / rotationSmoothing);
            }
            if (Time.fixedTime - lastVelocityTime >= 10 * Time.fixedDeltaTime || Time.fixedTime < lastVelocityTime) {
                lastVelocityPosition = transform.position;
                lastVelocityTime = Time.fixedTime;
            }
        }

        public static EntityPosition PositionMessageFromTransform(Transform transform) {
            var euler = transform.eulerAngles;
            var position = new EntityPosition();
            if (LocalToRTIPosition != null) {
                position.Local = LocalToRTIPosition(transform.position);
            } else {
                position.Local = new EntityPosition.Types.LocalPosition {
                    X = transform.position.x,
                    Y = transform.position.y,
                    Z = transform.position.z
                };
            }
            if (LocalToRTIEuler != null) {
                position.EulerRotation = LocalToRTIEuler(transform.rotation);
            } else {
                position.LocalRotation = new EntityPosition.Types.LocalRotation {
                    X = transform.rotation.x,
                    Y = transform.rotation.y,
                    Z = transform.rotation.z,
                    W = transform.rotation.w
                };
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

    }

}
