using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using BDArmory.Competition;

namespace BDArmory.Initialization
{
    [KSPAddon(KSPAddon.Startup.MainMenu, false)]
    public class Cleanup : MonoBehaviour
    {
        bool hasRun = false;
        bool inhibitAutoFunctions = false;
        void Awake()
        {
            if (hasRun) return;
            hasRun = true;
            var BDArmoryCoreFiles = Directory.GetFiles(Path.GetFullPath(Path.Combine(KSPUtil.ApplicationRootPath, "GameData", "BDArmory", "Plugins"))).Where(f => Path.GetFileName(f).StartsWith("BDArmory.Core")).ToList();
            if (BDArmoryCoreFiles.Count > 0)
            {
                inhibitAutoFunctions = true;
                var message = new List<string>();
                message.Add("BDArmory has moved to using a single DLL. The following old BDArmory.Core files will be removed:");
                foreach (var BDArmoryCoreFile in BDArmoryCoreFiles) message.Add("\t" + BDArmoryCoreFile);
                message.Add("Please restart KSP to avoid any potential issues.");
                Debug.LogWarning(string.Join("\n", message.Select(s => "[BDArmory.Initialization]: " + s)));
                PopupDialog.SpawnPopupDialog(
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.7f, 0.5f), // Seems to give a vertically centred dialog box with some width to show the longer strings.
                    "BDArmory Warning",
                    "BDArmory Warning",
                    string.Join("\n", message),
                    "OK",
                    false,
                    HighLogic.UISkin
                );
                foreach (var BDArmoryCoreFile in BDArmoryCoreFiles) File.Delete(BDArmoryCoreFile); // Delete the BDArmory.Core files.
            }
        }

        void Start()
        {
            if (!inhibitAutoFunctions) return;
            TournamentAutoResume.firstRun = false; // Prevents AUTO functionality from running once the level is loaded.
        }
    }
}