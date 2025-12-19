using System;
using System.Collections;
using System.Text.Json;
using FileSharingWithQR.Services;
using Google.Apis.Auth.AspNetCore3;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Download;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.TagHelpers;

namespace FileSharingWithQR.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class HomeController : ControllerBase
    {
        [HttpGet("driveFiles")]
        [GoogleScopedAuthorize(DriveService.ScopeConstants.DriveReadonly)]
        public async Task<IActionResult> DriveFileList([FromServices] IGoogleAuthProvider auth, int PageDirection)
        {
            GoogleCredential cred = await auth.GetCredentialAsync();
            var service = new DriveService(new BaseClientService.Initializer
            {
                HttpClientInitializer = cred
            });
            var filesRequest = service.Files.List();

            var stack = HttpContext.Session.Get<Stack<string>>("pageTokens");
            string pageToken = "";

            try
            {
                if (stack == null)
                {
                    Console.WriteLine("Session stack can not found");
                    stack = new Stack<string>();
                }
                else
                {
                    pageToken = PageTokenManager.GetPageToken(stack, PageDirection);
                }
            }
            catch(InvalidOperationException e)
            {
                Console.WriteLine("Stack boþaldi");
            }
            catch (ArgumentException e)
            {
                return BadRequest("PageDirection parameter should one of them: {1,0,-1}");
            }


            Console.WriteLine("PageToken for request:" + pageToken);

            filesRequest.PageToken = pageToken;
            var files = await filesRequest.ExecuteAsync();
            var filteredFiles = files.Files.Where(x => FileServices.IsMimeTypeValid(x.MimeType));
            var fileNamesAndIds = filteredFiles.Select(x => new { Id = x.Id, Name = x.Name }).ToList();

            
            stack.Push(files.NextPageToken);
            HttpContext.Session.SetStringStack("pageTokens", stack);

            Console.WriteLine();
            return Ok(fileNamesAndIds);
        }

        [HttpGet("driveFile-Preview/{fileId}")]
        [GoogleScopedAuthorize(DriveService.ScopeConstants.DriveReadonly)]
        public async Task<IActionResult> DrivePreviewFile([FromServices] IGoogleAuthProvider auth, string fileId)
        {
            try
            {
                GoogleCredential cred = await auth.GetCredentialAsync();
                var service = new DriveService(new BaseClientService.Initializer
                {
                    HttpClientInitializer = cred
                });

                // 1. Get isteði oluþtur
                var request = service.Files.Get(fileId);

                // 2. ÖNEMLÝ: Google'dan 'webViewLink' alanýný istediðimizi belirtiyoruz
                request.Fields = "webViewLink";

                // 3. Dosyanýn tamamýný DEÐÝL, sadece meta verisini al
                var fileMetaData = await request.ExecuteAsync();

                if (string.IsNullOrEmpty(fileMetaData.WebViewLink))
                {
                    // Bu dosyanýn bir web önizlemesi yoksa (belki bir binary dosyasýdýr)
                    // Hata dönebilir veya indirmeye zorlayabilirsiniz (mevcut kodunuz gibi)
                    return NotFound("Bu dosya için web önizlemesi bulunamadý.");
                }

                // 4. Ýndirme yok! MemoryStream yok!
                // Kullanýcýyý doðrudan Google'ýn önizleme linkine yönlendir.
                return Redirect(fileMetaData.WebViewLink);
            }
            catch (Exception e)
            {
                // ... (hata yönetimi kodunuz) ...
                Console.WriteLine("Hata: " + e.Message);
                return StatusCode(500, "Dosya önizlemesi alýnýrken bir hata oluþtu.");
            }
        }


        [HttpGet("driveFile-Download/{fileId}")]
        [GoogleScopedAuthorize(DriveService.ScopeConstants.DriveReadonly)]
        public async Task<IActionResult> DriveDownloadFile([FromServices] IGoogleAuthProvider auth, string fileId)
        {
            try
            {
                return Ok(await GoogleHelper.DownloadADriveFile(auth, fileId));
            }
            catch (Exception e)
            {
                // TODO(developer) - handle error appropriately
                if (e is AggregateException)
                {
                    Console.WriteLine("Credential Not found");
                }
                else
                {
                    throw;
                }
            }
            return NotFound();
        }

        [HttpGet("driveFile-Download2/{fileId}")]
        [GoogleScopedAuthorize(DriveService.ScopeConstants.DriveReadonly)]
        public async Task<IActionResult> DriveDownloadFilev2([FromServices] IGoogleAuthProvider auth, string fileId)
        {
            try
            {
                var result = await GoogleHelper.DownloadADriveFileWithMemoryStream(auth, fileId);
                return File(result.Item1, result.Item2);
            }
            catch (Exception e)
            {
                // TODO(developer) - handle error appropriately
                if (e is AggregateException)
                {
                    Console.WriteLine("Credential Not found");
                }
                else
                {
                    throw;
                }
            }
            return NotFound();
        }



        [HttpGet("FileToken/{fileId}")]
        [GoogleScopedAuthorize(DriveService.ScopeConstants.DriveReadonly)]
        public IActionResult GetFileToken([FromServices] IGoogleAuthProvider auth, string fileId, DateTime endTime)
        {
            try
            {
                Console.WriteLine("end:" + endTime);
                Console.WriteLine("now:" + DateTime.UtcNow);
                if (DateTime.Compare(endTime, DateTime.UtcNow) <= 0)
                {
                    Console.WriteLine("GetFileToken: endTime is invalid");
                    return BadRequest(new { error = "Expiration Date is invalid." });
                }

                return Ok(new { token = JwtHelper.CreateToken(fileId, endTime, "drive") });
            }
            catch (NotSupportedException e)
            {
                return BadRequest(e.Message);
            }
            catch (AggregateException e)
            {
                return Unauthorized(e.Message);
            }
        }


        [HttpPost("FileToken")]
        public async Task<IActionResult> GetLocalFileToken(IFormFile file, DateTime endTime)
        {
            Console.WriteLine("GetLocalFileToken.endTime:" + endTime);
            Console.WriteLine("DateTime.UtcNow:" + DateTime.UtcNow);
            if (DateTime.Compare(endTime, DateTime.UtcNow) <= 0)
            {
                return BadRequest(new { error = "End Time is not valid." });
            }
            // 0. Gelen dosyanýn temel kontrolleri (Güvenlik için iyi bir pratiktir)
            if (file == null || file.Length == 0)
            {
                Console.WriteLine("Dosya yüklenemedi");
                return BadRequest(new { error = "Dosya yüklenmedi." });
            }

  
            if (file.Length > 10 * 1024 * 1024)
            {
                Console.WriteLine("Dosya boyutu 10MB'tan büyük olamaz.");
                return BadRequest(new { error = "Dosya boyutu 10MB'tan büyük olamaz." });
            }

            try
            {
                // 1. Gerekli klasör adýný belirle
                string directoryPath = "UploadedFiles";

                // 2. Klasörün varlýðýný kontrol et (Çözüm 3)
                // Bu, uygulama baþladýðýnda sadece bir kez de yapýlabilir (örn: Program.cs)
                Directory.CreateDirectory(directoryPath);

                // 3. Dosya uzantýsýný al (Çözüm 1)
                // örn: ".docx" veya ".jpg"
                var extension = Path.GetExtension(file.FileName);

                 if (!FileServices.TryGetMimeType(extension.TrimStart('.'), out _))
                {
                    return BadRequest(new { error = "Desteklenmeyen dosya türü." });
                }

                // 4. Benzersiz dosya adýný oluþtur
                // örn: "0a1b2c3d-e4f5-4a5b-8c9d-0a1b2c3d4e5f.docx"
                var guid = Guid.NewGuid();
                var newFileName = $"{guid}{extension}";

                // 5. Tam dosya yolunu güvenli bir þekilde birleþtir
                var filePath = Path.Combine(directoryPath, newFileName);

                // 6. Dosyayý diske kaydet
                using (var stream = System.IO.File.Create(filePath))
                {
                    await file.CopyToAsync(stream);
                }

                // --- YENÝ KISIM: Metadata JSON oluþtur ---
                var metadata = new FileMetadata
                {
                    ExpireDate = endTime.ToUniversalTime(), // UTC kaydetmek her zaman iyidir
                    OriginalFileName = file.FileName
                };

                var jsonPath = Path.Combine(directoryPath, $"{guid}.json");
                await System.IO.File.WriteAllTextAsync(jsonPath, JsonSerializer.Serialize(metadata));


                return Ok(new { token = JwtHelper.CreateToken(newFileName, endTime, "local") });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Dosya yükleme hatasý: {ex.Message}");
                return StatusCode(500, new { error = "Dosya yüklenirken sunucuda bir hata oluþtu." });
            }
        }



        [HttpGet("File")]
        public async Task<IActionResult> GetFile([FromQuery] string token, [FromServices] IGoogleAuthProvider auth)
        {

            string fileName = "";
            string source = "";

            if (!JwtHelper.VerifyToken(token, out fileName, out source))
            {
                return StatusCode(StatusCodes.Status401Unauthorized, "The token is invalid. Sharing time is expired.");
            }

            //-get file by fileId
            Console.WriteLine("GetFile.fileName:" + fileName);

            var fileFolder = "UploadedFiles\\";
            if(source == "local")
            {
                var filePath = fileFolder + fileName;
 

                var extension = Path.GetExtension(filePath).TrimStart('.');
                Console.WriteLine("fileExt:" + extension);

                try
                {
                    var stream = System.IO.File.OpenRead(filePath);
                    string mimeType = "";
                    if (!FileServices.TryGetMimeType(extension, out mimeType))
                    {
                        return BadRequest(new { error = $"This file type ({extension}) is not supported. Supported types are: docx, pdf, jpeg, png, xlsx, pptx" });
                    }


                    return File(stream, mimeType, fileName);
                }
                catch(FileNotFoundException e)
                {
                    return NotFound();
                }
            }
            else if(source == "drive")
            {
                try
                {
                    var result = await GoogleHelper.DownloadADriveFileWithMemoryStream(auth, fileName);
                    return File(result.Item1, result.Item2, result.Item3);
                }
                catch (Exception e)
                {
                    // TODO(developer) - handle error appropriately
                    if (e is AggregateException)
                    {
                        Console.WriteLine("Credential Not found");
                    }
                    else
                    {
                        throw;
                    }
                }
                return NotFound();
            }
            else
            {
                return BadRequest("source value must be local or drive");
            }
        }


        [HttpGet("Login")]
        [Authorize]
        public IActionResult Login()
        {
            return Ok();
        }

        [HttpGet("Foo")]
        public IActionResult Foo()
        {
            var manager = new PageTokenManager();
            var str = "";
            if (string.IsNullOrEmpty(HttpContext.Session.GetString("Key")))
            {
                str = "yeni";
                HttpContext.Session.SetString("Key", "The Doctor");
            }
            var name = HttpContext.Session.GetString("Key");
            
            return Ok(str+name);
        }
    }
}
