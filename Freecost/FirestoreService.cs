using System;
using System.IO;
using System.Threading.Tasks;
using Google.Cloud.Firestore;
using Microsoft.Maui.Storage;
using System.Linq;

namespace Freecost
{
    public static class FirestoreService
    {
        public static FirestoreDb? Db { get; private set; }
        public static string FirebaseBucket { get; private set; } = string.Empty;


        public static async Task InitializeAsync()
        {
            if (Db != null)
            {
                return;
            }

            try
            {
                string keyFileName = "new_firebase_credentials.json";
                using var stream = await FileSystem.OpenAppPackageFileAsync(keyFileName);
                string tempPath = Path.Combine(FileSystem.AppDataDirectory, keyFileName);

                using (var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write))
                {
                    await stream.CopyToAsync(fileStream);
                }

                Environment.SetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS", tempPath);
                Db = FirestoreDb.Create("gfyfoodcost");
                FirebaseBucket = "gfyfoodcost.firebasestorage.app";
            }
            catch (Exception)
            {
                throw;
            }
        }

        public static async Task SyncServerToLocal()
        {
            if (Db == null || SessionService.CurrentRestaurant?.Id == null) return;
            string restaurantId = SessionService.CurrentRestaurant.Id;

            var conversionsQuery = Db.Collection("unitConversions");
            var conversionsSnapshot = await conversionsQuery.GetSnapshotAsync();
            var conversions = conversionsSnapshot.Documents.Select(doc => doc.ConvertTo<UnitConversion>()).ToList();
            await LocalStorageService.SaveAsync(conversions);

            // Fetch Ingredients
            var ingredientsQuery = Db.Collection("restaurants").Document(restaurantId).Collection("ingredients");
            var ingredientsSnapshot = await ingredientsQuery.GetSnapshotAsync();
            var ingredients = ingredientsSnapshot.Documents.Select(doc => doc.ConvertTo<IngredientCsvRecord>()).ToList();
            await LocalStorageService.SaveAsync(ingredients, restaurantId);

            // Fetch Recipes
            var recipesQuery = Db.Collection("recipes").WhereEqualTo("RestaurantId", restaurantId);
            var recipesSnapshot = await recipesQuery.GetSnapshotAsync();
            var recipes = recipesSnapshot.Documents.Select(doc => doc.ConvertTo<Recipe>()).ToList();
            await LocalStorageService.SaveAsync(recipes, restaurantId);

            // Fetch Entrees
            var entreesQuery = Db.Collection("entrees").WhereEqualTo("RestaurantId", restaurantId);
            var entreesSnapshot = await entreesQuery.GetSnapshotAsync();
            var entrees = entreesSnapshot.Documents.Select(doc => doc.ConvertTo<Entree>()).ToList();
            await LocalStorageService.SaveAsync(entrees, restaurantId);

            // Fetch Import Maps
            var mapsQuery = Db.Collection("importMaps");
            var mapsSnapshot = await mapsQuery.GetSnapshotAsync();
            var maps = mapsSnapshot.Documents.Select(doc => doc.ConvertTo<ImportMap>()).ToList();
            await LocalStorageService.SaveAsync(maps);
        }

        public static async Task SyncLocalToServer()
        {
            if (Db == null || SessionService.CurrentRestaurant?.Id == null) return;
            string restaurantId = SessionService.CurrentRestaurant.Id;

            var localConversions = await LocalStorageService.LoadAsync<UnitConversion>();
            var conversionsCollection = Db.Collection("unitConversions");
            foreach (var item in localConversions)
            {
                if (string.IsNullOrEmpty(item.Id))
                {
                    await conversionsCollection.AddAsync(item);
                }
                else
                {
                    await conversionsCollection.Document(item.Id).SetAsync(item, SetOptions.MergeAll);
                }
            }

            var allLocalData = await LocalStorageService.GetAllDataAsync(restaurantId);

            var ingredientsCollection = Db.Collection("restaurants").Document(restaurantId).Collection("ingredients");
            foreach (var item in allLocalData.Ingredients)
            {
                if (string.IsNullOrEmpty(item.Id))
                {
                    await ingredientsCollection.AddAsync(item);
                }
                else
                {
                    await ingredientsCollection.Document(item.Id).SetAsync(item, SetOptions.MergeAll);
                }
            }

            var recipesCollection = Db.Collection("recipes");
            foreach (var item in allLocalData.Recipes)
            {
                if (string.IsNullOrEmpty(item.Id))
                {
                    await recipesCollection.AddAsync(item);
                }
                else
                {
                    await recipesCollection.Document(item.Id).SetAsync(item, SetOptions.MergeAll);
                }
            }
            var entreesCollection = Db.Collection("entrees");
            foreach (var item in allLocalData.Entrees)
            {
                if (string.IsNullOrEmpty(item.Id)) await entreesCollection.AddAsync(item);
                else await entreesCollection.Document(item.Id).SetAsync(item, SetOptions.MergeAll);
            }

            var mapsCollection = Db.Collection("importMaps");
            foreach (var item in allLocalData.ImportMaps)
            {
                if (string.IsNullOrEmpty(item.Id)) await mapsCollection.AddAsync(item);
                else await mapsCollection.Document(item.Id).SetAsync(item, SetOptions.MergeAll);
            }
        }
    }
}