namespace GdzieKupicService.API.Contracts;

public class GuestContracts
{
    public class Register
    {
        public record Request(
            string Name
        );

        public record Response(
            string AccessToken,
            string RefreshToken
        );
    };
    
    public class GetPosts
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