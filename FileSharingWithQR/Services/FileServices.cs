using System.Management;

namespace FileSharingWithQR.Services
{
    public class FileServices
    {
        public static string GetMimeType(string extension)
        {
            string mimeType = "";
            switch (extension)
            {
                case "docx":
                    mimeType = "application/vnd.google-apps.document";
                    break;
                case "jpg":
                    mimeType = "image/jpeg";
                    break;
                case "pdf":
                    mimeType = "application/pdf";
                    break;
                default:
                    throw new ArgumentException("This file type is not supported. Supported types are: docx, pdf, jpeg, png");
            }

            return mimeType;
        }

        public static string GetExt(string mimeType)
        {
            string extension = "";
            switch (mimeType)
            {
                case "application/vnd.google-apps.document":
                    extension = "docx";
                    break;
                case "image/jpeg":
                    extension = "jpg";
                    break;
                case "application/pdf":
                    extension = "pdf";
                    break;
                default:
                    throw new ArgumentException("This file type is not supported. Supported types are: docx, pdf, jpeg, png");
            }

            return extension;
        }
    }
}
