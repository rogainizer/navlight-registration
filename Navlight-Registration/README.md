# Navlight Registration

Minimal WinForms registration app for a rogaine event using a shared MySQL database on a local network.

## Current scope

This first slice covers registration only:
- search preloaded teams by name
- load team details
- edit team name
- change category from valid event categories
- add or remove competitors
- mark a team as registered
- save changes back to MySQL

Tag assignment is intentionally not included yet.

## Project

- `Navlight.Registration.App` - WinForms desktop app
- `database/schema.sql` - MySQL schema for event, category, team, and competitor data

## Prerequisites

- Windows
- .NET 8 SDK
- MySQL Server reachable on the local network

This environment only has the .NET runtime installed, so the project files were created but not compiled here.

## Configuration

Copy `Navlight.Registration.App/appsettings.example.json` to `Navlight.Registration.App/appsettings.json`, then edit it with your database settings.

## Database setup

Run `database/schema.sql` on the MySQL host, then preload:
- event row
- categories for that event
- teams
- competitors

For local testing, you can also load `database/test-data.sql` after the schema to create a small sample event with categories, teams, and competitors.

## Next step

After the .NET 8 SDK is installed, restore and run the app:

```powershell
dotnet restore .\Navlight.Registration.App\Navlight.Registration.App.csproj
dotnet run --project .\Navlight.Registration.App\Navlight.Registration.App.csproj
```
