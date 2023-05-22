namespace Octoshift.Models
{
    public class CreateAttributionInvitationResult : GraphqlResult<CreateAttributionInvitationData>
    {
    }

    public class CreateAttributionInvitationData
    {
        public CreateAttributionInvitation CreateAttributionInvitation { get; set; }
    }

    public class CreateAttributionInvitation
    {
        public UserInfo Source { get; set; }
        public UserInfo Target { get; set; }
    }

    public class UserInfo
    {
        public string Id { get; set; }
        public string Login { get; set; }
    }
}
