using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Buffers.Text;
using System.Buffers; // Base64.EncodeToUtf8 için

public class JwtHelper
{
    public static string Key = Environment.GetEnvironmentVariable("JWTKey");

    // CreateToken metodunuz (Referans olarak, değişiklik yok)
    public static string CreateToken(string fileId, DateTime endDate)
    {
        //payload encoding in Base64
        string payload = JsonSerializer.Serialize(new { fileId = fileId, endDate = endDate });
        Console.WriteLine(payload);

        ReadOnlySpan<byte> bytes = new UTF8Encoding().GetBytes(payload);
        byte[] utf8Bytes = new byte[256];
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
        // signatureData = signatureData.Replace("=", "").TrimEnd(); // Bu satır gereksiz ve hatalı olabilir. JWT spec'ine göre padding'siz parçalar birleştirilir.

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

    // === ÖNERİLEN GÜNCELLENMİŞ VerifyToken METODU ===
    public static bool VerifyToken(string token, out string? fileId)
    {
        fileId = null;
        try
        {
            //-Token'ı parçalarına ayır
            var parts = token.Split(".");
            if (parts.Length != 3)
            {
                throw new ArgumentException("Token 3 parçadan oluşmuyor");
            }

            var headerInBase64 = parts[0];
            var payloadInBase64 = parts[1];
            var signatureInBase64 = parts[2];

            //-İmzayı Doğrula
            string signatureDataInBase64 = headerInBase64 + "." + payloadInBase64;

            // İmza doğrulama mantığını CreateToken ile aynı tutuyoruz (Span kullanarak)
            // Bu buffer SADECE imza hesaplaması için kullanılacak
            byte[] signatureBuffer = new byte[256];
            Span<byte> utf8SignatureSpan = new Span<byte>(signatureBuffer);

            HMACSHA256 signAlgo = new HMACSHA256(new UTF8Encoding().GetBytes(Key));
            byte[] signatureInBytes = signAlgo.ComputeHash(new UTF8Encoding().GetBytes(signatureDataInBase64));
            var signatureAsSpan = new ReadOnlySpan<byte>(signatureInBytes);

            var status = Base64.EncodeToUtf8(signatureAsSpan, utf8SignatureSpan, out int bytesConsumed, out int bytesWritten);

            if (status != OperationStatus.Done)
            {
                throw new ArgumentException("Token imzası Base64'e çevrilemedi.");
            }

            //--Hesaplanan imzayı Base64Url formatına çevir
            string computedSignature = Encoding.UTF8.GetString(utf8SignatureSpan.Slice(0, bytesWritten))
                                                    .Replace("=", "").Replace("+", "-").Replace("/", "_");

            if (computedSignature != signatureInBase64)
            {
                throw new ArgumentException("Token imzası geçersiz");
            }

            Console.WriteLine("Token imzası geçerli.");

            // === PAYLOAD ÇÖZÜMLEME (DÜZELTİLMİŞ KISIM) ===

            // 1. Base64Url string'ini standart Base64'e geri çevir.
            string standardBase64Payload = Base64UrlDecode(payloadInBase64);

            // 2. Standart .NET metoduyla Base64'ü çöz (En güvenli yol)
            byte[] payloadBytes = Convert.FromBase64String(standardBase64Payload);

            // 3. Byte dizisini temiz JSON string'ine dönüştür
            string payloadJson = Encoding.UTF8.GetString(payloadBytes);

            // Artık 'Span', '\0' temizleme veya buffer hataları yok.
            Console.WriteLine("Çözülen Payload JSON: " + payloadJson);

            // 4. JSON'ıDeserialize et (Hatanın oluştuğu yer burasıydı)
            var payloadObject = JsonSerializer.Deserialize<Payload>(payloadJson);
            if (payloadObject == null)
            {
                throw new ArgumentException("Payload JSON'dan okunamadı.");
            }

            Console.WriteLine($"Payload.fileId: {payloadObject.fileId}");
            Console.WriteLine($"Payload.endDate: {payloadObject.endDate}");

            //-Payload'u doğrula (Tarih kontrolü)
            if (DateTime.Compare(payloadObject.endDate, DateTime.UtcNow) <= 0)
            {
                Console.WriteLine("Token'ın süresi dolmuş.");
                return false;
            }

            fileId = payloadObject.fileId;
            return true;
        }
        catch (ArgumentException ex)
        {
            Console.WriteLine("Token doğrulama hatası (Arg): " + ex.Message);
            return false;
        }
        catch (JsonException ex)
        {
            Console.WriteLine("Token doğrulama hatası (JSON): " + ex.Message);
            // Önceki hatanız burada yakalanırdı
            return false;
        }
        catch (FormatException ex)
        {
            Console.WriteLine("Token doğrulama hatası (Base64 Format): " + ex.Message);
            // Hatalı Base64Url -> Base64 çevrimi burada yakalanır
            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine("Bilinmeyen token doğrulama hatası: " + ex.ToString());
            return false;
        }
    }

    /// <summary>
    /// Base64Url formatındaki bir string'i (padding'siz, '-', '_')
    /// standart Base64 formatına ('+', '/') çevirir ve eksik padding'i ('=') ekler.
    /// </summary>
    private static string Base64UrlDecode(string base64Url)
    {
        string base64 = base64Url.Replace('-', '+').Replace('_', '/');

        // Eksik padding'i (dolgu) yeniden ekle
        switch (base64.Length % 4)
        {
            case 2: base64 += "=="; break;
            case 3: base64 += "="; break;
        }
        return base64;
    }
}

// Payload sınıfınız (Referans olarak)
public class Payload
{
    public string fileId { get; set; }
    public DateTime endDate { get; set; }
}