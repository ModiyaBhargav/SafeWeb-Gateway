# SafeWeb Gateway privacy notice

Last updated: 9 November 2026

## What SafeWeb stores locally

SafeWeb stores its configuration, allow/block rules, and activity records in a SQLite database at:

%ProgramData%\SafeWeb\safeweb.db

Activity records can include timestamps, client IP addresses, requested domain names, allow/block decisions, and rule/reason information. This information is used by the local dashboard and filtering functions. The dashboard listens on 127.0.0.1:8787 and is intended to be accessed from the same Windows computer.

## Network requests

For DNS requests allowed by the configured rules, SafeWeb forwards DNS queries to the configured public DNS upstreams. The current source configures Cloudflare (1.1.1.1) and Google (8.8.8.8) as upstream resolvers. These DNS queries leave the local computer and are processed under the upstream providers' own privacy terms.

SafeWeb does not currently provide a cloud account, cloud synchronization, analytics, or application telemetry upload feature. The status API reports cloud=false. This statement concerns SafeWeb's application code; it does not describe Windows, network providers, DNS resolver providers, or third-party components.

## Administrator and network access

SafeWeb requires Administrator privileges to use WinDivert for packet interception on the Windows Mobile Hotspot / Internet Connection Sharing path. It inspects selected IPv4 DNS and visible TLS SNI metadata to apply the rules configured by the operator. It does not decrypt HTTPS content.

## User control and deletion

The computer/network operator chooses whether to install and run SafeWeb, configures its rules, and controls access to the local Windows computer. To remove stored SafeWeb configuration and activity data, stop the SafeWeb service/application and remove the local database folder only if the operator no longer needs its contents:

%ProgramData%\SafeWeb

Service removal instructions are provided in the project README and scripts.

## Third-party software

SafeWeb uses .NET and SQLite NuGet dependencies and the separately obtained WinDivert packet interception component. Their own privacy terms, licenses, and notices apply. See [Third-party notices](../THIRD-PARTY-NOTICES.md).

## Contact and updates

The source repository and issue tracker are available at https://github.com/ModiyaBhargav/SafeWeb-Gateway. This notice may be updated when the application's data flows change.
