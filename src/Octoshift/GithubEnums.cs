
namespace OctoshiftCLI
{
    public static class GithubEnums
    {
        public enum ArchiveMigrationStatus
        {
            Pending,
            Exporting,
            Exported,
            Failed
        }

        public static ArchiveMigrationStatus StringToArchiveMigrationStatus(string status)
        {
            return status switch
            {
                "pending" => ArchiveMigrationStatus.Pending,
                "exporting" => ArchiveMigrationStatus.Exporting,
                "exported" => ArchiveMigrationStatus.Exported,
                "failed" => ArchiveMigrationStatus.Failed,
                _ => ArchiveMigrationStatus.Failed,
            };
        }

        public static string ArchiveMigrationStatusToString(ArchiveMigrationStatus status)
        {
            return status switch
            {
                ArchiveMigrationStatus.Pending => "pending",
                ArchiveMigrationStatus.Exporting => "exporting",
                ArchiveMigrationStatus.Exported => "exported",
                ArchiveMigrationStatus.Failed => "failed",
                _ => "failed",
            };
        }

    }
}