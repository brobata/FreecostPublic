using Firebase.Storage;
using System.IO;
using System.Threading.Tasks;

namespace Freecost
{
    public static class FirebaseStorageService
    {
        public static async Task<string> UploadImageAsync(Stream fileStream, string fileName)
        {
            var storage = new FirebaseStorage(
                "gfyfoodcost.firebasestorage.app", // Corrected bucket name
                new FirebaseStorageOptions
                {
                    AuthTokenAsyncFactory = () => Task.FromResult(SessionService.AuthToken)
                });

            var downloadUrl = await storage.Child("images")
                                           .Child(fileName)
                                           .PutAsync(fileStream);

            return downloadUrl;
        }
    }
}