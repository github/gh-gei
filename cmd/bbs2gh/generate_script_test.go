package main

import (
	"bytes"
	"context"
	"strings"
	"testing"

	"github.com/github/gh-gei/pkg/logger"
	"github.com/github/gh-gei/pkg/scriptgen"
	"github.com/stretchr/testify/assert"
	"github.com/stretchr/testify/require"
)

// ---------------------------------------------------------------------------
// Test constants (matching C# test constants)
// ---------------------------------------------------------------------------

const (
	BBS_GITHUB_ORG       = "GITHUB-ORG"
	BBS_SERVER_URL       = "http://bbs-server-url"
	BBS_USERNAME         = "BBS-USERNAME"
	BBS_PASSWORD         = "BBS-PASSWORD"
	BBS_SSH_USER         = "SSH-USER"
	BBS_SSH_PRIVATE_KEY  = "path-to-ssh-private-key"
	BBS_ARCHIVE_DL_HOST  = "archive-download-host"
	BBS_SSH_PORT         = 2211
	BBS_SMB_USER         = "SMB-USER"
	BBS_SMB_DOMAIN       = "SMB-DOMAIN"
	BBS_OUTPUT           = "unit-test-output"
	BBS_FOO_PROJECT_KEY  = "FP"
	BBS_FOO_PROJECT_NAME = "BBS-FOO-PROJECT-NAME"
	BBS_BAR_PROJECT_KEY  = "BBS-BAR-PROJECT-NAME"
	BBS_BAR_PROJECT_NAME = "BP"
	BBS_FOO_REPO_1_SLUG  = "foorepo1"
	BBS_FOO_REPO_1_NAME  = "BBS-FOO-REPO-1-NAME"
	BBS_FOO_REPO_2_SLUG  = "foorepo2"
	BBS_FOO_REPO_2_NAME  = "BBS-FOO-REPO-2-NAME"
	BBS_BAR_REPO_1_SLUG  = "barrepo1"
	BBS_BAR_REPO_1_NAME  = "BBS-BAR-REPO-1-NAME"
	BBS_BAR_REPO_2_SLUG  = "barrepo2"
	BBS_BAR_REPO_2_NAME  = "BBS-BAR-REPO-2-NAME"
	BBS_SHARED_HOME      = "BBS-SHARED-HOME"
	BBS_AWS_BUCKET_NAME  = "AWS-BUCKET-NAME"
	BBS_AWS_REGION       = "AWS_REGION"
	BBS_UPLOADS_URL      = "UPLOADS-URL"
	bbsTestVersion       = "1.1.1"
)

// ---------------------------------------------------------------------------
// Mock implementation
// ---------------------------------------------------------------------------

type mockBbsGenScriptAPI struct {
	getProjectsFn func(ctx context.Context) ([]genScriptProject, error)
	getReposFn    func(ctx context.Context, projectKey string) ([]genScriptRepository, error)
}

func (m *mockBbsGenScriptAPI) GetProjects(ctx context.Context) ([]genScriptProject, error) {
	if m.getProjectsFn != nil {
		return m.getProjectsFn(ctx)
	}
	return nil, nil
}

func (m *mockBbsGenScriptAPI) GetRepos(ctx context.Context, projectKey string) ([]genScriptRepository, error) {
	if m.getReposFn != nil {
		return m.getReposFn(ctx, projectKey)
	}
	return nil, nil
}

// ---------------------------------------------------------------------------
// Helper: trimNonExecutableLines
// ---------------------------------------------------------------------------

// trimNonExecutableLines mirrors the C# TrimNonExecutableLines helper.
// It splits on \n, removes empty lines and lines starting with #,
// then skips the first skipFirst and last skipLast of the remaining lines.
func bbsTrimNonExecutableLines(script string, skipFirst, skipLast int) string {
	raw := strings.Split(script, "\n")
	var filtered []string
	for _, line := range raw {
		if strings.TrimSpace(line) == "" {
			continue
		}
		if strings.HasPrefix(strings.TrimSpace(line), "#") {
			continue
		}
		filtered = append(filtered, line)
	}

	if skipFirst > len(filtered) {
		skipFirst = len(filtered)
	}
	filtered = filtered[skipFirst:]

	if skipLast > len(filtered) {
		skipLast = len(filtered)
	}
	if skipLast > 0 {
		filtered = filtered[:len(filtered)-skipLast]
	}

	return strings.Join(filtered, "\n")
}

// ---------------------------------------------------------------------------
// Helper: default mock setup (one project, one repo)
// ---------------------------------------------------------------------------

