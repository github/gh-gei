using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Octoshift;
using Octoshift.Models;
using Xunit;

namespace OctoshiftCLI.Tests;

public class CodeScanningServiceTest
{
    private readonly Mock<GithubApi> _mockSourceGithubApi = TestHelpers.CreateMock<GithubApi>();
    private readonly Mock<GithubApi> _mockTargetGithubApi = TestHelpers.CreateMock<GithubApi>();
    private readonly Mock<OctoLogger> _mockOctoLogger = TestHelpers.CreateMock<OctoLogger>();

    private readonly CodeScanningAlertService _alertService;

    private const string SOURCE_ORG = "SOURCE-ORG";
    private const string SOURCE_REPO = "SOURCE-REPO";
    private const string TARGET_ORG = "TARGET-ORG";
    private const string TARGET_REPO = "TARGET-REPO";

    public CodeScanningServiceTest()
    {
        _alertService = new CodeScanningAlertService(_mockSourceGithubApi.Object, _mockTargetGithubApi.Object, _mockOctoLogger.Object);
    }

    [Fact]
    public async Task MigrateCodeScanningAlerts_Uses_Found_Default_Branch()
    {
        var analysisId = 123456;
        var SarifResponse = "MOCK_SARIF_REPORT";
        var CommitSha = "TEST_COMMIT_SHA";
        var Ref = "refs/heads/main";
        var CodeScanningAnalysisResult = new CodeScanningAnalysis
        {
            Id = analysisId,
            Category = "Category",
            CreatedAt = "2022-03-30T00:00:00Z",
            CommitSha = CommitSha,
            Ref = Ref
        };
        var instance = new CodeScanningAlertInstance
        {
            Ref = Ref,
            State = "fixed",
            AnalysisKey = "123456",
            CommitSha = CommitSha,
            Location = new CodeScanningAlertLocation
            {
                Path = "path/to/file.cs",
                StartLine = 3,
                StartColumn = 4,
                EndLine = 6,
                EndColumn = 25
            }
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

        _mockSourceGithubApi.Setup(x => x.GetDefaultBranch(SOURCE_ORG, SOURCE_REPO).Result).Returns("main");
        _mockSourceGithubApi.Setup(x => x.GetCodeScanningAnalysisForRepository(SOURCE_ORG, SOURCE_REPO, "main").Result).Returns(new[] { CodeScanningAnalysisResult });
        _mockSourceGithubApi.Setup(x => x.GetCodeScanningAlertsForRepository(SOURCE_ORG, SOURCE_REPO, "main").Result).Returns(new[] { sourceAlert });

        await _alertService.MigrateCodeScanningAlerts(SOURCE_ORG, SOURCE_REPO, TARGET_ORG, TARGET_REPO, false);

        _mockSourceGithubApi.Verify(x => x.GetCodeScanningAnalysisForRepository(SOURCE_ORG, SOURCE_REPO, "main"), Times.Once);
        _mockSourceGithubApi.Verify(x => x.GetCodeScanningAlertsForRepository(SOURCE_ORG, SOURCE_REPO, "main"), Times.Once);
    }

    [Fact]
    public async Task MigrateAnalyses_Migrate_Single_Analysis()
    {
        var analysisId = 123456;
        var SarifResponse = "MOCK_SARIF_REPORT";
        var CommitSha = "TEST_COMMIT_SHA";
        var Ref = "refs/heads/main";
        var CodeScanningAnalysisResult = new CodeScanningAnalysis
        {
            Id = analysisId,
            Category = "Category",
            CreatedAt = "2022-03-30T00:00:00Z",
            CommitSha = CommitSha,
            Ref = Ref
        };
        _mockSourceGithubApi.Setup(x => x.GetCodeScanningAnalysisForRepository(SOURCE_ORG, SOURCE_REPO, "main").Result).Returns(new[] { CodeScanningAnalysisResult });
        _mockSourceGithubApi.Setup(x => x.GetSarifReport(SOURCE_ORG, SOURCE_REPO, analysisId).Result).Returns(SarifResponse);

        var expectedContainer = new SarifContainer
        {
            Sarif = SarifResponse,
            Ref = Ref,
            CommitSha = CommitSha
        };

        await _alertService.MigrateAnalyses(SOURCE_ORG, SOURCE_REPO, TARGET_ORG, TARGET_REPO, "main", false);

        _mockTargetGithubApi.Verify(
            x => x.UploadSarifReport(
                It.IsAny<string>(),
                It.IsAny<string>(),
                ItExt.IsDeep(expectedContainer)
            ),
            Times.Once);
    }

    [Fact]
    public async Task MigrateAnalyses_Migrate_Multiple_Analysis()
    {
        var Ref = "refs/heads/main";
        var analysis1 = new CodeScanningAnalysis
        {
            Id = 1,
            Category = "Category",
            CreatedAt = "2022-03-30T00:00:00Z",
            CommitSha = "SHA_1",
            Ref = Ref
        };
        var analysis2 = new CodeScanningAnalysis
        {
            Id = 2,
            Category = "Category",
            CreatedAt = "2022-03-31T00:00:00Z",
            CommitSha = "SHA_2",
            Ref = Ref
        };

        const string sarifResponse1 = "SARIF_RESPONSE_1";
        const string sarifResponse2 = "SARIF_RESPONSE_2";


        _mockSourceGithubApi.Setup(x => x.GetCodeScanningAnalysisForRepository(SOURCE_ORG, SOURCE_REPO, "main").Result).Returns(new[] { analysis1, analysis2 });
        _mockSourceGithubApi.Setup(x => x.GetSarifReport(SOURCE_ORG, SOURCE_REPO, analysis1.Id).Result).Returns(sarifResponse1);
        _mockSourceGithubApi.Setup(x => x.GetSarifReport(SOURCE_ORG, SOURCE_REPO, analysis2.Id).Result).Returns(sarifResponse2);

        await _alertService.MigrateAnalyses(SOURCE_ORG, SOURCE_REPO, TARGET_ORG, TARGET_REPO, "main", false);

        _mockTargetGithubApi.Verify(
            x => x.UploadSarifReport(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<SarifContainer>()
            ),
            Times.Exactly(2));

        _mockTargetGithubApi.Verify(
            x => x.UploadSarifReport(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.Is<SarifContainer>(c => c.CommitSha == analysis1.CommitSha && c.Ref == Ref && c.Sarif == sarifResponse1)
            ),
            Times.Once);

        _mockTargetGithubApi.Verify(
            x => x.UploadSarifReport(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.Is<SarifContainer>(c => c.CommitSha == analysis2.CommitSha && c.Ref == Ref && c.Sarif == sarifResponse2)
            ),
            Times.Once);

        _mockTargetGithubApi.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task MigrateAnalyses_Dry_Run_Only_Logs_Count_But_Does_Not_Upload_Sarif()
    {
        var Ref = "refs/heads/main";
        var analysis1 = new CodeScanningAnalysis
        {
            Id = 1,
            Category = "Category",
            CreatedAt = "2022-03-30T00:00:00Z",
            CommitSha = "SHA_1",
            Ref = Ref
        };
        var analysis2 = new CodeScanningAnalysis
        {
            Id = 2,
            Category = "Category",
            CreatedAt = "2022-03-31T00:00:00Z",
            CommitSha = "SHA_2",
            Ref = Ref
        };

        const string sarifResponse1 = "SARIF_RESPONSE_1";
        const string sarifResponse2 = "SARIF_RESPONSE_2";

        _mockSourceGithubApi.Setup(x => x.GetCodeScanningAnalysisForRepository(SOURCE_ORG, SOURCE_REPO, "main").Result).Returns(new[] { analysis1, analysis2 });
        _mockSourceGithubApi.Setup(x => x.GetSarifReport(SOURCE_ORG, SOURCE_REPO, analysis1.Id).Result).Returns(sarifResponse1);
        _mockSourceGithubApi.Setup(x => x.GetSarifReport(SOURCE_ORG, SOURCE_REPO, analysis2.Id).Result).Returns(sarifResponse2);

        await _alertService.MigrateAnalyses(SOURCE_ORG, SOURCE_REPO, TARGET_ORG, TARGET_REPO, "main", true);

        _mockTargetGithubApi.Verify(
            x => x.UploadSarifReport(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<SarifContainer>()
            ),
            Times.Never);
    }

    [Fact]
    public async Task MigrateAlerts_Matches_Dismissed_Alert_By_Last_Instance_And_Updates_Target()
    {
        var CommitSha = "SHA_1";
        var Ref = "refs/heads/main";
        var lastInstance = new CodeScanningAlertInstance
        {
            Ref = Ref,
            State = "open",
            AnalysisKey = "123456",
            CommitSha = CommitSha,
            Location = new CodeScanningAlertLocation
            {
                Path = "path/to/file.cs",
                StartLine = 3,
                StartColumn = 4,
                EndLine = 6,
                EndColumn = 25
            }
        };

        var sourceAlert = new CodeScanningAlert
        {
            Number = 1,
            RuleId = "java/rule",
            State = "dismissed",
            DismissedAt = "2020-01-01T00:00:00Z",
            DismissedComment = "I was dismissed!",
            DismissedReason = "false positive",
            MostRecentInstance = lastInstance
        };

        var targetAlert = new CodeScanningAlert { Number = 2, State = "open", MostRecentInstance = CopyInstance(lastInstance), RuleId = "java/rule" };
        _mockSourceGithubApi.Setup(x => x.GetCodeScanningAlertsForRepository(SOURCE_ORG, SOURCE_REPO, "main").Result).Returns(new[] { sourceAlert });
        _mockTargetGithubApi.Setup(x => x.GetCodeScanningAlertsForRepository(TARGET_ORG, TARGET_REPO, "main").Result).Returns(new[] { targetAlert });

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
        var Location = new CodeScanningAlertLocation
        {
            Path = "path/to/file.cs",
            StartLine = 3,
            StartColumn = 4,
            EndLine = 6,
            EndColumn = 25
        };
        var instance1 = new CodeScanningAlertInstance
        {
            Ref = Ref,
            State = "open",
            AnalysisKey = "123456",
            CommitSha = CommitSha1,
            Location = Location
        };

        var instance2 = new CodeScanningAlertInstance
        {
            Ref = Ref,
            State = "open",
            AnalysisKey = "123457",
            CommitSha = CommitSha2,
            Location = Location
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

        _mockSourceGithubApi.Setup(x => x.GetCodeScanningAlertsForRepository(SOURCE_ORG, SOURCE_REPO, "main").Result).Returns(new[] { sourceAlert1, sourceAlert2 });
        _mockTargetGithubApi.Setup(x => x.GetCodeScanningAlertsForRepository(TARGET_ORG, TARGET_REPO, "main").Result).Returns(new[] { targetAlert2, targetAlert1 });

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
        var Location = new CodeScanningAlertLocation
        {
            Path = "path/to/file.cs",
            StartLine = 3,
            StartColumn = 4,
            EndLine = 6,
            EndColumn = 25
        };
        var instance1 = new CodeScanningAlertInstance
        {
            Ref = Ref1,
            State = "open",
            AnalysisKey = "123456",
            CommitSha = CommitSha,
            Location = Location
        };

        var instance2 = new CodeScanningAlertInstance
        {
            Ref = Ref2,
            State = "open",
            AnalysisKey = "123457",
            CommitSha = CommitSha,
            Location = Location
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

        _mockSourceGithubApi.Setup(x => x.GetCodeScanningAlertsForRepository(SOURCE_ORG, SOURCE_REPO, "main").Result).Returns(new[] { sourceAlert1, sourceAlert2 });
        _mockTargetGithubApi.Setup(x => x.GetCodeScanningAlertsForRepository(TARGET_ORG, TARGET_REPO, "main").Result).Returns(new[] { targetAlert2, targetAlert1 });

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
            State = "open",
            AnalysisKey = "123456",
            CommitSha = CommitSha,
            Location = new CodeScanningAlertLocation
            {
                Path = "path/to/file.cs",
                StartLine = 3,
                StartColumn = 4,
                EndLine = 6,
                EndColumn = 25
            }
        };

        var instance2 = new CodeScanningAlertInstance
        {
            Ref = Ref,
            State = "open",
            AnalysisKey = "123457",
            CommitSha = CommitSha,
            Location = new CodeScanningAlertLocation
            {
                Path = "path/to/file.cs",
                StartLine = 9,
                StartColumn = 1,
                EndLine = 35,
                EndColumn = 2
            }
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

        _mockSourceGithubApi.Setup(x => x.GetCodeScanningAlertsForRepository(SOURCE_ORG, SOURCE_REPO, "main").Result).Returns(new[] { sourceAlert1, sourceAlert2 });
        _mockTargetGithubApi.Setup(x => x.GetCodeScanningAlertsForRepository(TARGET_ORG, TARGET_REPO, "main").Result).Returns(new[] { targetAlert2, targetAlert1 });

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
        var mostRecentInstance = new CodeScanningAlertInstance
        {
            Ref = Ref,
            State = "open",
            AnalysisKey = "123456",
            CommitSha = CommitSha,
            Location = new CodeScanningAlertLocation
            {
                Path = "path/to/file.cs",
                StartLine = 3,
                StartColumn = 4,
                EndLine = 6,
                EndColumn = 25
            }
        };

        var postLoadedInstance1 = new CodeScanningAlertInstance
        {
            Ref = "refs/pull/3171/merge",
            State = "open",
            AnalysisKey = "123457",
            CommitSha = "OTHER_SHA_2",
            Location = new CodeScanningAlertLocation
            {
                Path = "path/to/file.cs",
                StartLine = 9,
                StartColumn = 1,
                EndLine = 35,
                EndColumn = 2
            }
        };
        
        var postLoadedInstance2 = new CodeScanningAlertInstance
        {
            Ref = Ref,
            State = "open",
            AnalysisKey = "123457",
            CommitSha = CommitSha,
            Location = new CodeScanningAlertLocation
            {
                Path = "path/to/file.cs",
                StartLine = 9,
                StartColumn = 1,
                EndLine = 35,
                EndColumn = 2
            }
        };

        var sourceAlert1 = new CodeScanningAlert
        {
            Number = 1,
            RuleId = "java/rule",
            State = "dismissed",
            DismissedAt = "2020-01-01T00:00:00Z",
            DismissedComment = "I was dismissed!",
            DismissedReason = "false positive",
            MostRecentInstance = mostRecentInstance
        };

        var targetAlert1 = new CodeScanningAlert { Number = 3, State = "open", MostRecentInstance = CopyInstance(postLoadedInstance2), RuleId = "java/rule" };

        _mockSourceGithubApi.Setup(x => x.GetCodeScanningAlertsForRepository(SOURCE_ORG, SOURCE_REPO, "main").Result).Returns(new[] { sourceAlert1  });
        _mockTargetGithubApi.Setup(x => x.GetCodeScanningAlertsForRepository(TARGET_ORG, TARGET_REPO, "main").Result).Returns(new[] { targetAlert1 });
        _mockSourceGithubApi.Setup(x => x.GetCodeScanningAlertInstances(SOURCE_ORG, SOURCE_REPO, sourceAlert1.Number).Result).Returns(new[] { postLoadedInstance1, postLoadedInstance2 });

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
        var instance1 = new CodeScanningAlertInstance
        {
            Ref = Ref,
            State = "fixed",
            AnalysisKey = "123456",
            CommitSha = CommitSha,
            Location = new CodeScanningAlertLocation
            {
                Path = "path/to/file.cs",
                StartLine = 3,
                StartColumn = 4,
                EndLine = 6,
                EndColumn = 25
            }
        };

        var sourceAlert1 = new CodeScanningAlert
        {
            Number = 1,
            RuleId = "java/rule",
            State = "fixed",
            DismissedAt = "2020-01-01T00:00:00Z",
            DismissedComment = "I was dismissed!",
            DismissedReason = "false positive",
            MostRecentInstance = instance1
        };

        var instance2 = new CodeScanningAlertInstance
        {
            Ref = Ref,
            State = "closed",
            AnalysisKey = "123457",
            CommitSha = CommitSha,
            Location = new CodeScanningAlertLocation
            {
                Path = "path/to/file.cs",
                StartLine = 9,
                StartColumn = 1,
                EndLine = 35,
                EndColumn = 2
            }
        };


        var sourceAlert2 = new CodeScanningAlert
        {
            Number = 2,
            RuleId = "java/rule",
            State = "closed",
            DismissedAt = "2021-01-01T00:00:00Z",
            DismissedComment = "2nd Dismissed Comment",
            DismissedReason = "won't fix",
            MostRecentInstance = instance2
        };

        var instance3 = new CodeScanningAlertInstance
        {
            Ref = Ref,
            State = "open",
            AnalysisKey = "123456",
            CommitSha = CommitSha,
            Location = new CodeScanningAlertLocation
            {
                Path = "path/to/file.cs",
                StartLine = 3,
                StartColumn = 4,
                EndLine = 6,
                EndColumn = 25
            }
        };

        var sourceAlert3 = new CodeScanningAlert
        {
            Number = 3,
            RuleId = "java/rule",
            State = "open",
            DismissedAt = "2020-01-01T00:00:00Z",
            DismissedComment = "I was dismissed!",
            DismissedReason = "false positive",
            MostRecentInstance = instance3
        };

        var targetAlert1 = new CodeScanningAlert { Number = 4, State = "open", MostRecentInstance = CopyInstance(instance3), RuleId = "java/rule" };

        _mockSourceGithubApi.Setup(x => x.GetCodeScanningAlertsForRepository(SOURCE_ORG, SOURCE_REPO, "main").Result).Returns(new[] { sourceAlert1, sourceAlert2, sourceAlert3 });
        _mockTargetGithubApi.Setup(x => x.GetCodeScanningAlertsForRepository(TARGET_ORG, TARGET_REPO, "main").Result).Returns(new[] { targetAlert1 });

        await _alertService.MigrateAlerts(SOURCE_ORG, SOURCE_REPO, TARGET_ORG, TARGET_REPO, "main", false);

        _mockTargetGithubApi.Verify(x => x.GetCodeScanningAlertsForRepository(TARGET_ORG, TARGET_REPO, "main"), Times.Once);
        _mockTargetGithubApi.Verify(x => x.UpdateCodeScanningAlert(
            TARGET_ORG,
            TARGET_REPO,
            targetAlert1.Number,
            sourceAlert3.State,
            sourceAlert3.DismissedReason,
            sourceAlert3.DismissedComment
        ), Times.Once);
        _mockTargetGithubApi.VerifyNoOtherCalls();

    }

    [Fact]
    public async Task MigrateAlerts_Dry_Run_Will_Not_Adjust_Any_Alerts_On_Target()
    {
        var CommitSha = "SHA_1";
        var Ref = "refs/heads/main";
        var lastInstance = new CodeScanningAlertInstance
        {
            Ref = Ref,
            State = "open",
            AnalysisKey = "123456",
            CommitSha = CommitSha,
            Location = new CodeScanningAlertLocation
            {
                Path = "path/to/file.cs",
                StartLine = 3,
                StartColumn = 4,
                EndLine = 6,
                EndColumn = 25
            }
        };

        var sourceAlert = new CodeScanningAlert
        {
            Number = 1,
            RuleId = "java/rule",
            State = "dismissed",
            DismissedAt = "2020-01-01T00:00:00Z",
            DismissedComment = "I was dismissed!",
            DismissedReason = "false positive",
            MostRecentInstance = lastInstance
        };

        var targetAlert = new CodeScanningAlert { Number = 2, State = "open", MostRecentInstance = CopyInstance(lastInstance), RuleId = "java/rule" };
        _mockSourceGithubApi.Setup(x => x.GetCodeScanningAlertsForRepository(SOURCE_ORG, SOURCE_REPO, "main").Result).Returns(new[] { sourceAlert });
        _mockTargetGithubApi.Setup(x => x.GetCodeScanningAlertsForRepository(TARGET_ORG, TARGET_REPO, "main").Result).Returns(new[] { targetAlert });

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

    // Avoid having referential equal instances to have real use case tests
    private CodeScanningAlertInstance CopyInstance(CodeScanningAlertInstance codeScanningAlertInstance)
    {
        return new CodeScanningAlertInstance()
        {
            AnalysisKey = codeScanningAlertInstance.AnalysisKey,
            CommitSha = codeScanningAlertInstance.CommitSha,
            Ref = codeScanningAlertInstance.Ref,
            State = codeScanningAlertInstance.State,
            Location = new CodeScanningAlertLocation
            {
                Path = codeScanningAlertInstance.Location.Path,
                StartLine = codeScanningAlertInstance.Location.StartLine,
                StartColumn = codeScanningAlertInstance.Location.StartColumn,
                EndLine = codeScanningAlertInstance.Location.EndLine,
                EndColumn = codeScanningAlertInstance.Location.EndColumn
            }
        };
    }
}

public static class ItExt
{
    public static T IsDeep<T>(T expected)
    {
        bool validate(T actual)
        {
            actual.Should().BeEquivalentTo(expected);
            return true;
        }
        return Match.Create<T>(s => validate(s));
    }
}
