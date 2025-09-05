using System.Text.Json;

namespace Freecost;

public static class LocalStorageService
{
    private static readonly string _basePath = FileSystem.AppDataDirectory;

    private static string GetPathFor<T>() => Path.Combine(_basePath, $"{typeof(T).Name}.json");

    public static async Task<List<T>> LoadAsync<T>()
    {
        var path = GetPathFor<T>();
        if (!File.Exists(path))
        {
            return new List<T>();
        }

        var json = await File.ReadAllTextAsync(path);
        return JsonSerializer.Deserialize<List<T>>(json) ?? new List<T>();
    }

    public static async Task SaveAsync<T>(List<T> data)
    {
        var path = GetPathFor<T>();
        var json = JsonSerializer.Serialize(data);
        await File.WriteAllTextAsync(path, json);
    }
}