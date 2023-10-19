using Inhumate.Unity.RTI;
using Inhumate.RTI.Proto;
using UnityEngine;

namespace Inhumate.Unity.Examples.RTI {

    [RequireComponent(typeof(RTIEntity))]
    public class RTITestEntityCommand : MonoBehaviour {

        public float bounceForce = 1000;

        private RTIEntity entity;

        void Start() {
            entity = GetComponent<RTIEntity>();
            entity.RegisterCommand(new Command {
                Name = "Bounce",
                Description = "Make me bounce"
            }.Argument("multiplier", "1", "float"), (command, exec) => {
                Bounce(float.Parse(command.GetArgumentValue(exec, "multiplier")));
                return new CommandResponse { Message = "boing" };
            });
            entity.RegisterCommand(new Command { Name = "Boing" }, (command, exec) => { Bounce(); });
        }

        void Bounce(float multiplier = 1) {
            var body = GetComponent<Rigidbody>();
            body.AddForce(Vector3.up * bounceForce * multiplier);
        }
    }

}
