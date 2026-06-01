# Passwordless Member Access Implementation Notes

## Runtime Configuration

Production email delivery uses `SmtpAuthEmailSender` and reads these keys from configuration:

- `Auth:Smtp:Host` - required SMTP host name.
- `Auth:Smtp:Port` - optional SMTP port; defaults to `587`.
- `Auth:Smtp:EnableTls` - optional TLS flag; defaults to `true`.
- `Auth:Smtp:Username` - optional SMTP username.
- `Auth:Smtp:Password` - optional SMTP password or provider token.
- `Auth:Smtp:SenderAddress` - required sender address for login emails.

Production must also set `Auth:PublicBaseUrl` to the public HTTPS origin for the app, for example `https://example.com`. Magic links are generated from this base URL. If it is omitted, the app falls back to the current request scheme and host.

Do not commit SMTP credentials or provider tokens. Set secrets through the deployment platform's environment variable or secret store.

## Local Development

Development registers `LoggingAuthEmailSender` instead of SMTP. Login requests write the generated passwordless link to application logs with the message template:

```text
Passwordless login link for {Email}: {LoginLink}
```

Use the normal local database connection settings already documented for the app. Auth schema is delivered through the explicit EF Core migration and is not applied automatically at app startup.

## Manual Smoke Test

1. Start the app in Development.
2. Open `/login`, enter an email address, and submit the form.
3. Copy the logged magic link from the application logs and open it.
4. Confirm the nav shows the signed-in member state.
5. Search from `/` and open a game detail page while signed in.
6. Submit logout and confirm the nav returns to anonymous state.
7. Reopen the consumed link and confirm it redirects to the safe failed-login path.
8. Open `/auth/magic-link/consume?token=invalid` and confirm it redirects to the safe failed-login path.

## Operational Notes

Magic links are valid for 15 minutes and are one-use. Unknown emails create a member only after successful link consumption. Return URLs are stored only if they are local relative URLs; external or malformed values normalize to `/`.

Request throttling is deferred for MVP. The `MagicLinkRequests` table stores normalized email, request IP address, user agent, creation time, expiry, and consumption state so throttling and audit-oriented hardening can be added later.
