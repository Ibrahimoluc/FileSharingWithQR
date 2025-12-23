using System;
using System.IO;
using System.Text.Json;
using Google.Apis.Auth.AspNetCore3;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Download;
using Google.Apis.Drive.v3;
using Google.Apis.Drive.v3.Data;
using Google.Apis.Services;
using static Google.Apis.Drive.v3.FilesResource;

namespace FileSharingWithQR.Services
{
    public class GoogleHelper
    {
        public static async Task<string> DownloadADriveFile(IGoogleAuthProvider auth, string fileId, DateTime endTime)
        {
            GoogleCredential cred = await auth.GetCredentialAsync();
            var service = new DriveService(new BaseClientService.Initializer
            {
                HttpClientInitializer = cred
            });


            var request = service.Files.Get(fileId);
            var fileMetaData = await request.ExecuteAsync();
            var mimeType = fileMetaData.MimeType;
            ExportRequest? exportRequest = null;

            if (IsGoogleWorkspaceFile(mimeType))
            {
                // Hangi formata dönüştüreceğimizi belirliyoruz
                var exportMimeType = GetExportMimeType(mimeType);
                Console.WriteLine($"Google Dosyası algılandı. {exportMimeType} formatına dönüştürülüyor...");

                // Export isteği oluştur
                exportRequest = service.Files.Export(fileId, exportMimeType);

                // İndirirken ilerlemeyi takip et (Opsiyonel)
                exportRequest.MediaDownloader.ProgressChanged += progress =>
                {
                    if (progress.Status == DownloadStatus.Failed)
                    {
                        Console.WriteLine("Dönüştürme başarısız: " + progress.Exception.Message);
                    }
                };

            }

            string extension = "";
            if(!FileServices.TryGetExt(mimeType, out extension))
            {
                throw new NotSupportedException(
                    $"This file type ({mimeType}) is not supported. Supported types are: docx, pdf, jpeg, png");
            }

            // Dosya uzunluğu kontrolü
            if(fileMetaData.Size > 15 * 1024 * 1024)
            {
                Console.WriteLine("Dosya boyutu 15MB'tan büyük olamaz.");
                throw new ApplicationException("Dosya boyutu 15MB'tan büyük olamaz.");
            }

            string fileGuid = Guid.NewGuid().ToString();
            var fileName = fileGuid + "." + extension;
            string directoryPath = "UploadedFiles";
            Directory.CreateDirectory(directoryPath);

            var filePath = Path.Combine(directoryPath, fileName);
            var stream = System.IO.File.Create(filePath);

            if (exportRequest != null)
            {
                await exportRequest.DownloadAsync(stream);
            }
            else
            {
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
                await request.DownloadAsync(stream);
            }

            // --- YENİ KISIM: Metadata JSON oluştur ---
            var metadata = new FileMetadata
            {
                ExpireDate = endTime.ToUniversalTime(), // UTC kaydetmek her zaman iyidir
                OriginalFileName = fileName
            };

            var jsonPath = Path.Combine(directoryPath, $"{fileGuid}.json");
            await System.IO.File.WriteAllTextAsync(jsonPath, JsonSerializer.Serialize(metadata));

            stream.Dispose();
            return fileName;
        }

        public static async Task<(Stream, string, string)> DownloadADriveFileWithMemoryStream(IGoogleAuthProvider auth, string fileId)
        {
            GoogleCredential cred = await auth.GetCredentialAsync();
            var service = new DriveService(new BaseClientService.Initializer
            {
                HttpClientInitializer = cred
            });


            var request = service.Files.Get(fileId);
            var fileMetaData = await request.ExecuteAsync();
            var mimeType = fileMetaData.MimeType;
            ExportRequest? exportRequest = null;

            if (IsGoogleWorkspaceFile(mimeType))
            {
                // Hangi formata dönüştüreceğimizi belirliyoruz
                var exportMimeType = GetExportMimeType(mimeType);
                Console.WriteLine($"Google Dosyası algılandı. {exportMimeType} formatına dönüştürülüyor...");

                // Export isteği oluştur
                exportRequest = service.Files.Export(fileId, exportMimeType);

                // İndirirken ilerlemeyi takip et (Opsiyonel)
                exportRequest.MediaDownloader.ProgressChanged += progress =>
                {
                    if (progress.Status == DownloadStatus.Failed)
                    {
                        Console.WriteLine("Dönüştürme başarısız: " + progress.Exception.Message);
                    }
                };

            }

            var stream = new MemoryStream();

            if (exportRequest != null)
            {
                await exportRequest.DownloadAsync(stream);
            }
            else
            {
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
                await request.DownloadAsync(stream);
            }
            if (stream.CanSeek)
            {
                stream.Seek(0, SeekOrigin.Begin);
            }

            string extension = "";
            if(!FileServices.TryGetExt(mimeType, out extension))
            {
                throw new Exception("Invalid file type");
            }

            var filename = Guid.NewGuid().ToString() + "." + extension;
            return (stream, mimeType, filename);
        }


        // Dosyanın Google formatında olup olmadığını kontrol eder
        private static bool IsGoogleWorkspaceFile(string mimeType)
        {
            return mimeType == "application/vnd.google-apps.document" ||
                   mimeType == "application/vnd.google-apps.spreadsheet" ||
                   mimeType == "application/vnd.google-apps.presentation";
        }


        // Google formatını Office formatına eşler
        private static string GetExportMimeType(string googleMimeType)
        {
            switch (googleMimeType)
            {
                case "application/vnd.google-apps.document":
                    return "application/vnd.openxmlformats-officedocument.wordprocessingml.document"; // .docx
                case "application/vnd.google-apps.spreadsheet":
                    return "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";       // .xlsx
                case "application/vnd.google-apps.presentation":
                    return "application/vnd.openxmlformats-officedocument.presentationml.presentation"; // .pptx
                default:
                    return "application/pdf"; // Bilinmeyenleri PDF yap (Güvenli liman)
            }
        }

        // MimeType'a göre uzantı döner
        private string GetExtensionForMimeType(string mimeType)
        {
            switch (mimeType)
            {
                case "application/vnd.openxmlformats-officedocument.wordprocessingml.document": return ".docx";
                case "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet": return ".xlsx";
                case "application/vnd.openxmlformats-officedocument.presentationml.presentation": return ".pptx";
                case "application/pdf": return ".pdf";
                default: return "";
            }
        }
    }


}
