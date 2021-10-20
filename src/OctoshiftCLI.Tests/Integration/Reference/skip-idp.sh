# =========== Organization: OCLI ===========
# No GitHub App in this org, skipping the re-wiring of Azure Pipelines to GitHub repos

# === Team Project: OCLI/int-tfvc ===
# Skipping this Team Project because it has no git repos

# === Team Project: OCLI/int-git ===
./octoshift create-team --github-org "GuacamoleResearch" --team-name "int-git-Maintainers"
./octoshift create-team --github-org "GuacamoleResearch" --team-name "int-git-Admins"

./octoshift lock-ado-repo --ado-org "OCLI" --ado-team-project "int-git" --ado-repo "git-empty"
./octoshift migrate-repo --ado-org "OCLI" --ado-team-project "int-git" --ado-repo "git-empty" --github-org "GuacamoleResearch" --github-repo "int-git-git-empty"
./octoshift disable-ado-repo --ado-org "OCLI" --ado-team-project "int-git" --ado-repo "git-empty"
./octoshift configure-autolink --github-org "GuacamoleResearch" --github-repo "int-git-git-empty" --ado-org "OCLI" --ado-team-project "int-git"
./octoshift add-team-to-repo --github-org "GuacamoleResearch" --github-repo "int-git-git-empty" --team "int-git-Maintainers" --role "maintain"
./octoshift add-team-to-repo --github-org "GuacamoleResearch" --github-repo "int-git-git-empty" --team "int-git-Admins" --role "admin"

./octoshift lock-ado-repo --ado-org "OCLI" --ado-team-project "int-git" --ado-repo "int-git"
./octoshift migrate-repo --ado-org "OCLI" --ado-team-project "int-git" --ado-repo "int-git" --github-org "GuacamoleResearch" --github-repo "int-git-int-git"
./octoshift disable-ado-repo --ado-org "OCLI" --ado-team-project "int-git" --ado-repo "int-git"
./octoshift configure-autolink --github-org "GuacamoleResearch" --github-repo "int-git-int-git" --ado-org "OCLI" --ado-team-project "int-git"
./octoshift add-team-to-repo --github-org "GuacamoleResearch" --github-repo "int-git-int-git" --team "int-git-Maintainers" --role "maintain"
./octoshift add-team-to-repo --github-org "GuacamoleResearch" --github-repo "int-git-int-git" --team "int-git-Admins" --role "admin"

./octoshift lock-ado-repo --ado-org "OCLI" --ado-team-project "int-git" --ado-repo "int-git1"
./octoshift migrate-repo --ado-org "OCLI" --ado-team-project "int-git" --ado-repo "int-git1" --github-org "GuacamoleResearch" --github-repo "int-git-int-git1"
./octoshift disable-ado-repo --ado-org "OCLI" --ado-team-project "int-git" --ado-repo "int-git1"
./octoshift configure-autolink --github-org "GuacamoleResearch" --github-repo "int-git-int-git1" --ado-org "OCLI" --ado-team-project "int-git"
./octoshift add-team-to-repo --github-org "GuacamoleResearch" --github-repo "int-git-int-git1" --team "int-git-Maintainers" --role "maintain"
./octoshift add-team-to-repo --github-org "GuacamoleResearch" --github-repo "int-git-int-git1" --team "int-git-Admins" --role "admin"

./octoshift integrate-boards --ado-org "OCLI" --ado-team-project "int-git" --github-org "GuacamoleResearch" --github-repos "int-git-git-empty,int-git-int-git,int-git-int-git1"


