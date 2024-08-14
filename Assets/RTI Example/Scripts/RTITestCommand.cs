using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Inhumate.Unity.RTI;
using Inhumate.RTI.Proto;
using Inhumate.RTI;

namespace Inhumate.Unity.Examples.RTI {

    public class RTITestCommand : MonoBehaviour {
        protected RTIConnection RTI => RTIConnection.Instance;

        void Start() {
            RTI.RegisterCommands(this);
            RTI.RegisterCommand(new Command {
                Name = "Log",
                Description = "Log a message"
            }.Argument("message", required: true), (command, exec) => { 
                var message = command.GetArgumentValue(exec, "message");
                if (string.IsNullOrWhiteSpace(message))
                    return new CommandResponse { Failed = true, Message = "No log message provided" };
                Debug.Log(message);
                return new CommandResponse();
            });
            RTI.RegisterCommand(new Command {
                Name = "Sleep",
                Description = "Sleeps for a while.\nYou may say how long."
            }.Argument("duration", "1", "float", "How long to sleep for (seconds)"), (command, exec) => {
                Sleep(float.Parse(command.GetArgumentValue(exec, "duration")), exec.TransactionId);
                return null;
            });
        }

        void OnDestroy() {
            RTI.UnregisterCommand("Sleep");
        }

        void Sleep(float duration, string transactionId = "") {
            Time.timeScale = 0;
            StartCoroutine(DelayedResumeWithResponse(duration, transactionId));
        }

        private IEnumerator DelayedResumeWithResponse(float duration, string transactionId) {
            yield return new WaitForSecondsRealtime(duration);
            Time.timeScale = 1;
            RTI.PublishCommandResponse(transactionId, new CommandResponse { Message = "zzz <yawn> oh hello" });
        }

        [RTICommand]
        void DoStuff() {
            Debug.Log("Doing stuff");
        }

        [RTICommand("DoItWrong")]
        void DoItWrong(string name, int num) {
            Debug.Log($"Can't be done: {name} {num}");
        }

        [RTICommand("Attr Sleep", "Sleeps for a while")]
        [RTICommandArgument("duration", "1", "float", "How long to sleep for (seconds)")]
        [RTICommandArgument("other param")]
        CommandResponse Sleep2(Command command, ExecuteCommand exec) {
            Sleep(float.Parse(command.GetArgumentValue(exec, "duration")), exec.TransactionId);
            return null;
        }

    }

}
