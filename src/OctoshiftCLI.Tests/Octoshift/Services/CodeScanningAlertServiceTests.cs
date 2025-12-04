using System.Collections.ObjectModel;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Octoshift.Models;
using OctoshiftCLI.Models;
using OctoshiftCLI.Services;
using Xunit;

namespace OctoshiftCLI.Tests.Octoshift.Services;

public class CodeScanningAlertServiceTests
{
    private readonly Mock<GithubApi> _mockSourceGithubApi = TestHelpers.CreateMock<GithubApi>();
    private readonly Mock<GithubApi> _mockTargetGithubApi = TestHelpers.CreateMock<GithubApi>();
    private readonly Mock<OctoLogger> _mockOctoLogger = TestHelpers.CreateMock<OctoLogger>();

    private readonly CodeScanningAlertService _alertService;

    private const string SOURCE_ORG = "SOURCE-ORG";
    private const string SOURCE_REPO = "SOURCE-REPO";
    private const string TARGET_ORG = "TARGET-ORG";
    private const string TARGET_REPO = "TARGET-REPO";

    public CodeScanningAlertServiceTests()
    {
        _alertService = new CodeScanningAlertService(_mockSourceGithubApi.Object, _mockTargetGithubApi.Object, _mockOctoLogger.Object);
    }

    [Fact]
    public async Task MigrateCodeScanningAlerts_Uses_Found_Default_Branch()
    {
        _mockSourceGithubApi.Setup(x => x.GetDefaultBranch(SOURCE_ORG, SOURCE_REPO)).ReturnsAsync("main");
        _mockSourceGithubApi.Setup(x => x.GetCodeScanningAnalysisForRepository(SOURCE_ORG, SOURCE_REPO, "main")).ReturnsAsync(Enumerable.Empty<CodeScanningAnalysis>());
        _mockTargetGithubApi.Setup(x => x.GetCodeScanningAnalysisForRepository(TARGET_ORG, TARGET_REPO, "main")).ReturnsAsync(Enumerable.Empty<CodeScanningAnalysis>());
        _mockSourceGithubApi.Setup(x => x.GetCodeScanningAlertsForRepository(SOURCE_ORG, SOURCE_REPO, "main")).ReturnsAsync(Enumerable.Empty<CodeScanningAlert>());
        _mockTargetGithubApi.Setup(x => x.GetCodeScanningAlertsForRepository(TARGET_ORG, TARGET_REPO, "main")).ReturnsAsync(Enumerable.Empty<CodeScanningAlert>());

        await _alertService.MigrateCodeScanningAlerts(SOURCE_ORG, SOURCE_REPO, TARGET_ORG, TARGET_REPO, false);

        _mockSourceGithubApi.Verify(x => x.GetCodeScanningAnalysisForRepository(SOURCE_ORG, SOURCE_REPO, "main"), Times.Once);
        _mockSourceGithubApi.Verify(x => x.GetCodeScanningAlertsForRepository(SOURCE_ORG, SOURCE_REPO, "main"), Times.Once);
        _mockTargetGithubApi.Verify(x => x.GetCodeScanningAnalysisForRepository(TARGET_ORG, TARGET_REPO, "main"), Times.Once);
        _mockTargetGithubApi.Verify(x => x.GetCodeScanningAlertsForRepository(TARGET_ORG, TARGET_REPO, "main"), Times.Once);
    }

    [Fact]
    public async Task MigrateAnalyses_Migrate_Single_Analysis()
    {
        var analysisId = 123456;
        var sarifId = "foo";
        var sarif = "MOCK_SARIF_REPORT";
        var sarifCommitSha = "TEST_COMMIT_SHA";
        var sarifRef = "refs/heads/main";
        var analysis = new CodeScanningAnalysis
        {
            Id = analysisId,
            CreatedAt = "2022-03-30T00:00:00Z",
            CommitSha = sarifCommitSha,
            Ref = sarifRef
        };
        var processingStatus = new SarifProcessingStatus
        {
            Status = SarifProcessingStatus.Complete,
            Errors = Enumerable.Empty<string>()
        };

        _mockSourceGithubApi.Setup(x => x.GetCodeScanningAnalysisForRepository(SOURCE_ORG, SOURCE_REPO, "main")).ReturnsAsync(new[] { analysis });
        _mockTargetGithubApi.Setup(x => x.GetCodeScanningAnalysisForRepository(TARGET_ORG, TARGET_REPO, "main")).ReturnsAsync(Enumerable.Empty<CodeScanningAnalysis>());
        _mockSourceGithubApi.Setup(x => x.GetSarifReport(SOURCE_ORG, SOURCE_REPO, analysisId)).ReturnsAsync(sarif);
        _mockTargetGithubApi.Setup(x => x.UploadSarifReport(TARGET_ORG, TARGET_REPO, sarif, sarifCommitSha, sarifRef)).ReturnsAsync(sarifId);
        _mockTargetGithubApi.Setup(x => x.GetSarifProcessingStatus(TARGET_ORG, TARGET_REPO, sarifId)).ReturnsAsync(processingStatus);

        await _alertService.MigrateAnalyses(SOURCE_ORG, SOURCE_REPO, TARGET_ORG, TARGET_REPO, "main", false);

        _mockTargetGithubApi.Verify(x => x.GetSarifProcessingStatus(TARGET_ORG, TARGET_REPO, sarifId));
    }

