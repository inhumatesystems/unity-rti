
// This demonstrates how to provide custom functions for converting between
// local and geodetic coordinates, which is typically scene dependent

using Inhumate.Unity.RTI;
using Inhumate.RTI.Proto;
using UnityEngine;

public class RTITestSceneCoordinates : MonoBehaviour {
    public Vector2 leftTopXY, leftTopLonLat, rightBottomXY, rightBottomLonLat;

    void Awake() {
        RTIPosition.LocalToGeodetic = (Vector3 position) => {
            return new EntityPosition.Types.GeodeticPosition {
                Latitude = rightBottomLonLat.y + (position.z - rightBottomXY.y) * (leftTopLonLat.y - rightBottomLonLat.y) / (leftTopXY.y - rightBottomXY.y),
                Longitude = leftTopLonLat.x + (position.x - leftTopXY.x) * (rightBottomLonLat.x - leftTopLonLat.x) / (rightBottomXY.x - leftTopXY.x),
                Altitude = position.y,
            };
        };
        RTIPosition.GeodeticToLocal = (EntityPosition.Types.GeodeticPosition geo) => {
            return new Vector3(
                leftTopXY.x + ((float)geo.Longitude - leftTopLonLat.x) * (rightBottomXY.x - leftTopXY.x) / (rightBottomLonLat.x - leftTopLonLat.x),
                (float)geo.Altitude,
                rightBottomXY.y + ((float)geo.Latitude - rightBottomLonLat.y) * (leftTopXY.y - rightBottomXY.y) / (leftTopLonLat.y - rightBottomLonLat.y)
            );
        };
    }

    void OnDestroy() {
        RTIPosition.LocalToGeodetic = null;
        RTIPosition.GeodeticToLocal = null;
    }

}
