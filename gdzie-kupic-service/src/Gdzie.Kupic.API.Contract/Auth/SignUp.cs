namespace Gdzie.Kupic.Service.API.Contract.Auth;

public class SignUp
{
    public class Request
    {
        public string Email { get; set; }
        public string Password { get; set; }
        public string Role { get; set; }

        /// <summary>Optional first name (letters, spaces, hyphens, apostrophes; at most 50 characters).</summary>
        public string? FirstName { get; set; }
    }

    public class Response
    {
        public Response(string accessToken, string refreshToken, DateTime expiresAt)
        {
            AccessToken = accessToken;
            RefreshToken = refreshToken;
            ExpiresAt = expiresAt;
        }

        public string AccessToken { get; }
        public string RefreshToken { get; }
        public DateTime ExpiresAt { get; }
    }
}