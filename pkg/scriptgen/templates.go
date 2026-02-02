package scriptgen

// PowerShell script templates embedded in the binary
// These are used to generate migration scripts for all CLI variants (gei, ado2gh, bbs2gh)

const (
	// PwshShebang is the PowerShell shebang line
	PwshShebang = "#!/usr/bin/env pwsh"

	// ExecFunctionBlock defines the Exec helper function for sequential scripts
	ExecFunctionBlock = `
function Exec {
    param (
        [scriptblock]$ScriptBlock
    )
    & @ScriptBlock
    if ($lastexitcode -ne 0) {
        exit $lastexitcode
    }
}`

	// ExecAndGetMigrationIDFunctionBlock defines the helper function for parallel scripts
	ExecAndGetMigrationIDFunctionBlock = `
function ExecAndGetMigrationID {
    param (
        [scriptblock]$ScriptBlock
    )
    $MigrationID = & @ScriptBlock | ForEach-Object {
        Write-Host $_
        $_
    } | Select-String -Pattern "\(ID: (.+)\)" | ForEach-Object { $_.matches.groups[1] }
    return $MigrationID
}`

	// ValidateGHPAT validates that GH_PAT is set
	ValidateGHPAT = `
if (-not $env:GH_PAT) {
    Write-Error "GH_PAT environment variable must be set to a valid GitHub Personal Access Token with the appropriate scopes. For more information see https://docs.github.com/en/migrations/using-github-enterprise-importer/preparing-to-migrate-with-github-enterprise-importer/managing-access-for-github-enterprise-importer#creating-a-personal-access-token-for-github-enterprise-importer"
    exit 1
} else {
    Write-Host "GH_PAT environment variable is set and will be used to authenticate to GitHub."
}`

	// ValidateAzureStorageConnectionString validates Azure storage credentials
	ValidateAzureStorageConnectionString = `
if (-not $env:AZURE_STORAGE_CONNECTION_STRING) {
    Write-Error "AZURE_STORAGE_CONNECTION_STRING environment variable must be set to a valid Azure Storage Connection String that will be used to upload the migration archive to Azure Blob Storage."
    exit 1
} else {
    Write-Host "AZURE_STORAGE_CONNECTION_STRING environment variable is set and will be used to upload the migration archive to Azure Blob Storage."
}`

	// ValidateAWSAccessKeyID validates AWS access key ID
	ValidateAWSAccessKeyID = `
if (-not $env:AWS_ACCESS_KEY_ID) {
    Write-Error "AWS_ACCESS_KEY_ID environment variable must be set to a valid AWS Access Key ID that will be used to upload the migration archive to AWS S3."
    exit 1
} else {
    Write-Host "AWS_ACCESS_KEY_ID environment variable is set and will be used to upload the migration archive to AWS S3."
}`

	// ValidateAWSSecretAccessKey validates AWS secret access key
	ValidateAWSSecretAccessKey = `
if (-not $env:AWS_SECRET_ACCESS_KEY) {
    Write-Error "AWS_SECRET_ACCESS_KEY environment variable must be set to a valid AWS Secret Access Key that will be used to upload the migration archive to AWS S3."
    exit 1
} else {
    Write-Host "AWS_SECRET_ACCESS_KEY environment variable is set and will be used to upload the migration archive to AWS S3."
}`

	// ValidateADOPAT validates that ADO_PAT is set (for ado2gh)
	ValidateADOPAT = `
if (-not $env:ADO_PAT) {
    Write-Error "ADO_PAT environment variable must be set to a valid Azure DevOps Personal Access Token."
    exit 1
} else {
    Write-Host "ADO_PAT environment variable is set and will be used to authenticate to Azure DevOps."
}`

	// ValidateBBSUsername validates that BBS_USERNAME is set (for bbs2gh)
	ValidateBBSUsername = `
if (-not $env:BBS_USERNAME) {
    Write-Error "BBS_USERNAME environment variable must be set."
    exit 1
} else {
    Write-Host "BBS_USERNAME environment variable is set and will be used to authenticate to Bitbucket Server."
}`

	// ValidateBBSPassword validates that BBS_PASSWORD is set (for bbs2gh)
	ValidateBBSPassword = `
if (-not $env:BBS_PASSWORD) {
    Write-Error "BBS_PASSWORD environment variable must be set."
    exit 1
} else {
    Write-Host "BBS_PASSWORD environment variable is set and will be used to authenticate to Bitbucket Server."
}`
)
