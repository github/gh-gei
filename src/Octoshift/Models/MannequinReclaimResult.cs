namespace Octoshift.Models
{
    public class MannequinReclaimResult : GraphqlResult<ReattributeMannequinToUserInputData>
    {
    }

    public class ReattributeMannequinToUserInputData
    {
        public ReattributeMannequinToUserInput ReattributeMannequinToUserInput { get; set; }
    }

    public class ReattributeMannequinToUserInput
    {
        public UserInfo Source { get; set; }
        public UserInfo Target { get; set; }
    }
}
