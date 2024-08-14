using System;
using System.Linq;
using Inhumate.RTI;
using Inhumate.RTI.Proto;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

namespace Inhumate.Unity.RTI {

    public abstract class RTIGeometry : MonoBehaviour {

        abstract public string Id { get; }

        public bool persistent { get; internal set; } = true;
        public bool published { get; internal set; }
        public bool owned { get; internal set; } = true;

        protected RTIConnection RTI => RTIConnection.Instance;

        protected bool useLocalCoordinates = true;
        protected bool useGeodeticCoordinates = true;
        protected bool updateRequested;
        protected bool registered;

        public void RequestUpdate() {
            updateRequested = true;
        }

        protected virtual void Start() {
            if (RTI.GetGeometryById(Id)) {
                Debug.LogWarning($"Geometry {Id} has already been registered", this);
            }
            registered = RTI.RegisterGeometry(this);
        }

        protected virtual void Update() {
            if (registered && owned && RTI.IsConnected) {
                if (!published) {
                    if (persistent && RTI.persistentGeometryOwnerClientId != null && !RTI.IsPersistentGeometryOwner) {
                        owned = false;
                        //ownerClientId = RTI.persistentGeometryOwnerClientId;
                    } else {
                        if (RTI.debugEntities) Debug.Log($"RTI publish create geometry {Id}", this);
                        RTI.Publish(RTIChannel.Geometry, GeometryData);
                    }
                    published = true;
                } else if (updateRequested) {
                    updateRequested = false;
                    Publish();
                }
            }
        }

        protected void Publish() {
            RTI.Publish(RTIChannel.Geometry, GeometryData);
        }

        void OnDestroy() {
            bool hopefullyOtherClientTakesOver = RTI.quitting && persistent && RTI.Client.KnownClients.Count(c => c.Application == RTI.Client.Application) > 1;
            if (registered && published && owned && RTI.IsConnected && !hopefullyOtherClientTakesOver) {
                published = false;
                if (RTI.debugEntities) Debug.Log($"RTI publish deleted geometry {Id}");
                var data = GeometryData;
                data.Deleted = true;
                RTI.Publish(RTIChannel.Geometry, data);
            }
            RTI.UnregisterGeometry(this);
        }

        void OnApplicationQuit() {
            if (published && owned && !persistent) OnDestroy();
        }

        abstract internal Geometry GeometryData { get; }

        protected Inhumate.RTI.Proto.Color GetColor(UnityEngine.Color color) {
            Inhumate.RTI.Proto.Color col = null;
            if (color.a > 1e-5 || color.maxColorComponent > 1e-5) {
                col = new Inhumate.RTI.Proto.Color {
                    Red = (int)Math.Round(color.r * 255),
                    Green = (int)Math.Round(color.g * 255),
                    Blue = (int)Math.Round(color.b * 255)
                };
            }
            return col;
        }

        protected Geometry.Types.Point2D CreatePoint2D(Vector3 position) {
            var point = new Geometry.Types.Point2D();
            if (useLocalCoordinates) {
                point.Local = new Geometry.Types.LocalPoint2D {
                    X = position.x,
                    Y = position.z
                };
            }
            if (useGeodeticCoordinates && RTIPosition.LocalToGeodetic != null) {
                point.Geodetic = GeodeticPoint2D(RTIPosition.LocalToGeodetic(position));
            }
            return point;
        }

        protected Geometry.Types.Point3D CreatePoint3D(Vector3 position) {
            var point = new Geometry.Types.Point3D();
            if (useLocalCoordinates) {
                point.Local = new Geometry.Types.LocalPoint3D {
                    X = position.x,
                    Y = position.y,
                    Z = position.z
                };
            }
            if (useGeodeticCoordinates && RTIPosition.LocalToGeodetic != null) {
                point.Geodetic = GeodeticPoint3D(RTIPosition.LocalToGeodetic(position));
            }
            return point;
        }

        protected Geometry.Types.GeodeticPoint2D GeodeticPoint2D(EntityPosition.Types.GeodeticPosition position) {
            return new Geometry.Types.GeodeticPoint2D {
                Latitude = position.Latitude,
                Longitude = position.Longitude
            };
        }

        protected Geometry.Types.GeodeticPoint3D GeodeticPoint3D(EntityPosition.Types.GeodeticPosition position) {
            return new Geometry.Types.GeodeticPoint3D {
                Latitude = position.Latitude,
                Longitude = position.Longitude,
                Altitude = position.Altitude
            };
        }

