using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Networking;

using BDArmory.Settings;

namespace BDArmory.Competition.RemoteOrchestration
{

    public class BDAScoreClient
    {
        private BDAScoreService service;

        private string baseUrl;

        private string basePath;

        public string vesselPath = "";

        public string vesselStagingPath = "";

        public string competitionHash = "";

        public string NPCPath = "";


        public bool pendingRequest = false;

        public CompetitionModel competition = null;

        public HeatModel activeHeat = null;

        public HashSet<int> activeVessels = new HashSet<int>();

        public Dictionary<int, HeatModel> heats = new Dictionary<int, HeatModel>();

        public Dictionary<int, VesselModel> vessels = new Dictionary<int, VesselModel>();

        public Dictionary<int, PlayerModel> players = new Dictionary<int, PlayerModel>();

        public Dictionary<string, Tuple<string, string>> playerVessels = new Dictionary<string, Tuple<string, string>>(); // Registry of in-game vessel names with actual player and vessel names.


        public BDAScoreClient(BDAScoreService service, string basePath, string hash)
        {
            Debug.Log("[BDArmory.BDAScoreService] Client started with working directory: " + basePath);
            //this.baseUrl = "http://localhost:3000";
            this.baseUrl = "https://" + BDArmorySettings.REMOTE_ORCHESTRATION_BASE_URL;
            this.basePath = Path.GetFullPath(basePath); // Removes the 'KSP_x64_Data/../'
            this.service = service;
            this.vesselPath = basePath + "/" + hash;
            this.vesselStagingPath = basePath + "/" + hash + "/staging";
            this.NPCPath = Path.Combine(basePath, "NPC");
            this.competitionHash = hash;
        }

        /// <summary>
        /// Acts as a runtime interface for resolving vessel ids into vessel concerns.
        /// </summary>
        private class ScoreClientVesselSource : VesselSource
        {
            private Dictionary<int, VesselModel> vessels;
            private Dictionary<int, PlayerModel> players;
            private string stagingPath;
            public ScoreClientVesselSource(Dictionary<int, VesselModel> vessels, Dictionary<int, PlayerModel> players, string stagingPath)
            {
                this.vessels = vessels;
                this.players = players;
                this.stagingPath = stagingPath;
            }

            public string GetLocalPath(int id)
            {
                var vessel = vessels[id];
                var player = players[vessel.player_id];
                return string.Format("{0}/{1}_{2}.craft", stagingPath, player.name, vessel.name);
            }

            public VesselModel GetVessel(int id)
            {
                return vessels[id];
            }
        }

        public VesselSource AsVesselSource()
        {
            return new ScoreClientVesselSource(vessels, players, vesselStagingPath);
        }

        /// <summary>
        /// Fetch competition metadata
        /// </summary>
        /// <param name="hash">competition id</param>
        public IEnumerator GetCompetition(string hash)
        {
            if (pendingRequest)
            {
                Debug.Log("[BDArmory.BDAScoreClient] Request already pending");
                yield break;
            }
            pendingRequest = true;

            string uri = string.Format("{0}/competitions/{1}.json", baseUrl, hash);
            Debug.Log(string.Format("[BDArmory.BDAScoreClient] GET {0}", uri));
            using (UnityWebRequest webRequest = UnityWebRequest.Get(uri))
            {
                yield return webRequest.SendWebRequest();
                if (!webRequest.isHttpError)
                {
                    ReceiveCompetition(webRequest.downloadHandler.text);
                }
                else
                {
                    Debug.LogWarning(string.Format("[BDArmory.BDAScoreClient] Failed to get competition {0}: {1}", hash, webRequest.error));
                }
            }

            pendingRequest = false;
        }