func newDefaultBbsMock() *mockBbsGenScriptAPI {
	return &mockBbsGenScriptAPI{
		getProjectsFn: func(_ context.Context) ([]genScriptProject, error) {
			return []genScriptProject{{ID: 1, Key: BBS_FOO_PROJECT_KEY, Name: BBS_FOO_PROJECT_NAME}}, nil
		},
		getReposFn: func(_ context.Context, projectKey string) ([]genScriptRepository, error) {
			if projectKey == BBS_FOO_PROJECT_KEY {
				return []genScriptRepository{{ID: 1, Slug: BBS_FOO_REPO_1_SLUG, Name: BBS_FOO_REPO_1_NAME}}, nil
			}
			return nil, nil
		},
	}
}

// ---------------------------------------------------------------------------
// Helper: run generate-script command
// ---------------------------------------------------------------------------

func runBbsGenScript(t *testing.T, api *mockBbsGenScriptAPI, args ...string) string {
	t.Helper()

	var buf bytes.Buffer
	log := logger.New(false, &buf)

	var scriptOutput string
	writeToFile := func(_, content string) error {
		scriptOutput = content
		return nil
	}

	cmd := newGenerateScriptCmd(api, bbsTestVersion, log, writeToFile)
	cmd.SetArgs(args)
	cmd.SetOut(&buf)
	cmd.SetErr(&buf)

	err := cmd.ExecuteContext(context.Background())
	require.NoError(t, err)

	return scriptOutput
}

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------

// 1. No_Projects
func TestBbsGenerateScript_No_Projects(t *testing.T) {
	api := &mockBbsGenScriptAPI{
		getProjectsFn: func(_ context.Context) ([]genScriptProject, error) {
			return nil, nil
		},
	}

	output := runBbsGenScript(t, api,
		"--bbs-server-url", BBS_SERVER_URL,
		"--github-org", BBS_GITHUB_ORG,
		"--output", BBS_OUTPUT,
	)

	trimmed := bbsTrimNonExecutableLines(output, 33, 0)
	assert.Equal(t, "", trimmed)
}

// 2. Validates_Env_Vars
func TestBbsGenerateScript_Validates_Env_Vars(t *testing.T) {
	api := newDefaultBbsMock()

	output := runBbsGenScript(t, api,
		"--bbs-server-url", BBS_SERVER_URL,
		"--github-org", BBS_GITHUB_ORG,
		"--output", BBS_OUTPUT,
	)

	assert.Contains(t, output, scriptgen.ValidateGHPAT)
	assert.Contains(t, output, bbsValidateBBSPassword)
	assert.Contains(t, output, bbsValidateBBSUsername)
	assert.Contains(t, output, scriptgen.ValidateAzureStorageConnectionString)
}

// 3. Validates_Env_Vars_BBS_USERNAME_Not_Validated_When_Passed_As_Arg
func TestBbsGenerateScript_Validates_Env_Vars_BBS_USERNAME_Not_Validated_When_Passed_As_Arg(t *testing.T) {
	api := newDefaultBbsMock()

	output := runBbsGenScript(t, api,
		"--bbs-server-url", BBS_SERVER_URL,
		"--github-org", BBS_GITHUB_ORG,
		"--bbs-username", BBS_USERNAME,
		"--output", BBS_OUTPUT,
	)

	assert.Contains(t, output, scriptgen.ValidateGHPAT)
	assert.Contains(t, output, bbsValidateBBSPassword)
	assert.NotContains(t, output, bbsValidateBBSUsername)
	assert.Contains(t, output, scriptgen.ValidateAzureStorageConnectionString)
}

// 4. Validates_Env_Vars_BBS_PASSWORD_Not_Validated_When_Kerberos
func TestBbsGenerateScript_Validates_Env_Vars_BBS_PASSWORD_Not_Validated_When_Kerberos(t *testing.T) {
	api := newDefaultBbsMock()

	output := runBbsGenScript(t, api,
		"--bbs-server-url", BBS_SERVER_URL,
		"--github-org", BBS_GITHUB_ORG,
		"--kerberos",
		"--output", BBS_OUTPUT,
	)

	assert.Contains(t, output, scriptgen.ValidateGHPAT)
	assert.NotContains(t, output, bbsValidateBBSPassword)
	assert.NotContains(t, output, bbsValidateBBSUsername)
	assert.Contains(t, output, scriptgen.ValidateAzureStorageConnectionString)
}

