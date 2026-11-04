# IdentityService configuration

Production and staging must supply secrets via environment variables or Aspire/AppHost references. Empty values in `appsettings.json` are intentional.

| Key | Required outside Development | Purpose |
| --- | --- | --- |
| `ConnectionStrings__identity` | Yes | PostgreSQL connection string |
| `InternalAuth__ApiKey` | Yes | `X-Internal-Key` for every route except `/health` and `/swagger` |
| `Jwt__SigningKey` | Yes (min 32 chars) | HMAC signing key for access tokens |
| `Kavenegar__ApiKey` | Yes | SMS OTP delivery through `verify/lookup` |
| `Kavenegar__Template` | Yes | Approved lookup template name. English, no spaces or underscores, and the panel text must contain `%token` |
| `Email__WebhookUrl` | Yes | HTTP webhook that sends verification email |

Phone OTP uses Kavenegar Lookup (`POST /v1/{API-KEY}/verify/lookup.json`). The sender line is chosen by Kavenegar, so no sender is configured here. Create an approved template in the panel, for example named `shenokotp`, whose text contains `%token` and no other tokens. The code is a 6-digit number, which satisfies the lookup token rules (no spaces, newlines, or separators). Iranian mobiles are sent as `09…`.

Development uses an in-memory SMS capture when `Kavenegar:ApiKey` is empty. Setting both `Kavenegar:ApiKey` and `Kavenegar:Template` sends real lookup messages, including in Development.

Optional JWT tuning: `Jwt__Issuer`, `Jwt__Audience`, `Jwt__AccessTokenMinutes`, `Jwt__RefreshTokenDays`.

Swagger UI is available in Development at `/swagger`. Requests still need `X-Internal-Key`. The Development key is `dev-internal-key`.

Public routes are reached through the Gateway, which forwards `X-Internal-Key`. Identity exposes `POST /register` (not `/users/register`).
