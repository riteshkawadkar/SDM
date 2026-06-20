namespace BCrypt.Net
{
    public static class BCrypt
    {
        // Minimal shim: not cryptographically secure; only for tests.
        public static string HashPassword(string password)
        {
            // return a simple reversible placeholder for tests
            return "$shim$" + System.Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(password));
        }

        public static bool Verify(string text, string hash)
        {
            if (hash != null && hash.StartsWith("$shim$"))
            {
                var decoded = System.Text.Encoding.UTF8.GetString(System.Convert.FromBase64String(hash.Substring(6)));
                return decoded == text;
            }

            // fallback: not able to verify
            return false;
        }
    }
}