// 5. Validates_Env_Vars_AWS
func TestBbsGenerateScript_Validates_Env_Vars_AWS(t *testing.T) {
	api := newDefaultBbsMock()

	output := runBbsGenScript(t, api,
		"--bbs-server-url", BBS_SERVER_URL,
		"--github-org", BBS_GITHUB_ORG,
		"--aws-bucket-name", BBS_AWS_BUCKET_NAME,
		"--output", BBS_OUTPUT,
	)

	assert.Contains(t, output, scriptgen.ValidateGHPAT)
	assert.Contains(t, output, scriptgen.ValidateAWSAccessKeyID)
	assert.Contains(t, output, scriptgen.ValidateAWSSecretAccessKey)
	assert.NotContains(t, output, scriptgen.ValidateAzureStorageConnectionString)
}

// 6. Validates_Env_Vars_AZURE_STORAGE_CONNECTION_STRING_Not_Validated_When_Aws
func TestBbsGenerateScript_Validates_Env_Vars_AZURE_Not_Validated_When_Aws(t *testing.T) {
	api := newDefaultBbsMock()

	output := runBbsGenScript(t, api,
		"--bbs-server-url", BBS_SERVER_URL,
		"--github-org", BBS_GITHUB_ORG,
		"--aws-bucket-name", BBS_AWS_BUCKET_NAME,
		"--output", BBS_OUTPUT,
	)

	assert.NotContains(t, output, scriptgen.ValidateAzureStorageConnectionString)
	assert.Contains(t, output, scriptgen.ValidateAWSAccessKeyID)
	assert.Contains(t, output, scriptgen.ValidateAWSSecretAccessKey)
}

// 7. Validates_Env_Vars_AZURE_STORAGE_CONNECTION_STRING_And_AWS_Not_Validated_When_UseGithubStorage
func TestBbsGenerateScript_Validates_Env_Vars_Not_Validated_When_UseGithubStorage(t *testing.T) {
	api := newDefaultBbsMock()

	output := runBbsGenScript(t, api,
		"--bbs-server-url", BBS_SERVER_URL,
		"--github-org", BBS_GITHUB_ORG,
		"--use-github-storage",
		"--output", BBS_OUTPUT,
	)

	assert.NotContains(t, output, scriptgen.ValidateAzureStorageConnectionString)
	assert.NotContains(t, output, scriptgen.ValidateAWSAccessKeyID)
	assert.NotContains(t, output, scriptgen.ValidateAWSSecretAccessKey)
}

// 8. Validates_Env_Vars_SMB_PASSWORD
func TestBbsGenerateScript_Validates_Env_Vars_SMB_PASSWORD(t *testing.T) {
	api := newDefaultBbsMock()

	output := runBbsGenScript(t, api,
		"--bbs-server-url", BBS_SERVER_URL,
		"--github-org", BBS_GITHUB_ORG,
		"--smb-user", BBS_SMB_USER,
		"--output", BBS_OUTPUT,
	)

	assert.Contains(t, output, bbsValidateSMBPassword)
}

// 9. No_Repos
func TestBbsGenerateScript_No_Repos(t *testing.T) {
	api := &mockBbsGenScriptAPI{
		getProjectsFn: func(_ context.Context) ([]genScriptProject, error) {
			return []genScriptProject{{ID: 1, Key: BBS_FOO_PROJECT_KEY, Name: BBS_FOO_PROJECT_NAME}}, nil
		},
		getReposFn: func(_ context.Context, _ string) ([]genScriptRepository, error) {
			return nil, nil
		},
	}

	output := runBbsGenScript(t, api,
		"--bbs-server-url", BBS_SERVER_URL,
		"--github-org", BBS_GITHUB_ORG,
		"--output", BBS_OUTPUT,
	)

	assert.Contains(t, output, "# Skipping this project because it has no git repos.")
	trimmed := bbsTrimNonExecutableLines(output, 33, 0)
	assert.Equal(t, "", trimmed)
}

