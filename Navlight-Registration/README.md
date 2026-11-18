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

## Importing teams

In the admin team import dialog, select **Spreadsheet** for an Excel workbook or **Navlight** for a CSV file. Enter the event name and date and indicate whether the first row is a header.

- Spreadsheet: the first worksheet contains Team Number, Category, Competitor Name, Team Name, and Course.
- Navlight CSV: columns are Team No, an unused column, Grade, Name, Team Name, and Event Code. Grade becomes the category and Event Code becomes the course.

Use one row per competitor. Rows with the same team number are grouped, and missing categories and courses are created for the selected event. Importing an existing team replaces its competitor list. For both formats, team and competitor names are trimmed, `&` becomes `and`, and parentheses and commas are removed.

## Next step

After the .NET 8 SDK is installed, restore and run the app:

```powershell
dotnet restore .\Navlight.Registration.App\Navlight.Registration.App.csproj
dotnet run --project .\Navlight.Registration.App\Navlight.Registration.App.csproj
```

## Tests

Run the registration app tests with:

```powershell
dotnet test .\Navlight.Registration.App.Tests\Navlight.Registration.App.Tests.csproj
```
