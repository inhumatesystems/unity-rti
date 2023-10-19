using UnityEngine;

namespace Inhumate.Unity.RTI {

    [CreateAssetMenu(fileName = "Scenario", menuName = "RTI/Scenario")]
    public class RTIScenario : ScriptableObject {

        public string sceneName;

        [TextArea(3, 10)]
        public string description;

        public RTIParameter[] parameters = new RTIParameter[] {};


        public Inhumate.RTI.Proto.Scenario ToProto() {
            var proto = new Inhumate.RTI.Proto.Scenario {
                Name = name,
                Description = description
            };
            foreach (var parameter in parameters) proto.Parameters.Add(parameter.ToProto());
            return proto;
        }

    }

}
