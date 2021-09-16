using System.Collections.Generic;

namespace EFMOffline.Models
{
    public class MediaMetadata
    {
        public string Field { get; set; }

        public string Label { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("Value")]
        public System.Text.Json.JsonElement RawValue { get; set; }

        [System.Text.Json.Serialization.JsonIgnore]
        public List<string> Value
        {
            get
            {
                if (RawValue.ValueKind == System.Text.Json.JsonValueKind.String)
                    return new List<string> { RawValue.GetString() };
                else if (RawValue.ValueKind == System.Text.Json.JsonValueKind.Array)
                {
                    var values = new List<string>();
                    var enumerator = RawValue.EnumerateArray();

                    while (enumerator.MoveNext())
                    {
                        values.Add(enumerator.Current.GetString());
                    }
                    return values;
                }

                return null;
            }
        }
    }
}
