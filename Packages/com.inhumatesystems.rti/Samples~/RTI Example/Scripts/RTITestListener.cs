using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Inhumate.UnityRTI;

public class RTITestListener : RTITextBehaviour
{
    public override string ChannelName => "foo";

    protected override void OnMessage(string message) {
        Debug.Log($"Received foo: {message}");
    }

    void Awake()
    {
        RTI.WhenConnected(() => {
            Debug.Log("Connected", this);
        });
        RTI.WhenConnectedOnce(() => {
            Debug.Log("Connected once", this);
        });
    }

}
