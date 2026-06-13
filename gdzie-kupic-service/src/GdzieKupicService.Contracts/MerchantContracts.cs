namespace GdzieKupicService.API.Contracts;

public abstract class MerchantContracts
{
    public class Register
    {
        public record Request(
            string Email,
            string Password,
            string Name
        );

        public record Response(
            string AccessToken,
            string RefreshToken
        );
    };

    public class Login
    {
        public record Request(
            string Email,
            string Password
        );

        public record Response(
            string AccessToken,
            string RefreshToken
        );
    };

    public class GetFeed
    {
        public record Response(
            IEnumerable<PostDto> Posts
        );
    }

    public class GetActivePosts
    {
        public record Response(
            IEnumerable<PostDto> Posts
        );
    }

    public class AddPostComment
    {
        public record Request(
            string Content
        );
    }

    public class EditPostComment
    {
        public record Request(
            string Content
        );
    }
}