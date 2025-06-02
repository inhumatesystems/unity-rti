using Inhumate.UnityRTI;
using Inhumate.RTI.Proto;
using UnityEngine;

namespace Inhumate.UnityRTI.Example {

    [RequireComponent(typeof(RTIEntity))]
    public class RTITestEntityCommand : MonoBehaviour {

        public float bounceForce = 1000;

        void Start() {
            GetComponent<RTIEntity>().RegisterCommands(this);
            /*
            entity.RegisterCommand(new Command {
                Name = "Bounce",
                Description = "Make me bounce"
            }.Argument("multiplier", "1", "float"), (command, exec) => {
                Bounce(float.Parse(command.GetArgumentValue(exec, "multiplier")));
                return new CommandResponse { Message = "boing" };
            });
            entity.RegisterCommand(new Command { Name = "Boing" }, (command, exec) => { Bounce(); });
            */
        }

        [RTICommand]
        void Boing() {
            Bounce();
        }

        [RTICommand("Bounce", "Make me bounce")]
        [RTICommandArgument("multiplier", "1", "float", "How much to bounce")]
        void Bounce(Command command, ExecuteCommand exec) {
            Bounce(float.Parse(command.GetArgumentValue(exec, "multiplier")));
        }

        [RTICommand]
        CommandResponse BounceWithFeedback() {
            Bounce();
            return new CommandResponse { Message = "Weeeeeeee" };
        }   

        void Bounce(float multiplier = 1) {
            var body = GetComponent<Rigidbody>();
            body.AddForce(Vector3.up * bounceForce * multiplier);
        }
    }

}
