using GTFO.API.Utilities;
using System;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using System.Text.Json;
using UnityEngine;

namespace ScanPosOverride.JSON
{
    public sealed class MyVector3Converter : JsonConverter<Vector3>
    {
        public override bool HandleNull => false;

        public override Vector3 Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            Vector3 vector = Vector3.zero;
            switch (reader.TokenType)
            {
                case JsonTokenType.StartObject:
                    {
                        int currentDepth = reader.CurrentDepth;
                        while (reader.Read())
                        {
                            if (reader.TokenType == JsonTokenType.EndObject && reader.CurrentDepth == currentDepth)
                            {
                                SPOLogger.Warning($"Parsed Vector3 : {vector}");
                                return vector;
                            }

                            if (reader.TokenType != JsonTokenType.PropertyName)
                            {
                                throw new JsonException("Expected PropertyName token");
                            }

                            string? @string = reader.GetString();
                            reader.Read();
                            switch (@string!.ToLowerInvariant())
                            {
                                case "x":
                                    vector.x = reader.GetSingle();
                                    break;
                                case "y":
                                    vector.y = reader.GetSingle();
                                    break;
                                case "z":
                                    vector.z = reader.GetSingle();
                                    break;
                            }
                        }

                        throw new JsonException("Expected EndObject token");
                    }
                case JsonTokenType.String:
                    {
                        string text = reader.GetString()!.Trim();
                        if (TryParseVector3(text, out vector))
                        {
                            return vector;
                        }

                        throw new JsonException("Vector3 format is not right: " + text);
                    }
                default:
                    {
                        DefaultInterpolatedStringHandler defaultInterpolatedStringHandler = new DefaultInterpolatedStringHandler(38, 1);
                        defaultInterpolatedStringHandler.AppendLiteral("Vector3Json type: ");
                        defaultInterpolatedStringHandler.AppendFormatted(reader.TokenType);
                        defaultInterpolatedStringHandler.AppendLiteral(" is not implemented!");
                        throw new JsonException(defaultInterpolatedStringHandler.ToStringAndClear());
                    }
            }
        }

        private static bool TryParseVector3(string input, out Vector3 vector)
        {
            if (!RegexUtils.TryParseVectorString(input, out var vectorArray))
            {
                vector = Vector3.zero;
                return false;
            }

            if (vectorArray.Length < 3)
            {
                vector = Vector3.zero;
                return false;
            }

            vector = new Vector3(vectorArray[0], vectorArray[1], vectorArray[2]);
            return true;
        }

        public override void Write(Utf8JsonWriter writer, Vector3 value, JsonSerializerOptions options)
        {
            writer.WriteStringValue($"({value.x} {value.y} {value.z})");
        }
    }

}