// 10. Two_Projects_Two_Repos_Each_All_Options
func TestBbsGenerateScript_Two_Projects_Two_Repos_Each_All_Options(t *testing.T) {
	api := &mockBbsGenScriptAPI{
		getProjectsFn: func(_ context.Context) ([]genScriptProject, error) {
			return []genScriptProject{
				{ID: 1, Key: BBS_FOO_PROJECT_KEY, Name: BBS_FOO_PROJECT_NAME},
				{ID: 2, Key: BBS_BAR_PROJECT_KEY, Name: BBS_BAR_PROJECT_NAME},
			}, nil
		},
		getReposFn: func(_ context.Context, projectKey string) ([]genScriptRepository, error) {
			switch projectKey {
			case BBS_FOO_PROJECT_KEY:
				return []genScriptRepository{
					{ID: 1, Slug: BBS_FOO_REPO_1_SLUG, Name: BBS_FOO_REPO_1_NAME},
					{ID: 2, Slug: BBS_FOO_REPO_2_SLUG, Name: BBS_FOO_REPO_2_NAME},
				}, nil
			case BBS_BAR_PROJECT_KEY:
				return []genScriptRepository{
					{ID: 3, Slug: BBS_BAR_REPO_1_SLUG, Name: BBS_BAR_REPO_1_NAME},
					{ID: 4, Slug: BBS_BAR_REPO_2_SLUG, Name: BBS_BAR_REPO_2_NAME},
				}, nil
			}
			return nil, nil
		},
	}

	output := runBbsGenScript(t, api,
		"--bbs-server-url", BBS_SERVER_URL,
		"--github-org", BBS_GITHUB_ORG,
		"--bbs-username", BBS_USERNAME,
		"--ssh-user", BBS_SSH_USER,
		"--ssh-private-key", BBS_SSH_PRIVATE_KEY,
		"--ssh-port", "2211",
		"--archive-download-host", BBS_ARCHIVE_DL_HOST,
		"--bbs-shared-home", BBS_SHARED_HOME,
		"--verbose",
		"--keep-archive",
		"--output", BBS_OUTPUT,
	)

	// C# uses script.Contains(migrateRepoCommandN) for each of the 4 commands
	cmd1 := `Exec { gh bbs2gh migrate-repo --bbs-server-url "` + BBS_SERVER_URL + `" --bbs-username "` + BBS_USERNAME + `" --bbs-shared-home "` + BBS_SHARED_HOME + `" --bbs-project "` + BBS_FOO_PROJECT_KEY + `" --bbs-repo "` + BBS_FOO_REPO_1_SLUG + `" --ssh-user "` + BBS_SSH_USER + `" --ssh-private-key "` + BBS_SSH_PRIVATE_KEY + `" --ssh-port 2211 --archive-download-host ` + BBS_ARCHIVE_DL_HOST + ` --github-org "` + BBS_GITHUB_ORG + `" --github-repo "` + BBS_FOO_PROJECT_KEY + `-` + BBS_FOO_REPO_1_SLUG + `" --verbose --keep-archive --target-repo-visibility private }`
	cmd2 := `Exec { gh bbs2gh migrate-repo --bbs-server-url "` + BBS_SERVER_URL + `" --bbs-username "` + BBS_USERNAME + `" --bbs-shared-home "` + BBS_SHARED_HOME + `" --bbs-project "` + BBS_FOO_PROJECT_KEY + `" --bbs-repo "` + BBS_FOO_REPO_2_SLUG + `" --ssh-user "` + BBS_SSH_USER + `" --ssh-private-key "` + BBS_SSH_PRIVATE_KEY + `" --ssh-port 2211 --archive-download-host ` + BBS_ARCHIVE_DL_HOST + ` --github-org "` + BBS_GITHUB_ORG + `" --github-repo "` + BBS_FOO_PROJECT_KEY + `-` + BBS_FOO_REPO_2_SLUG + `" --verbose --keep-archive --target-repo-visibility private }`
	cmd3 := `Exec { gh bbs2gh migrate-repo --bbs-server-url "` + BBS_SERVER_URL + `" --bbs-username "` + BBS_USERNAME + `" --bbs-shared-home "` + BBS_SHARED_HOME + `" --bbs-project "` + BBS_BAR_PROJECT_KEY + `" --bbs-repo "` + BBS_BAR_REPO_1_SLUG + `" --ssh-user "` + BBS_SSH_USER + `" --ssh-private-key "` + BBS_SSH_PRIVATE_KEY + `" --ssh-port 2211 --archive-download-host ` + BBS_ARCHIVE_DL_HOST + ` --github-org "` + BBS_GITHUB_ORG + `" --github-repo "` + BBS_BAR_PROJECT_KEY + `-` + BBS_BAR_REPO_1_SLUG + `" --verbose --keep-archive --target-repo-visibility private }`
	cmd4 := `Exec { gh bbs2gh migrate-repo --bbs-server-url "` + BBS_SERVER_URL + `" --bbs-username "` + BBS_USERNAME + `" --bbs-shared-home "` + BBS_SHARED_HOME + `" --bbs-project "` + BBS_BAR_PROJECT_KEY + `" --bbs-repo "` + BBS_BAR_REPO_2_SLUG + `" --ssh-user "` + BBS_SSH_USER + `" --ssh-private-key "` + BBS_SSH_PRIVATE_KEY + `" --ssh-port 2211 --archive-download-host ` + BBS_ARCHIVE_DL_HOST + ` --github-org "` + BBS_GITHUB_ORG + `" --github-repo "` + BBS_BAR_PROJECT_KEY + `-` + BBS_BAR_REPO_2_SLUG + `" --verbose --keep-archive --target-repo-visibility private }`
	assert.Contains(t, output, cmd1)
	assert.Contains(t, output, cmd2)
	assert.Contains(t, output, cmd3)
	assert.Contains(t, output, cmd4)
}

