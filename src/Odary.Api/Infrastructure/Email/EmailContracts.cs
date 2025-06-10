namespace Odary.Api.Infrastructure.Email;

public class EmailCommands
{
    public class V1
    {
        public record SendEmail(
            string To,
            string Subject,
            string Body,
            bool IsHtml = true,
            string? ToName = null);

        public record SendUserInvitation(
            string Email,
            string FirstName,
            string LastName,
            string InvitationToken,
            DateTimeOffset ExpiresAt);

        public record SendPasswordReset(
            string Email,
            string FirstName,
            string ResetToken,
            DateTimeOffset ExpiresAt);
    }
}

public class EmailResources
{
    public class V1
    {
        public record EmailSent
        {
            public string To { get; init; } = string.Empty;
            public string Subject { get; init; } = string.Empty;
            public DateTimeOffset SentAt { get; init; }
        }
    }
} 