        private void ReceiveCompetition(string response)
        {
            if (response == null || "".Equals(response))
            {
                Debug.LogWarning(string.Format("[BDArmory.BDAScoreClient] Received empty competition response"));
                return;
            }
            CompetitionModel competition = JsonUtility.FromJson<CompetitionModel>(response);
            if (competition == null)
            {
                Debug.LogWarning(string.Format("[BDArmory.BDAScoreClient] Failed to parse competition: {0}", response));
            }
            else
            {
                this.competition = competition;
                Debug.Log(string.Format("[BDArmory.BDAScoreClient] Competition: {0}", competition.ToString()));
            }
        }

        /// <summary>
        /// Fetch heat manifest, which describes the groupings of players in various stages.
        /// </summary>
        /// <param name="hash">competition id</param>
        public IEnumerator GetHeats(string hash)
        {
            if (pendingRequest)
            {
                Debug.Log("[BDArmory.BDAScoreClient] Request already pending");
                yield break;
            }
            pendingRequest = true;

            string uri = string.Format("{0}/competitions/{1}/heats.csv", baseUrl, hash);
            Debug.Log(string.Format("[BDArmory.BDAScoreClient] GET {0}", uri));
            using (UnityWebRequest webRequest = UnityWebRequest.Get(uri))
            {
                yield return webRequest.SendWebRequest();
                if (!webRequest.isHttpError)
                {
                    ReceiveHeats(webRequest.downloadHandler.text);
                }
                else
                {
                    Debug.LogWarning(string.Format("[BDArmory.BDAScoreClient] Failed to get heats for {0}: {1}", hash, webRequest.error));
                }
            }

            pendingRequest = false;
        }

        private void ReceiveHeats(string response)
        {
            if (response == null || "".Equals(response))
            {
                Debug.LogWarning(string.Format("[BDArmory.BDAScoreClient] Received empty heat collection response"));
                return;
            }
            List<HeatModel> collection = HeatModel.FromCsv(response);
            heats.Clear();
            if (collection == null)
            {
                Debug.LogWarning(string.Format("[BDArmory.BDAScoreClient] Failed to parse heat collection: {0}", response));
                return;
            }
            foreach (HeatModel heatModel in collection)
            {
                Debug.Log(string.Format("[BDArmory.BDAScoreClient] Heat: {0}", heatModel.ToString()));
                heats.Add(heatModel.id, heatModel);
            }
            Debug.Log(string.Format("[BDArmory.BDAScoreClient] Heats: {0}", heats.Count));
        }

        /// <summary>
        /// Fetch player metadata for all participants
        /// </summary>
        /// <param name="hash">competition id</param>
        public IEnumerator GetPlayers(string hash)
        {
            if (pendingRequest)
            {
                Debug.Log("[BDArmory.BDAScoreClient] Request already pending");
                yield break;
            }
            pendingRequest = true;

            string uri = string.Format("{0}/competitions/{1}/players.csv", baseUrl, hash);
            Debug.Log(string.Format("[BDArmory.BDAScoreClient] GET {0}", uri));
            using (UnityWebRequest webRequest = UnityWebRequest.Get(uri))
            {
                yield return webRequest.SendWebRequest();
                if (!webRequest.isHttpError)
                {
                    ReceivePlayers(webRequest.downloadHandler.text);
                }
                else
                {
                    Debug.LogWarning(string.Format("[BDArmory.BDAScoreClient] Failed to get players for {0}: {1}", hash, webRequest.error));
                }
            }

            pendingRequest = false;
        }

        private void ReceivePlayers(string response)
        {
            if (response == null || "".Equals(response))
            {
                Debug.LogWarning(string.Format("[BDArmory.BDAScoreClient] Received empty player collection response"));
                return;
            }
            List<PlayerModel> collection = PlayerModel.FromCsv(response);
            players.Clear();
            if (collection == null)
            {
                Debug.LogWarning(string.Format("[BDArmory.BDAScoreClient] Failed to parse player collection: {0}", response));
                return;
            }
            foreach (PlayerModel playerModel in collection)
            {
                Debug.Log(string.Format("[BDArmory.BDAScoreClient] Player {0}", playerModel.ToString()));
                if (!players.ContainsKey(playerModel.id))
                    players.Add(playerModel.id, playerModel);
                else
                    Debug.LogWarning("[BDArmory.BDAScoreClient] Player " + playerModel.id + " already exists in the competition.");
            }
            Debug.Log(string.Format("[BDArmory.BDAScoreClient] Players: {0}", players.Count));
        }

