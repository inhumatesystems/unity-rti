using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Inhumate.Unity.RTI;

public class FooDTO {
    public string Foo;
    public int Bar;
}

public class RTIJsonTestListener : RTIJsonBehaviour<FooDTO>
{
    public override string ChannelName => "foojson";

    protected override void OnMessage(FooDTO message) {
        Debug.Log($"Received foojson: {message.Foo} {message.Bar}");
    }

    void Awake()
    {
        RTI.WhenConnected(() => {
            Publish(new FooDTO { Foo = "bar", Bar = 42 });
        });
    }

}
