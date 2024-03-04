using UnityEngine;

namespace BDArmory.Utils
{
    public class ConfigNodeUtils
    {
        static public string FindPartModuleConfigNodeValue(ConfigNode configNode, string moduleName, string fieldName, string moduleID = "", string moduleIDValue = "")
        {
            if (configNode == null) return null;
            string retval = null;
            // Search this node.
            if (configNode.values != null)
            {
                /*
                    if (configNode.name == "MODULE" && configNode.HasValue("name") && configNode.GetValue("name") == moduleName)
                        if (configNode.HasValue(fieldName))
                            return configNode.GetValue(fieldName);
                */
                foreach (var module in configNode.GetNodes())
                {
                    if (module.name == "MODULE" && module.HasValue("name") && module.GetValue("name") == moduleName)
                        if (module.HasValue(fieldName))
                            if (string.IsNullOrEmpty(moduleID) || module.GetValue(moduleID) == moduleIDValue)
                                return module.GetValue(fieldName);
                }
            }
            // Search sub-nodes.
            if (configNode.nodes != null)
            {
                for (int i = 0; i < configNode.nodes.Count; ++i)
                    if ((retval = FindPartModuleConfigNodeValue(configNode.nodes[i], moduleName, fieldName)) != null)
                        return retval;
            }
            return null;
        }

        static public void PrintConfigNode(ConfigNode configNode, string indent = "")
        {
            Debug.Log("[BDArmory.ConfigNodeUtils]: " + indent + configNode.ToString() + ":: ");
            for (int i = 0; i < configNode.values.Count; ++i)
                Debug.Log("[BDArmory.ConfigNodeUtils]:   " + indent + configNode.values[i].name + ": " + configNode.values[i].value);
            foreach (var node in configNode.GetNodes())
            {
                PrintConfigNode(node, indent + " ");
            }
        }
    }
}