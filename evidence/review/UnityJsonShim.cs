// Review-only adapter. Real FileStream/File.Replace and production AtomicSaveStore are used;
// this does NOT validate Unity JsonUtility behavior or Unity Player runtime compatibility.
namespace UnityEngine
{
    public static class JsonUtility
    {
        private static readonly System.Text.Json.JsonSerializerOptions Options = new System.Text.Json.JsonSerializerOptions { IncludeFields = true };
        public static string ToJson(object value) => System.Text.Json.JsonSerializer.Serialize(value, value.GetType(), Options);
        public static T FromJson<T>(string json)
        {
            try { return System.Text.Json.JsonSerializer.Deserialize<T>(json, Options); }
            catch (System.Text.Json.JsonException e) { throw new System.ArgumentException(e.Message, e); }
        }
    }
}
