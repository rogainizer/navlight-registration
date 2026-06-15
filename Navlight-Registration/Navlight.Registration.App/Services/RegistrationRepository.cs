using MySqlConnector;
using Navlight.Registration.App.Models;

namespace Navlight.Registration.App.Services;

public sealed class RegistrationRepository
{
    private readonly string _connectionString;

    public RegistrationRepository(DatabaseOptions options)
    {
        _connectionString = options.ToConnectionString();
    }

    public async Task<IReadOnlyList<TeamSearchResult>> SearchTeamsAsync(string searchTerm, bool registeredOnly = false)
    {
                var trimmedSearchTerm = searchTerm.Trim();
                var searchByTeamNumber = trimmedSearchTerm.All(char.IsDigit);

        const string sql = """
            SELECT TeamId, TeamNumber, Name, Registered
            FROM Team
                        WHERE ((@searchByTeamNumber = 1 AND TeamNumber = @teamNumber)
                                     OR (@searchByTeamNumber = 0 AND Name LIKE @searchTerm))
              AND (@registeredOnly = 0 OR Registered = 1)
            ORDER BY Name
            LIMIT 50;
            """;

        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@searchByTeamNumber", searchByTeamNumber);
        command.Parameters.AddWithValue("@teamNumber", trimmedSearchTerm);
        command.Parameters.AddWithValue("@searchTerm", $"%{trimmedSearchTerm}%");
        command.Parameters.AddWithValue("@registeredOnly", registeredOnly);

        var results = new List<TeamSearchResult>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            results.Add(new TeamSearchResult
            {
                TeamId = reader.GetInt32("TeamId"),
                TeamNumber = reader.GetString("TeamNumber"),
                Name = reader.GetString("Name"),
                Registered = reader.GetBoolean("Registered")
            });
        }

