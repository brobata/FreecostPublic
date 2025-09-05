using System;
using System.IO;
using System.Threading.Tasks;
using Google.Cloud.Firestore;
using Microsoft.Maui.Storage;

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
                FirebaseBucket = "gfyfoodcost.firebasestorage.app"; // This line is intentionally left as is, the fix is in FirebaseStorageService.cs
            }
            catch (Exception)
            {
                // Handle exceptions (e.g., log them, show an alert)
                // For now, we'll just rethrow to see the error during development
                throw;
            }
        }
    }
}