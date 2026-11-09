# Third-party notices

SafeWeb Gateway uses or downloads third-party components. Their own licences
and notices continue to apply; the MIT licence in this repository applies to
SafeWeb Gateway's original project code only, not to third-party components.

## WinDivert

The `scripts/install-windivert.ps1` script downloads the x64 WinDivert 2.2.2-A
release from the upstream WinDivert GitHub release page at runtime. The
WinDivert project documents its licensing as GNU Lesser General Public
License v3.0 (LGPL-3.0). The downloaded DLL and driver are intentionally not
committed to this repository. Consult the exact upstream release archive for
its complete licence, notices, and redistribution conditions before bundling
or redistributing those binaries.

Upstream: https://github.com/basil00/WinDivert
Release used by the installer: https://github.com/basil00/WinDivert/releases/tag/v2.2.2

## NuGet dependencies

The application references these NuGet packages (and their transitive
packages) in `SafeWebGateway.csproj`:

- Microsoft.Data.Sqlite 8.0.10
- Microsoft.Extensions.Hosting.WindowsServices 8.0.1

Review the package metadata and applicable Microsoft .NET licence notices
when preparing a distributable release. NuGet packages are restored during
build and are not vendored in this repository.
