# GitHub Actions Workflow for NuGet Publishing

This repository includes a GitHub Actions workflow that automatically publishes the OpenGIS.Utils package to NuGet.org when a new version tag is created.

## How It Works

The workflow is triggered when you push a tag in the format `v*.*.*` (e.g., `v1.0.0`, `v1.2.3`).

When triggered, the workflow will:
1. Extract the version number from the tag (removing the 'v' prefix)
2. Update the version in the `.csproj` file
3. Restore dependencies
4. Build the project in Release configuration
5. Run all tests
6. Create a NuGet package
7. Publish the package to NuGet.org
8. Create a GitHub Release with the package attached

## Setup Requirements

Before the workflow can publish to NuGet.org, you need to add your NuGet API key as a repository secret:

1. Go to https://www.nuget.org/account/apikeys
2. Create a new API key with "Push" permission for the `OpenGIS.Utils` package
3. In your GitHub repository, go to Settings → Secrets and variables → Actions
4. Create a new repository secret named `NUGET_API_KEY` with your API key value

## Creating a Release

To create a new release and publish to NuGet:

1. Make sure all changes are committed and tests pass
2. Update the CHANGELOG.md with release notes
3. Create and push a version tag:
   ```bash
   git tag v1.0.0
   git push origin v1.0.0
   ```
4. The GitHub Action will automatically run and publish the package

## Version Format

Tags must follow the format `v*.*.*` where each `*` is a number (Semantic Versioning):
- `v1.0.0` - Major version (breaking changes)
- `v1.1.0` - Minor version (new features, backward compatible)
- `v1.0.1` - Patch version (bug fixes)

## Monitoring the Workflow

You can monitor the workflow execution in the "Actions" tab of your GitHub repository. If the workflow fails:
1. Check the workflow logs for error details
2. Verify that the `NUGET_API_KEY` secret is set correctly
3. Ensure tests are passing
4. Check that the version number is valid

## Manual Publishing (Alternative)

If you need to publish manually without using GitHub Actions:

```bash
# Build and pack
dotnet pack src/OpenGIS.Utils/OpenGIS.Utils.csproj --configuration Release --output ./nupkg

# Push to NuGet.org
dotnet nuget push ./nupkg/*.nupkg --api-key YOUR_API_KEY --source https://api.nuget.org/v3/index.json
```