    [Fact]
    public async Task MigrateAnalyses_Stops_If_SARIF_Processing_Status_Is_Failed()
    {
        var analysisId = 123456;
        var sarifId = "foo";
        var sarif = "MOCK_SARIF_REPORT";
        var sarifCommitSha = "TEST_COMMIT_SHA";
        var sarifRef = "refs/heads/main";
        var analysis = new CodeScanningAnalysis
        {
            Id = analysisId,
            CreatedAt = "2022-03-30T00:00:00Z",
            CommitSha = sarifCommitSha,
            Ref = sarifRef
        };
        var processingStatus = new SarifProcessingStatus
        {
            Status = SarifProcessingStatus.Failed,
            Errors = new Collection<string>(new[] { "error1", "error2" })
        };

        _mockSourceGithubApi.Setup(x => x.GetCodeScanningAnalysisForRepository(SOURCE_ORG, SOURCE_REPO, "main")).ReturnsAsync(new[] { analysis });
        _mockTargetGithubApi.Setup(x => x.GetCodeScanningAnalysisForRepository(TARGET_ORG, TARGET_REPO, "main")).ReturnsAsync(Enumerable.Empty<CodeScanningAnalysis>());
        _mockSourceGithubApi.Setup(x => x.GetSarifReport(SOURCE_ORG, SOURCE_REPO, analysisId)).ReturnsAsync(sarif);
        _mockTargetGithubApi.Setup(x => x.UploadSarifReport(TARGET_ORG, TARGET_REPO, sarif, sarifCommitSha, sarifRef)).ReturnsAsync(sarifId);
        _mockTargetGithubApi.Setup(x => x.GetSarifProcessingStatus(TARGET_ORG, TARGET_REPO, sarifId)).ReturnsAsync(processingStatus);

        await FluentActions.Invoking(async () => await _alertService.MigrateAnalyses(SOURCE_ORG, SOURCE_REPO, TARGET_ORG, TARGET_REPO, "main", false))
            .Should().ThrowAsync<OctoshiftCliException>()
            .WithMessage("SARIF processing failed for analysis 123456. Received the following Error(s): \n- error1\n- error2");
    }