        return results;
    }

    public async Task<TeamRegistration> GetTeamRegistrationAsync(int teamId)
    {
        const string teamSql = """
            SELECT TeamId, EventId, TeamNumber, Name, CategoryId, CourseId, Registered, RegisteredAt, FlightPlan, FlightPlanAt, LastUpdatedAt
            FROM Team
            WHERE TeamId = @teamId;
            """;

        const string competitorSql = """
            SELECT CompetitorId, Name
            FROM Competitor
            WHERE TeamId = @teamId
            ORDER BY Name;
            """;

        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();

        TeamRegistration? registration = null;

        await using (var teamCommand = new MySqlCommand(teamSql, connection))
        {
            teamCommand.Parameters.AddWithValue("@teamId", teamId);
            await using var reader = await teamCommand.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                var registeredAtOrdinal = reader.GetOrdinal("RegisteredAt");
                var flightPlanAtOrdinal = reader.GetOrdinal("FlightPlanAt");
                registration = new TeamRegistration
                {
                    TeamId = reader.GetInt32("TeamId"),
                    EventId = reader.GetInt32("EventId"),
                    TeamNumber = reader.GetString("TeamNumber"),
                    Name = reader.GetString("Name"),
                    CategoryId = reader.GetInt32("CategoryId"),
                    CourseId = reader.GetInt32("CourseId"),
                    Registered = reader.GetBoolean("Registered"),
                    RegisteredAt = reader.IsDBNull(registeredAtOrdinal) ? null : reader.GetDateTime("RegisteredAt"),
                    FlightPlan = reader.GetBoolean("FlightPlan"),
                    FlightPlanAt = reader.IsDBNull(flightPlanAtOrdinal) ? null : reader.GetDateTime("FlightPlanAt"),
                    LastUpdatedAt = reader.GetDateTime("LastUpdatedAt")
                };
            }
        }

        if (registration is null)
        {
            throw new InvalidOperationException("The selected team no longer exists.");
        }

        await using (var competitorCommand = new MySqlCommand(competitorSql, connection))
        {
            competitorCommand.Parameters.AddWithValue("@teamId", teamId);
            await using var reader = await competitorCommand.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                registration.Competitors.Add(new CompetitorRecord
                {
                    CompetitorId = reader.GetInt32("CompetitorId"),
                    Name = reader.GetString("Name")
                });
            }
        }

        return registration;
    }

    public async Task<TeamRegistration> GetTeamRegistrationWithTagsAsync(int teamId)
    {
        var registration = await GetTeamRegistrationAsync(teamId);

        const string tagsSql = """
            SELECT TagCode
            FROM TagAssignment
            WHERE TeamId = @teamId
            ORDER BY TagCode;
            """;

        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var tagCommand = new MySqlCommand(tagsSql, connection);
        tagCommand.Parameters.AddWithValue("@teamId", teamId);
        await using var reader = await tagCommand.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            registration.TagCodes.Add(reader.GetString("TagCode"));
        }

        return registration;
    }

    public async Task<IReadOnlyList<CategoryOption>> GetCategoriesAsync(int eventId)
    {
        const string sql = """
            SELECT CategoryId, Name
            FROM Category
            WHERE EventId = @eventId
              AND Active = 1
            ORDER BY Name;
            """;

        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@eventId", eventId);

        var categories = new List<CategoryOption>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            categories.Add(new CategoryOption
            {
                CategoryId = reader.GetInt32("CategoryId"),
                Name = reader.GetString("Name")
            });
        }

        return categories;
    }

    public async Task<IReadOnlyList<CourseOption>> GetCoursesAsync(int eventId)
    {
        const string sql = """
            SELECT CourseId, Name
            FROM Course
            WHERE EventId = @eventId
              AND Active = 1
            ORDER BY Name;
            """;

        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@eventId", eventId);

        var courses = new List<CourseOption>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            courses.Add(new CourseOption
            {
                CourseId = reader.GetInt32("CourseId"),
                Name = reader.GetString("Name")
            });
        }

        return courses;
    }

    public async Task SaveRegistrationAsync(TeamRegistration registration)
    {
        const string updateTeamSql = """
            UPDATE Team
            SET Name = @name,
                CategoryId = @categoryId,
                CourseId = @courseId,
                Registered = 1,
                FlightPlan = @flightPlan,
                LastUpdatedAt = UTC_TIMESTAMP(),
                RegisteredAt = CASE
                    WHEN RegisteredAt IS NULL THEN UTC_TIMESTAMP()
                    ELSE RegisteredAt
                END,
                FlightPlanAt = CASE
                    WHEN @flightPlan = 0 THEN NULL
                    WHEN FlightPlanAt IS NULL THEN UTC_TIMESTAMP()
                    ELSE FlightPlanAt
                END
            WHERE TeamId = @teamId
              AND LastUpdatedAt = @lastUpdatedAt;
            """;

        const string deleteCompetitorsSql = "DELETE FROM Competitor WHERE TeamId = @teamId;";
        const string insertCompetitorSql = "INSERT INTO Competitor (TeamId, Name) VALUES (@teamId, @name);";

        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        try
        {
            await using (var updateTeamCommand = new MySqlCommand(updateTeamSql, connection, transaction))
            {
                updateTeamCommand.Parameters.AddWithValue("@teamId", registration.TeamId);
                updateTeamCommand.Parameters.AddWithValue("@name", registration.Name);
                updateTeamCommand.Parameters.AddWithValue("@categoryId", registration.CategoryId);
                updateTeamCommand.Parameters.AddWithValue("@courseId", registration.CourseId);
                updateTeamCommand.Parameters.AddWithValue("@flightPlan", registration.FlightPlan);
                updateTeamCommand.Parameters.AddWithValue("@lastUpdatedAt", registration.LastUpdatedAt);
                ThrowIfConcurrencyConflict(await updateTeamCommand.ExecuteNonQueryAsync());
            }

            await using (var deleteCommand = new MySqlCommand(deleteCompetitorsSql, connection, transaction))
            {
                deleteCommand.Parameters.AddWithValue("@teamId", registration.TeamId);
                await deleteCommand.ExecuteNonQueryAsync();
            }

            foreach (var competitor in registration.Competitors)
            {
                await using var insertCommand = new MySqlCommand(insertCompetitorSql, connection, transaction);
                insertCommand.Parameters.AddWithValue("@teamId", registration.TeamId);
                insertCommand.Parameters.AddWithValue("@name", competitor.Name);
                await insertCommand.ExecuteNonQueryAsync();
            }

            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task SaveRegistrationAndTagAssignmentsAsync(TeamRegistration registration)
    {
        const string updateTeamSql = """
            UPDATE Team
            SET Name = @name,
                CategoryId = @categoryId,
                CourseId = @courseId,
                Registered = 1,
                FlightPlan = @flightPlan,
                LastUpdatedAt = UTC_TIMESTAMP(),
                RegisteredAt = CASE
                    WHEN RegisteredAt IS NULL THEN UTC_TIMESTAMP()
                    ELSE RegisteredAt
                END,
                FlightPlanAt = CASE
                    WHEN @flightPlan = 0 THEN NULL
                    WHEN FlightPlanAt IS NULL THEN UTC_TIMESTAMP()
                    ELSE FlightPlanAt
                END
            WHERE TeamId = @teamId
              AND LastUpdatedAt = @lastUpdatedAt;
            """;

        const string deleteCompetitorsSql = "DELETE FROM Competitor WHERE TeamId = @teamId;";
        const string insertCompetitorSql = "INSERT INTO Competitor (TeamId, Name) VALUES (@teamId, @name);";
        const string deleteTagsSql = "DELETE FROM TagAssignment WHERE TeamId = @teamId;";
        const string insertTagSql = "INSERT INTO TagAssignment (TeamId, TagCode) VALUES (@teamId, @tagCode);";

        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        try
        {
            await using (var updateTeamCommand = new MySqlCommand(updateTeamSql, connection, transaction))
            {
                updateTeamCommand.Parameters.AddWithValue("@teamId", registration.TeamId);
                updateTeamCommand.Parameters.AddWithValue("@name", registration.Name);
                updateTeamCommand.Parameters.AddWithValue("@categoryId", registration.CategoryId);
                updateTeamCommand.Parameters.AddWithValue("@courseId", registration.CourseId);
                updateTeamCommand.Parameters.AddWithValue("@flightPlan", registration.FlightPlan);
                updateTeamCommand.Parameters.AddWithValue("@lastUpdatedAt", registration.LastUpdatedAt);
                ThrowIfConcurrencyConflict(await updateTeamCommand.ExecuteNonQueryAsync());
            }

            await using (var deleteCompetitorsCommand = new MySqlCommand(deleteCompetitorsSql, connection, transaction))
            {
                deleteCompetitorsCommand.Parameters.AddWithValue("@teamId", registration.TeamId);
                await deleteCompetitorsCommand.ExecuteNonQueryAsync();
            }

            foreach (var competitor in registration.Competitors)
            {
                await using var insertCompetitorCommand = new MySqlCommand(insertCompetitorSql, connection, transaction);
                insertCompetitorCommand.Parameters.AddWithValue("@teamId", registration.TeamId);
                insertCompetitorCommand.Parameters.AddWithValue("@name", competitor.Name);
                await insertCompetitorCommand.ExecuteNonQueryAsync();
            }

            await using (var deleteTagsCommand = new MySqlCommand(deleteTagsSql, connection, transaction))
            {
                deleteTagsCommand.Parameters.AddWithValue("@teamId", registration.TeamId);
                await deleteTagsCommand.ExecuteNonQueryAsync();
            }

            foreach (var tagCode in registration.TagCodes)
            {
                await using var insertTagCommand = new MySqlCommand(insertTagSql, connection, transaction);
                insertTagCommand.Parameters.AddWithValue("@teamId", registration.TeamId);
                insertTagCommand.Parameters.AddWithValue("@tagCode", tagCode);
                await insertTagCommand.ExecuteNonQueryAsync();
            }

            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task SaveAdminTeamAsync(TeamRegistration registration)
    {
        const string insertTeamSql = """
            INSERT INTO Team (EventId, TeamNumber, Name, CategoryId, CourseId, Registered, RegisteredAt, FlightPlan, FlightPlanAt)
            VALUES (
                @eventId,
                @teamNumber,
                @name,
                @categoryId,
                @courseId,
                @registered,
                CASE WHEN @registered = 1 THEN UTC_TIMESTAMP() ELSE NULL END,
                @flightPlan,
                @flightPlanAt
            );
            SELECT LAST_INSERT_ID();
            """;

        const string updateTeamSql = """
            UPDATE Team
            SET TeamNumber = @teamNumber,
                Name = @name,
                CategoryId = @categoryId,
                CourseId = @courseId,
                Registered = @registered,
                FlightPlan = @flightPlan,
                LastUpdatedAt = UTC_TIMESTAMP(),
                RegisteredAt = CASE
                    WHEN @registered = 0 THEN NULL
                    WHEN RegisteredAt IS NULL THEN UTC_TIMESTAMP()
                    ELSE RegisteredAt
                END,
                FlightPlanAt = @flightPlanAt
            WHERE TeamId = @teamId
              AND LastUpdatedAt = @lastUpdatedAt;
            """;

        const string deleteCompetitorsSql = "DELETE FROM Competitor WHERE TeamId = @teamId;";
        const string insertCompetitorSql = "INSERT INTO Competitor (TeamId, Name) VALUES (@teamId, @name);";
        const string deleteTagsSql = "DELETE FROM TagAssignment WHERE TeamId = @teamId;";
        const string insertTagSql = "INSERT INTO TagAssignment (TeamId, TagCode) VALUES (@teamId, @tagCode);";

        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        var teamId = registration.TeamId;

        try
        {
            if (registration.TeamId == 0)
            {
                await using var insertTeamCommand = new MySqlCommand(insertTeamSql, connection, transaction);
                insertTeamCommand.Parameters.AddWithValue("@eventId", registration.EventId);
                insertTeamCommand.Parameters.AddWithValue("@teamNumber", registration.TeamNumber);
                insertTeamCommand.Parameters.AddWithValue("@name", registration.Name);
                insertTeamCommand.Parameters.AddWithValue("@categoryId", registration.CategoryId);
                insertTeamCommand.Parameters.AddWithValue("@courseId", registration.CourseId);
                insertTeamCommand.Parameters.AddWithValue("@registered", registration.Registered);
                insertTeamCommand.Parameters.AddWithValue("@flightPlan", registration.FlightPlan);
                insertTeamCommand.Parameters.AddWithValue("@flightPlanAt", registration.FlightPlanAt);
                teamId = Convert.ToInt32(await insertTeamCommand.ExecuteScalarAsync());
            }
            else
            {
                await using var updateTeamCommand = new MySqlCommand(updateTeamSql, connection, transaction);
                updateTeamCommand.Parameters.AddWithValue("@teamId", registration.TeamId);
                updateTeamCommand.Parameters.AddWithValue("@teamNumber", registration.TeamNumber);
                updateTeamCommand.Parameters.AddWithValue("@name", registration.Name);
                updateTeamCommand.Parameters.AddWithValue("@categoryId", registration.CategoryId);
                updateTeamCommand.Parameters.AddWithValue("@courseId", registration.CourseId);
                updateTeamCommand.Parameters.AddWithValue("@registered", registration.Registered);
                updateTeamCommand.Parameters.AddWithValue("@flightPlan", registration.FlightPlan);
                updateTeamCommand.Parameters.AddWithValue("@flightPlanAt", registration.FlightPlanAt);
                updateTeamCommand.Parameters.AddWithValue("@lastUpdatedAt", registration.LastUpdatedAt);
                ThrowIfConcurrencyConflict(await updateTeamCommand.ExecuteNonQueryAsync());
            }

            await using (var deleteCompetitorsCommand = new MySqlCommand(deleteCompetitorsSql, connection, transaction))
            {
                deleteCompetitorsCommand.Parameters.AddWithValue("@teamId", teamId);
                await deleteCompetitorsCommand.ExecuteNonQueryAsync();
            }

            foreach (var competitor in registration.Competitors)
            {
                await using var insertCompetitorCommand = new MySqlCommand(insertCompetitorSql, connection, transaction);
                insertCompetitorCommand.Parameters.AddWithValue("@teamId", teamId);
                insertCompetitorCommand.Parameters.AddWithValue("@name", competitor.Name);
                await insertCompetitorCommand.ExecuteNonQueryAsync();
            }

            await using (var deleteTagsCommand = new MySqlCommand(deleteTagsSql, connection, transaction))
            {
                deleteTagsCommand.Parameters.AddWithValue("@teamId", teamId);
                await deleteTagsCommand.ExecuteNonQueryAsync();
            }

            foreach (var tagCode in registration.TagCodes)
            {
                await using var insertTagCommand = new MySqlCommand(insertTagSql, connection, transaction);
                insertTagCommand.Parameters.AddWithValue("@teamId", teamId);
                insertTagCommand.Parameters.AddWithValue("@tagCode", tagCode);
                await insertTagCommand.ExecuteNonQueryAsync();
            }

            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<int> GetDefaultEventIdAsync()
    {
        const string sql = """
            SELECT EventId
            FROM Event
            ORDER BY EventDate DESC, EventId DESC
            LIMIT 1;
            """;

        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new MySqlCommand(sql, connection);
        var result = await command.ExecuteScalarAsync();
        if (result is null)
        {
            throw new InvalidOperationException("No event exists. Create an event before adding teams.");
        }

        return Convert.ToInt32(result);
    }

    public async Task<int> GetOrCreateEventAsync(string eventName, DateTime? eventDate)
    {
        const string selectSql = """
            SELECT EventId
            FROM Event
            WHERE Name = @name
              AND EventDate <=> @eventDate
            LIMIT 1;
            """;

        const string insertSql = """
            INSERT INTO Event (Name, EventDate)
            VALUES (@name, @eventDate);
            SELECT LAST_INSERT_ID();
            """;

        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();

        await using (var selectCommand = new MySqlCommand(selectSql, connection))
        {
            selectCommand.Parameters.AddWithValue("@name", eventName);
            selectCommand.Parameters.AddWithValue("@eventDate", eventDate);

            var existingEventId = await selectCommand.ExecuteScalarAsync();
            if (existingEventId is not null)
            {
                return Convert.ToInt32(existingEventId);
            }
        }

        await using var insertCommand = new MySqlCommand(insertSql, connection);
        insertCommand.Parameters.AddWithValue("@name", eventName);
        insertCommand.Parameters.AddWithValue("@eventDate", eventDate);
        return Convert.ToInt32(await insertCommand.ExecuteScalarAsync());
    }

    public async Task<int> CreateCategoryAsync(int eventId, string categoryName)
    {
        const string sql = """
            INSERT INTO Category (EventId, Name, Active)
            VALUES (@eventId, @name, 1);
            SELECT LAST_INSERT_ID();
            """;

        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@eventId", eventId);
        command.Parameters.AddWithValue("@name", categoryName);
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    public async Task<int> CreateCourseAsync(int eventId, string courseName)
    {
        const string sql = """
            INSERT INTO Course (EventId, Name, Active)
            VALUES (@eventId, @name, 1);
            SELECT LAST_INSERT_ID();
            """;

        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@eventId", eventId);
        command.Parameters.AddWithValue("@name", courseName);
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    public async Task<int?> GetTeamIdByNumberAsync(int eventId, string teamNumber)
    {
        const string sql = """
            SELECT TeamId
            FROM Team
            WHERE EventId = @eventId
              AND TeamNumber = @teamNumber
            LIMIT 1;
            """;

        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@eventId", eventId);
        command.Parameters.AddWithValue("@teamNumber", teamNumber);

        var result = await command.ExecuteScalarAsync();
        return result is null ? null : Convert.ToInt32(result);
    }

    public async Task ClearDatabaseAsync()
    {
        const string deleteTagAssignmentsSql = "DELETE FROM TagAssignment;";
        const string deleteCompetitorsSql = "DELETE FROM Competitor;";
        const string deleteTeamsSql = "DELETE FROM Team;";
        const string deleteCategoriesSql = "DELETE FROM Category;";
        const string deleteCoursesSql = "DELETE FROM Course;";
        const string deleteEventsSql = "DELETE FROM Event;";

        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        try
        {
            await using (var command = new MySqlCommand(deleteTagAssignmentsSql, connection, transaction))
            {
                await command.ExecuteNonQueryAsync();
            }

            await using (var command = new MySqlCommand(deleteCompetitorsSql, connection, transaction))
            {
                await command.ExecuteNonQueryAsync();
            }

            await using (var command = new MySqlCommand(deleteTeamsSql, connection, transaction))
            {
                await command.ExecuteNonQueryAsync();
            }

            await using (var command = new MySqlCommand(deleteCategoriesSql, connection, transaction))
            {
                await command.ExecuteNonQueryAsync();
            }

            await using (var command = new MySqlCommand(deleteCoursesSql, connection, transaction))
            {
                await command.ExecuteNonQueryAsync();
            }

            await using (var command = new MySqlCommand(deleteEventsSql, connection, transaction))
            {
                await command.ExecuteNonQueryAsync();
            }

            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task DeleteTeamAsync(int teamId)
    {
        const string sql = "DELETE FROM Team WHERE TeamId = @teamId;";

        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@teamId", teamId);
        await command.ExecuteNonQueryAsync();
    }

    public async Task<TeamTagAssignment> GetTeamTagAssignmentAsync(int teamId)
    {
        const string teamSql = """
            SELECT Team.TeamId,
                   Team.TeamNumber,
                   Team.Name,
                   Category.Name AS CategoryName,
                 Course.Name AS CourseName,
                   Team.Registered,
                                     Team.RegisteredAt,
                                     Team.LastUpdatedAt
            FROM Team
            INNER JOIN Category ON Category.CategoryId = Team.CategoryId
             INNER JOIN Course ON Course.CourseId = Team.CourseId
            WHERE Team.TeamId = @teamId;
            """;

        const string competitorSql = """
            SELECT Name
            FROM Competitor
            WHERE TeamId = @teamId
            ORDER BY Name;
            """;

        const string tagsSql = """
            SELECT TagCode
            FROM TagAssignment
            WHERE TeamId = @teamId
            ORDER BY TagCode;
            """;

        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();

        TeamTagAssignment? team = null;

        await using (var teamCommand = new MySqlCommand(teamSql, connection))
        {
            teamCommand.Parameters.AddWithValue("@teamId", teamId);
            await using var reader = await teamCommand.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                var registeredAtOrdinal = reader.GetOrdinal("RegisteredAt");
                team = new TeamTagAssignment
                {
                    TeamId = reader.GetInt32("TeamId"),
                    TeamNumber = reader.GetString("TeamNumber"),
                    Name = reader.GetString("Name"),
                    CategoryName = reader.GetString("CategoryName"),
                    CourseName = reader.GetString("CourseName"),
                    Registered = reader.GetBoolean("Registered"),
                    RegisteredAt = reader.IsDBNull(registeredAtOrdinal) ? null : reader.GetDateTime("RegisteredAt"),
                    LastUpdatedAt = reader.GetDateTime("LastUpdatedAt")
                };
            }
        }

        if (team is null)
        {
            throw new InvalidOperationException("The selected team no longer exists.");
        }

        await using (var competitorCommand = new MySqlCommand(competitorSql, connection))
        {
            competitorCommand.Parameters.AddWithValue("@teamId", teamId);
            await using var reader = await competitorCommand.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                team.Competitors.Add(reader.GetString("Name"));
            }
        }

        await using (var tagCommand = new MySqlCommand(tagsSql, connection))
        {
            tagCommand.Parameters.AddWithValue("@teamId", teamId);
            await using var reader = await tagCommand.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                team.TagCodes.Add(reader.GetString("TagCode"));
            }
        }

        return team;
    }

    public async Task<string?> GetTagAssignmentOwnerDisplayAsync(string tagCode, int? excludeTeamId = null)
    {
        const string sql = """
            SELECT Team.TeamNumber, Team.Name
            FROM TagAssignment
            INNER JOIN Team ON Team.TeamId = TagAssignment.TeamId
            WHERE TagAssignment.TagCode = @tagCode
              AND (@excludeTeamId IS NULL OR Team.TeamId <> @excludeTeamId)
            LIMIT 1;
            """;

        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@tagCode", tagCode);
        command.Parameters.AddWithValue("@excludeTeamId", excludeTeamId);

        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            return null;
        }

        return $"team {reader.GetString("TeamNumber")} ({reader.GetString("Name")})";
    }

    public async Task<(string TagCode, string OwnerDisplay)?> GetFirstTagAssignmentConflictAsync(
        IReadOnlyCollection<string> tagCodes,
        int? excludeTeamId = null)
    {
        foreach (var tagCode in tagCodes)
        {
            var ownerDisplay = await GetTagAssignmentOwnerDisplayAsync(tagCode, excludeTeamId);
            if (ownerDisplay is not null)
            {
                return (tagCode, ownerDisplay);
            }
        }

        return null;
    }

        public async Task SaveTagAssignmentsAsync(int teamId, DateTime lastUpdatedAt, IReadOnlyList<string> tagCodes)
    {
                const string updateTeamSql = """
                        UPDATE Team
                        SET LastUpdatedAt = UTC_TIMESTAMP()
                        WHERE TeamId = @teamId
                            AND LastUpdatedAt = @lastUpdatedAt;
                        """;

        const string deleteSql = "DELETE FROM TagAssignment WHERE TeamId = @teamId;";
        const string insertSql = "INSERT INTO TagAssignment (TeamId, TagCode) VALUES (@teamId, @tagCode);";

        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        try
        {
            await using (var updateTeamCommand = new MySqlCommand(updateTeamSql, connection, transaction))
            {
                updateTeamCommand.Parameters.AddWithValue("@teamId", teamId);
                updateTeamCommand.Parameters.AddWithValue("@lastUpdatedAt", lastUpdatedAt);
                ThrowIfConcurrencyConflict(await updateTeamCommand.ExecuteNonQueryAsync());
            }

            await using (var deleteCommand = new MySqlCommand(deleteSql, connection, transaction))
            {
                deleteCommand.Parameters.AddWithValue("@teamId", teamId);
                await deleteCommand.ExecuteNonQueryAsync();
            }

            foreach (var tagCode in tagCodes)
            {
                await using var insertCommand = new MySqlCommand(insertSql, connection, transaction);
                insertCommand.Parameters.AddWithValue("@teamId", teamId);
                insertCommand.Parameters.AddWithValue("@tagCode", tagCode);
                await insertCommand.ExecuteNonQueryAsync();
            }

            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    private static void ThrowIfConcurrencyConflict(int affectedRowCount)
    {
        if (affectedRowCount == 0)
        {
            throw new InvalidOperationException("This team was updated by another operator. Reload it before saving again.");
        }
    }

    public async Task<IReadOnlyList<AdminTeamOverviewRow>> GetAdminTeamOverviewAsync()
    {
        const string sql = """
            SELECT Team.TeamId,
                   Team.TeamNumber,
                   Team.Name,
                   Category.Name AS CategoryName,
                                     Course.Name AS CourseName,
                   COALESCE(GROUP_CONCAT(DISTINCT Competitor.Name ORDER BY Competitor.Name SEPARATOR ', '), '') AS Competitors,
                   COALESCE(GROUP_CONCAT(DISTINCT TagAssignment.TagCode ORDER BY TagAssignment.TagCode SEPARATOR ', '), '') AS Tags,
                                     Team.FlightPlan,
                   CASE
                       WHEN Team.Registered = 0 THEN 'Not registered'
                       ELSE 'Registered'
                   END AS Status
            FROM Team
            INNER JOIN Category ON Category.CategoryId = Team.CategoryId
                 INNER JOIN Course ON Course.CourseId = Team.CourseId
            LEFT JOIN Competitor ON Competitor.TeamId = Team.TeamId
            LEFT JOIN TagAssignment ON TagAssignment.TeamId = Team.TeamId
              GROUP BY Team.TeamId, Team.TeamNumber, Team.Name, Category.Name, Course.Name, Team.FlightPlan, Team.Registered
            ORDER BY Team.TeamNumber, Team.Name;
            """;

        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new MySqlCommand(sql, connection);
        var rows = new List<AdminTeamOverviewRow>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            rows.Add(new AdminTeamOverviewRow
            {
                TeamId = reader.GetInt32("TeamId"),
                TeamNumber = reader.GetString("TeamNumber"),
                TeamName = reader.GetString("Name"),
                CategoryName = reader.GetString("CategoryName"),
                CourseName = reader.GetString("CourseName"),
                Competitors = reader.GetString("Competitors"),
                Tags = reader.GetString("Tags"),
                FlightPlan = reader.GetBoolean("FlightPlan"),
                Status = reader.GetString("Status")
            });
        }

        return rows;
    }
}