        /// <summary>
        /// Fetch all vessel metadata.
        /// </summary>
        /// <param name="hash">competition hash</param>
        public IEnumerator GetVessels(string hash)
        {
            if (pendingRequest)
            {
                Debug.Log("[BDArmory.BDAScoreClient] Request already pending");
                yield break;
            }
            pendingRequest = true;

            string uri = string.Format("{0}/competitions/{1}/vessels/manifest.csv", baseUrl, hash);
            Debug.Log(string.Format("[BDArmory.BDAScoreClient] GET {0}", uri));
            using (UnityWebRequest webRequest = UnityWebRequest.Get(uri))
            {
                yield return webRequest.SendWebRequest();
                if (!webRequest.isHttpError)
                {
                    ReceiveVessels(webRequest.downloadHandler.text);
                }
                else
                {
                    Debug.LogWarning(string.Format("[BDArmory.BDAScoreClient] Failed to get vessels {0}: {1}", hash, webRequest.error));
                }
            }

            pendingRequest = false;
        }

        private void ReceiveVessels(string response)
        {
            if (response == null || "".Equals(response))
            {
                Debug.Log(string.Format("[BDArmory.BDAScoreClient] Received empty vessel collection response"));
                return;
            }
            List<VesselModel> collection = VesselModel.FromCsv(response);
            vessels.Clear();
            if (collection == null)
            {
                Debug.LogWarning(string.Format("[BDArmory.BDAScoreClient] Failed to parse vessel collection: {0}", response));
                return;
            }
            foreach (VesselModel vesselModel in collection)
            {
                if (!vessels.ContainsKey(vesselModel.id)) // Skip duplicates.
                {
                    Debug.Log(string.Format("[BDArmory.BDAScoreClient] Vessel {0}", vesselModel.ToString()));
                    vessels.Add(vesselModel.id, vesselModel);
                }
                else
                {
                    Debug.LogWarning("[BDArmory.BDAScoreClient]: Vessel " + vesselModel.ToString() + " is already in the vessel list, skipping.");
                }
            }
            Debug.Log(string.Format("[BDArmory.BDAScoreClient] Vessels: {0}", vessels.Count));
        }

        /// <summary>
        /// Fetch vessel manifest for the given heat.
        /// </summary>
        /// <param name="hash">competition hash</param>
        /// <param name="heatModel">heat model</param>
        public IEnumerator GetHeatVessels(string hash, HeatModel heatModel)
        {
            if (pendingRequest)
            {
                Debug.Log("[BDArmory.BDAScoreClient] Request already pending");
                yield break;
            }
            pendingRequest = true;

            activeVessels.Clear();

            string uri = string.Format("{0}/competitions/{1}/heats/{2}/vessels.csv", baseUrl, hash, heatModel.id);
            Debug.Log(string.Format("[BDArmory.BDAScoreClient] GET {0}", uri));
            using (UnityWebRequest webRequest = UnityWebRequest.Get(uri))
            {
                yield return webRequest.SendWebRequest();
                if (!webRequest.isHttpError)
                {
                    ReceiveHeatVessels(webRequest.downloadHandler.text);
                }
                else
                {
                    Debug.LogWarning(string.Format("[BDArmory.BDAScoreClient] Failed to get vessel manifest for {0}, heat {1}: {2}", hash, heatModel.id, webRequest.error));
                }
            }

            pendingRequest = false;
        }

