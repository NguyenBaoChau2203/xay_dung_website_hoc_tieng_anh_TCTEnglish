using System.Security.Cryptography;
using System.Text;

namespace TCTVocabulary.Services
{
    /// <summary>Cryptographically random class join codes (readable charset).</summary>
    public static class ClassJoinCode
    {
        public const int Length = 8;

        // Avoid 0/O/1/I ambiguity.
        private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

        public static string Generate()
        {
            var chars = new char[Length];
            var bytes = RandomNumberGenerator.GetBytes(Length);
            for (var i = 0; i < Length; i++)
            {
                chars[i] = Alphabet[bytes[i] % Alphabet.Length];
            }

            return new string(chars);
        }

        public static string Normalize(string? code)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                return string.Empty;
            }

            var builder = new StringBuilder(code.Length);
            foreach (var ch in code.Trim().ToUpperInvariant())
            {
                if (ch is '-' or ' ')
                {
                    continue;
                }

                builder.Append(ch);
            }

            return builder.ToString();
        }

        public static bool IsValidFormat(string? code)
        {
            var normalized = Normalize(code);
            if (normalized.Length != Length)
            {
                return false;
            }

            foreach (var ch in normalized)
            {
                if (Alphabet.IndexOf(ch) < 0)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
