﻿using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BDArmory.Evolution
{
    public class EngineGimbalNudgeMutation : VariantMutation
    {
        const string moduleName = "ModuleGimbal";

        public string[] partNames;
        public string paramName;
        public float modifier;
        public string key;
        public int direction;
        private List<MutatedPart> mutatedParts = new List<MutatedPart>();
        public EngineGimbalNudgeMutation(string[] partNames, string paramName, float modifier, string key, int direction)
        {
            this.partNames = partNames;
            this.paramName = paramName;
            this.modifier = modifier;
            this.key = key;
            this.direction = direction;
        }

        public void Apply(ConfigNode craft, VariantEngine engine)
        {
            Debug.Log("[BDArmory.EngineGimbalNudgeMutation]: Evolution EngineGimbalNudgeMutation applying");
            Dictionary<string, ConfigNode> matchingNodeMap = new Dictionary<string, ConfigNode>();
            foreach (var partName in partNames)
            {
                matchingNodeMap[partName] = engine.GetNode(partName);
            }
            MutateMap(matchingNodeMap, craft, engine);
        }

        public Variant GetVariant(string id, string name)
        {
            return new Variant(id, name, mutatedParts, key, direction);
        }

        private void MutateMap(Dictionary<string, ConfigNode> nodeMap, ConfigNode craft, VariantEngine engine)
        {
            foreach (var partNames in nodeMap.Keys)
            {
                foreach (var partName in partNames.Split(','))
                {
                    MutateNode(nodeMap, engine, partName);
                }
            }
        }

        private void MutateNode(Dictionary<string, ConfigNode> nodeMap, VariantEngine engine, string partName)
        {
            var partNode = nodeMap[partName];
            //var moduleNode = partNode.GetNodes().Where(e => e.name == "MODULE" && e.GetValue("name") == moduleName).First();
            ConfigNode moduleNode = engine.FindModuleNodes(partNode, moduleName).First();
            float existingValue;
            float.TryParse(moduleNode.GetValue(paramName), out existingValue);
            Debug.Log(string.Format("Evolution EngineGimbalNudgeMutation found existing value {0} = {1}", paramName, existingValue));
            if (engine.NudgeNode(moduleNode, paramName, modifier))
            {
                var value = existingValue * (1 + modifier);
                Debug.Log(string.Format("Evolution EngineGimbalNudgeMutation mutated part {0}, module {1}, param {2}, existing: {3}, value: {4}", partName, moduleName, paramName, existingValue, value));
                mutatedParts.Add(new MutatedPart(partName, moduleName, paramName, existingValue, value));
            }
            else
            {
                Debug.Log(string.Format("Evolution EngineGimbalNudgeMutation unable to mutate {0}", paramName));
            }

        }
    }
}