    [Fact]
    public async Task MigrateAnalyses_Migrate_Multiple_Analysis()
    {
        var Ref = "refs/heads/main";
        var analysis1 = new CodeScanningAnalysis
        {
            Id = 1,
            CreatedAt = "2022-03-30T00:00:00Z",
            CommitSha = "SHA_1",
            Ref = Ref
        };
        var analysis2 = new CodeScanningAnalysis
        {
            Id = 2,
            CreatedAt = "2022-03-31T00:00:00Z",
            CommitSha = "SHA_2",
            Ref = Ref
        };

        const string sarifResponse1 = "SARIF_RESPONSE_1";
        const string sarifResponse2 = "SARIF_RESPONSE_2";
        var processingStatus = new SarifProcessingStatus
        {
            Status = SarifProcessingStatus.Complete,
            Errors = []
        };

        _mockSourceGithubApi.Setup(x => x.GetCodeScanningAnalysisForRepository(SOURCE_ORG, SOURCE_REPO, "main")).ReturnsAsync(new[] { analysis1, analysis2 });
        _mockSourceGithubApi.Setup(x => x.GetSarifReport(SOURCE_ORG, SOURCE_REPO, analysis1.Id)).ReturnsAsync(sarifResponse1);
        _mockSourceGithubApi.Setup(x => x.GetSarifReport(SOURCE_ORG, SOURCE_REPO, analysis2.Id)).ReturnsAsync(sarifResponse2);
        _mockTargetGithubApi.Setup(x => x.UploadSarifReport(TARGET_ORG, TARGET_REPO, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync("sarif-id");
        _mockTargetGithubApi.Setup(x => x.GetSarifProcessingStatus(TARGET_ORG, TARGET_REPO, It.IsAny<string>()))
            .ReturnsAsync(processingStatus);

        await _alertService.MigrateAnalyses(SOURCE_ORG, SOURCE_REPO, TARGET_ORG, TARGET_REPO, "main", false);

        _mockTargetGithubApi.Verify(x => x.GetCodeScanningAnalysisForRepository(TARGET_ORG, TARGET_REPO, "main"),
            Times.Once);

        _mockTargetGithubApi.Verify(
            x => x.UploadSarifReport(
                TARGET_ORG,
                TARGET_REPO,
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>()
            ),
            Times.Exactly(2));

        _mockTargetGithubApi.Verify(
            x => x.UploadSarifReport(
                TARGET_ORG,
                TARGET_REPO,
                sarifResponse1,
                analysis1.CommitSha,
                Ref
            ),
            Times.Once);

        _mockTargetGithubApi.Verify(
            x => x.UploadSarifReport(
                TARGET_ORG,
                TARGET_REPO,
                sarifResponse2,
                analysis2.CommitSha,
                Ref
            ),
            Times.Once);

        _mockTargetGithubApi.Verify(
            x => x.GetSarifProcessingStatus(
                TARGET_ORG,
                TARGET_REPO,
                "sarif-id"),
            Times.Exactly(2));

        _mockTargetGithubApi.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task MigrateAnalyses_Skips_Analysis_That_Exist_On_Target_By_Count()
    {
        var Ref = "refs/heads/main";
        var analysis1 = new CodeScanningAnalysis
        {
            Id = 1,
            CreatedAt = "2022-03-30T00:00:00Z",
            CommitSha = "SHA_1",
            Ref = Ref
        };
        var analysis2 = new CodeScanningAnalysis
        {
            Id = 2,
            CreatedAt = "2022-03-31T00:00:00Z",
            CommitSha = "SHA_2",
            Ref = Ref
        };
        var analysis3 = new CodeScanningAnalysis
        {
            Id = 3,
            CreatedAt = "2022-04-01T00:00:00Z",
            CommitSha = "SHA_3",
            Ref = Ref
        };

        const string sarifResponse2 = "SARIF_RESPONSE_2";
        const string sarifResponse3 = "SARIF_RESPONSE_3";
        var processingStatus =
            new SarifProcessingStatus { Status = SarifProcessingStatus.Complete, Errors = [] };

        _mockSourceGithubApi.Setup(x => x.GetCodeScanningAnalysisForRepository(SOURCE_ORG, SOURCE_REPO, "main")).ReturnsAsync(new[] { analysis1, analysis2, analysis3 });
        _mockTargetGithubApi.Setup(x => x.GetCodeScanningAnalysisForRepository(TARGET_ORG, TARGET_REPO, "main")).ReturnsAsync(new[] { analysis1 });

        _mockSourceGithubApi.Setup(x => x.GetSarifReport(SOURCE_ORG, SOURCE_REPO, analysis2.Id)).ReturnsAsync(sarifResponse2);
        _mockSourceGithubApi.Setup(x => x.GetSarifReport(SOURCE_ORG, SOURCE_REPO, analysis3.Id)).ReturnsAsync(sarifResponse3);
        _mockTargetGithubApi.Setup(x => x.UploadSarifReport(TARGET_ORG, TARGET_REPO, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync("sarif-id");
        _mockTargetGithubApi.Setup(x => x.GetSarifProcessingStatus(TARGET_ORG, TARGET_REPO, It.IsAny<string>()))
            .ReturnsAsync(processingStatus);

        await _alertService.MigrateAnalyses(SOURCE_ORG, SOURCE_REPO, TARGET_ORG, TARGET_REPO, "main", false);

        _mockTargetGithubApi.Verify(x => x.GetCodeScanningAnalysisForRepository(TARGET_ORG, TARGET_REPO, "main"),
            Times.Once);

        _mockTargetGithubApi.Verify(
            x => x.UploadSarifReport(
                TARGET_ORG,
                TARGET_REPO,
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>()
            ),
            Times.Exactly(2));

        _mockTargetGithubApi.Verify(
            x => x.UploadSarifReport(
                TARGET_ORG,
                TARGET_REPO,
                sarifResponse2,
                analysis2.CommitSha,
                Ref
            ),
            Times.Once);

        _mockTargetGithubApi.Verify(
            x => x.UploadSarifReport(
                TARGET_ORG,
                TARGET_REPO,
                sarifResponse3,
                analysis3.CommitSha,
                Ref
            ),
            Times.Once);

        _mockTargetGithubApi.Verify(
            x => x.GetSarifProcessingStatus(
                TARGET_ORG,
                TARGET_REPO,
                "sarif-id"),
            Times.Exactly(2));

        _mockTargetGithubApi.VerifyNoOtherCalls();
    }


    [Fact]
    public async Task MigrateAnalyses_Dry_Run_Does_Not_Upload_Sarif()
    {
        var Ref = "refs/heads/main";
        var analysis1 = new CodeScanningAnalysis
        {
            Id = 1,
            CreatedAt = "2022-03-30T00:00:00Z",
            CommitSha = "SHA_1",
            Ref = Ref
        };
        var analysis2 = new CodeScanningAnalysis
        {
            Id = 2,
            CreatedAt = "2022-03-31T00:00:00Z",
            CommitSha = "SHA_2",
            Ref = Ref
        };

        const string sarifResponse1 = "SARIF_RESPONSE_1";
        const string sarifResponse2 = "SARIF_RESPONSE_2";

        _mockSourceGithubApi.Setup(x => x.GetCodeScanningAnalysisForRepository(SOURCE_ORG, SOURCE_REPO, "main")).ReturnsAsync(new[] { analysis1, analysis2 });
        _mockSourceGithubApi.Setup(x => x.GetSarifReport(SOURCE_ORG, SOURCE_REPO, analysis1.Id)).ReturnsAsync(sarifResponse1);
        _mockSourceGithubApi.Setup(x => x.GetSarifReport(SOURCE_ORG, SOURCE_REPO, analysis2.Id)).ReturnsAsync(sarifResponse2);

        await _alertService.MigrateAnalyses(SOURCE_ORG, SOURCE_REPO, TARGET_ORG, TARGET_REPO, "main", true);

        _mockTargetGithubApi.Verify(
            x => x.UploadSarifReport(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>()
            ),
            Times.Never);
    }

    [Fact]
    public async Task MigrateAlerts_Matches_Dismissed_Alert_By_Last_Instance_And_Updates_Target()
    {
        var CommitSha = "SHA_1";
        var Ref = "refs/heads/main";
        var instance = new CodeScanningAlertInstance
        {
            Ref = Ref,
            CommitSha = CommitSha,
            Path = "path/to/file.cs",
            StartLine = 3,
            StartColumn = 4,
            EndLine = 6,
            EndColumn = 25
        };

        var sourceAlert = new CodeScanningAlert
        {
            Number = 1,
            RuleId = "java/rule",
            State = "dismissed",
            DismissedAt = "2020-01-01T00:00:00Z",
            DismissedComment = "I was dismissed!",
            DismissedReason = "false positive",
            MostRecentInstance = instance
        };

        var targetAlert = new CodeScanningAlert { Number = 2, State = "open", MostRecentInstance = CopyInstance(instance), RuleId = "java/rule" };
        _mockSourceGithubApi.Setup(x => x.GetCodeScanningAlertsForRepository(SOURCE_ORG, SOURCE_REPO, "main")).ReturnsAsync(new[] { sourceAlert });
        _mockTargetGithubApi.Setup(x => x.GetCodeScanningAlertsForRepository(TARGET_ORG, TARGET_REPO, "main")).ReturnsAsync(new[] { targetAlert });

        await _alertService.MigrateAlerts(SOURCE_ORG, SOURCE_REPO, TARGET_ORG, TARGET_REPO, "main", false);

        _mockTargetGithubApi.Verify(x => x.UpdateCodeScanningAlert(
            TARGET_ORG,
            TARGET_REPO,
            2,
            sourceAlert.State,
            sourceAlert.DismissedReason,
            sourceAlert.DismissedComment
        ), Times.Once);
    }

    [Fact]
    public async Task MigrateAlerts_Matches_Alerts_By_Commit_SHA()
    {
        var CommitSha1 = "SHA_1";
        var CommitSha2 = "SHA_2";
        var Ref = "refs/heads/main";
        var instance1 = new CodeScanningAlertInstance
        {
            Ref = Ref,
            CommitSha = CommitSha1,
            Path = "path/to/file.cs",
            StartLine = 3,
            StartColumn = 4,
            EndLine = 6,
            EndColumn = 25
        };

        var instance2 = new CodeScanningAlertInstance
        {
            Ref = Ref,
            CommitSha = CommitSha2,
            Path = "path/to/file.cs",
            StartLine = 3,
            StartColumn = 4,
            EndLine = 6,
            EndColumn = 25
        };

        var sourceAlert1 = new CodeScanningAlert
        {
            Number = 1,
            RuleId = "java/rule",
            State = "dismissed",
            DismissedAt = "2020-01-01T00:00:00Z",
            DismissedComment = "I was dismissed!",
            DismissedReason = "false positive",
            MostRecentInstance = instance1
        };

        var sourceAlert2 = new CodeScanningAlert
        {
            Number = 2,
            RuleId = "java/rule",
            State = "dismissed",
            DismissedAt = "2021-01-01T00:00:00Z",
            DismissedComment = "2nd Dismissed Comment",
            DismissedReason = "won't fix",
            MostRecentInstance = instance2
        };

        var targetAlert1 = new CodeScanningAlert { Number = 3, State = "open", MostRecentInstance = CopyInstance(instance1), RuleId = "java/rule" };
        var targetAlert2 = new CodeScanningAlert { Number = 4, State = "open", MostRecentInstance = CopyInstance(instance2), RuleId = "java/rule" };

        _mockSourceGithubApi.Setup(x => x.GetCodeScanningAlertsForRepository(SOURCE_ORG, SOURCE_REPO, "main")).ReturnsAsync(new[] { sourceAlert1, sourceAlert2 });
        _mockTargetGithubApi.Setup(x => x.GetCodeScanningAlertsForRepository(TARGET_ORG, TARGET_REPO, "main")).ReturnsAsync(new[] { targetAlert2, targetAlert1 });

        await _alertService.MigrateAlerts(SOURCE_ORG, SOURCE_REPO, TARGET_ORG, TARGET_REPO, "main", false);

        _mockTargetGithubApi.Verify(x => x.UpdateCodeScanningAlert(
            TARGET_ORG,
            TARGET_REPO,
            targetAlert1.Number,
            sourceAlert1.State,
            sourceAlert1.DismissedReason,
            sourceAlert1.DismissedComment
        ), Times.Once);

        _mockTargetGithubApi.Verify(x => x.UpdateCodeScanningAlert(
            TARGET_ORG,
            TARGET_REPO,
            targetAlert2.Number,
            sourceAlert2.State,
            sourceAlert2.DismissedReason,
            sourceAlert2.DismissedComment
        ), Times.Once);
    }

    [Fact]
    public async Task MigrateAlerts_Matches_Alerts_By_Ref()
    {
        var CommitSha = "SHA_1";
        var Ref1 = "refs/heads/main";
        var Ref2 = "refs/heads/dev";
        var instance1 = new CodeScanningAlertInstance
        {
            Ref = Ref1,
            CommitSha = CommitSha,
            Path = "path/to/file.cs",
            StartLine = 3,
            StartColumn = 4,
            EndLine = 6,
            EndColumn = 25
        };

        var instance2 = new CodeScanningAlertInstance
        {
            Ref = Ref2,
            CommitSha = CommitSha,
            Path = "path/to/file.cs",
            StartLine = 3,
            StartColumn = 4,
            EndLine = 6,
            EndColumn = 25
        };

        var sourceAlert1 = new CodeScanningAlert
        {
            Number = 1,
            RuleId = "java/rule",
            State = "dismissed",
            DismissedAt = "2020-01-01T00:00:00Z",
            DismissedComment = "I was dismissed!",
            DismissedReason = "false positive",
            MostRecentInstance = instance1
        };

        var sourceAlert2 = new CodeScanningAlert
        {
            Number = 2,
            RuleId = "java/rule",
            State = "dismissed",
            DismissedAt = "2021-01-01T00:00:00Z",
            DismissedComment = "2nd Dismissed Comment",
            DismissedReason = "won't fix",
            MostRecentInstance = instance2
        };

        var targetAlert1 = new CodeScanningAlert { Number = 3, State = "open", MostRecentInstance = CopyInstance(instance1), RuleId = "java/rule" };
        var targetAlert2 = new CodeScanningAlert { Number = 4, State = "open", MostRecentInstance = CopyInstance(instance2), RuleId = "java/rule" };

        _mockSourceGithubApi.Setup(x => x.GetCodeScanningAlertsForRepository(SOURCE_ORG, SOURCE_REPO, "main")).ReturnsAsync(new[] { sourceAlert1, sourceAlert2 });
        _mockTargetGithubApi.Setup(x => x.GetCodeScanningAlertsForRepository(TARGET_ORG, TARGET_REPO, "main")).ReturnsAsync(new[] { targetAlert2, targetAlert1 });

        await _alertService.MigrateAlerts(SOURCE_ORG, SOURCE_REPO, TARGET_ORG, TARGET_REPO, "main", false);

        _mockTargetGithubApi.Verify(x => x.UpdateCodeScanningAlert(
            TARGET_ORG,
            TARGET_REPO,
            targetAlert1.Number,
            sourceAlert1.State,
            sourceAlert1.DismissedReason,
            sourceAlert1.DismissedComment
        ), Times.Once);

        _mockTargetGithubApi.Verify(x => x.UpdateCodeScanningAlert(
            TARGET_ORG,
            TARGET_REPO,
            targetAlert2.Number,
            sourceAlert2.State,
            sourceAlert2.DismissedReason,
            sourceAlert2.DismissedComment
        ), Times.Once);
    }

    [Fact]
    public async Task MigrateAlerts_Matches_Alerts_By_Rules_Id_And_Entire_Location()
    {
        var CommitSha = "SHA_1";
        var Ref = "refs/heads/main";
        var instance1 = new CodeScanningAlertInstance
        {
            Ref = Ref,
            CommitSha = CommitSha,
            Path = "path/to/file.cs",
            StartLine = 3,
            StartColumn = 4,
            EndLine = 6,
            EndColumn = 25
        };

        var instance2 = new CodeScanningAlertInstance
        {
            Ref = Ref,
            CommitSha = CommitSha,
            Path = "path/to/file.cs",
            StartLine = 9,
            StartColumn = 1,
            EndLine = 35,
            EndColumn = 2
        };

        var sourceAlert1 = new CodeScanningAlert
        {
            Number = 1,
            RuleId = "java/rule",
            State = "dismissed",
            DismissedAt = "2020-01-01T00:00:00Z",
            DismissedComment = "I was dismissed!",
            DismissedReason = "false positive",
            MostRecentInstance = instance1
        };

        var sourceAlert2 = new CodeScanningAlert
        {
            Number = 2,
            RuleId = "java/rule",
            State = "dismissed",
            DismissedAt = "2021-01-01T00:00:00Z",
            DismissedComment = "2nd Dismissed Comment",
            DismissedReason = "won't fix",
            MostRecentInstance = instance2
        };

        var targetAlert1 = new CodeScanningAlert { Number = 3, State = "open", MostRecentInstance = CopyInstance(instance1), RuleId = "java/rule" };
        var targetAlert2 = new CodeScanningAlert { Number = 4, State = "open", MostRecentInstance = CopyInstance(instance2), RuleId = "java/rule" };

        _mockSourceGithubApi.Setup(x => x.GetCodeScanningAlertsForRepository(SOURCE_ORG, SOURCE_REPO, "main")).ReturnsAsync(new[] { sourceAlert1, sourceAlert2 });
        _mockTargetGithubApi.Setup(x => x.GetCodeScanningAlertsForRepository(TARGET_ORG, TARGET_REPO, "main")).ReturnsAsync(new[] { targetAlert2, targetAlert1 });

        await _alertService.MigrateAlerts(SOURCE_ORG, SOURCE_REPO, TARGET_ORG, TARGET_REPO, "main", false);

        _mockTargetGithubApi.Verify(x => x.UpdateCodeScanningAlert(
            TARGET_ORG,
            TARGET_REPO,
            targetAlert1.Number,
            sourceAlert1.State,
            sourceAlert1.DismissedReason,
            sourceAlert1.DismissedComment
        ), Times.Once);

        _mockTargetGithubApi.Verify(x => x.UpdateCodeScanningAlert(
            TARGET_ORG,
            TARGET_REPO,
            targetAlert2.Number,
            sourceAlert2.State,
            sourceAlert2.DismissedReason,
            sourceAlert2.DismissedComment
        ), Times.Once);
    }

    [Fact]
    public async Task MigrateAlerts_Matches_By_All_Source_Instances_If_Most_Recent_Does_Not_Match()
    {
        var CommitSha = "SHA_1";
        var Ref = "refs/heads/main";
        var firstInstance = new CodeScanningAlertInstance
        {
            Ref = Ref,
            CommitSha = CommitSha,
            Path = "path/to/file.cs",
            StartLine = 3,
            StartColumn = 4,
            EndLine = 6,
            EndColumn = 25
        };

        var secondInstance = new CodeScanningAlertInstance
        {
            Ref = "refs/pull/3171/merge",
            CommitSha = "OTHER_SHA_2",
            Path = "path/to/file.cs",
            StartLine = 9,
            StartColumn = 1,
            EndLine = 35,
            EndColumn = 2
        };

        var thirdInstance = new CodeScanningAlertInstance
        {
            Ref = Ref,
            CommitSha = CommitSha,
            Path = "path/to/file.cs",
            StartLine = 9,
            StartColumn = 1,
            EndLine = 35,
            EndColumn = 2
        };

        var sourceAlert1 = new CodeScanningAlert
        {
            Number = 1,
            RuleId = "java/rule",
            State = "dismissed",
            DismissedAt = "2020-01-01T00:00:00Z",
            DismissedComment = "I was dismissed!",
            DismissedReason = "false positive",
            MostRecentInstance = thirdInstance
        };

        var targetAlert1 = new CodeScanningAlert { Number = 3, State = "open", MostRecentInstance = CopyInstance(firstInstance), RuleId = "java/rule" };

        _mockSourceGithubApi.Setup(x => x.GetCodeScanningAlertsForRepository(SOURCE_ORG, SOURCE_REPO, "main")).ReturnsAsync(new[] { sourceAlert1 });
        _mockTargetGithubApi.Setup(x => x.GetCodeScanningAlertsForRepository(TARGET_ORG, TARGET_REPO, "main")).ReturnsAsync(new[] { targetAlert1 });
        _mockSourceGithubApi.Setup(x => x.GetCodeScanningAlertInstances(SOURCE_ORG, SOURCE_REPO, sourceAlert1.Number)).ReturnsAsync(new[] { firstInstance, secondInstance, thirdInstance });

        await _alertService.MigrateAlerts(SOURCE_ORG, SOURCE_REPO, TARGET_ORG, TARGET_REPO, "main", false);

        _mockTargetGithubApi.Verify(x => x.UpdateCodeScanningAlert(
            TARGET_ORG,
            TARGET_REPO,
            targetAlert1.Number,
            sourceAlert1.State,
            sourceAlert1.DismissedReason,
            sourceAlert1.DismissedComment
        ), Times.Once);
    }

    [Fact]
    public async Task MigrateAlerts_Skips_Sources_Other_Than_Open_Or_Dismissed()
    {
        var CommitSha = "SHA_1";
        var Ref = "refs/heads/main";
        var fixedInstance = new CodeScanningAlertInstance
        {
            Ref = Ref,
            CommitSha = CommitSha,
            Path = "path/to/file.cs",
            StartLine = 3,
            StartColumn = 4,
            EndLine = 6,
            EndColumn = 25
        };

        var fixedSourceAlert = new CodeScanningAlert
        {
            Number = 1,
            RuleId = "java/rule",
            State = "fixed",
            DismissedAt = "2020-01-01T00:00:00Z",
            DismissedComment = "I was dismissed!",
            DismissedReason = "false positive",
            MostRecentInstance = fixedInstance
        };

        var closedInstance = new CodeScanningAlertInstance
        {
            Ref = Ref,
            CommitSha = CommitSha,
            Path = "path/to/file.cs",
            StartLine = 9,
            StartColumn = 1,
            EndLine = 35,
            EndColumn = 2
        };

        var closedSourceAlert = new CodeScanningAlert
        {
            Number = 2,
            RuleId = "java/rule",
            State = "closed",
            MostRecentInstance = closedInstance
        };

        var openInstance = new CodeScanningAlertInstance
        {
            Ref = Ref,
            CommitSha = CommitSha,
            Path = "path/to/file.cs",
            StartLine = 3,
            StartColumn = 4,
            EndLine = 6,
            EndColumn = 25
        };

        var openSourceAlert = new CodeScanningAlert
        {
            Number = 3,
            RuleId = "java/rule",
            State = "open",
            MostRecentInstance = openInstance
        };

        var dismissedInstance = new CodeScanningAlertInstance
        {
            Ref = Ref,
            CommitSha = CommitSha,
            Path = "path/to/file-2.cs",
            StartLine = 7,
            StartColumn = 8,
            EndLine = 9,
            EndColumn = 30
        };

        var dismissedSourceAlert = new CodeScanningAlert
        {
            Number = 4,
            RuleId = "java/rule2",
            State = "dismissed",
            DismissedAt = "2020-01-01T00:00:00Z",
            DismissedComment = "I was dismissed!",
            DismissedReason = "false positive",
            MostRecentInstance = dismissedInstance
        };

        var targetAlert1 = new CodeScanningAlert { Number = 5, State = "dismissed", MostRecentInstance = CopyInstance(openInstance), RuleId = "java/rule" };
        var targetAlert2 = new CodeScanningAlert { Number = 6, State = "open", MostRecentInstance = CopyInstance(dismissedInstance), RuleId = "java/rule2" };

        _mockSourceGithubApi.Setup(x => x.GetCodeScanningAlertsForRepository(SOURCE_ORG, SOURCE_REPO, "main")).ReturnsAsync(new[] { fixedSourceAlert, closedSourceAlert, openSourceAlert, dismissedSourceAlert });
        _mockTargetGithubApi.Setup(x => x.GetCodeScanningAlertsForRepository(TARGET_ORG, TARGET_REPO, "main")).ReturnsAsync(new[] { targetAlert1, targetAlert2 });

        await _alertService.MigrateAlerts(SOURCE_ORG, SOURCE_REPO, TARGET_ORG, TARGET_REPO, "main", false);

        _mockTargetGithubApi.Verify(x => x.GetCodeScanningAlertsForRepository(TARGET_ORG, TARGET_REPO, "main"), Times.Once);
        _mockTargetGithubApi.Verify(x => x.UpdateCodeScanningAlert(
            TARGET_ORG,
            TARGET_REPO,
            targetAlert1.Number,
            openSourceAlert.State,
            openSourceAlert.DismissedReason,
            openSourceAlert.DismissedComment
        ), Times.Once);

        _mockTargetGithubApi.Verify(x => x.UpdateCodeScanningAlert(
            TARGET_ORG,
            TARGET_REPO,
            targetAlert2.Number,
            dismissedSourceAlert.State,
            dismissedSourceAlert.DismissedReason,
            dismissedSourceAlert.DismissedComment
        ), Times.Once);
        _mockTargetGithubApi.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task MigrateAlerts_Skips_Alerts_With_Same_State()
    {
        var CommitSha = "SHA_1";
        var Ref = "refs/heads/main";

        var openInstance = new CodeScanningAlertInstance
        {
            Ref = Ref,
            CommitSha = CommitSha,
            Path = "path/to/file.cs",
            StartLine = 3,
            StartColumn = 4,
            EndLine = 6,
            EndColumn = 25
        };

        var openSourceAlert = new CodeScanningAlert
        {
            Number = 1,
            RuleId = "java/rule",
            State = "open",
            MostRecentInstance = openInstance
        };

        var dismissedInstance = new CodeScanningAlertInstance
        {
            Ref = Ref,
            CommitSha = CommitSha,
            Path = "path/to/file-2.cs",
            StartLine = 7,
            StartColumn = 8,
            EndLine = 9,
            EndColumn = 30
        };

        var dismissedSourceAlert = new CodeScanningAlert
        {
            Number = 2,
            RuleId = "java/rule2",
            State = "dismissed",
            DismissedAt = "2020-01-01T00:00:00Z",
            DismissedComment = "I was dismissed!",
            DismissedReason = "false positive",
            MostRecentInstance = dismissedInstance
        };

        var openTargetAlert = new CodeScanningAlert { Number = 3, State = "open", MostRecentInstance = CopyInstance(openInstance), RuleId = "java/rule" };
        var dismissedTargetAlert = new CodeScanningAlert { Number = 4, State = "dismissed", MostRecentInstance = CopyInstance(dismissedInstance), RuleId = "java/rule2" };

        _mockSourceGithubApi.Setup(x => x.GetCodeScanningAlertsForRepository(SOURCE_ORG, SOURCE_REPO, "main")).ReturnsAsync(new[] { openSourceAlert, dismissedSourceAlert });
        _mockTargetGithubApi.Setup(x => x.GetCodeScanningAlertsForRepository(TARGET_ORG, TARGET_REPO, "main")).ReturnsAsync(new[] { openTargetAlert, dismissedTargetAlert });

        await _alertService.MigrateAlerts(SOURCE_ORG, SOURCE_REPO, TARGET_ORG, TARGET_REPO, "main", false);

        _mockTargetGithubApi.Verify(x => x.GetCodeScanningAlertsForRepository(TARGET_ORG, TARGET_REPO, "main"), Times.Once);
        _mockTargetGithubApi.VerifyNoOtherCalls();
    }


    [Fact]
    public async Task MigrateAlerts_Dry_Run_Will_Not_Adjust_Any_Alerts_On_Target()
    {
        var CommitSha = "SHA_1";
        var Ref = "refs/heads/main";
        var instance = new CodeScanningAlertInstance
        {
            Ref = Ref,
            CommitSha = CommitSha,
            Path = "path/to/file.cs",
            StartLine = 3,
            StartColumn = 4,
            EndLine = 6,
            EndColumn = 25
        };

        var sourceAlert = new CodeScanningAlert
        {
            Number = 1,
            RuleId = "java/rule",
            State = "dismissed",
            DismissedAt = "2020-01-01T00:00:00Z",
            DismissedComment = "I was dismissed!",
            DismissedReason = "false positive",
            MostRecentInstance = instance
        };

        var targetAlert = new CodeScanningAlert { Number = 2, State = "open", MostRecentInstance = CopyInstance(instance), RuleId = "java/rule" };
        _mockSourceGithubApi.Setup(x => x.GetCodeScanningAlertsForRepository(SOURCE_ORG, SOURCE_REPO, "main")).ReturnsAsync(new[] { sourceAlert });
        _mockTargetGithubApi.Setup(x => x.GetCodeScanningAlertsForRepository(TARGET_ORG, TARGET_REPO, "main")).ReturnsAsync(new[] { targetAlert });

        await _alertService.MigrateAlerts(SOURCE_ORG, SOURCE_REPO, TARGET_ORG, TARGET_REPO, "main", true);

        _mockTargetGithubApi.Verify(x => x.UpdateCodeScanningAlert(
            TARGET_ORG,
            TARGET_REPO,
            It.IsAny<int>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>()
        ), Times.Never);
    }

    [Fact]
    public async Task MigrateAlerts_Skips_An_Analysis_When_SARIF_Report_Not_Found()
    {
        // Arrange
        var Ref = "refs/heads/main";
        var analysis1 = new CodeScanningAnalysis
        {
            Id = 1,
            CreatedAt = "2022-03-30T00:00:00Z",
            CommitSha = "SHA_1",
            Ref = Ref
        };
        var analysis2 = new CodeScanningAnalysis
        {
            Id = 2,
            CreatedAt = "2022-03-31T00:00:00Z",
            CommitSha = "SHA_2",
            Ref = Ref
        };

        const string sarifResponse2 = "SARIF_RESPONSE_2";
        var processingStatus = new SarifProcessingStatus
        {
            Status = SarifProcessingStatus.Complete,
            Errors = []
        };

        _mockSourceGithubApi.Setup(x => x.GetCodeScanningAnalysisForRepository(SOURCE_ORG, SOURCE_REPO, "main")).ReturnsAsync(new[] { analysis1, analysis2 });
        _mockSourceGithubApi.Setup(x => x.GetSarifReport(SOURCE_ORG, SOURCE_REPO, analysis1.Id))
            .ThrowsAsync(new HttpRequestException("No analysis found for analysis ID 1", null, HttpStatusCode.NotFound));
        _mockSourceGithubApi.Setup(x => x.GetSarifReport(SOURCE_ORG, SOURCE_REPO, analysis2.Id)).ReturnsAsync(sarifResponse2);
        _mockTargetGithubApi.Setup(x => x.UploadSarifReport(TARGET_ORG, TARGET_REPO, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync("sarif-id");
        _mockTargetGithubApi.Setup(x => x.GetSarifProcessingStatus(TARGET_ORG, TARGET_REPO, It.IsAny<string>()))
            .ReturnsAsync(processingStatus);

        // Act
        await _alertService.MigrateAnalyses(SOURCE_ORG, SOURCE_REPO, TARGET_ORG, TARGET_REPO, "main", false);

        // Assert
        _mockTargetGithubApi.Verify(
            x => x.UploadSarifReport(
                TARGET_ORG,
                TARGET_REPO,
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>()
            ),
            Times.Once);
        _mockTargetGithubApi.Verify(
            x => x.UploadSarifReport(
                TARGET_ORG,
                TARGET_REPO,
                sarifResponse2,
                analysis2.CommitSha,
                Ref
            ),
            Times.Once);

        _mockTargetGithubApi.Verify(
            x => x.GetSarifProcessingStatus(
                TARGET_ORG,
                TARGET_REPO,
                "sarif-id"),
            Times.Once);

        _mockOctoLogger.Verify(log => log.LogWarning($"Skipping analysis {analysis1.Id} because no analysis was found for it (1 / 2)..."));
    }

    // Avoid having referential equal instances to have real use case tests
    private CodeScanningAlertInstance CopyInstance(CodeScanningAlertInstance codeScanningAlertInstance)
    {
        return new CodeScanningAlertInstance()
        {
            CommitSha = codeScanningAlertInstance.CommitSha,
            Ref = codeScanningAlertInstance.Ref,
            Path = codeScanningAlertInstance.Path,
            StartLine = codeScanningAlertInstance.StartLine,
            StartColumn = codeScanningAlertInstance.StartColumn,
            EndLine = codeScanningAlertInstance.EndLine,
            EndColumn = codeScanningAlertInstance.EndColumn
        };
    }
}
