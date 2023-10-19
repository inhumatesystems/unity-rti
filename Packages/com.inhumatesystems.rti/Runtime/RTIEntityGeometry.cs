using System;
using Inhumate.RTI.Proto;
using UnityEngine;

namespace Inhumate.Unity.RTI {

    public class RTIEntityGeometry : RTIGeometry {

        public string entityType;
        public override string Id => entityType;

        public enum GeometryShape {
            Auto,
            Point3D,
            Mesh,
            MeshWithNormals,
            MeshFromColliders,
        }
        public GeometryShape shape = GeometryShape.Auto;

        public bool scalable = true;

        public UnityEngine.Color color;
        [Range(0, 1)]
        public float opacity = 1f;

        public UnityEngine.Color labelColor;
        [Range(0, 1)]
        public float labelOpacity = 1f;

        public bool wireframe = false;

        void Awake() {
            if (string.IsNullOrWhiteSpace(entityType)) entityType = name;
            useGeodeticCoordinates = false;
        }

        protected override void Start() {
            base.Start();
            var renderers = GetComponentsInChildren<Renderer>();
            foreach (var renderer in renderers) renderer.enabled = false;
        }

        internal override GeometryOperation.Types.Geometry GeometryData {
            get {
                var geometry = new GeometryOperation.Types.Geometry {
                    Usage = GeometryOperation.Types.Usage.Entity,
                    Scalable = scalable,
                    Color = GetColor(color),
                    Transparency = 1 - opacity,
                    LabelColor = GetColor(labelColor),
                    LabelTransparency = 1 - labelOpacity,
                    Wireframe = wireframe,
                };
                GeometryShape useShape = shape;
                if (shape == GeometryShape.Auto) {
                    if (GetComponentsInChildren<MeshFilter>().Length > 0) {
                        useShape = GeometryShape.Mesh;
                    } else if (GetComponentsInChildren<Collider>().Length > 0) {
                        useShape = GeometryShape.MeshFromColliders;
                    } else {
                        useShape = GeometryShape.Point3D;
                    }
                }
                switch (useShape) {
                    case GeometryShape.Point3D: {
                            geometry.Point3D = CreatePoint3D(transform.localPosition);
                            break;
                        }
                    case GeometryShape.Mesh: {
                            var meshFilters = GetComponentsInChildren<MeshFilter>();
                            if (meshFilters != null && meshFilters.Length > 0) {
                                geometry.Mesh = CreateMesh(meshFilters, false, true);
                            } else {
                                Debug.LogError($"No mesh filter for geometry {Id}", this);
                            }
                            break;
                        }
                    case GeometryShape.MeshWithNormals: {
                            var meshFilters = GetComponentsInChildren<MeshFilter>();
                            if (meshFilters != null && meshFilters.Length > 0) {
                                geometry.Mesh = CreateMesh(meshFilters, true, true);
                            } else {
                                Debug.LogError($"No mesh filter for geometry {Id}", this);
                            }
                            break;
                        }
                    case GeometryShape.MeshFromColliders: {
                            var colliders = GetComponentsInChildren<Collider>();
                            if (colliders != null && colliders.Length > 0) {
                                geometry.Mesh = CreateMeshFromColliders(colliders, true);
                            } else {
                                Debug.LogError($"No colliders for geometry {Id}", this);
                            }
                            break;
                        }
                }
                return geometry;
            }
        }
 
    }
}
