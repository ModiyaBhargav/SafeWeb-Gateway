# Code signing policy

## Current status

**Application status: not submitted / not approved.** SafeWeb Gateway releases remain unsigned until a signing provider has reviewed and approved the project and the signing pipeline is operational. A valid signature from a trusted provider may help Windows establish publisher and file integrity; it does not guarantee Smart App Control or SmartScreen acceptance for every build or device.

**Proposed service:** Free code signing provided by SignPath.io, certificate by SignPath Foundation. This is a planned route only; SafeWeb Gateway does not currently hold a SignPath subscription or permission to use a Foundation certificate.

## Project and release scope

- Project: SafeWeb Gateway
- Source repository: https://github.com/ModiyaBhargav/SafeWeb-Gateway
- License for original project code: MIT
- Maintainer: ModiyaBhargav
- Release/build channel: GitHub Actions Windows x64 workflow and public GitHub Releases.
- Only release artifacts built by the reviewed, repository-controlled workflow from this repository may be submitted for signing, after provider approval and manual release approval.
- Intended signing scope: SafeWeb Gateway executable and other first-party PE binaries built from this repository, subject to provider approval and file metadata policy.
- Third-party binaries, including WinDivert and NuGet/runtime components, will not be signed using a SafeWeb/SignPath project certificate. Their own signatures and licenses remain applicable.

## Team roles

SafeWeb Gateway currently has one maintainer:

- **Author / committer:** ModiyaBhargav — maintains the source code, build scripts, and release workflow.
- **Reviewer:** ModiyaBhargav — reviews and decides whether contributions from non-committers are suitable before merging.
- **Signing approver:** ModiyaBhargav — manually reviews each release source commit, workflow run, and artifact before approving a signing request.

The same person currently holds all three roles because this is a single-maintainer project. This is disclosed rather than implying independent review. All users with GitHub or signing-service access must enable multi-factor authentication before signing is enabled.

## Build and release integrity

- Release binaries must be built by the public GitHub Actions workflow from a reviewed repository commit.
- No manually built local binaries or third-party project binaries will be submitted as SafeWeb source outputs.
- The workflow records the source commit and generates SHA-256 checksums.
- Each release signing request must receive manual approval.
- Signing will remain disabled until the provider approves the project and the trusted build/origin verification configuration is complete.

## Functionality and security scope

SafeWeb is a local Windows Mobile Hotspot / Internet Connection Sharing traffic-filtering gateway. It applies user-configured domain allow/block rules to IPv4 DNS and visible IPv4 TLS SNI traffic, and can block UDP/443 (QUIC). It is not a vulnerability scanner, exploit framework, penetration-testing toolkit, or tool for bypassing Windows security controls. It does not disable Smart App Control, Defender, VBS, or Code Integrity.

The current build has documented limitations, including IPv6, DoH, DoT, ECH, VPN and direct-IP bypass paths. See [Limitations](LIMITATIONS.md).

## Privacy

SafeWeb's dashboard binds to localhost (127.0.0.1). Rule and activity data, including observed client IP addresses and domain activity, are stored in a local SQLite database under %ProgramData%\SafeWeb\safeweb.db. For allowed DNS requests, SafeWeb forwards DNS queries to the configured public upstream resolvers (currently Cloudflare 1.1.1.1 and Google 8.8.8.8). Those DNS requests necessarily leave the local computer to be resolved by the selected upstream. The application source currently reports cloud = false and contains no application telemetry or account upload feature. Users should review the upstream resolver privacy policies and the behavior of Windows, WinDivert, and other third-party components separately.

## User-visible system changes and removal

SafeWeb requires Administrator privileges for packet interception and uses the WinDivert driver. It does not intentionally change Windows Mobile Hotspot / ICS ownership of port 53. Users are warned about these requirements and documented limitations before running it. Service installation and removal instructions are provided in the README and scripts.

## Provider and certificate disclaimer

SignPath Foundation eligibility is determined solely by SignPath Foundation. SafeWeb has not been accepted, and no certificate or signing capability is claimed. If accepted, the certificate would identify SignPath Foundation as publisher under its program. Signing does not certify that the application is bug-free, malware-free, or guaranteed to run under every Windows application-control policy.
