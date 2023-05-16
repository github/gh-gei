namespace Octoshift.Models
{
    public class ReattributeMannequinToUserResult : GraphqlResult<ReattributeMannequinToUserData>
    {
    }

    public class ReattributeMannequinToUserData
    {
        public ReattributeMannequinToUser ReattributeMannequinToUser { get; set; }
    }

    public class ReattributeMannequinToUser
    {
        public UserInfo Source { get; set; }
        public UserInfo Target { get; set; }
    }
}
