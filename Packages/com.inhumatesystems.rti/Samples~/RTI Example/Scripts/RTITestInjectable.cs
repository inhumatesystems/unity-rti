using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Inhumate.Unity.RTI;
using Inhumate.RTI.Proto;

public class RTITestInjectable : RTIInjectionBehaviour {

    void Update() {
        if (injection != null) {
            switch (injection.State) {
                case Injection.Types.State.Enabled:
                    if (RTI.time - injection.EnableTime > 5) Begin();
                    break;
                case Injection.Types.State.Running:
                    if (RTI.time - injection.StartTime > 5 || injectable.endMode == Injectable.Types.ControlMode.Immediate) End();
                    break;
            }
        }
    }

    public override bool Enable() {
        Debug.Log($"Inject {name} {injection.Id} enabled");
        injection.Title = "Let's go";
        var stuff = GetParameterValue("stuff");
        if (!string.IsNullOrEmpty(stuff)) injection.Title += " " + stuff;
        return true;
    }

    public override void Disable() {
        Debug.Log($"Inject {name} {injection.Id} disabled");
        injection.Title = "";
    }

    public override void Begin() {
        Debug.Log($"Inject {name} {injection.Id} started");
        injection.Title = "Stuff is rolling...";
        running = true;
    }

    public override void Cancel() {
        Debug.Log($"Inject {name} {injection.Id} canceled");
        injection.Title = "To hell with it";
        running = false;
    }

    public override void Stop() {
        Debug.Log($"Inject {name} {injection.Id} stopped");
        injection.Title = "OK fine be that way";
        running = false;
    }

    public override void End() {
        Debug.Log($"Inject {name} {injection.Id} ended");
        injection.Title = "It is done";
        running = false;
    }

}
