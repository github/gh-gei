namespace Octoshift.Models
{
    public class ReattributeMannequinToBotResult : GraphqlResult<ReattributeMannequinToBotData>
    {
    }

    public class ReattributeMannequinToBotData
    {
        public ReattributeMannequinToBot ReattributeMannequinToBot { get; set; }
    }

    public class ReattributeMannequinToBot
    {
        public UserInfo Source { get; set; }
        public UserInfo Target { get; set; }
    }
}
