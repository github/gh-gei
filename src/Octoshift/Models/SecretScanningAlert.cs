namespace Octoshift.Models
{
    public class SecretScanningAlert
    {
        public int Number { get; set; }
        public string CreatedAt { get; set; }
        public string Url { get; set; }
        public string State { get; set; }
        public string Resolution { get; set; }
        public string ResolvedAt { get; set; }
        public string ResolvedBy { get; set; }
        public string SecretType { get; set; }
        public string SecretTypeDisplayName { get; set; }
        public string Secret { get; set; }
        public bool PushProtectionBypassed { get; set; }
        public string PushProtectionBypassedAt { get; set; }
        public string PushProtectionBypassedBy { get; set; }
    }
}
