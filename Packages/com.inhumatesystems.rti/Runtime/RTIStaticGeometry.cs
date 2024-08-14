using System;
using System.Linq;
using Inhumate.RTI;
using Inhumate.RTI.Proto;
using UnityEngine;
using UnityEngine.Splines;

namespace Inhumate.Unity.RTI {

    public class RTIStaticGeometry : RTIGeometry {


        public enum GeometryShape {
            Auto,
            Point,
            Point3D,
            Line,
            Line3D,
            Spline,
            Spline3D,
            PolygonFromBounds,
            Mesh,
            MeshWithNormals,
            MeshFromColliders,
        }
        public GeometryShape shape = GeometryShape.Auto;

        public enum Coordinates {
            LocalAndGeodetic,
            Local,
            Geodetic
        }
        public Coordinates coordinates;

        public string type;
        public Geometry.Types.Category category;

        public UnityEngine.Color color;
        [Range(0, 1)]
        public float opacity = 1f;

        public string title;
        public bool titleFromName;
        public UnityEngine.Color labelColor;
        [Range(0, 1)]
        public float labelOpacity = 1f;

        public bool wireframe = false;

        public float lineWidth = 1f;

        public string id;
        public override string Id => id;

        void Awake() {
            if (string.IsNullOrWhiteSpace(id)) id = GenerateId();
        }

        string GenerateId() {
            string path = "";
            var current = transform;
            while (current != null) {
                path = "/" + current.name.Trim().Replace(" (", "").Replace(")", "").Replace(" ", "_") + path;
                current = current.parent;
            }
            var components = GetComponents<RTIStaticGeometry>();
            if (components.Length > 1) path += "-" + Array.IndexOf(components, this);
            return RTI.Application + path;
        }

        void OnEnable() {
            if (owned && published && RTI.IsConnected) Publish();
        }

        void OnDisable() {
            if (owned && published && RTI.IsConnected) Publish();
        }

        internal override Geometry GeometryData {
            get {
                useLocalCoordinates = coordinates == Coordinates.LocalAndGeodetic || coordinates == Coordinates.Local;
                useGeodeticCoordinates = coordinates == Coordinates.LocalAndGeodetic || coordinates == Coordinates.Geodetic;
                var geometry = new Geometry {
                    Id = Id,
                    OwnerClientId = RTI.ClientId,
                    Color = GetColor(color),
                    Transparency = 1 - opacity,
                    Title = !string.IsNullOrWhiteSpace(title) ? title : titleFromName ? name : "",
                    LabelColor = GetColor(labelColor),
                    LabelTransparency = 1 - labelOpacity,
                    Wireframe = wireframe,
                    LineWidth = lineWidth
                };
                GeometryShape useShape = shape;
                if (shape == GeometryShape.Auto) {
                    if (GetComponentsInChildren<MeshFilter>().Length > 0) {
                        useShape = GeometryShape.Mesh;
                    } else if (GetComponentsInChildren<Collider>().Length > 0) {
                        useShape = GeometryShape.MeshFromColliders;
                    } else if (GetComponent<LineRenderer>() != null) {
                        useShape = GeometryShape.Line3D;
                    } else if (GetComponent<SplineContainer>() != null) {
                        useShape = GeometryShape.Spline3D;
                    } else {
                        useShape = GeometryShape.Point3D;
                    }
                }
                switch (useShape) {
                    case GeometryShape.Point:
                        geometry.Point = CreatePoint2D(transform.position);
                        break;
                    case GeometryShape.Point3D:
                        geometry.Point3D = CreatePoint3D(transform.position);
                        break;
                    case GeometryShape.Line: {
                            var renderer = GetComponent<LineRenderer>();
                            if (renderer != null) {
                                geometry.Line = CreateLine2D(renderer);
                            } else {
                                Debug.LogError($"No line renderer for geometry {Id}", this);
                            }
                            break;
                        }
                    case GeometryShape.Line3D: {
                            var renderer = GetComponent<LineRenderer>();
                            if (renderer != null) {
                                geometry.Line3D = CreateLine3D(renderer);
                            } else {
                                Debug.LogError($"No line renderer for geometry {Id}", this);
                            }
                            break;
                        }
                    case GeometryShape.Spline: {
                            var splineContainer = GetComponent<SplineContainer>();
                            if (splineContainer != null) {
                                geometry.Spline = CreateSpline2D(splineContainer);
                            } else {
                                Debug.LogError($"No spline container for geometry {Id}", this);
                            }
                            break;
                        }
                    case GeometryShape.Spline3D: {
                            var splineContainer = GetComponent<SplineContainer>();
                            if (splineContainer != null) {
                                geometry.Spline3D = CreateSpline3D(splineContainer);
                            } else {
                                Debug.LogError($"No spline container for geometry {Id}", this);
                            }
                            break;
                        }
                    case GeometryShape.PolygonFromBounds: {
                            var meshFilter = GetComponent<MeshFilter>();
                            if (meshFilter != null) {
                                geometry.Polygon = CreatePolygon(meshFilter.mesh);
                                break;
                            }
                            var colliders = GetComponentsInChildren<Collider>();
                            if (colliders != null && colliders.Length > 0) {
                                geometry.Polygon = CreatePolygonFromColliders(colliders);
                                break;
                            }
                            var renderer = GetComponent<Renderer>();
                            if (renderer != null) {
                                geometry.Polygon = CreatePolygonFromBounds(renderer.bounds);
                                break;
                            }
                            Debug.LogError($"No mesh filter, colliders or renderer for polygon geometry {Id}", this);
                            break;
                        }
                    case GeometryShape.Mesh: {
                            var meshFilters = GetComponentsInChildren<MeshFilter>();
                            if (meshFilters != null && meshFilters.Length > 0) {
                                geometry.Mesh = CreateMesh(meshFilters, false);
                            } else {
                                Debug.LogError($"No mesh filter for geometry {Id}", this);
                            }
                            break;
                        }
                    case GeometryShape.MeshWithNormals: {
                            var meshFilters = GetComponentsInChildren<MeshFilter>();
                            if (meshFilters != null && meshFilters.Length > 0) {
                                geometry.Mesh = CreateMesh(meshFilters, true);
                            } else {
                                Debug.LogError($"No mesh filter for geometry {Id}", this);
                            }
                            break;
                        }
                    case GeometryShape.MeshFromColliders: {
                            var colliders = GetComponentsInChildren<Collider>();
                            if (colliders != null && colliders.Length > 0) {
                                geometry.Mesh = CreateMeshFromColliders(colliders);
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