        protected Geometry.Types.Line2D CreateLine2D(LineRenderer renderer) {
            var line = new Geometry.Types.Line2D();
            for (var i = 0; i < renderer.positionCount; i++) {
                var position = renderer.GetPosition(i);
                if (!renderer.useWorldSpace) position = transform.TransformPoint(position);
                line.Points.Add(CreatePoint2D(position));
            }
            if (renderer.loop) line.Points.Add(CreatePoint2D(renderer.GetPosition(0)));
            return line;
        }

        protected Geometry.Types.Line3D CreateLine3D(LineRenderer renderer) {
            var line = new Geometry.Types.Line3D();
            for (var i = 0; i < renderer.positionCount; i++) {
                var position = renderer.GetPosition(i);
                if (!renderer.useWorldSpace) position = transform.TransformPoint(position);
                line.Points.Add(CreatePoint3D(position));
            }
            if (renderer.loop) line.Points.Add(CreatePoint3D(renderer.GetPosition(0)));
            return line;
        }

        protected Geometry.Types.Spline2D CreateSpline2D(SplineContainer splineContainer) {
            var gspline = new Geometry.Types.Spline2D();
            var uspline = splineContainer.Spline;
            foreach (var knot in uspline) {
                gspline.Points.Add(CreatePoint2D(transform.TransformPoint(knot.Position)));
                gspline.ControlPoints.Add(CreatePoint2D(transform.TransformPoint(knot.Position + math.rotate(knot.Rotation, knot.TangentOut))));
            }
            if (uspline.Closed) {
                var first = uspline.First();
                gspline.Points.Add(CreatePoint2D(transform.TransformPoint(first.Position)));
                gspline.ControlPoints.Add(CreatePoint2D(transform.TransformPoint(first.Position + math.rotate(first.Rotation, first.TangentOut))));
            }
            return gspline;
        }

        protected Geometry.Types.Spline3D CreateSpline3D(SplineContainer splineContainer) {
            var gspline = new Geometry.Types.Spline3D();
            var uspline = splineContainer.Spline;
            foreach (var knot in uspline) {
                gspline.Points.Add(CreatePoint3D(transform.TransformPoint(knot.Position)));
                gspline.ControlPoints.Add(CreatePoint3D(transform.TransformPoint(knot.Position + math.rotate(knot.Rotation, knot.TangentOut))));
            }
            if (uspline.Closed) {
                var first = uspline.First();
                gspline.Points.Add(CreatePoint3D(transform.TransformPoint(first.Position)));
                gspline.ControlPoints.Add(CreatePoint3D(transform.TransformPoint(first.Position + math.rotate(first.Rotation, first.TangentOut))));
            }
            return gspline;
        }

        protected Geometry.Types.Polygon CreatePolygon(Mesh mesh) {
            var polygon = new Geometry.Types.Polygon();
            var bounds = mesh.bounds;
            var corner1 = transform.TransformPoint(bounds.min.x, 0, bounds.min.z);
            var corner2 = transform.TransformPoint(bounds.min.x, 0, bounds.max.z);
            var corner3 = transform.TransformPoint(bounds.max.x, 0, bounds.max.z);
            var corner4 = transform.TransformPoint(bounds.max.x, 0, bounds.min.z);
            polygon.Points.Add(CreatePoint2D(corner1));
            polygon.Points.Add(CreatePoint2D(corner2));
            polygon.Points.Add(CreatePoint2D(corner3));
            polygon.Points.Add(CreatePoint2D(corner4));
            polygon.Base = transform.TransformPoint(bounds.min).y;
            polygon.Height = transform.TransformPoint(bounds.max).y - polygon.Base;
            return polygon;
        }

        protected Geometry.Types.Polygon CreatePolygonFromColliders(Collider[] colliders) {
            var polygon = new Geometry.Types.Polygon();
            var bounds = new Bounds();
            foreach (var collider in colliders) {
                bounds.Encapsulate(collider.bounds.max);
                bounds.Encapsulate(collider.bounds.min);
            }
            return CreatePolygonFromBounds(bounds);
        }

