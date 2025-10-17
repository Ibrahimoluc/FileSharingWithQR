using System.Buffers;
using System.Buffers.Text;
using System.Security.Cryptography;
using System.Security.Cryptography.Xml;
using System.Text;
using System.Text.Json;
using Google.Apis.Drive.v3.Data;
using Microsoft.AspNetCore.DataProtection.KeyManagement;

namespace FileSharingWithQR.Services
{
    public class JwtHelper
    {
        public static string Key = Environment.GetEnvironmentVariable("JWTKey");

        public static string CreateToken(string fileId, DateTime endDate)
        {
            //payload encoding in Base64
            string payload = JsonSerializer.Serialize(new { fileId = fileId, endDate = endDate });
            Console.WriteLine(payload);

            ReadOnlySpan<byte> bytes = new UTF8Encoding().GetBytes(payload);
            byte[] utf8Bytes = new byte[128];
            Span<byte> utf8 = new Span<byte>(utf8Bytes);
            int bytesConsumed = 0;
            int bytesWritten = 0;

            OperationStatus status = Base64.EncodeToUtf8(bytes, utf8, out bytesConsumed, out bytesWritten);
            Console.WriteLine(status);

            string payloadInBase64 = Encoding.UTF8.GetString(utf8.Slice(0, bytesWritten)).Replace("=", "").Replace("+", "-").Replace("/", "_");
            Console.WriteLine(payloadInBase64);

            //hazır veri, sabit olarak kullanıcam(header in Base64)
            string headerInBase64 = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9";

            string signatureData = headerInBase64 + "." + payloadInBase64;
            Console.WriteLine(signatureData);
            signatureData = signatureData.Replace("=", "").TrimEnd();



            //key ile signature denemeliyiz
            string key = Key;
            HMACSHA256 signAlgo = new HMACSHA256(new UTF8Encoding().GetBytes(key));
            byte[] signatureInBytes = signAlgo.ComputeHash(new UTF8Encoding().GetBytes(signatureData));

            bytes = new ReadOnlySpan<byte>(signatureInBytes);
            utf8.Clear();
            status = Base64.EncodeToUtf8(bytes, utf8, out bytesConsumed, out bytesWritten);
            Console.WriteLine(status);

            //JWT Base64 icin ozel bir sey kullanıyor, o ozel durumları burada uyguluyoruz
            string signature = Encoding.UTF8.GetString(utf8.Slice(0, bytesWritten)).Replace("=", "").Replace("+", "-").Replace("/", "_");
            Console.WriteLine(signature);
            Console.WriteLine(BitConverter.ToString(new UTF8Encoding().GetBytes(signature)));

            //birlestirip jwt yi olusturma
            string jwt = headerInBase64 + "." + payloadInBase64 + "." + signature;

            return jwt;
        }

        public static bool VerifyToken(string token, out string? fileId)
        {
            //-Seperate parts of the token
            var strings = token.Split(".");
            foreach(var str in strings)
            {
                Console.WriteLine(str);
            }

            var headerInBase64 = strings[0];
            var payloadInBase64 = strings[1];
            var signatureInBase64 = strings[2];

            //-Verify
            string signatureDataInBase64 = headerInBase64 + "." + payloadInBase64;

            byte[] utf8Bytes = new byte[128];
            Span<byte> utf8 = new Span<byte>(utf8Bytes);

            HMACSHA256 signAlgo = new HMACSHA256(new UTF8Encoding().GetBytes(Key));
            byte[] signatureInBytes = signAlgo.ComputeHash(new UTF8Encoding().GetBytes(signatureDataInBase64));
            var signatureAsSpan = new ReadOnlySpan<byte>(signatureInBytes);

            int bytesConsumed;
            int bytesWritten;
            var status = Base64.EncodeToUtf8(signatureAsSpan, utf8, out bytesConsumed, out bytesWritten);
            Console.WriteLine("Status:" + status);

            //--JWT Base64 icin ozel bir sey kullanıyor, o ozel durumları burada uyguluyoruz
            string computedSignature = Encoding.UTF8.GetString(utf8.Slice(0, bytesWritten)).Replace("=", "").Replace("+", "-").Replace("/", "_");
            //Console.WriteLine(signature);
            //Console.WriteLine(BitConverter.ToString(new UTF8Encoding().GetBytes(signature)));

            if(computedSignature != signatureInBase64)
            {
                throw new ArgumentException("Token is invalid");
            }
            Console.WriteLine("Token geçerli, computed signature:" + computedSignature);


            //-if valid get payload part and convert to Utf8json
            ReadOnlySpan<byte> payloadInBase64AsBytes = new UTF8Encoding().GetBytes(payloadInBase64);
            byte[] payloadAsBytes = new byte[128];
            Span<byte> payloadAsSpan = new Span<byte>(utf8Bytes);

            Base64.DecodeFromUtf8(payloadInBase64AsBytes, payloadAsSpan, out bytesConsumed, out bytesWritten);
            var payload = Encoding.UTF8.GetString(payloadAsSpan);
            var newPayload = payload;

            if (payload.EndsWith("\0"))
            {
                Console.WriteLine("paylaod ends with 0's, they are removed...");
                newPayload = payload.Replace("\0", "");
                Console.WriteLine("newPayload As Bits:" + BitConverter.ToString(new UTF8Encoding().GetBytes(newPayload)));
            }

            Console.WriteLine("Payload:" + payload);
            Console.WriteLine("Payload As Bits:" + BitConverter.ToString(new UTF8Encoding().GetBytes(payload)));


            //converting string to json and get endDate part
            var payloadObject = JsonSerializer.Deserialize<Payload>(newPayload);
            Console.WriteLine($"Payload.fileId: {payloadObject.fileId}");
            Console.WriteLine($"Payload.endDate: {payloadObject.endDate}");

            if (DateTime.Compare(payloadObject.endDate, DateTime.UtcNow) <= 0)
            {
                fileId = null;
                return false;
            }

            fileId = payloadObject.fileId;
            return true;
        }
    }

    public class Payload
    {
        public string fileId { get; set; }
        public DateTime endDate { get; set; }

    }
}
