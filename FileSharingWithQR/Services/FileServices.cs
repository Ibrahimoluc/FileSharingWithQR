using System.Management;

namespace FileSharingWithQR.Services
{
    public class FileServices
    {
        //public static List<KeyValuePair<string, string>> validExtsAndMimeTypes = new List<KeyValuePair<string, string>>(){
        //    new KeyValuePair<string, string>("docx", "application/vnd.google-apps.document"),
        //    new KeyValuePair<string, string>("jpg", "image/jpeg"),
        //    new KeyValuePair<string, string>("pdf", "application/pdf")
        //};

        public static Dictionary<string, string> validExtsAndMimeTypes = new Dictionary<string, string>(){
            {"docx", "application/vnd.google-apps.document"},
            {"jpg", "image/jpeg"},
            {"pdf", "application/pdf"},
            {"xlsx", "application/vnd.google-apps.spreadsheet" },
            {"pptx", "application/vnd.google-apps.presentation" }
        };
        public static bool TryGetMimeType(string extension, out string mimeType)
        {
            //var mimeType = validExtsAndMimeTypes.FirstOrDefault(x => x.Key == extension);
            //if(mimeType.Equals(default(KeyValuePair<string, string>)))
            //{
            //    throw new ArgumentException("This file type is not supported. Supported types are: docx, pdf, jpeg, png");
            //}

            //return mimeType.Value;
            mimeType = "";
            if (IsExtensionValid(extension))
            {
                mimeType = validExtsAndMimeTypes[extension];
                return true;
            }

            return false;
        }

        public static bool TryGetExt(string mimeType, out string extension)
        {

            extension = "";
            if (IsMimeTypeValid(mimeType))
            {
                extension = validExtsAndMimeTypes.FirstOrDefault(x => x.Value == mimeType).Key;
                return true;
            }

            return false;
        }

        public static bool IsMimeTypeValid(string mimetype)
        {
            return validExtsAndMimeTypes.ContainsValue(mimetype);
        }

        public static bool IsExtensionValid(string extension)
        {
            return validExtsAndMimeTypes.ContainsKey(extension);
        }
    }
}
