using System;
using UnityEngine;
using NaughtyAttributes;

namespace Inhumate.UnityRTI {

    [Serializable]
    public class RTIParameter {
        public string name;

        [Tooltip("User-friendly label. If left blank, same as name.")]
        public string label;

        public string description;

        public RTIParameterType type;

        [AllowNesting]
        [ShowIf("type", RTIParameterType.Choice)]
        [Tooltip("List of choices, separated by pipe (|), semicolon (;) or comma (,)")]
        public string choices;

        public string defaultValue;

        public bool required;

        public Inhumate.RTI.Proto.Parameter ToProto() {
            var proto = new Inhumate.RTI.Proto.Parameter {
                Name = name,
                Label = label,
                Description = description,
                DefaultValue = defaultValue,
                Type = type.ToString().ToLower(),
                Required = required
            };
            if (type == RTIParameterType.Choice && choices.Length > 0) {
                if (choices.Contains("|")) {
                    proto.Type += "|" + choices;
                } else if (choices.Contains(";")) {
                    proto.Type += "|" + string.Join("|", choices.Split(';'));
                } else {
                    proto.Type += "|" + string.Join("|", choices.Split(','));
                }
            }
            return proto;
        }
    }

    public enum RTIParameterType {
        String,
        Text,
        Float,
        Integer,
        Switch,
        Checkbox,
        Choice
    }
}
