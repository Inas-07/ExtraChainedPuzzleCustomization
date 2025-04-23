using System;
using System.Text.Json.Serialization;
using System.Text.Json;
using System.IO;
using GTFO.API.JSON.Converters;
using ScanPosOverride.JSON.PData;

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
            _setting.Converters.Add(new MyVector3Converter());
            if (MTFOPartialDataUtil.IsLoaded && InjectLibUtil.IsLoaded)
            {
                _setting.Converters.Add(InjectLibUtil.InjectLibConnector);
                _setting.Converters.Add(new LocalizedTextConverter());
                SPOLogger.Log("InjectLib (AWO) && PartialData support found!");
            }
            else
            {
                if (MTFOPartialDataUtil.IsLoaded)
                {
                    _setting.Converters.Add(MTFOPartialDataUtil.PersistentIDConverter);
                    _setting.Converters.Add(WritableLocalizedTextConverter.Converter);
                    //_setting.Converters.Add(MTFOPartialDataUtil.LocalizedTextConverter);
                    SPOLogger.Log("PartialData support found!");
                }
                else
                {
                    if (InjectLibUtil.IsLoaded)
                    {
                        _setting.Converters.Add(InjectLibUtil.InjectLibConnector);
                        SPOLogger.Log("InjectLib (AWO) support found!");
                    }
                    _setting.Converters.Add(new LocalizedTextConverter());
                }
            }

            if (MTFOPartialDataUtil.IsLoaded)
            {
                MTFOPartialDataUtil.ReadPDataGUID();
            }

            //_setting.Converters.Add(new JsonStringEnumConverter());
            //_setting.Converters.Add(new MyVector3Converter());
            //if (MTFOPartialDataUtil.IsLoaded && MTFOPartialDataUtil.Initialized)
            //{
            //    _setting.Converters.Add(MTFOPartialDataUtil.PersistentIDConverter);
            //    _setting.Converters.Add(MTFOPartialDataUtil.LocalizedTextConverter);
            //    SPOLogger.Log("PartialData Support Found!");
            //}
            //else
            //{
            //    _setting.Converters.Add(new LocalizedTextConverter());
            //}

            //if (InjectLibUtil.IsLoaded)
            //{
            //    _setting.Converters.Add(InjectLibUtil.InjectLibConnector);
            //    SPOLogger.Log("InjectLib (AWO) support found!");
            //}
        }

        public static T Deserialize<T>(string json)
        {
            if (MTFOPartialDataUtil.IsLoaded && InjectLibUtil.IsLoaded)
            {
                json = MTFOPartialDataUtil.ConvertAllGUID(json);
            }

            return JsonSerializer.Deserialize<T>(json, _setting);
        }

        public static object Deserialize(Type type, string json)
        {
            if (MTFOPartialDataUtil.IsLoaded && InjectLibUtil.IsLoaded)
            {
                json = MTFOPartialDataUtil.ConvertAllGUID(json);
            }

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
