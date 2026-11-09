SAFEWEB GATEWAY v4.5 — WINDOWS x64 TEST PACKAGE

STATUS
This is an unsigned enforcement-test package, not an enterprise-grade security product.
Smart App Control may block the unsigned SafeWebGateway.exe. Do not disable Smart App Control
or Windows security features to run it. If Windows blocks it, stop and retain the displayed
message for troubleshooting.

REQUIREMENTS
- Windows 10/11 x64
- Administrator permission for packet interception
- .NET 8 x64 runtime AND ASP.NET Core 8 x64 runtime
- Windows Mobile Hotspot / Internet Connection Sharing (ICS)
- Internet access for the official WinDivert runtime download

INSTALL / START
1. Extract the complete ZIP to a permanent folder, for example C:\SafeWeb.
2. Double-click Start-SafeWeb.cmd.
3. Approve the Windows UAC elevation prompt if you initiated the launch.
4. The launcher checks .NET runtimes, downloads the official WinDivert 2.2.2-A package
   when missing, verifies that the WinDivert driver has a valid Authenticode signature,
   places the runtime beside the application, and starts SafeWeb.
5. Dashboard: http://127.0.0.1:8787

IMPORTANT NETWORK NOTES
- Do not stop SharedAccess, disable Mobile Hotspot, or change the ICS DNS port.
- SafeWeb currently enforces IPv4 DNS and IPv4 HTTPS SNI paths.
- IPv6, DNS-over-HTTPS, DNS-over-TLS, ECH, VPNs, direct-IP applications and other paths
  may bypass hostname filtering.
- UDP/443 (QUIC) is blocked by default and may affect some applications.
- Fail-open behavior is intentional: if WinDivert fails, ICS may continue providing
  ordinary Internet access.
- Review README.md and docs/LIMITATIONS.md in the source repository before testing.

SECURITY
- This package is unsigned. A valid WinDivert driver signature does NOT mean the SafeWeb
  application is signed or trusted by Smart App Control.
- Do not bypass Smart App Control, create broad allow rules, or disable Windows security.
- The launcher verifies the WinDivert driver signature before copying it. It does not
  install a certificate or alter Windows code-integrity policy.
- Use only on a test network/device you own or are authorized to administer.

SOURCE AND NOTICES
Source: https://github.com/ModiyaBhargav/SafeWeb-Gateway
Third-party notices: THIRD-PARTY-NOTICES.md
SafeWeb original source code licence: MIT. Third-party components remain under their own licences.