// 11. Filters_By_Project
func TestBbsGenerateScript_Filters_By_Project(t *testing.T) {
	// GetProjects should NOT be called when --bbs-project is set
	getProjectsCalled := false
	api := &mockBbsGenScriptAPI{
		getProjectsFn: func(_ context.Context) ([]genScriptProject, error) {
			getProjectsCalled = true
			return nil, nil
		},
		getReposFn: func(_ context.Context, projectKey string) ([]genScriptRepository, error) {
			if projectKey == BBS_FOO_PROJECT_KEY {
				return []genScriptRepository{
					{ID: 1, Slug: BBS_FOO_REPO_1_SLUG, Name: BBS_FOO_REPO_1_NAME},
					{ID: 2, Slug: BBS_FOO_REPO_2_SLUG, Name: BBS_FOO_REPO_2_NAME},
				}, nil
			}
			return nil, nil
		},
	}

	output := runBbsGenScript(t, api,
		"--bbs-server-url", BBS_SERVER_URL,
		"--github-org", BBS_GITHUB_ORG,
		"--bbs-username", BBS_USERNAME,
		"--bbs-project", BBS_FOO_PROJECT_KEY,
		"--bbs-shared-home", BBS_SHARED_HOME,
		"--archive-download-host", BBS_ARCHIVE_DL_HOST,
		"--ssh-user", BBS_SSH_USER,
		"--ssh-private-key", BBS_SSH_PRIVATE_KEY,
		"--ssh-port", "2211",
		"--verbose",
		"--output", BBS_OUTPUT,
	)

	assert.False(t, getProjectsCalled, "GetProjects should not be called when --bbs-project is set")

	cmd1 := `Exec { gh bbs2gh migrate-repo --bbs-server-url "` + BBS_SERVER_URL + `" --bbs-username "` + BBS_USERNAME + `" --bbs-shared-home "` + BBS_SHARED_HOME + `" --bbs-project "` + BBS_FOO_PROJECT_KEY + `" --bbs-repo "` + BBS_FOO_REPO_1_SLUG + `" --ssh-user "` + BBS_SSH_USER + `" --ssh-private-key "` + BBS_SSH_PRIVATE_KEY + `" --ssh-port 2211 --archive-download-host ` + BBS_ARCHIVE_DL_HOST + ` --github-org "` + BBS_GITHUB_ORG + `" --github-repo "` + BBS_FOO_PROJECT_KEY + `-` + BBS_FOO_REPO_1_SLUG + `" --verbose --target-repo-visibility private }`
	cmd2 := `Exec { gh bbs2gh migrate-repo --bbs-server-url "` + BBS_SERVER_URL + `" --bbs-username "` + BBS_USERNAME + `" --bbs-shared-home "` + BBS_SHARED_HOME + `" --bbs-project "` + BBS_FOO_PROJECT_KEY + `" --bbs-repo "` + BBS_FOO_REPO_2_SLUG + `" --ssh-user "` + BBS_SSH_USER + `" --ssh-private-key "` + BBS_SSH_PRIVATE_KEY + `" --ssh-port 2211 --archive-download-host ` + BBS_ARCHIVE_DL_HOST + ` --github-org "` + BBS_GITHUB_ORG + `" --github-repo "` + BBS_FOO_PROJECT_KEY + `-` + BBS_FOO_REPO_2_SLUG + `" --verbose --target-repo-visibility private }`
	assert.Contains(t, output, cmd1)
	assert.Contains(t, output, cmd2)

	assert.NotContains(t, output, BBS_BAR_PROJECT_KEY)
}

