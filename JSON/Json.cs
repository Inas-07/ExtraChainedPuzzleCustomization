﻿using System;
using System.Text.Json.Serialization;
using System.Text.Json;
using System.IO;

namespace ScanPosOverride.JSON
{
    internal static class Json
    {
        private static readonly JsonSerializerOptions _setting = new()
        {
            ReadCommentHandling = JsonCommentHandling.Skip,
            IncludeFields = false,
            PropertyNameCaseInsensitive = true,
            WriteIndented = true,
            IgnoreReadOnlyProperties = true
        };

        static Json()
        {
            _setting.Converters.Add(new JsonStringEnumConverter());

            if (MTFOPartialDataUtil.IsLoaded && MTFOPartialDataUtil.Initialized)
            {
                _setting.Converters.Add(MTFOPartialDataUtil.PersistentIDConverter);
                _setting.Converters.Add(MTFOPartialDataUtil.LocalizedTextConverter);
                SPOLogger.Log("PartialData Support Found!");
            }
            else
            {
                _setting.Converters.Add(new LocalizedTextConverter());
            }
        }

        public static T Deserialize<T>(string json)
        {
            return JsonSerializer.Deserialize<T>(json, _setting);
        }

        public static object Deserialize(Type type, string json)
        {
            return JsonSerializer.Deserialize(json, type, _setting);
        }

        public static string Serialize<T>(T value)
        {
            return JsonSerializer.Serialize(value, _setting);
        }

        public static void Load<T>(string file, out T config) where T : new()
        {
            if (file.Length < ".json".Length)
            {
                config = default;
                return;
            }

            if (file.Substring(file.Length - ".json".Length) != ".json")
            {
                file += ".json";
            }

            string filePath = Path.Combine(Plugin.OVERRIDE_SCAN_POS_PATH, file);

            file = File.ReadAllText(filePath);
            config = Deserialize<T>(file);
        }
    }
}
