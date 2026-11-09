# v4 smoke tests

1. `instagram.com` blocks `www.instagram.com`.
2. `notinstagram.com` does not match `instagram.com`.
3. Allowlist mode blocks domains without an ALLOW rule.
4. Windows hotspot remains ON while SafeWeb starts.
5. UDP/53 from a hotspot client is intercepted by WinDivert.
6. A blocked DNS name produces NXDOMAIN.
7. A blocked visible TLS SNI causes the client packet to be dropped.
8. UDP/443 is blocked when QUIC protection is enabled.
9. Port 53 remains owned by Windows `SharedAccess`.
