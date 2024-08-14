using System.Collections;
using Inhumate.Unity.RTI;
using Inhumate.RTI.Proto;
using UnityEngine;

namespace Inhumate.Unity.Examples.RTI {

    public class RTITestPlayerController : MonoBehaviour {

        public float rotationSpeed = 90;
        public float force = 1000;
        public float uprightTorque = 5000;
        public float turnTorque = 50;
        public new Camera camera;
        public GameObject boxPrefab;

        void Start() {
            camera.enabled = true;
            camera.gameObject.SetActive(true);
            RTIConnection.Instance.RegisterCommand(new Command { Name = "DropBox" }.Argument("id"), (command, exec) => {
                var id = command.GetArgumentValue(exec, "id");
                var entity = DropBox(id);
                return new CommandResponse().Return("id", entity.id);
            });
        }

        void Update() {
            if (Input.GetKeyDown(KeyCode.B)) DropBox();
        }

        void FixedUpdate() {
            var body = GetComponent<Rigidbody>();
            if (body != null) {
                if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) {
                    body.AddForce(transform.forward * force);
                }
                if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) {
                    body.AddForce(-transform.forward * force);
                }
                if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) {
                    body.AddTorque(0, turnTorque, 0);
                }
                if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) {
                    body.AddTorque(0, -turnTorque, 0);
                }
                var rot = Quaternion.FromToRotation(transform.up, Vector3.up);
                body.AddTorque(new Vector3(rot.x, rot.y, rot.z) * uprightTorque);
            } else {
                if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) {
                    transform.position += transform.forward * force * Time.fixedDeltaTime / 100;
                }
                if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) {
                    transform.position -= transform.forward * force * Time.fixedDeltaTime / 100;
                }
                if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) {
                    transform.rotation *= Quaternion.AngleAxis(turnTorque * Time.fixedDeltaTime * 3, transform.up);
                }
                if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) {
                    transform.rotation *= Quaternion.AngleAxis(-turnTorque * Time.fixedDeltaTime * 3, transform.up);
                }
            }
        }

        RTIEntity DropBox(string id = null) {
            var go = Instantiate(boxPrefab, new Vector3(Random.Range(-9, 9), 5, Random.Range(-9, 9)), Quaternion.identity);
            var entity = go.GetComponent<RTIEntity>();
            if (!string.IsNullOrWhiteSpace(id)) entity.id = id;
            return entity;
        }
    }

}