        private void ReceiveHeatVessels(string response)
        {
            if (response == null || "".Equals(response))
            {
                Debug.Log(string.Format("[BDArmory.BDAScoreClient] Received empty heat vessel collection response"));
                return;
            }
            List<VesselModel> collection = VesselModel.FromCsv(response);
            if (collection == null)
            {
                Debug.LogWarning(string.Format("[BDArmory.BDAScoreClient] Failed to parse heat vessel collection: {0}", response));
                return;
            }
            SwapCraftFiles();
            foreach (VesselModel vesselModel in collection)
            {
                activeVessels.Add(vesselModel.id);
            }
            Debug.Log(string.Format("[BDArmory.BDAScoreClient] Active vessels: {0}", activeVessels.Count));
        }

        /// <summary>
        /// Submit scores for a heat.
        /// </summary>
        /// <param name="hash">competition id</param>
        /// <param name="heat">heat id</param>
        /// <param name="records">records to send</param>
        public IEnumerator PostRecords(string hash, int heat, List<RecordModel> records)
        {
            List<string> recordsJson = records.Select(e => e.ToJSON()).ToList();
            Debug.Log(string.Format("[BDArmory.BDAScoreClient] Prepare records for {0} players", records.Count()));
            string recordsJsonStr = string.Join(",", recordsJson);
            string requestBody = string.Format("{{\"records\":[{0}]}}", recordsJsonStr);

            byte[] rawBody = Encoding.UTF8.GetBytes(requestBody);
            string uri = string.Format("{0}/competitions/{1}/heats/{2}/records/batch.json?client_secret={3}", baseUrl, hash, heat, BDArmorySettings.REMOTE_CLIENT_SECRET);
            string uriWithoutSecret = string.Format("{0}/competitions/{1}/heats/{2}/records/batch.json?client_secret=****", baseUrl, hash, heat);
            Debug.Log(string.Format("[BDArmory.BDAScoreClient] POST {0}:\n{1}", uriWithoutSecret, requestBody));
            using (UnityWebRequest webRequest = new UnityWebRequest(uri))
            {
                webRequest.SetRequestHeader("Content-Type", "application/json");
                webRequest.uploadHandler = new UploadHandlerRaw(rawBody);
                webRequest.downloadHandler = new DownloadHandlerBuffer();
                webRequest.method = UnityWebRequest.kHttpVerbPOST;

                yield return webRequest.SendWebRequest();

                Debug.Log(string.Format("[BDArmory.BDAScoreClient] score reporting status: {0}", webRequest.downloadHandler.text));
                if (webRequest.isHttpError)
                {
                    Debug.LogWarning(string.Format("[BDArmory.BDAScoreClient] Failed to post records: {0}", webRequest.error));
                }
            }
        }

        /// <summary>
        /// Fetch vessel craft files from remote and store them locally in autospawn/:hash/staging
        /// </summary>
        /// <param name="hash">competition id</param>
        public IEnumerator GetCraftFiles(string hash)
        {
            pendingRequest = true;
            // DO NOT DELETE THE DIRECTORY. Delete the craft files inside it.
            // This is much safer.
            if (Directory.Exists(vesselPath))
            {
                Debug.Log("[BDArmory.BDAScoreClient] Deleting existing craft in staging directory " + vesselStagingPath);
                DirectoryInfo info = new DirectoryInfo(vesselStagingPath);
                FileInfo[] craftFiles = info.GetFiles("*.craft")
                    .Where(e => e.Extension == ".craft")
                    .ToArray();
                foreach (FileInfo file in craftFiles)
                {
                    File.Delete(file.FullName);
                }
            }
            else
            {
                Debug.Log("[BDArmory.BDAScoreClient] Creating staging directory " + vesselStagingPath);
                Directory.CreateDirectory(vesselStagingPath);
            }

            playerVessels.Clear();
            // already have the vessels in memory; just need to fetch the files
            foreach (VesselModel v in vessels.Values)
            {
                Debug.Log(string.Format("[BDArmory.BDAScoreClient] GET {0}", v.craft_url));
                using (UnityWebRequest webRequest = UnityWebRequest.Get(v.craft_url))
                {
                    yield return webRequest.SendWebRequest();
                    if (!webRequest.isHttpError)
                    {
                        byte[] rawBytes = webRequest.downloadHandler.data;
                        SaveCraftFile(v, rawBytes);
                    }
                    else
                    {
                        Debug.LogWarning(string.Format("[BDArmory.BDAScoreClient] Failed to get craft for {0}: {1}", v.id, webRequest.error));
                    }
                }
            }
            pendingRequest = false;
        }