// 12. One_Repo_With_Kerberos
func TestBbsGenerateScript_One_Repo_With_Kerberos(t *testing.T) {
	api := newDefaultBbsMock()

	output := runBbsGenScript(t, api,
		"--bbs-server-url", BBS_SERVER_URL,
		"--github-org", BBS_GITHUB_ORG,
		"--bbs-username", BBS_USERNAME,
		"--bbs-shared-home", BBS_SHARED_HOME,
		"--archive-download-host", BBS_ARCHIVE_DL_HOST,
		"--ssh-user", BBS_SSH_USER,
		"--ssh-private-key", BBS_SSH_PRIVATE_KEY,
		"--ssh-port", "2211",
		"--verbose",
		"--kerberos",
		"--output", BBS_OUTPUT,
	)

	expected := `Exec { gh bbs2gh migrate-repo --bbs-server-url "` + BBS_SERVER_URL + `" --bbs-username "` + BBS_USERNAME + `" --bbs-shared-home "` + BBS_SHARED_HOME + `" --bbs-project "` + BBS_FOO_PROJECT_KEY + `" --bbs-repo "` + BBS_FOO_REPO_1_SLUG + `" --ssh-user "` + BBS_SSH_USER + `" --ssh-private-key "` + BBS_SSH_PRIVATE_KEY + `" --ssh-port 2211 --archive-download-host ` + BBS_ARCHIVE_DL_HOST + ` --github-org "` + BBS_GITHUB_ORG + `" --github-repo "` + BBS_FOO_PROJECT_KEY + `-` + BBS_FOO_REPO_1_SLUG + `" --verbose --kerberos --target-repo-visibility private }`
	assert.Contains(t, output, expected)
}

// 13. One_Repo_With_No_Ssl_Verify
func TestBbsGenerateScript_One_Repo_With_No_Ssl_Verify(t *testing.T) {
	api := newDefaultBbsMock()

	output := runBbsGenScript(t, api,
		"--bbs-server-url", BBS_SERVER_URL,
		"--github-org", BBS_GITHUB_ORG,
		"--bbs-username", BBS_USERNAME,
		"--bbs-shared-home", BBS_SHARED_HOME,
		"--ssh-user", BBS_SSH_USER,
		"--ssh-private-key", BBS_SSH_PRIVATE_KEY,
		"--ssh-port", "2211",
		"--verbose",
		"--no-ssl-verify",
		"--output", BBS_OUTPUT,
	)

	expected := `Exec { gh bbs2gh migrate-repo --bbs-server-url "` + BBS_SERVER_URL + `" --bbs-username "` + BBS_USERNAME + `" --bbs-shared-home "` + BBS_SHARED_HOME + `" --bbs-project "` + BBS_FOO_PROJECT_KEY + `" --bbs-repo "` + BBS_FOO_REPO_1_SLUG + `" --ssh-user "` + BBS_SSH_USER + `" --ssh-private-key "` + BBS_SSH_PRIVATE_KEY + `" --ssh-port 2211 --github-org "` + BBS_GITHUB_ORG + `" --github-repo "` + BBS_FOO_PROJECT_KEY + `-` + BBS_FOO_REPO_1_SLUG + `" --verbose --no-ssl-verify --target-repo-visibility private }`
	assert.Contains(t, output, expected)
}

// 14. One_Repo_With_Smb
func TestBbsGenerateScript_One_Repo_With_Smb(t *testing.T) {
	api := newDefaultBbsMock()

	output := runBbsGenScript(t, api,
		"--bbs-server-url", BBS_SERVER_URL,
		"--github-org", BBS_GITHUB_ORG,
		"--bbs-username", BBS_USERNAME,
		"--bbs-shared-home", BBS_SHARED_HOME,
		"--smb-user", BBS_SMB_USER,
		"--smb-domain", BBS_SMB_DOMAIN,
		"--verbose",
		"--output", BBS_OUTPUT,
	)

	expected := `Exec { gh bbs2gh migrate-repo --bbs-server-url "` + BBS_SERVER_URL + `" --bbs-username "` + BBS_USERNAME + `" --bbs-shared-home "` + BBS_SHARED_HOME + `" --bbs-project "` + BBS_FOO_PROJECT_KEY + `" --bbs-repo "` + BBS_FOO_REPO_1_SLUG + `" --smb-user "` + BBS_SMB_USER + `" --smb-domain ` + BBS_SMB_DOMAIN + ` --github-org "` + BBS_GITHUB_ORG + `" --github-repo "` + BBS_FOO_PROJECT_KEY + `-` + BBS_FOO_REPO_1_SLUG + `" --verbose --target-repo-visibility private }`
	assert.Contains(t, output, expected)
}

