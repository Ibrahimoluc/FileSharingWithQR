using System.Text.Json;

namespace FileSharingWithQR.Services
{
    public class FileCleanupService : BackgroundService
    {
        private readonly ILogger<FileCleanupService> _logger;
        private readonly string _uploadFolder = "UploadedFiles";

        public FileCleanupService(ILogger<FileCleanupService> logger)
        {
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Dosya temizleme servisi başladı.");

            // Uygulama çalıştığı sürece döngü devam eder
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    CleanExpiredFiles();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Dosya temizleme sırasında hata oluştu.");
                }

                // Örn: Her 1 saatte bir çalışsın (Test için süreyi kısaltabilirsin)
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }

        private void CleanExpiredFiles()
        {
            if (!Directory.Exists(_uploadFolder)) return;

            // Klasördeki tüm .json (metadata) dosyalarını bul
            var metaFiles = Directory.GetFiles(_uploadFolder, "*.json");

            foreach (var metaFile in metaFiles)
            {
                try
                {
                    // JSON'ı oku
                    var jsonContent = File.ReadAllText(metaFile);
                    var metadata = JsonSerializer.Deserialize<FileMetadata>(jsonContent);

                    // Süresi dolmuş mu?
                    if (metadata != null && DateTime.UtcNow > metadata.ExpireDate)
                    {
                        // 1. Asıl dosyayı sil
                        // (Meta dosyasının uzantısını değiştirerek asıl dosya yolunu buluyoruz)
                        // Örn: guid.json -> guid.docx (veya guid.* diyip hepsini silebilirsin)

                        // Not: Asıl dosyanın uzantısını json içinde tutarsan daha net olur.
                        // Şimdilik aynı isimdeki tüm uzantıları siliyoruz:
                        var fileNameWithoutExt = Path.GetFileNameWithoutExtension(metaFile);
                        var relatedFiles = Directory.GetFiles(_uploadFolder, fileNameWithoutExt + ".*");

                        foreach (var fileToDelete in relatedFiles)
                        {
                            File.Delete(fileToDelete);
                            _logger.LogInformation($"Süresi dolan dosya silindi: {fileToDelete}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    // Tek bir dosyada hata olursa döngü kırılmasın, loglayıp devam etsin
                    _logger.LogWarning($"Dosya silinemedi: {metaFile}. Hata: {ex.Message}");
                }
            }
        }
    }

    // Basit metadata sınıfı
    public class FileMetadata
    {
        public DateTime ExpireDate { get; set; }
        public string OriginalFileName { get; set; } // Opsiyonel
    }
}