        int count = 0;
        private void SaveCraftFile(VesselModel vessel, byte[] bytes)
        {
            PlayerModel p = players[vessel.player_id];
            if (p == null)
            {
                Debug.LogWarning(string.Format("[BDArmory.BDAScoreClient] Failed to save craft for vessel {0}, player {1}", vessel.id, vessel.player_id));
                return;
            }

            string vesselName = string.Format("{0}_{1}", p.name, vessel.name);
            playerVessels.Add(vesselName, new Tuple<string, string>(p.name, vessel.name));
            string filename;
            try
            {
                filename = string.Format("{0}/{1}.craft", vesselStagingPath, vesselName);
                System.IO.File.WriteAllBytes(filename, bytes);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[BDArmory.BDAScoreClient]: Invalid filename: {e.Message}");
                filename = string.Format("{0}/Invalid filename {1}.craft", vesselStagingPath, ++count);
                System.IO.File.WriteAllBytes(filename, bytes);
            }

            // load the file and modify its vessel name to match the player
            string[] lines = File.ReadAllLines(filename);
            string pattern = ".*ship = (.+)";
            string[] modifiedLines = lines
                .Select(e => Regex.Replace(e, pattern, "ship = " + vesselName))
                .Where(e => !e.Contains("VESSELNAMING"))
                .ToArray();
            pattern = ".*version = (.+)";
            modifiedLines = modifiedLines
                .Select(e => Regex.Replace(e, pattern, "version = 1.12.2"))
                .ToArray();
            File.WriteAllLines(filename, modifiedLines);
            Debug.Log(string.Format("[BDArmory.BDAScoreClient] Saved craft for player {0}", vesselName));
            //if (vesselName.Contains(BDArmorySettings.REMOTE_ORCHESTRATION_NPC_SWAPPER)) //grab either ships or players that contain NPC identifier
            //{
            //    SwapCraftFiles(vesselName); //doing this after initial load/editing to make sure nothing breaks by swapping earlier
            //}
        }

        public void SwapCraftFiles()
        {

            DirectoryInfo info = new DirectoryInfo(vesselStagingPath);
            FileInfo[] craftFiles = info.GetFiles("*.craft");
            //.Where(e => e.Extension == ".craft").ToArray();
            if (!Directory.Exists(NPCPath))
            {
                Debug.Log("[BDArmory.BDAScoreClient] Creating staging directory " + NPCPath);
                Directory.CreateDirectory(NPCPath);
            }
            DirectoryInfo NPCinfo = new DirectoryInfo(NPCPath);
            FileInfo[] NPCFiles = NPCinfo.GetFiles("*.craft");

            foreach (FileInfo file in craftFiles)
            {
                if (file.Name.Contains(BDArmorySettings.REMOTE_ORCHESTRATION_NPC_SWAPPER))
                {
                    string vesselname = file.Name;
                    string filename = string.Format("{0}/{1}", vesselStagingPath, vesselname);
                    vesselname = vesselname.Remove(vesselname.Length - 6, 6); //cull the .craft from the string
                    Debug.Log("[BDArmory.BDAScoreClient] Swapping existing craft " + vesselname + " in spawn directory");
                    
                    int i;
                    i = (int)UnityEngine.Random.Range(0, NPCFiles.Count() - 1);
                    Debug.Log(string.Format("[BDArmory.BDAScoreClient] {0} craft, selected number {1}", NPCFiles.Count(), i));

                    string NPCfilename = string.Format("{0}/{1}", NPCPath, NPCFiles[i].Name); //.craft included in the craftFiles[i].name
                    string[] NPClines = File.ReadAllLines(NPCfilename); //kludge, probably easier to just copy the file from NPC dir to the autospawn dir
                    string pattern = ".*ship = (.+)";
                    string[] modifiedLines = NPClines
                        .Select(e => Regex.Replace(e, pattern, "ship = " + vesselname))
                        .Where(e => !e.Contains("VESSELNAMING"))
                        .ToArray();
                    File.WriteAllLines(filename, modifiedLines);
                    Debug.Log(string.Format("[BDArmory.BDAScoreClient] Swapped craft {0} with NPC {1}", vesselname, NPCfilename));
                }
            }
        }

