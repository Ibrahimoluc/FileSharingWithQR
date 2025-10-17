using System.Collections;
using FileSharingWithQR.Services;
using Google.Apis.Auth.AspNetCore3;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Download;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.TagHelpers;

namespace FileSharingWithQR.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class HomeController : ControllerBase
    {
        //private static List<string> pageTokens = new List<string>(new string[]{""});
        //private static Dictionary<int, string> pageTokensMap = new Dictionary<int, string>();

        //[HttpGet("driveFiles")]
        //[GoogleScopedAuthorize(DriveService.ScopeConstants.DriveReadonly)]
        //public async Task<IActionResult> DriveFileList([FromServices] IGoogleAuthProvider auth, int Page)
        //{
        //    GoogleCredential cred = await auth.GetCredentialAsync();
        //    var service = new DriveService(new BaseClientService.Initializer
        //    {
        //        HttpClientInitializer = cred
        //    });
        //    //var files = await service.Files.List().ExecuteAsync();
        //    var filesRequest = service.Files.List();
        //    //var files = await filesRequest.ExecuteAsync();

        //    //filesRequest.PageToken = files.NextPageToken;
        //    //positive value for getting Prev Page, 0 for getting nextPage
        //    string pageToken = Request.Cookies["CurrPageToken"] ?? ""; 
        //    if (Page==0)
        //    {
        //        pageToken = Request.Cookies["CurrPageToken"] ?? "";
        //    }
        //    else if (Page<0)
        //    {
        //        pageToken = Request.Cookies["PrevPageToken"] ?? "";
        //    }
        //    else
        //    {
        //        pageToken = Request.Cookies["NextPageToken"] ?? "";
        //    }

        //    filesRequest.PageToken = pageToken;
        //    var files = await filesRequest.ExecuteAsync();

        //    //var fileNames = files2.Files.Select(x => x.Name).ToList();
        //    var fileNamesAndIds = files.Files.Select(x => new {Id = x.Id, Name = x.Name}).ToList();

        //    Response.Cookies.Append("NextPageToken", files.NextPageToken);
        //    Response.Cookies.Append("CurrPageToken", filesRequest.PageToken);
        //    Response.Cookies.Append("PrevPageToken", Request.Cookies["CurrPageToken"] ?? "");


        //    return Ok(fileNamesAndIds);
        //    //return Ok(files.NextPageToken);
        //}

        //[HttpGet("driveFiles")]
        //[GoogleScopedAuthorize(DriveService.ScopeConstants.DriveReadonly)]
        //public async Task<IActionResult> DriveFileList([FromServices] IGoogleAuthProvider auth, int PageDirection)
        //{
        //    GoogleCredential cred = await auth.GetCredentialAsync();
        //    var service = new DriveService(new BaseClientService.Initializer
        //    {
        //        HttpClientInitializer = cred
        //    });
        //    var filesRequest = service.Files.List();

        //    //Get pageNumber and NextPageToken as session value
        //    var pageToken = HttpContext.Session.GetString("NextPageToken") ?? "";
        //    var pageNumber = HttpContext.Session.GetInt32("NextPageNumber") ?? 0;
        //    int newPageNumber = 0;
        //    Console.WriteLine("PageNumber:" + pageNumber);
        //    Console.WriteLine("PageToken:" + pageToken);

        //    if (pageNumber == 0)
        //    {
        //        if (!pageTokensMap.ContainsKey(pageNumber))
        //        {
        //            Console.WriteLine("New page token is adding");
        //            pageTokensMap.Add(pageNumber, pageToken);
        //        }
        //        newPageNumber = 1;
        //    }
        //    else if (PageDirection == 1 )
        //    {
        //        //Next Page
        //        if (!pageTokensMap.ContainsKey(pageNumber))
        //        {
        //            Console.WriteLine("New page token is adding");
        //            pageTokensMap.Add(pageNumber, pageToken);
        //        }
        //        else
        //        {
        //            pageToken = pageTokensMap[pageNumber];
        //        }
        //        newPageNumber = pageNumber+1;
        //    }
        //    else if (PageDirection == 0)
        //    {
        //        //Current Page
        //        pageToken = pageTokensMap[pageNumber - 1];
        //        newPageNumber = pageNumber;
        //    }
        //    else
        //    {
        //        //Prev Page
        //        if(pageNumber == 1)
        //        {
        //            pageToken = pageTokensMap[pageNumber - 1];
        //        }
        //        else
        //        {
        //            pageToken = pageTokensMap[pageNumber - 2];
        //        }
        //        newPageNumber = pageNumber - 1;
        //    }

        //    Console.WriteLine("PageToken for request:" + pageToken);
        //    filesRequest.PageToken = pageToken;
        //    var files = await filesRequest.ExecuteAsync();

        //    //var fileNames = files2.Files.Select(x => x.Name).ToList();
        //    var fileNamesAndIds = files.Files.Select(x => new { Id = x.Id, Name = x.Name }).ToList();


        //    Console.WriteLine("Session bilgileri oluþturuluyor...");
        //    HttpContext.Session.SetString("NextPageToken", files.NextPageToken);
        //    HttpContext.Session.SetInt32("NextPageNumber", newPageNumber);

        //    return Ok(fileNamesAndIds);
        //}

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
                else if (PageDirection == 1)
                {
                    //Next Page
                    Console.WriteLine("Stack count:" + stack.Count);
                    foreach (var item in stack)
                    {
                        Console.WriteLine($"{item} ");
                    }
                    pageToken = stack.Peek();
                }
                else if (PageDirection == 0)
                {
                    //Current Page
                    stack.Pop();
                    pageToken = stack.Peek();
                }
                else if (PageDirection == -1)
                {
                    //Prev Page
                    stack.Pop();
                    stack.Pop();
                    pageToken = stack.Peek();
                }
                else
                {
                    return BadRequest("PageDirection parameter should one of them: {1,0,-1}");
                }
            }catch(InvalidOperationException e)
            {
                Console.WriteLine("Stack boþaldi");
            }


            Console.WriteLine("PageToken for request:" + pageToken);

            filesRequest.PageToken = pageToken;
            var files = await filesRequest.ExecuteAsync();
            var fileNamesAndIds = files.Files.Select(x => new { Id = x.Id, Name = x.Name }).ToList();            
            
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


                var request = service.Files.Get(fileId);
                var stream = new MemoryStream();
                var fileMetaData = await request.ExecuteAsync();
                var mimeType = fileMetaData.MimeType;

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
                request.DownloadWithStatus(stream);
                if (stream.CanSeek)
                {
                    stream.Seek(0, SeekOrigin.Begin);
                }

                return File(stream, mimeType);
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
                Console.WriteLine("now:" + DateTime.Now);
                if (DateTime.Compare(endTime, DateTime.UtcNow) <= 0)
                {
                    throw new ApplicationException("The sharing time of the file is ended");
                }

                await GoogleHelper.DownloadADriveFile(auth, fileId);
        
                return Ok(JwtHelper.CreateToken(fileId, endTime));
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


        [HttpGet("File")]
        public async Task<IActionResult> GetFile([FromQuery]string token)
        {
            try
            {
                //-check token endDate is valid
                string fileId = "";
                if (!JwtHelper.VerifyToken(token, out fileId)) 
                {
                    return StatusCode(StatusCodes.Status401Unauthorized, "The token is invalid. Sharing time is expired.");
                }
 
                //-get file by fileId
                Console.WriteLine("GetFile.fileId" + fileId);

                var files = Directory.GetFiles("Files");
                Console.WriteLine("files.length:" + files.Length);

                //var filesWithoutExts = files.Select(file => file.Remove(file.LastIndexOf('.')));

                var fileIdwithPath = "Files\\" + fileId;
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

                return File(stream, FileServices.GetMimeType(fileExt));
                //return Ok();
            }
            catch (Exception e)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, e.Message);
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