// 15. One_Repo_With_Smb_And_TargetApiUrl
func TestBbsGenerateScript_One_Repo_With_Smb_And_TargetApiUrl(t *testing.T) {
	api := newDefaultBbsMock()
	targetAPIURL := "https://foo.com/api/v3"

	output := runBbsGenScript(t, api,
		"--bbs-server-url", BBS_SERVER_URL,
		"--github-org", BBS_GITHUB_ORG,
		"--bbs-username", BBS_USERNAME,
		"--bbs-shared-home", BBS_SHARED_HOME,
		"--smb-user", BBS_SMB_USER,
		"--smb-domain", BBS_SMB_DOMAIN,
		"--target-api-url", targetAPIURL,
		"--verbose",
		"--output", BBS_OUTPUT,
	)

	expected := `Exec { gh bbs2gh migrate-repo --target-api-url "` + targetAPIURL + `" --bbs-server-url "` + BBS_SERVER_URL + `" --bbs-username "` + BBS_USERNAME + `" --bbs-shared-home "` + BBS_SHARED_HOME + `" --bbs-project "` + BBS_FOO_PROJECT_KEY + `" --bbs-repo "` + BBS_FOO_REPO_1_SLUG + `" --smb-user "` + BBS_SMB_USER + `" --smb-domain ` + BBS_SMB_DOMAIN + ` --github-org "` + BBS_GITHUB_ORG + `" --github-repo "` + BBS_FOO_PROJECT_KEY + `-` + BBS_FOO_REPO_1_SLUG + `" --verbose --target-repo-visibility private }`
	assert.Contains(t, output, expected)
}

// 16. One_Repo_With_Smb_And_Archive_Download_Host
func TestBbsGenerateScript_One_Repo_With_Smb_And_Archive_Download_Host(t *testing.T) {
	api := newDefaultBbsMock()

	output := runBbsGenScript(t, api,
		"--bbs-server-url", BBS_SERVER_URL,
		"--github-org", BBS_GITHUB_ORG,
		"--bbs-username", BBS_USERNAME,
		"--bbs-shared-home", BBS_SHARED_HOME,
		"--smb-user", BBS_SMB_USER,
		"--smb-domain", BBS_SMB_DOMAIN,
		"--archive-download-host", BBS_ARCHIVE_DL_HOST,
		"--verbose",
		"--output", BBS_OUTPUT,
	)

	expected := `Exec { gh bbs2gh migrate-repo --bbs-server-url "` + BBS_SERVER_URL + `" --bbs-username "` + BBS_USERNAME + `" --bbs-shared-home "` + BBS_SHARED_HOME + `" --bbs-project "` + BBS_FOO_PROJECT_KEY + `" --bbs-repo "` + BBS_FOO_REPO_1_SLUG + `" --smb-user "` + BBS_SMB_USER + `" --smb-domain ` + BBS_SMB_DOMAIN + ` --archive-download-host ` + BBS_ARCHIVE_DL_HOST + ` --github-org "` + BBS_GITHUB_ORG + `" --github-repo "` + BBS_FOO_PROJECT_KEY + `-` + BBS_FOO_REPO_1_SLUG + `" --verbose --target-repo-visibility private }`
	assert.Contains(t, output, expected)
}

// 17. Generated_Script_Contains_The_Cli_Version_Comment
func TestBbsGenerateScript_Generated_Script_Contains_The_Cli_Version_Comment(t *testing.T) {
	api := newDefaultBbsMock()

	output := runBbsGenScript(t, api,
		"--bbs-server-url", BBS_SERVER_URL,
		"--github-org", BBS_GITHUB_ORG,
		"--output", BBS_OUTPUT,
	)

	assert.Contains(t, output, "# =========== Created with CLI version "+bbsTestVersion+" ===========")
}

// 18. Generated_Script_StartsWith_Shebang
func TestBbsGenerateScript_Generated_Script_StartsWith_Shebang(t *testing.T) {
	api := newDefaultBbsMock()

	output := runBbsGenScript(t, api,
		"--bbs-server-url", BBS_SERVER_URL,
		"--github-org", BBS_GITHUB_ORG,
		"--output", BBS_OUTPUT,
	)

	assert.True(t, strings.HasPrefix(output, "#!/usr/bin/env pwsh"))
}

// 19. Generated_Script_Contains_Exec_Function_Block
func TestBbsGenerateScript_Generated_Script_Contains_Exec_Function_Block(t *testing.T) {
	api := newDefaultBbsMock()

	output := runBbsGenScript(t, api,
		"--bbs-server-url", BBS_SERVER_URL,
		"--github-org", BBS_GITHUB_ORG,
		"--output", BBS_OUTPUT,
	)

	assert.Contains(t, output, scriptgen.ExecFunctionBlock)
}