        /// <summary>
        /// Attempt to start a heat. Failed attempts are not retried.
        /// </summary>
        /// <param name="hash">competition id</param>
        /// <param name="heat">heat model</param>
        public IEnumerator StartHeat(string hash, HeatModel heat)
        {
            if (this.activeHeat != null)
            {
                Debug.Log("[BDArmory.BDAScoreClient] Attempted to start a heat while already active");
                yield break;
            }

            if (pendingRequest)
            {
                Debug.Log("[BDArmory.BDAScoreClient] Request already pending");
                yield break;
            }
            pendingRequest = true;

            string uri = string.Format("{0}/competitions/{1}/heats/{2}/start", baseUrl, hash, heat.id);
            using (UnityWebRequest webRequest = new UnityWebRequest(uri))
            {
                yield return webRequest.SendWebRequest();
                if (!webRequest.isHttpError)
                {
                    // only set active heat on success
                    this.activeHeat = heat;
                    UI.RemoteOrchestrationWindow.Instance.UpdateClientStatus();

                    Debug.Log(string.Format("[BDArmory.BDAScoreClient] Started heat {1} in  stage {2} of {0}", hash, heat.order, heat.stage));
                }
                else
                {
                    Debug.LogWarning(string.Format("[BDArmory.BDAScoreClient] Failed to start heat {1} in stage {2} of {0}: {3}", hash, heat.order, heat.stage, webRequest.error));
                }
            }

            pendingRequest = false;
        }

        /// <summary>
        /// Attempt to stop the active heat. Failed attempts are not retried.
        /// </summary>
        /// <param name="hash">competition id</param>
        /// <param name="heat">heat model</param>
        public IEnumerator StopHeat(string hash, HeatModel heat)
        {
            if (this.activeHeat == null)
            {
                Debug.Log("[BDArmory.BDAScoreClient] Attempted to stop a heat when none is active");
                yield break;
            }

            if (pendingRequest)
            {
                Debug.Log("[BDArmory.BDAScoreClient] Request already pending");
                yield break;
            }
            pendingRequest = true;

            string uri = string.Format("{0}/competitions/{1}/heats/{2}/stop", baseUrl, hash, heat.id);
            using (UnityWebRequest webRequest = new UnityWebRequest(uri))
            {
                yield return webRequest.SendWebRequest();
                if (!webRequest.isHttpError)
                {
                    // only clear active heat on success
                    this.activeHeat = null;

                    Debug.Log(string.Format("[BDArmory.BDAScoreClient] Stopped heat {1} in stage {2} of {0}", hash, heat.order, heat.stage));
                }
                else
                {
                    Debug.LogWarning(string.Format("[BDArmory.BDAScoreClient] Failed to stop heat {2} in stage {1} of {0}: {3}", hash, heat.stage, heat.order, webRequest.error));
                }
            }

            pendingRequest = false;
        }

    }
}
