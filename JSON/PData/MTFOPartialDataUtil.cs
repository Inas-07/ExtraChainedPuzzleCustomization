using BepInEx.Unity.IL2CPP;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.IO;
using System.Text.Json.Nodes;
using System.Text.Json;
using System.Text.Json.Serialization;
using ScanPosOverride.JSON.Extensions;

namespace ScanPosOverride.JSON.PData
{
    public static class MTFOPartialDataUtil
    {
        public const string PLUGIN_GUID = "MTFO.Extension.PartialBlocks";

        public static JsonConverter PersistentIDConverter { get; private set; } = null;
        public static JsonConverter LocalizedTextConverter { get; private set; } = null;
        public static bool IsLoaded { get; private set; } = false;
        public static bool Initialized { get; private set; } = false;
        public static string PartialDataPath { get; private set; } = string.Empty;
        public static string ConfigPath { get; private set; } = string.Empty;

        public const string ID_FILE_NAME = "_persistentID.json";

        private static Dictionary<string, uint> pdataGUID = null;
        static MTFOPartialDataUtil()
        {

            if (IL2CPPChainloader.Instance.Plugins.TryGetValue(PLUGIN_GUID, out var info))
            {
                try
                {
                    var ddAsm = info?.Instance?.GetType()?.Assembly ?? null;

                    if (ddAsm is null)
                        throw new Exception("Assembly is Missing!");

                    var types = ddAsm.GetTypes();
                    var converterType = types.First(t => t.Name == "PersistentIDConverter");
                    if (converterType is null)
                        throw new Exception("Unable to Find PersistentIDConverter Class");

                    var dataManager = types.First(t => t.Name == "PartialDataManager");
                    if (dataManager is null)
                        throw new Exception("Unable to Find PartialDataManager Class");

                    var localizedTextConverterType = types.First(t => t.Name == "LocalizedTextConverter");
                    if (localizedTextConverterType is null)
                        throw new Exception("Unable to Find LocalizedTextConverter Class");

                    var initProp = dataManager.GetProperty("Initialized", BindingFlags.Public | BindingFlags.Static);
                    var dataPathProp = dataManager.GetProperty("PartialDataPath", BindingFlags.Public | BindingFlags.Static);
                    var configPathProp = dataManager.GetProperty("ConfigPath", BindingFlags.Public | BindingFlags.Static);

                    if (initProp is null)
                        throw new Exception("Unable to Find Property: Initialized");

                    if (dataPathProp is null)
                        throw new Exception("Unable to Find Property: PartialDataPath");

                    if (configPathProp is null)
                        throw new Exception("Unable to Find Field: ConfigPath");

                    Initialized = (bool)initProp.GetValue(null);
                    PartialDataPath = (string)dataPathProp.GetValue(null);
                    ConfigPath = (string)configPathProp.GetValue(null);

                    PersistentIDConverter = (JsonConverter)Activator.CreateInstance(converterType);
                    LocalizedTextConverter = (JsonConverter)Activator.CreateInstance(localizedTextConverterType);
                    IsLoaded = true;
                }
                catch (Exception e)
                {
                    SPOLogger.Error($"Exception thrown while reading data from MTFO_Extension_PartialData:\n{e}");
                }
            }
        }

        internal static void ReadPDataGUID()
        {
            if (pdataGUID != null) return;

            if (!IsLoaded)
            {
                SPOLogger.Error("ReadPDataGUID: invoked when not loaded, which is a broken operation");
                return;
            }

            var id_file_path = Path.Combine(PartialDataPath, ID_FILE_NAME);
            if (!File.Exists(id_file_path))
            {
                SPOLogger.Error("ReadPDataGUID: PartialData GUID file not found");
                return;
            }

            string content = File.ReadAllText(id_file_path);
            var guids = Json.Deserialize<List<PDataGUID>>(content);

            pdataGUID = new();
            foreach (var guid in guids)
            {
                if (!pdataGUID.TryAdd(guid.GUID, guid.ID))
                {
                    SPOLogger.Error($"ReadPDataGUID: cannot add '{guid.GUID}', probably there's a duplicate");
                }
            }

            SPOLogger.Log($"ReadPDataGUID: Loaded {pdataGUID.Count} PData GUID");
        }

        public static bool TryGetID(string guid, out uint id)
        {
            id = 0u;
            return pdataGUID?.TryGetValue(guid, out id) ?? false;
        }

        public static string ConvertAllGUID(string json)
        {
            if (!IsLoaded)
            {
                SPOLogger.Error("MTFOPartialDataUtil.ConvertAllGUID: partial data is not loaded");
                return json;
            }

            var root = JsonNode.Parse(json,
                nodeOptions: new() { },
                documentOptions: new() { CommentHandling = JsonCommentHandling.Skip });
            foreach (var item in root.DescendantItemsAndSelf().Where(i => i.name != null && i.node is JsonValue).ToList())
            {
                var value = item.node.GetValue<JsonElement>();
                if (value.ValueKind == JsonValueKind.String && item.name != null)
                {
                    if (TryGetID(item.node.ToString(), out uint id) && id != 0)
                    {
                        var parent = (JsonObject)item.parent;
                        parent.Remove(item.name);
                        parent.Add(item.name, id);
                    }
                }
            }

            return root.ToString();

            //I could just write a wrapper for json serializer that replaces all pdata guid with uint id before passing it to json converter in injectlib
        }
    }
}
