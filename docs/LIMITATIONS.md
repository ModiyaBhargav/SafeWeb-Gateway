# v4 limitations

- IPv4 enforcement only in this build.
- UDP DNS interception only. TCP DNS is not transparently proxied yet.
- IPv4 HTTPS SNI inspection is implemented for visible TLS ClientHello data.
- ECH hides the hostname from a passive SNI inspector.
- DoH, DoT, VPNs and direct-IP connections need separate bypass controls.
- UDP/443/QUIC is blocked by default to improve hostname visibility; this can affect applications that require QUIC.
- Fail-open is intentional while testing. If WinDivert is missing or fails, ICS continues working.
- Device names/MAC discovery is not yet a full DHCP lease database; the enforcement path identifies devices by source IP.
