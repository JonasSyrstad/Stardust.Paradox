# NuGet Publishing Guide - Stardust.Paradox.Data.InMemory

This guide covers publishing the Stardust.Paradox.Data.InMemory package to NuGet as a pre-release.

## ?? Pre-Publishing Checklist

### ? Required Files
- [x] **Project file** updated with NuGet metadata
- [x] **README.md** with comprehensive package documentation
- [x] **TESTING_GUIDE.md** with detailed usage instructions
- [x] **SCENARIO_FRAMEWORK_README.md** with scenario documentation
- [x] **Icon** (placeholder created - needs actual icon file)
- [x] **License** (inherited from repository)

### ? Package Metadata
- [x] **Version**: 1.0.0-preview.1 (pre-release)
- [x] **Description**: Comprehensive and clear
- [x] **Tags**: Relevant keywords for discoverability
- [x] **License**: Apache-2.0
- [x] **Repository URLs**: GitHub links
- [x] **Release Notes**: Detailed feature list

### ? Documentation
- [x] **Package README**: User-friendly with quick start
- [x] **Testing Guide**: Comprehensive testing instructions
- [x] **Scenario Documentation**: Complete scenario framework guide
- [x] **API Documentation**: XML documentation enabled
- [x] **Examples**: Multiple usage examples provided

## ?? Publishing Steps

### 1. Verify Build and Tests
Before publishing, ensure everything builds and tests pass:

```bash
# Build the project
dotnet build Stardust.Paradox.Data.InMemory/Stardust.Paradox.Data.InMemory.csproj --configuration Release

# Run all tests
dotnet test Stardust.Paradox.Data.InMemory.Tests/ --configuration Release

# Verify package creation
dotnet pack Stardust.Paradox.Data.InMemory/Stardust.Paradox.Data.InMemory.csproj --configuration Release --output ./nupkg
```

### 2. Create Package Icon (Optional but Recommended)
Create a 64x64 PNG icon file named `icon.png` in the project root:
- Should represent graph/network concepts
- Include Stardust branding elements
- Work on both light and dark backgrounds
- Professional and recognizable

### 3. Update Version for Release
Current version is set to `1.0.0-preview.1`. For future releases:

**Pre-release versions:**
- `1.0.0-preview.2`
- `1.0.0-rc.1`
- `1.0.0-beta.1`

**Stable release:**
- `1.0.0`

### 4. Generate NuGet Package
```bash
# Navigate to project directory
cd Stardust.Paradox.Data.InMemory

# Create release package
dotnet pack --configuration Release --output ../nupkg

# Verify package contents
dotnet nuget verify ../nupkg/Stardust.Paradox.Data.InMemory.1.0.0-preview.1.nupkg
```

### 5. Test Package Locally (Recommended)
Before publishing to NuGet, test the package locally:

```bash
# Create a local NuGet source
nuget sources add -name "Local" -source "C:\path\to\nupkg"

# Create a test project
dotnet new console -n TestPackage
cd TestPackage

# Add the local package
dotnet add package Stardust.Paradox.Data.InMemory --version 1.0.0-preview.1 --source "Local"

# Test basic functionality
# (Add test code to Program.cs)
dotnet run
```

### 6. Publish to NuGet
```bash
# Get your NuGet API key from https://www.nuget.org/account/apikeys

# Set the API key (one-time setup)
nuget setApiKey YOUR_API_KEY_HERE

# Push to NuGet
dotnet nuget push ../nupkg/Stardust.Paradox.Data.InMemory.1.0.0-preview.1.nupkg --source https://api.nuget.org/v3/index.json

# Or with explicit API key
dotnet nuget push ../nupkg/Stardust.Paradox.Data.InMemory.1.0.0-preview.1.nupkg --api-key YOUR_API_KEY_HERE --source https://api.nuget.org/v3/index.json
```

## ?? Package Validation

### Automated Checks
The project file includes settings for automated validation:
- **Deterministic builds**: Ensures reproducible packages
- **Source Link**: Links to source code in GitHub
- **Symbol packages**: Debugging support
- **Documentation**: XML documentation included

### Manual Verification
After publishing, verify the package on NuGet.org:

1. **Package Page**: https://www.nuget.org/packages/Stardust.Paradox.Data.InMemory/
2. **Check Dependencies**: Ensure correct dependency versions
3. **Verify Documentation**: README should display correctly
4. **Test Installation**: Try installing in a new project

## ?? Continuous Integration Setup

### GitHub Actions (Recommended)
Create `.github/workflows/nuget-publish.yml`:

```yaml
name: Publish to NuGet

on:
  push:
    tags:
      - 'v*'  # Trigger on version tags

jobs:
  publish:
    runs-on: ubuntu-latest
    
    steps:
    - uses: actions/checkout@v3
    
    - name: Setup .NET
      uses: actions/setup-dotnet@v3
      with:
        dotnet-version: '8.0.x'
    
    - name: Restore dependencies
      run: dotnet restore
    
    - name: Build
      run: dotnet build --configuration Release --no-restore
    
    - name: Test
      run: dotnet test --configuration Release --no-build --verbosity normal
    
    - name: Pack
      run: dotnet pack Stardust.Paradox.Data.InMemory/Stardust.Paradox.Data.InMemory.csproj --configuration Release --output ./nupkg
    
    - name: Publish to NuGet
      run: dotnet nuget push ./nupkg/*.nupkg --api-key ${{ secrets.NUGET_API_KEY }} --source https://api.nuget.org/v3/index.json
```

### Required Secrets
Add to GitHub repository secrets:
- `NUGET_API_KEY`: Your NuGet.org API key

## ?? Post-Publishing Tasks

### 1. Update Documentation
- [ ] Update main repository README with installation instructions
- [ ] Add package badges to documentation
- [ ] Update any related documentation

### 2. Announce Release
- [ ] Create GitHub release with changelog
- [ ] Update project documentation
- [ ] Notify team/community of new package

### 3. Monitor Usage
- [ ] Track package download statistics
- [ ] Monitor for issues or feedback
- [ ] Plan future releases based on usage

## ?? Troubleshooting

### Common Issues

**Build Errors:**
```bash
# Clear NuGet caches
dotnet nuget locals all --clear

# Restore packages
dotnet restore --force
```

**Package Validation Errors:**
- Ensure all required metadata is present
- Check file paths in project file
- Verify icon file exists and is correct format

**Publishing Errors:**
- Verify API key is correct and active
- Check package version doesn't already exist
- Ensure package meets NuGet.org requirements

**Symbol Package Issues:**
- Ensure source files are included
- Check SourceLink configuration
- Verify debugging symbols are generated

## ?? Version Strategy

### Pre-release Sequence
1. `1.0.0-preview.1` - Current (initial preview)
2. `1.0.0-preview.2` - Bug fixes and improvements
3. `1.0.0-rc.1` - Release candidate
4. `1.0.0` - Stable release

### Future Releases
- **Patch**: `1.0.1` - Bug fixes
- **Minor**: `1.1.0` - New features, backward compatible
- **Major**: `2.0.0` - Breaking changes

## ?? Success Metrics

### Package Quality Indicators
- **Download Count**: Track adoption
- **User Feedback**: Monitor issues and discussions
- **Documentation Views**: Track README and guide usage
- **Integration Success**: Monitor successful installations

### Goals for 1.0.0 Stable
- [ ] 100+ downloads
- [ ] No critical issues reported
- [ ] Positive user feedback
- [ ] Complete documentation
- [ ] Stable API surface

---

Ready to publish! The package is well-documented, thoroughly tested, and ready for the .NET community. ??