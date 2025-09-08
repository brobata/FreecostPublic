using Plugin.Firebase.Firestore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Plugin.Firebase.Core;

namespace Freecost
{
    public static class FirestoreService
    {
        private static IFirestore Firestore => CrossFirebase.Current.Firestore;

        public static Task InitializeAsync() => Task.CompletedTask;

        public static async Task SyncLocalToServer()
        {
            var restaurantId = SessionService.CurrentRestaurant?.Id;
            if (string.IsNullOrEmpty(restaurantId)) return;

            var localData = await LocalStorageService.GetAllDataAsync(restaurantId);
            var batch = Firestore.CreateBatch();

            var ingredientsCollection = Firestore.Collection("restaurants").Document(restaurantId).Collection("ingredients");
            foreach (var item in localData.Ingredients)
            {
                if (!string.IsNullOrEmpty(item.Id)) batch.Set(ingredientsCollection.Document(item.Id), item);
            }

            var recipesCollection = Firestore.Collection("recipes");
            foreach (var item in localData.Recipes)
            {
                if (!string.IsNullOrEmpty(item.Id)) batch.Set(recipesCollection.Document(item.Id), item);
            }

            var entreesCollection = Firestore.Collection("entrees");
            foreach (var item in localData.Entrees)
            {
                if (!string.IsNullOrEmpty(item.Id)) batch.Set(entreesCollection.Document(item.Id), item);
            }

            await batch.CommitAsync();
        }

        public static async Task SyncServerToLocal()
        {
            var restaurantId = SessionService.CurrentRestaurant?.Id;
            if (string.IsNullOrEmpty(restaurantId)) return;

            var serverIngredients = await GetIngredientsAsync(restaurantId);
            await LocalStorageService.SaveAsync(serverIngredients, restaurantId);

            var serverRecipes = await GetRecipesAsync(restaurantId);
            await LocalStorageService.SaveAsync(serverRecipes, restaurantId);

            var serverEntrees = await GetEntreesAsync(restaurantId);
            await LocalStorageService.SaveAsync(serverEntrees, restaurantId);
        }

        public static async Task<List<UnitConversion>> GetUnitConversionsAsync()
        {
            var snapshot = await Firestore.Collection("unitConversions").GetAsync();
            return snapshot.Documents.Select(doc =>
            {
                var conversion = doc.ToObject<UnitConversion>();
                conversion.Id = doc.Id;
                return conversion;
            }).ToList();
        }

        public static async Task<List<ImportMap>> GetImportMapsAsync()
        {
            var snapshot = await Firestore.Collection("importMaps").GetAsync();
            return snapshot.Documents.Select(doc =>
            {
                var map = doc.ToObject<ImportMap>();
                map.Id = doc.Id;
                return map;
            }).ToList();
        }

        public static async Task<List<IngredientCsvRecord>> GetIngredientsAsync(string restaurantId)
        {
            if (string.IsNullOrEmpty(restaurantId)) return new List<IngredientCsvRecord>();
            var snapshot = await Firestore.Collection("restaurants").Document(restaurantId).Collection("ingredients").GetAsync();
            return snapshot.Documents.Select(doc =>
            {
                var ingredient = doc.ToObject<IngredientCsvRecord>();
                ingredient.Id = doc.Id;
                return ingredient;
            }).ToList();
        }

        public static async Task<List<Recipe>> GetRecipesAsync(string restaurantId)
        {
            if (string.IsNullOrEmpty(restaurantId)) return new List<Recipe>();
            var snapshot = await Firestore.Collection("recipes").WhereEqualsTo("RestaurantId", restaurantId).GetAsync();
            return snapshot.Documents.Select(doc => {
                var recipe = doc.ToObject<Recipe>();
                recipe.Id = doc.Id;
                return recipe;
            }).ToList();
        }

        public static async Task<List<Entree>> GetEntreesAsync(string restaurantId)
        {
            if (string.IsNullOrEmpty(restaurantId)) return new List<Entree>();
            var snapshot = await Firestore.Collection("entrees").WhereEqualsTo("RestaurantId", restaurantId).GetAsync();
            return snapshot.Documents.Select(doc => {
                var entree = doc.ToObject<Entree>();
                entree.Id = doc.Id;
                return entree;
            }).ToList();
        }
    }
}

