using Localization;
using ScanPosOverride.JSON.PData;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Text.Json;
using System.Threading.Tasks;

namespace ScanPosOverride.JSON
{
    public sealed class WritableLocalizedTextConverter : JsonConverter<LocalizedText>
    {
        public static WritableLocalizedTextConverter Converter { get; } = new();

        public override bool HandleNull => false;

        public override LocalizedText Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (MTFOPartialDataUtil.LocalizedTextConverter != null)
            {
                return ((JsonConverter<LocalizedText>)MTFOPartialDataUtil.LocalizedTextConverter).Read(ref reader, typeToConvert, options);
            }

            throw new NotImplementedException();
        }

        public override void Write(Utf8JsonWriter writer, LocalizedText value, JsonSerializerOptions options)
        {
            if (value.Id != 0)
            {
                JsonSerializer.Serialize(writer, value.Id, options); // TODO: write pdata GUID
            }
            else
            {
                JsonSerializer.Serialize(writer, value.UntranslatedText, options);
            }
        }
    }
}