// 20. One_Repo_With_Aws_Bucket_Name_And_Region
func TestBbsGenerateScript_One_Repo_With_Aws_Bucket_Name_And_Region(t *testing.T) {
	api := newDefaultBbsMock()

	output := runBbsGenScript(t, api,
		"--bbs-server-url", BBS_SERVER_URL,
		"--github-org", BBS_GITHUB_ORG,
		"--bbs-username", BBS_USERNAME,
		"--bbs-shared-home", BBS_SHARED_HOME,
		"--archive-download-host", BBS_ARCHIVE_DL_HOST,
		"--ssh-user", BBS_SSH_USER,
		"--ssh-private-key", BBS_SSH_PRIVATE_KEY,
		"--ssh-port", "2211",
		"--verbose",
		"--aws-bucket-name", BBS_AWS_BUCKET_NAME,
		"--aws-region", BBS_AWS_REGION,
		"--output", BBS_OUTPUT,
	)

	expected := `Exec { gh bbs2gh migrate-repo --bbs-server-url "` + BBS_SERVER_URL + `" --bbs-username "` + BBS_USERNAME + `" --bbs-shared-home "` + BBS_SHARED_HOME + `" --bbs-project "` + BBS_FOO_PROJECT_KEY + `" --bbs-repo "` + BBS_FOO_REPO_1_SLUG + `" --ssh-user "` + BBS_SSH_USER + `" --ssh-private-key "` + BBS_SSH_PRIVATE_KEY + `" --ssh-port 2211 --archive-download-host ` + BBS_ARCHIVE_DL_HOST + ` --github-org "` + BBS_GITHUB_ORG + `" --github-repo "` + BBS_FOO_PROJECT_KEY + `-` + BBS_FOO_REPO_1_SLUG + `" --verbose --aws-bucket-name "` + BBS_AWS_BUCKET_NAME + `" --aws-region "` + BBS_AWS_REGION + `" --target-repo-visibility private }`
	assert.Contains(t, output, expected)
}

// 21. BBS_Single_Repo_With_UseGithubStorage
func TestBbsGenerateScript_BBS_Single_Repo_With_UseGithubStorage(t *testing.T) {
	const localProjectKey = "BBS-PROJECT"
	const localRepoSlug = "repo-slug"

	api := &mockBbsGenScriptAPI{
		getProjectsFn: func(_ context.Context) ([]genScriptProject, error) {
			return []genScriptProject{{ID: 1, Key: localProjectKey, Name: "BBS Project Name"}}, nil
		},
		getReposFn: func(_ context.Context, projectKey string) ([]genScriptRepository, error) {
			if projectKey == localProjectKey {
				return []genScriptRepository{{ID: 1, Slug: localRepoSlug, Name: "RepoName"}}, nil
			}
			return nil, nil
		},
	}

	output := runBbsGenScript(t, api,
		"--bbs-server-url", BBS_SERVER_URL,
		"--github-org", BBS_GITHUB_ORG,
		"--use-github-storage",
		"--target-api-url", "https://foo.com/api/v3",
		"--bbs-project", localProjectKey,
		"--output", BBS_OUTPUT,
	)

	assert.Contains(t, output, `--bbs-server-url "http://bbs-server-url"`)
	assert.Contains(t, output, `--bbs-project "BBS-PROJECT"`)
	assert.Contains(t, output, `--github-org "GITHUB-ORG"`)
	assert.Contains(t, output, "--use-github-storage")
}

// 22. BBS_Single_Repo_With_TargetUploadsUrl
func TestBbsGenerateScript_BBS_Single_Repo_With_TargetUploadsUrl(t *testing.T) {
	const localProjectKey = "BBS-PROJECT"
	const localRepoSlug = "repo-slug"

	api := &mockBbsGenScriptAPI{
		getProjectsFn: func(_ context.Context) ([]genScriptProject, error) {
			return []genScriptProject{{ID: 1, Key: localProjectKey, Name: "BBS Project Name"}}, nil
		},
		getReposFn: func(_ context.Context, projectKey string) ([]genScriptRepository, error) {
			if projectKey == localProjectKey {
				return []genScriptRepository{{ID: 1, Slug: localRepoSlug, Name: "RepoName"}}, nil
			}
			return nil, nil
		},
	}

	output := runBbsGenScript(t, api,
		"--bbs-server-url", BBS_SERVER_URL,
		"--github-org", BBS_GITHUB_ORG,
		"--target-api-url", "https://foo.com/api/v3",
		"--target-uploads-url", BBS_UPLOADS_URL,
		"--bbs-project", localProjectKey,
		"--output", BBS_OUTPUT,
	)

	assert.Contains(t, output, `--bbs-server-url "http://bbs-server-url"`)
	assert.Contains(t, output, `--bbs-project "BBS-PROJECT"`)
	assert.Contains(t, output, `--github-org "GITHUB-ORG"`)
	assert.Contains(t, output, `--target-uploads-url "UPLOADS-URL"`)
}
