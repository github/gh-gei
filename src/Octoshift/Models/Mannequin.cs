
namespace OctoshiftCLI.Models
{
    public class Claimant
    {
        public string Id { get; set; }
        public string Login { get; set; }
    }
    public class Mannequin
    {
        public string Id { get; set; }
        public string Login { get; set; }
        public Claimant MappedUser { get; set; }
    }
}