        protected Geometry.Types.Polygon CreatePolygonFromBounds(Bounds bounds) {
            var polygon = new Geometry.Types.Polygon();
            var corner1 = new Vector3(bounds.min.x, 0, bounds.min.z);
            var corner2 = new Vector3(bounds.min.x, 0, bounds.max.z);
            var corner3 = new Vector3(bounds.max.x, 0, bounds.max.z);
            var corner4 = new Vector3(bounds.max.x, 0, bounds.min.z);
            polygon.Points.Add(CreatePoint2D(corner1));
            polygon.Points.Add(CreatePoint2D(corner2));
            polygon.Points.Add(CreatePoint2D(corner3));
            polygon.Points.Add(CreatePoint2D(corner4));
            polygon.Base = bounds.min.y;
            polygon.Height = bounds.max.y - polygon.Base;
            return polygon;
        }
        protected Geometry.Types.Mesh CreateMesh(MeshFilter[] meshFilters, bool withNormals = false, bool local = false) {
            var outmesh = new Geometry.Types.Mesh();
            int offset = 0;
            foreach (var meshFilter in meshFilters) {
                var inmesh = meshFilter.mesh;
                foreach (var point in inmesh.vertices) {
                    var tpoint = meshFilter.transform.TransformPoint(point);
                    if (local && transform.parent != null) tpoint = transform.parent.InverseTransformPoint(tpoint);
                    outmesh.Vertices.Add(new Geometry.Types.LocalPoint3D {
                        X = tpoint.x,
                        Y = tpoint.y,
                        Z = tpoint.z
                    });
                }
                foreach (var index in inmesh.triangles) {
                    outmesh.Indices.Add(offset + index);
                }
                if (withNormals) {
                    foreach (var normal in inmesh.normals) {
                        var tnormal = meshFilter.transform.TransformVector(normal);
                        if (local && transform.parent != null) tnormal = transform.parent.InverseTransformVector(tnormal);
                        outmesh.Normals.Add(new Geometry.Types.LocalPoint3D {
                            X = tnormal.x,
                            Y = tnormal.y,
                            Z = tnormal.z
                        });
                    }
                }
                offset += inmesh.vertices.Length;
            }
            return outmesh;
        }

        protected Geometry.Types.Mesh CreateMeshFromColliders(Collider[] colliders, bool local = false) {
            var outmesh = new Geometry.Types.Mesh();
            int offset = 0;
            foreach (var collider in colliders) {
                if (collider is BoxCollider) {
                    BoxCollider box = (BoxCollider)collider;
                    foreach (var corner in BOX_CORNERS) {
                        var tpoint = collider.transform.TransformPoint(box.center + Vector3.Scale(corner, box.size) - box.size / 2);
                        if (local && transform.parent != null) tpoint = transform.parent.InverseTransformPoint(tpoint);
                        outmesh.Vertices.Add(new Geometry.Types.LocalPoint3D {
                            X = tpoint.x,
                            Y = tpoint.y,
                            Z = tpoint.z
                        });
                    }
                    foreach (var index in BOX_INDICES) {
                        outmesh.Indices.Add(offset + index);
                    }
                    offset += BOX_CORNERS.Length;
                } else {
                    foreach (var corner in BOX_CORNERS) {
                        var tpoint = collider.bounds.center + Vector3.Scale(corner, collider.bounds.extents) - collider.bounds.extents;
                        if (local && transform.parent != null) tpoint = transform.parent.InverseTransformPoint(tpoint);
                        outmesh.Vertices.Add(new Geometry.Types.LocalPoint3D {
                            X = tpoint.x,
                            Y = tpoint.y,
                            Z = tpoint.z
                        });
                    }
                    foreach (var index in BOX_INDICES) {
                        outmesh.Indices.Add(offset + index);
                    }
                    offset += BOX_CORNERS.Length;
                }
            }
            return outmesh;
        }

        protected Vector3[] BOX_CORNERS = new Vector3[] {
            new Vector3 (0, 0, 0),
            new Vector3 (1, 0, 0),
            new Vector3 (1, 1, 0),
            new Vector3 (0, 1, 0),
            new Vector3 (0, 1, 1),
            new Vector3 (1, 1, 1),
            new Vector3 (1, 0, 1),
            new Vector3 (0, 0, 1),
        };

        protected int[] BOX_INDICES = new int[] {
            0, 2, 1, //face front
            0, 3, 2,
            2, 3, 4, //face top
            2, 4, 5,
            1, 2, 5, //face right
            1, 5, 6,
            0, 7, 4, //face left
            0, 4, 3,
            5, 4, 7, //face back
            5, 7, 6,
            0, 6, 7, //face bottom
            0, 1, 6
        };

    }

}
