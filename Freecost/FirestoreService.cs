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

        public static async Task InitializeAsync()
        {
            if (Db != null)
            {
                return;
            }

            try
            {
                string keyFileName = "gfyfoodcost-firebase-adminsdk-fbsvc-cee0d30bfe.json";
                using var stream = await FileSystem.OpenAppPackageFileAsync(keyFileName);
                string tempPath = Path.Combine(FileSystem.AppDataDirectory, keyFileName);

                using (var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write))
                {
                    await stream.CopyToAsync(fileStream);
                }

                Environment.SetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS", tempPath);
                Db = FirestoreDb.Create("gfyfoodcost");
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
