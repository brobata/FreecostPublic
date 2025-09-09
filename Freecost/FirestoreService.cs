using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Freecost
{
    public static class FirestoreService
    {
        private static readonly HttpClient _httpClient = new();
        private const string FirebaseProjectId = "gfyfoodcost"; // Your Firebase Project ID
        private static readonly string _baseApiUrl = $"https://firestore.googleapis.com/v1/projects/{FirebaseProjectId}/databases/(default)/documents";

        public static Task InitializeAsync() => Task.CompletedTask;

        #region Get Data
        public static async Task<T?> GetDocumentAsync<T>(string path, string? authToken) where T : class
        {
            if (string.IsNullOrEmpty(authToken)) return null;
            var request = new HttpRequestMessage(HttpMethod.Get, $"{_baseApiUrl}/{path}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authToken);
            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode) return null;
            var jsonString = await response.Content.ReadAsStringAsync();
            var firestoreDoc = JsonConvert.DeserializeObject<FirestoreDocument>(jsonString);
            var item = firestoreDoc?.To<T>();
            if (item != null)
            {
                var idProperty = typeof(T).GetProperty("Id");
                if (idProperty != null && idProperty.CanWrite)
                {
                    idProperty.SetValue(item, path.Split('/').Last());
                }
            }
            return item;
        }

        public static async Task<List<T>> GetCollectionAsync<T>(string path, string? authToken) where T : class, new()
        {
            if (string.IsNullOrEmpty(authToken)) return new List<T>();
            var list = new List<T>();
            var request = new HttpRequestMessage(HttpMethod.Get, $"{_baseApiUrl}/{path}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authToken);
            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode) return list;
            var jsonString = await response.Content.ReadAsStringAsync();
            var firestoreResponse = JsonConvert.DeserializeObject<FirestoreCollection>(jsonString);
            if (firestoreResponse?.Documents != null)
            {
                foreach (var doc in firestoreResponse.Documents)
                {
                    var item = doc.To<T>();
                    if (item != null)
                    {
                        var id = doc.Name?.Split('/').LastOrDefault();
                        var idProperty = typeof(T).GetProperty("Id");
                        if (idProperty != null && idProperty.CanWrite && id != null)
                        {
                            idProperty.SetValue(item, id);
                        }
                        list.Add(item);
                    }
                }
            }
            return list;
        }
        #endregion

        #region Save Data
        public static async Task<bool> AddDocumentAsync<T>(string collectionPath, T data, string? authToken) where T : class
        {
            if (string.IsNullOrEmpty(authToken)) return false;
            var firestoreDoc = FirestoreDocument.From(data);
            var jsonPayload = JsonConvert.SerializeObject(firestoreDoc, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
            var request = new HttpRequestMessage(HttpMethod.Post, $"{_baseApiUrl}/{collectionPath}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authToken);
            request.Content = content;
            var response = await _httpClient.SendAsync(request);
            return response.IsSuccessStatusCode;
        }

        public static async Task<bool> SetDocumentAsync<T>(string documentPath, T data, string? authToken) where T : class
        {
            if (string.IsNullOrEmpty(authToken)) return false;
            var firestoreDoc = FirestoreDocument.From(data);
            var jsonPayload = JsonConvert.SerializeObject(firestoreDoc, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
            var request = new HttpRequestMessage(new HttpMethod("PATCH"), $"{_baseApiUrl}/{documentPath}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authToken);
            request.Content = content;
            var response = await _httpClient.SendAsync(request);
            return response.IsSuccessStatusCode;
        }
        #endregion

        #region Delete Data
        public static async Task<bool> DeleteDocumentAsync(string documentPath, string? authToken)
        {
            if (string.IsNullOrEmpty(authToken)) return false;
            var request = new HttpRequestMessage(HttpMethod.Delete, $"{_baseApiUrl}/{documentPath}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authToken);
            var response = await _httpClient.SendAsync(request);
            return response.IsSuccessStatusCode;
        }
        #endregion

        #region Helper Classes
        private class FirestoreCollection
        {
            public List<FirestoreDocument>? Documents { get; set; }
        }

        private class FirestoreDocument
        {
            public string? Name { get; set; }
            public JObject? Fields { get; set; }

            public T? To<T>() where T : class
            {
                if (Fields == null) return null;
                var obj = new JObject();
                foreach (var prop in Fields.Properties())
                {
                    obj.Add(prop.Name, UnwrapFirestoreValue(prop.Value));
                }
                return obj.ToObject<T>();
            }

            private JToken? UnwrapFirestoreValue(JToken token)
            {
                if (token is not JObject valueWrapper) return null;
                var typeKey = valueWrapper.Properties().FirstOrDefault()?.Name;
                var rawValue = valueWrapper.Properties().FirstOrDefault()?.Value;

                switch (typeKey)
                {
                    case "mapValue":
                        var mapFields = rawValue?["fields"];
                        if (mapFields is JObject jObjectFields)
                        {
                            var netObj = new JObject();
                            foreach (var fieldProp in jObjectFields.Properties())
                            {
                                var unwrappedValue = UnwrapFirestoreValue(fieldProp.Value);
                                if (unwrappedValue != null)
                                {
                                    netObj.Add(fieldProp.Name, unwrappedValue);
                                }
                            }
                            return netObj;
                        }
                        return new JObject();

                    case "arrayValue":
                        var arrayValues = rawValue?["values"];
                        if (arrayValues is JArray jArray)
                        {
                            var netArray = new JArray();
                            foreach (var item in jArray)
                            {
                                var unwrappedValue = UnwrapFirestoreValue(item);
                                if (unwrappedValue != null)
                                {
                                    netArray.Add(unwrappedValue);
                                }
                            }
                            return netArray;
                        }
                        return new JArray();
                    default:
                        return rawValue;
                }
            }

            public static FirestoreDocument From<T>(T data) where T : class
            {
                var jsonData = JObject.FromObject(data, new JsonSerializer { NullValueHandling = NullValueHandling.Ignore });
                var fields = new JObject();
                foreach (var prop in jsonData.Properties())
                {
                    if (prop.Name.Equals("Id", StringComparison.OrdinalIgnoreCase)) continue;
                    fields.Add(prop.Name, WrapFirestoreValue(prop.Value));
                }
                return new FirestoreDocument { Fields = fields };
            }

            private static JObject WrapFirestoreValue(JToken? value)
            {
                if (value == null) return new JObject { { "nullValue", null } };
                switch (value.Type)
                {
                    case JTokenType.String:
                        return new JObject { { "stringValue", value } };
                    case JTokenType.Float:
                    case JTokenType.Integer:
                        return new JObject { { "doubleValue", value } };
                    case JTokenType.Boolean:
                        return new JObject { { "booleanValue", value } };
                    case JTokenType.Array:
                        var arrayValues = ((JArray)value).Select(WrapFirestoreValue);
                        return new JObject { { "arrayValue", new JObject { { "values", new JArray(arrayValues) } } } };
                    case JTokenType.Object:
                        var mapValues = new JObject();
                        foreach (var mapProp in ((JObject)value).Properties())
                        {
                            mapValues.Add(mapProp.Name, WrapFirestoreValue(mapProp.Value));
                        }
                        return new JObject { { "mapValue", new JObject { { "fields", mapValues } } } };
                    default:
                        return new JObject { { "stringValue", value.ToString() } };
                }
            }
        }
        #endregion
    }
}