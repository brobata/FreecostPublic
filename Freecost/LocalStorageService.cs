using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.IO;

namespace Freecost;

public static class LocalStorageService
{
    private static readonly string _basePath = FileSystem.AppDataDirectory;

    private static string GetPathFor<T>(string? restaurantId = null)
    {
        string typeName = typeof(T).Name;
        // For global, non-restaurant-specific data that is part of the online experience
        if (typeName == nameof(Restaurant) || typeName == nameof(UnitConversion) || typeName == nameof(ImportMap))
        {
            return Path.Combine(_basePath, typeName + ".json");
        }

        string fileName = $"{typeName}_{restaurantId ?? SessionService.LocalDataId}.json";
        return Path.Combine(_basePath, fileName);
    }

    public static async Task<List<T>> LoadAsync<T>(string? restaurantId = null) where T : new()
    {
        var path = GetPathFor<T>(restaurantId);
        if (!File.Exists(path))
        {
            return new List<T>();
        }
        var json = await File.ReadAllTextAsync(path);
        return JsonSerializer.Deserialize<List<T>>(json) ?? new List<T>();
    }

    public static async Task SaveAsync<T>(List<T> data, string? restaurantId = null)
    {
        var path = GetPathFor<T>(restaurantId);
        var json = JsonSerializer.Serialize(data);
        await File.WriteAllTextAsync(path, json);
    }

    public static async Task SaveAllDataAsync(string restaurantId, AllData allData)
    {
        await SaveAsync(allData.Ingredients, restaurantId);
        await SaveAsync(allData.Recipes, restaurantId);
        await SaveAsync(allData.Entrees, restaurantId);
        await SaveAsync(allData.ImportMaps);
        await SaveAsync(allData.UnitConversions);
    }

    public static async Task<AllData> GetAllDataAsync(string restaurantId)
    {
        var allData = new AllData
        {
            Ingredients = await LoadAsync<IngredientCsvRecord>(restaurantId),
            Recipes = await LoadAsync<Recipe>(restaurantId),
            Entrees = await LoadAsync<Entree>(restaurantId),
            ImportMaps = await LoadAsync<ImportMap>(),
            UnitConversions = await LoadAsync<UnitConversion>()
        };
        return allData;
    }

    public static Task ClearAllOnlineDataAsync()
    {
        var files = Directory.GetFiles(_basePath, "*.json");
        foreach (var file in files)
        {
            if (!Path.GetFileName(file).Contains(SessionService.LocalDataId))
            {
                File.Delete(file);
            }
        }
        return Task.CompletedTask;
    }
}
