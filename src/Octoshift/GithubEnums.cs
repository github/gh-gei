
namespace OctoshiftCLI
{
    public class GithubEnums
    {
        public enum ArchiveMigrationStatus
        {
            Pending,
            Exporting,
            Exported,
            Failed
        }

        public static ArchiveMigrationStatus StringToArchiveMigrationStatus(string status) {
            switch (status) {
                case "pending":
                    return ArchiveMigrationStatus.Pending;
                case "exporting":
                    return ArchiveMigrationStatus.Exporting;
                case "exported":
                    return ArchiveMigrationStatus.Exported;
                case "failed":
                    return ArchiveMigrationStatus.Failed;
                default:
                    return ArchiveMigrationStatus.Failed;
            }
        }

        public static string ArchiveMigrationStatusToString(ArchiveMigrationStatus status) {
            switch (status) {
                case ArchiveMigrationStatus.Pending:
                    return "pending";
                case ArchiveMigrationStatus.Exporting:
                    return "exporting";
                case ArchiveMigrationStatus.Exported:
                    return "exported";
                case ArchiveMigrationStatus.Failed:
                    return "failed";
                default:
                    return "failed";
            }
        }

    }
}
