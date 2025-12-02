using System.Collections;
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


        [HttpGet("FileToken/{fileId}")]
        [GoogleScopedAuthorize(DriveService.ScopeConstants.DriveReadonly)]
        public async Task<IActionResult> GetFileToken([FromServices] IGoogleAuthProvider auth, string fileId, DateTime endTime)
        {
            try
            {
                Console.WriteLine("end:" + endTime);
                Console.WriteLine("now:" + DateTime.UtcNow);
                if (DateTime.Compare(endTime, DateTime.UtcNow) <= 0)
                {
                    Console.WriteLine("GetFileToken: endTime is invalid");
                    throw new ApplicationException("The sharing time of the file is ended");
                }

                string fileGuid = await GoogleHelper.DownloadADriveFile(auth, fileId);
        
                return Ok(new { token = JwtHelper.CreateToken(fileGuid, endTime) });
            }
            catch(NotSupportedException e)
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
                return BadRequest("Dosya yüklenmedi.");
            }

  
            if (file.Length > 10 * 1024 * 1024)
            {
                Console.WriteLine("Dosya boyutu 10MB'tan büyük olamaz.");
                return BadRequest("Dosya boyutu 10MB'tan büyük olamaz.");
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
                    return BadRequest("Desteklenmeyen dosya türü.");
                }

                // 4. Benzersiz dosya adýný oluþtur
                // örn: "0a1b2c3d-e4f5-4a5b-8c9d-0a1b2c3d4e5f.docx"
                var newFileName = $"{Guid.NewGuid()}{extension}";

                // 5. Tam dosya yolunu güvenli bir þekilde birleþtir
                var filePath = Path.Combine(directoryPath, newFileName);

                // 6. Dosyayý diske kaydet
                using (var stream = System.IO.File.Create(filePath))
                {
                    await file.CopyToAsync(stream);
                }

                // 7. Frontend'e dosyanýn yeni kimliðini (adýný) döndür (Çözüm 2)
                // DÝKKAT: GetFile API'niz dosya adýný UZANTISIZ bekliyordu.
                // Bu yüzden sadece GUID'i (uzantýsýz halini) döndürmek daha doðru olabilir.
                // Bu, GetFile API'nizdeki dosya arama mantýðýnýza baðlý.

                // Öneri: GetFile API'nizi de uzantýlý isimle arayacak þekilde güncelleyin.
                // Þimdilik, GetFile'ýn uzantýsýz aradýðýný varsayarak:
                var fileId = Path.GetFileNameWithoutExtension(newFileName); // Sadece GUID kýsmý

                // Eðer GetFile'ý güncellerseniz þunu kullanýn:
                // var fileId = newFileName; // "guid.docx"

                return Ok(new { token = JwtHelper.CreateToken(fileId, endTime) });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Dosya yükleme hatasý: {ex.Message}");
                return StatusCode(500, "Dosya yüklenirken sunucuda bir hata oluþtu.");
            }
        }


        [HttpGet("File")]
        public async Task<IActionResult> GetFile([FromQuery]string token)
        {
            //try
            //{
                //-check token endDate is valid
                string fileId = "";
                if (!JwtHelper.VerifyToken(token, out fileId)) 
                {
                    return StatusCode(StatusCodes.Status401Unauthorized, "The token is invalid. Sharing time is expired.");
                }
 
                //-get file by fileId
                Console.WriteLine("GetFile.fileId" + fileId);

                var files = Directory.GetFiles("UploadedFiles");
                Console.WriteLine("files.length:" + files.Length);

                //var filesWithoutExts = files.Select(file => file.Remove(file.LastIndexOf('.')));

                var fileIdwithPath = "UploadedFiles\\" + fileId;
                string? filePath = null;
                string fileExt = "";
                foreach (var file in files)
                {
                    var fileWithoutext = file.Remove(file.LastIndexOf('.'));
                    if (fileWithoutext == fileIdwithPath)
                    {
                        filePath = file;
                        fileExt = file.Substring(file.LastIndexOf('.') + 1);
                    }
                }

                if (filePath == null)
                {
                    Console.WriteLine("tokende belirtilen dosya sistemde bulunamadý, file:" + fileId);
                    return StatusCode(StatusCodes.Status401Unauthorized, "The file is not found.");
                }

                Console.WriteLine("fileExt:" + fileExt);
                Console.WriteLine("FilePath:" + filePath);
                var stream = System.IO.File.OpenRead(filePath);

                string mimeType = "";
                if(!FileServices.TryGetMimeType(fileExt, out mimeType))
                {
                    return BadRequest($"This file type ({fileExt}) is not supported. Supported types are: docx, pdf, jpeg, png");
                }

            string fileDownloadName = fileId + "." + fileExt;

            return File(stream, mimeType, fileDownloadName);
            //}
           //catch (Exception e)
            //{
                
            //}
        
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
