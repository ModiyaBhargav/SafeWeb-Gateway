# Architecture v4

## Components

1. Windows Mobile Hotspot / Internet Connection Sharing remains the DHCP/NAT/gateway owner.
2. `RuleStore` stores policy and activity in `%ProgramData%\SafeWeb\safeweb.db`.
3. `DomainFilter` performs normalized exact/subdomain matching.
4. `DnsProxy` is only a local upstream DNS worker on `127.0.0.1:5353`; it does not bind ICS port 53.
5. `WinDivertGateway` captures hotspot IPv4 UDP/53 and forwarded IPv4 TCP/UDP 443 traffic.
6. The dashboard exposes policy, enforcement state, hotspot interface, and activity.

## Why WinDivert

Windows ICS already occupies the gateway DNS socket. A normal `UdpClient` cannot take port 53 from `SharedAccess`. WinDivert can capture and re-inject packets at the Windows network/forwarded-packet layers, allowing SafeWeb to filter traffic without replacing ICS.

## DNS flow

```text
client UDP source:random -> 192.168.137.1:53
        |
        +-- WinDivert captures before ICS handler
        |
        +-- SafeWeb extracts DNS question
             |
             +-- BLOCK -> NXDOMAIN response to client
             |
             +-- ALLOW -> 1.1.1.1/8.8.8.8 -> response -> client
```

## HTTPS flow

```text
client -> server:443 TCP
        |
        +-- WinDivert captures inbound forwarded packets
        |
        +-- SafeWeb accumulates ClientHello bytes
        |
        +-- SNI matches BLOCK -> drop packet/flow
        |
        +-- otherwise re-inject unchanged
```

UDP/443 is blocked by default to prevent QUIC from hiding the normal TLS-over-TCP SNI path.
