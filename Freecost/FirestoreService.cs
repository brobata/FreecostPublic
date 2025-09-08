using Plugin.Firebase.Firestore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Freecost
{
    public static class FirestoreService
    {
        private static IFirestore _firestore = CrossFirebase.Current.Firestore;

        public static async Task<List<UnitConversion>> GetUnitConversionsAsync()
        {
            var snapshot = await _firestore.Collection("unitConversions").GetAsync();
            return snapshot.Documents.Select(doc =>
            {
                var conversion = doc.ToObject<UnitConversion>();
                conversion.Id = doc.Id;
                return conversion;
            }).ToList();
        }

        public static async Task<List<ImportMap>> GetImportMapsAsync()
        {
            var snapshot = await _firestore.Collection("importMaps").GetAsync();
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
            var snapshot = await _firestore.Collection("restaurants").Document(restaurantId).Collection("ingredients").GetAsync();
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
            var snapshot = await _firestore.Collection("recipes").WhereEqualsTo("RestaurantId", restaurantId).GetAsync();
            return snapshot.Documents.Select(doc => {
                var recipe = doc.ToObject<Recipe>();
                recipe.Id = doc.Id;
                return recipe;
            }).ToList();
        }

        public static async Task<List<Entree>> GetEntreesAsync(string restaurantId)
        {
            if (string.IsNullOrEmpty(restaurantId)) return new List<Entree>();
            var snapshot = await _firestore.Collection("entrees").WhereEqualsTo("RestaurantId", restaurantId).GetAsync();
            return snapshot.Documents.Select(doc => {
                var entree = doc.ToObject<Entree>();
                entree.Id = doc.Id;
                return entree;
            }).ToList();
        }
    }
}