
using System.Text.Json;

namespace ClientLibrary.Helpers
{
    public class Serializations
    {
        public static string SerializeObj<T>(T modelObject) => JsonSerializer.Serialize(modelObject);
        public static T? DeserializeJsonString<T>(string jsonString) => JsonSerializer.Deserialize<T>(jsonString);
        public static IList<T>? DeserializeJsonList<T>(string jsonString) => JsonSerializer.Deserialize<IList<T>>(jsonString);
    }
}
