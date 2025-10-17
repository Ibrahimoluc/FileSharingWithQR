using System.Collections;
using System.Text.Json;

namespace FileSharingWithQR.Services
{
    public static class SessionExtensions
    {
        public static void Set<T>(this ISession session, string key, T value)
        {
            session.SetString(key, JsonSerializer.Serialize(value));
        }

        public static void SetStringStack(this ISession session, string key, Stack<string> value)
        {
            session.SetString(key, JsonSerializer.Serialize(value.ToArray().Reverse()));
        }

        public static T? Get<T>(this ISession session, string key)
        {
            var value = session.GetString(key);
            return value == null ? default : JsonSerializer.Deserialize<T>(value);
        }


    }
}
