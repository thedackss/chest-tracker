# Signing setup (SignPath Foundation)

`.github/workflows/release.yml` signs the exe when these exist in the GitHub repo
(Settings > Secrets and variables > Actions):

| Kind | Name | Value |
|---|---|---|
| Variable | `SIGNPATH_ORGANIZATION_ID` | SignPath organization id |
| Secret | `SIGNPATH_API_TOKEN` | API token of a SignPath user with submitter rights |

The SignPath side must use these slugs:

- project: `chest-tracker`, linked to the predefined **GitHub.com** trusted build system
- signing policy: `release-signing` (manual approval)
- artifact configuration: `exe`, contents of [artifact-configuration.xml](artifact-configuration.xml)

Release: bump `<Version>` in `src/ChestTracker/ChestTracker.csproj`, commit, push a tag `vX.Y.Z`,
then approve the signing request on signpath.io. The workflow waits up to an hour for it.
