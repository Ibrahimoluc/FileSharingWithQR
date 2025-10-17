using Google.Apis.Auth.AspNetCore3;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Download;
using Google.Apis.Drive.v3;
using Google.Apis.Drive.v3.Data;
using Google.Apis.Services;

namespace FileSharingWithQR.Services
{
    public class GoogleHelper
    {
        public static async Task<Google.Apis.Drive.v3.Data.File> DownloadADriveFile(IGoogleAuthProvider auth, string fileId)
        {
            GoogleCredential cred = await auth.GetCredentialAsync();
            var service = new DriveService(new BaseClientService.Initializer
            {
                HttpClientInitializer = cred
            });


            var request = service.Files.Get(fileId);
            var fileMetaData = await request.ExecuteAsync();
            var mimeType = fileMetaData.MimeType;
            //var extension = "";

            //switch (mimeType)
            //{
            //    case "application/vnd.google-apps.document":
            //        extension = "docx";
            //        break;
            //    case "image/jpeg":
            //        extension = "jpg";
            //        break;
            //    default:
            //        throw new ArgumentException("This file type is not supported. Supported types are: docx, pdf, jpeg, png");
            //}
            string extension = FileServices.GetExt(mimeType);

            //var stream = new MemoryStream();
            var stream = System.IO.File.Create("UploadedFiles/" + fileId + "." + extension);

            // Add a handler which will be notified on progress changes.
            // It will notify on each chunk download and when the
            // download is completed or failed.
            request.MediaDownloader.ProgressChanged +=
                progress =>
                {
                    switch (progress.Status)
                    {
                        case DownloadStatus.Downloading:
                            {
                                Console.WriteLine(progress.BytesDownloaded);
                                break;
                            }
                        case DownloadStatus.Completed:
                            {
                                Console.WriteLine("Download complete.");
                                break;
                            }
                        case DownloadStatus.Failed:
                            {
                                Console.WriteLine("Download failed. " + progress.Exception.Message);
                                break;
                            }
                    }
                };
            //request.DownloadWithStatus(stream);
            await request.DownloadAsync(stream);
            //if (stream.CanSeek)
            //{
            //    Console.WriteLine("Stream seek lenebilir");
            //    stream.Seek(0, SeekOrigin.Begin);
            //}
            //yapmassan istek bittikten hemen sonra, dosyayı 0kb olarak görebilirsin.
            //Ama belli bir süre geçince gerçekten yüklenmiş olur.
            //await stream.FlushAsync(); 

            stream.Dispose();

            return fileMetaData;
        }
    }
}
