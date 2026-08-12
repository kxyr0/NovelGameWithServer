# Subscription Entitlement Backend Contract

Client route:

```http
GET /player/subscription/entitlement
Authorization: Bearer <jwt>
Accept: application/json
```

Successful response:

```json
{
  "schemaVersion": 1,
  "userId": "player_123",
  "status": "active",
  "startsAtUtc": "2026-07-01T00:00:00.0000000Z",
  "expiresAtUtc": "2026-08-01T00:00:00.0000000Z",
  "verifiedAtUtc": "2026-07-22T12:00:00.0000000Z",
  "accessRule": "",
  "episodeIds": ["episode_1", "episode_2"],
  "signedToken": "<server-signed compact token>",
  "serverSignature": "<optional detached signature>"
}
```

Rules:

- The server is the source of truth.
- `verifiedAtUtc` must be server UTC time.
- `expiresAtUtc` is the subscription expiration in UTC.
- `episodeIds` lists subscription episodes allowed by this entitlement.
- `accessRule: "all_subscription"` may be used instead of listing every episode.
- `signedToken` or `serverSignature` must cover the entitlement payload.
- The client must not receive or store a server signing secret.
- Temporary server errors should return non-2xx; the client will fall back to a valid signed local cache only inside the 72-hour offline window.
