# MindMatch TURN server

MindMatch uses WebRTC with short-lived TURN REST credentials. This Coturn service is a separate media relay; the HTTP API creates per-user temporary credentials using the same shared secret.

## Managed Cloudflare TURN (recommended for the current API)

Create a Cloudflare Realtime TURN key and an API token scoped to **Calls Write**. On the API host, configure `Turn__Provider=Cloudflare`, `Turn__CloudflareTurnKeyId`, `Turn__CloudflareApiToken`, and `Turn__CredentialLifetimeSeconds=3600`. Keep the token server-side; the API exchanges it for short-lived TURN credentials and returns only those credentials to the caller. Cloudflare documents TURN usage at $0.05 per real-time GB outbound when not used with Realtime SFU.

The local sample variable names are in the repository `.env.example`. ASP.NET does not automatically load that file; provide these values through the API host's secret/environment configuration. The Cloudflare Quick Tunnel URL for the HTTP API remains separate and does not carry TURN media traffic.

Cloudflare's direct TURN service can be used with the existing peer-to-peer call signaling. Calls still require both clients to reach `turn.cloudflare.com`; the client uses relay-only ICE so media is relayed and peer IP addresses are not exchanged.

## Requirements

- A Linux VPS with a public IPv4 address and Docker Compose.
- A DNS record such as `turn.example.com` pointing directly to that VPS.
- Firewall/security-group access for inbound UDP and TCP port `3478`, and inbound UDP ports `49160-49200`.
- Outbound UDP access from the VPS. Do not put TURN behind a Cloudflare orange-cloud proxy or Cloudflare Quick Tunnel; those do not proxy the WebRTC UDP relay traffic used here.

## Configure and start

From this directory, copy `.env.example` to `.env`, set `TURN_REALM` to the public TURN hostname, and generate a secret of at least 32 UTF-8 bytes for `TURN_SHARED_SECRET`. Keep the secret private and out of source control.

Then start Coturn:

```sh
docker compose --env-file .env up -d
docker compose logs -f coturn
```

The container uses host networking so Coturn can advertise its relay ports without Docker NAT rewriting. Its allocated UDP relay range is intentionally bounded to `49160-49200`; open that same range in the VPS firewall.

## Configure the API

Set these environment variables on the API host, using the **same secret** as `turn/.env`:

```text
Turn__Urls__0=turn:turn.example.com:3478?transport=udp
Turn__Urls__1=turn:turn.example.com:3478?transport=tcp
Turn__SharedSecret=<same private TURN_SHARED_SECRET>
Turn__CredentialLifetimeSeconds=600
```

Replace `turn.example.com` with the real public hostname, then restart the API. Verify an authenticated request to `GET /api/calls/turn-credentials` returns `200` and two URLs instead of `503`. The response contains short-lived credentials; do not log or share them.

## iOS/TestFlight

Calls require `react-native-webrtc` in a native iOS binary. This project includes the WebRTC config plugin and camera/microphone permission descriptions. After changing native app configuration, create and distribute a new TestFlight build; an Expo OTA update cannot add the native WebRTC module or alter iOS entitlements/permissions.

Once the TURN DNS, firewall, Coturn service, and API settings are live, test audio and video calls between two accounts on separate mobile networks (for example, Wi-Fi and cellular). The client deliberately uses relay-only ICE so calls do not reveal peer IP addresses and so successful setup confirms TURN is reachable.

## Troubleshooting

- API returns `503` from `/api/calls/turn-credentials`: check `Turn__Urls__*`, secret length, and API restart.
- ICE stays in `checking` or ends `failed`: check DNS, UDP/TCP `3478`, UDP `49160-49200`, VPS outbound UDP, and that Coturn/API secrets match exactly.
- TestFlight says native module is unavailable: install a newly built TestFlight binary containing the WebRTC config plugin; OTA alone is insufficient.
