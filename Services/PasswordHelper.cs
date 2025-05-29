using System.Text.RegularExpressions;

namespace MusicCatalogWebApplication.Services
{
    public class PasswordHelper
    {
        private static readonly Regex SpecialCharRegex = new Regex(@"[!@#$%^&*()_+=\[{\]};:<>|./?,-]");
        private static readonly string[] CommonPasswords = { "password", "123456", "qwerty", "admin" };

        public static PasswordStrength CheckPasswordStrength(string password)
        {
            if (string.IsNullOrWhiteSpace(password))
                return PasswordStrength.VeryWeak;

            int score = 0;

            if (password.Length >= 8) score++;
            if (password.Length >= 12) score++;
            if (password.Any(char.IsUpper)) score++;
            if (password.Any(char.IsLower)) score++;
            if (password.Any(char.IsDigit)) score++;
            if (SpecialCharRegex.IsMatch(password)) score++;

            return score switch
            {
                < 3 => PasswordStrength.Weak,
                3 or 4 => PasswordStrength.Medium,
                _ => PasswordStrength.Strong
            };
        }

        public static bool IsPasswordStrong(string password)
        {
            if (string.IsNullOrWhiteSpace(password) || password.Length < 8)
                return false;

            var strength = CheckPasswordStrength(password);
            return strength >= PasswordStrength.Medium && !IsCommonPassword(password);
        }

        public static bool IsCommonPassword(string password)
        {
            return CommonPasswords.Contains(password.ToLowerInvariant());
        }

        public static string HashPassword(string password)
        {
            if (!IsPasswordStrong(password))
                throw new ArgumentException("Пароль слишком слабый или является распространенным.");
            return BCrypt.Net.BCrypt.HashPassword(password);
        }

        public static bool VerifyPassword(string inputPassword, string storedHash)
        {
            return BCrypt.Net.BCrypt.Verify(inputPassword, storedHash);
        }

        public static string GenerateRandomPassword(int length = 12)
        {
            const string validChars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890!@#$%^&*()_+-=[]{};':\",./<>?\\|`~";
            var random = new Random();

            var passwordChars = new List<char>
            {
                "abcdefghijklmnopqrstuvwxyz"[random.Next(26)],
                "ABCDEFGHIJKLMNOPQRSTUVWXYZ"[random.Next(26)],
                "0123456789"[random.Next(10)],
                "!@#$%^&*()_+-=[]{};':\",./<>?\\|`~"[random.Next(30)]
            };

            while (passwordChars.Count < length)
            {
                passwordChars.Add(validChars[random.Next(validChars.Length)]);
            }

            return new string(passwordChars.OrderBy(x => random.Next()).ToArray());
        }

        public enum PasswordStrength
        {
            VeryWeak,
            Weak,
            Medium,
            Strong
        }
    }
}