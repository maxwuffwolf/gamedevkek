using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Game.Data
{
    /// <summary>Диапазон чисел. В JSON пишется как [min, max] или просто число.</summary>
    [JsonConverter(typeof(FloatRangeConverter))]
    public struct FloatRange
    {
        public float Min;
        public float Max;

        public FloatRange(float min, float max) { Min = min; Max = max; }
        public FloatRange(float value) : this(value, value) { }

        public float Roll(Random r) => Min + (float)r.NextDouble() * (Max - Min);
        public override string ToString() => Math.Abs(Min - Max) < 0.0001f ? Min.ToString("0.##") : $"{Min:0.##}-{Max:0.##}";
    }

    /// <summary>Целочисленный диапазон (включительно). В JSON: [min, max] или число.</summary>
    [JsonConverter(typeof(IntRangeConverter))]
    public struct IntRange
    {
        public int Min;
        public int Max;

        public IntRange(int min, int max) { Min = min; Max = max; }
        public IntRange(int value) : this(value, value) { }

        public int Roll(Random r) => Max <= Min ? Min : r.Next(Min, Max + 1);
        public override string ToString() => Min == Max ? Min.ToString() : $"{Min}-{Max}";
    }

    public sealed class FloatRangeConverter : JsonConverter<FloatRange>
    {
        public override FloatRange ReadJson(JsonReader reader, Type objectType, FloatRange existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            var t = JToken.Load(reader);
            if (t.Type == JTokenType.Array)
            {
                var a = (JArray)t;
                if (a.Count == 0) return default;
                float min = a[0].Value<float>();
                float max = a.Count > 1 ? a[1].Value<float>() : min;
                return new FloatRange(min, max);
            }
            return new FloatRange(t.Value<float>());
        }

        public override void WriteJson(JsonWriter writer, FloatRange value, JsonSerializer serializer)
        {
            writer.WriteStartArray();
            writer.WriteValue(value.Min);
            writer.WriteValue(value.Max);
            writer.WriteEndArray();
        }
    }

    public sealed class IntRangeConverter : JsonConverter<IntRange>
    {
        public override IntRange ReadJson(JsonReader reader, Type objectType, IntRange existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            var t = JToken.Load(reader);
            if (t.Type == JTokenType.Array)
            {
                var a = (JArray)t;
                if (a.Count == 0) return default;
                int min = a[0].Value<int>();
                int max = a.Count > 1 ? a[1].Value<int>() : min;
                return new IntRange(min, max);
            }
            return new IntRange(t.Value<int>());
        }

        public override void WriteJson(JsonWriter writer, IntRange value, JsonSerializer serializer)
        {
            writer.WriteStartArray();
            writer.WriteValue(value.Min);
            writer.WriteValue(value.Max);
            writer.WriteEndArray();
        }
    }
}
