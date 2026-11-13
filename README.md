<a id="readme-top"></a>

<p align="center">
  <img src="src/KeepClip.App/Assets/AppLogo.png" width="80" alt="KeepClip logo">
</p>

<h1 align="center">KeepClip</h1>

<p align="center"><strong>Copy once. Find it when you need it.</strong></p>
<p align="center">A local clipboard history for Windows, built with C# and WinUI 3.</p>
<p align="center">
  <a href="https://github.com/gdetra/KeepClip/actions/workflows/windows-ci.yml"><img src="https://github.com/gdetra/KeepClip/actions/workflows/windows-ci.yml/badge.svg?branch=main" alt="Windows CI"></a>
  &nbsp; x64 · ARM64 · x86
</p>

KeepClip keeps text, images, and file references in a searchable panel. Pin frequently used items and paste them into another application with **Win+V** or **Ctrl+Shift+V**. The current app interface is in Italian.

![KeepClip showing sample clipboard entries](docs/media/keepclip-overview.png)

## Downloads

See the [release notes](CHANGELOG.md) for new features and fixes.

- [Get KeepClip from Microsoft Store](https://apps.microsoft.com/detail/9pjtns091173?ocid=webpdpshare)

- [Portable builds for x64, ARM64, and x86](https://github.com/gdetra/KeepClip/actions/workflows/windows-release.yml)
- [Browse published versions](https://github.com/gdetra/KeepClip/releases)

Portable build artifacts require GitHub sign-in. Select a successful portable run and download the artifact for your architecture. Extract the included ZIP completely and run `KeepClip.exe`, keeping all extracted files together.

## Build from source

### Requirements

- Windows with the Windows SDK and WinUI / Windows App SDK development tools.
- The .NET SDK specified in [global.json](global.json).
- The .NET 8 runtime for the pinned GitVersion tool.

Open [KeepClip.slnx](KeepClip.slnx) in a compatible IDE, or use PowerShell:

```powershell
git clone https://github.com/gdetra/KeepClip.git
cd KeepClip
dotnet tool restore
dotnet restore KeepClip.slnx --locked-mode
dotnet build src/KeepClip.App/KeepClip.App.csproj -c Release -p:Platform=x64 --no-restore
```

To create a self-contained portable build:

```powershell
./scripts/Build-Portable.ps1 -Architecture x64
```

`-Architecture` accepts `x64`, `arm64`, or `x86`. The script prints the generated archive path. Extract the archive and launch `KeepClip.exe`.

### Run tests

```powershell
dotnet test tests/KeepClip.Tests -c Release --arch x64 --no-restore
dotnet test tests/KeepClip.Windows.Tests -c Release --arch x64 --no-restore
```

Use `--arch x86` to run the 32-bit tests; the matching .NET runtime must be installed.

### Format code

```powershell
./scripts/Format-Code.ps1
./scripts/Format-Markup.ps1
```

Add `-Check` to verify formatting without changing files. Shared conventions are defined in [.editorconfig](.editorconfig).

## Contributing

Submit changes through a pull request targeting `main`. Read [CONTRIBUTING.md](CONTRIBUTING.md) for branch names, code conventions, testing, and the review process.

- [Report a bug](https://github.com/gdetra/KeepClip/issues/new)
- [Discuss an improvement](https://github.com/gdetra/KeepClip/issues)

## Privacy

Clipboard history stays on the device. KeepClip stores file references rather than copies of the original files. See the [privacy policy](docs/privacy.md) for details.
