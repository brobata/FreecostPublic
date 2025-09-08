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
        string fileName = typeof(T).Name + ".json";
        if (!string.IsNullOrEmpty(restaurantId))
        {
            fileName = $"{typeof(T).Name}_{restaurantId}.json";
        }
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
        await SaveAsync(allData.ImportMaps); // Maps are global
        await SaveAsync(allData.UnitConversions); // Conversions are global
    }

    public static async Task<AllData> GetAllDataAsync(string restaurantId)
    {
        var allData = new AllData
        {
            Ingredients = await LoadAsync<IngredientCsvRecord>(restaurantId),
            Recipes = await LoadAsync<Recipe>(restaurantId),
            Entrees = await LoadAsync<Entree>(restaurantId),
            ImportMaps = await LoadAsync<ImportMap>(), // Maps are global
            UnitConversions = await LoadAsync<UnitConversion>() // Conversions are global
        };
        return allData;
    }
}