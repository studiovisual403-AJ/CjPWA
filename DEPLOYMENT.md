# Deployment notes

## Configuration

Provide production settings through the host's environment or secret store. Do not commit credentials.

- `ConnectionStrings__DefaultConnection`: production MySQL connection string.
- `Brevo__Login`, `Brevo__SmtpKey`, and `Brevo__SenderEmail`: email account settings.
- `App__BaseUrl`: public application URL, including `https://`.
- `AllowedHosts`: the public host name.

The application refuses to send promotion email when `App__BaseUrl` is missing or is not HTTPS. Local development uses the localhost URL from `appsettings.Development.json`.

Login throttling uses the client IP address seen by ASP.NET Core. If the app runs behind a reverse proxy, configure trusted forwarded headers at the host so ASP.NET Core sees the real client IP; do not trust forwarded headers from arbitrary clients.

## Production database updates

Normal production startup does not apply migrations. Review and back up the target database, load the production environment variables in a deployment shell, set `ASPNETCORE_ENVIRONMENT=Production`, and run the EF Core database update command from the repository root:

```text
dotnet ef database update --project SmartOrderSystem/SmartOrderSystem.csproj --startup-project SmartOrderSystem/SmartOrderSystem.csproj
```

Then start the application. Startup migrations remain enabled in Development for the existing local workflow.

## Uploaded product images

Images are written to local directories below `wwwroot` (`images` and `uploads/products`). In production, mount durable, backed-up storage at those paths and grant the app write access. Ephemeral container filesystems can lose uploads when instances are replaced; source code alone cannot make local disk durable.
