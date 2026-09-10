using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;
using Xunit;

namespace Profile.Tests;

/// <summary>
/// API integration tests — full stack (Minimal API → EF Npgsql → Postgres).
/// Shares one <see cref="ProfileApiFixture"/> (single test database);
/// tests isolate by using a fresh user id each.
/// Needs Postgres: local dev (localhost:5432) or CI service.
/// Env overrides: TEST_POSTGRES (server conn), TEST_PROFILE_DB (db name).
/// </summary>
[Collection("ProfileApi")]
public class ProfileApiTests(ProfileApiFixture fixture)
{
    private static string NewUser() => Guid.NewGuid().ToString();

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method, string url, string userId, object? body = null)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Add("X-User-Id", userId);
        if (body is not null)
            request.Content = JsonContent.Create(body);
        return await fixture.Client.SendAsync(request);
    }

    private static async Task<JsonDocument> AsJsonAsync(HttpResponseMessage res)
    {
        var text = await res.Content.ReadAsStringAsync();
        return JsonDocument.Parse(text);
    }

    // ── PROFILE-01-01 / 05 ────────────────────────────────────────────────
    [Fact]
    public async Task PutMe_ThenGetMe_ReturnsDecryptedPii()
    {
        var user = NewUser();

        var put = await SendAsync(HttpMethod.Put, "/api/profiles/me", user, new
        {
            fullName = "Nguyen Van A",
            phone = "0901234567",
            address = "123 Le Duan, Da Nang",
            headline = "Backend Dev",
            summary = (string?)null,
            avatarUrl = (string?)null,
            dateOfBirth = "1998-05-20",
        });
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);

        var get = await SendAsync(HttpMethod.Get, "/api/profiles/me", user);
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);

        using var doc = await AsJsonAsync(get);
        var root = doc.RootElement;
        Assert.Equal("Nguyen Van A", root.GetProperty("fullName").GetString());
        Assert.Equal("0901234567", root.GetProperty("phone").GetString());
        Assert.Equal("123 Le Duan, Da Nang", root.GetProperty("address").GetString());
        Assert.Equal("1998-05-20", root.GetProperty("dateOfBirth").GetString());
    }

    [Fact]
    public async Task GetMe_WithoutHeader_Returns401()
    {
        var res = await fixture.Client.GetAsync("/api/profiles/me");

        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task GetMe_UnknownUser_Returns404()
    {
        var res = await SendAsync(HttpMethod.Get, "/api/profiles/me", NewUser());

        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    // ── PROFILE-01-06: public hides PII ───────────────────────────────────
    [Fact]
    public async Task GetPublicProfile_ExcludesPii()
    {
        var user = NewUser();
        await SendAsync(HttpMethod.Put, "/api/profiles/me", user, new
        {
            fullName = "Public Person",
            phone = "0909999999",
            address = "Secret",
            dateOfBirth = "2000-01-01",
        });

        var res = await fixture.Client.GetAsync($"/api/profiles/{user}");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var text = await res.Content.ReadAsStringAsync();
        Assert.DoesNotContain("0909999999", text);
        using var doc = JsonDocument.Parse(text);
        Assert.False(doc.RootElement.TryGetProperty("phone", out _));
        Assert.False(doc.RootElement.TryGetProperty("address", out _));
        Assert.False(doc.RootElement.TryGetProperty("dateOfBirth", out _));
        Assert.Equal("Public Person", doc.RootElement.GetProperty("fullName").GetString());
    }

    // ── PROFILE-01-02: skills ─────────────────────────────────────────────
    [Fact]
    public async Task AddSkill_Valid_Returns201()
    {
        var res = await SendAsync(HttpMethod.Post, "/api/profiles/skills", NewUser(), new
        {
            name = "C#",
            proficiency = 5,
            yearsOfExperience = 4,
        });

        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
    }

    [Fact]
    public async Task AddSkill_InvalidProficiency_Returns400()
    {
        var res = await SendAsync(HttpMethod.Post, "/api/profiles/skills", NewUser(), new
        {
            name = "C#",
            proficiency = 6,
            yearsOfExperience = 1,
        });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task DeleteSkill_Owner_Returns204_OtherUser_Returns403()
    {
        var owner = NewUser();
        var create = await SendAsync(HttpMethod.Post, "/api/profiles/skills", owner, new
        {
            name = "Go",
            proficiency = 2,
            yearsOfExperience = 1,
        });
        using var doc = await AsJsonAsync(create);
        var skillId = doc.RootElement.GetProperty("id").GetGuid();

        var forbidden = await SendAsync(HttpMethod.Delete, $"/api/profiles/skills/{skillId}", NewUser());
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        var deleted = await SendAsync(HttpMethod.Delete, $"/api/profiles/skills/{skillId}", owner);
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
    }

    [Fact]
    public async Task AddSkill_NameTooLong_Returns400_Not500()
    {
        var res = await SendAsync(HttpMethod.Post, "/api/profiles/skills", NewUser(), new
        {
            name = new string('x', 101),
            proficiency = 3,
            yearsOfExperience = 1,
        });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task DeleteSkill_UnknownId_Returns404()
    {
        var res = await SendAsync(HttpMethod.Delete, $"/api/profiles/skills/{Guid.NewGuid()}", NewUser());

        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    // ── PROFILE-01-03: experiences ────────────────────────────────────────
    [Fact]
    public async Task AddExperience_EndBeforeStart_Returns400()
    {
        var res = await SendAsync(HttpMethod.Post, "/api/profiles/experiences", NewUser(), new
        {
            company = "Acme",
            title = "Dev",
            startDate = "2023-01-01",
            endDate = "2022-01-01",
            isCurrent = false,
        });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task AddExperience_CompanyTooLong_Returns400_Not500()
    {
        var res = await SendAsync(HttpMethod.Post, "/api/profiles/experiences", NewUser(), new
        {
            company = new string('x', 257),
            title = "Dev",
            startDate = "2022-01-01",
            endDate = (string?)null,
            isCurrent = true,
        });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task UpdateExperience_OtherUser_Returns403()
    {
        var owner = NewUser();
        var create = await SendAsync(HttpMethod.Post, "/api/profiles/experiences", owner, new
        {
            company = "Acme",
            title = "Dev",
            startDate = "2022-01-01",
            endDate = (string?)null,
            isCurrent = true,
        });
        using var doc = await AsJsonAsync(create);
        var expId = doc.RootElement.GetProperty("id").GetGuid();

        var res = await SendAsync(HttpMethod.Put, $"/api/profiles/experiences/{expId}", NewUser(), new
        {
            company = "Acme",
            title = "Senior Dev",
            startDate = "2022-01-01",
            endDate = (string?)null,
            isCurrent = true,
        });

        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    // ── PROFILE-01-04: educations ─────────────────────────────────────────
    [Fact]
    public async Task AddEducation_ThenUpdate_Returns200()
    {
        var user = NewUser();
        var create = await SendAsync(HttpMethod.Post, "/api/profiles/educations", user, new
        {
            institution = "HUST",
            degree = "BSc",
            field = "CS",
            startDate = "2018-09-01",
            endDate = "2022-06-01",
            grade = "3.6",
        });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        using var doc = await AsJsonAsync(create);
        var eduId = doc.RootElement.GetProperty("id").GetGuid();

        var update = await SendAsync(HttpMethod.Put, $"/api/profiles/educations/{eduId}", user, new
        {
            institution = "HUST",
            degree = "MSc",
            field = "CS",
            startDate = "2018-09-01",
            endDate = "2022-06-01",
            grade = "3.8",
        });

        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
    }

    [Fact]
    public async Task AddEducation_InstitutionTooLong_Returns400_Not500()
    {
        var res = await SendAsync(HttpMethod.Post, "/api/profiles/educations", NewUser(), new
        {
            institution = new string('x', 257),
            startDate = "2018-09-01",
            endDate = "2022-06-01",
        });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task DeleteEducation_OtherUser_Returns403()
    {
        var owner = NewUser();
        var create = await SendAsync(HttpMethod.Post, "/api/profiles/educations", owner, new
        {
            institution = "HUST",
            startDate = "2018-09-01",
            endDate = "2022-06-01",
        });
        using var doc = await AsJsonAsync(create);
        var eduId = doc.RootElement.GetProperty("id").GetGuid();

        var forbidden = await SendAsync(HttpMethod.Delete, $"/api/profiles/educations/{eduId}", NewUser());

        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
    }
}

/// <summary>
/// Shared API fixture: spins up the real app (Development, DevAuth on)
/// against a dedicated Postgres database, migrated via EF.
/// </summary>
[CollectionDefinition("ProfileApi")]
public class ProfileApiCollection : ICollectionFixture<ProfileApiFixture>;

public class ProfileApiFixture : IAsyncLifetime
{
    public HttpClient Client { get; private set; } = null!;
    private WebApplicationFactory<Program> _factory = null!;
    private string _dbName = "";
    private string _serverConn = "";

    public async Task InitializeAsync()
    {
        _serverConn = Environment.GetEnvironmentVariable("TEST_POSTGRES")
            ?? "Host=localhost;Port=5432;Username=postgres;Password=postgres";
        _dbName = Environment.GetEnvironmentVariable("TEST_PROFILE_DB")
            ?? "job_platform_profile_test";

        // Create the test database if missing (via maintenance db).
        var maintBuilder = new NpgsqlConnectionStringBuilder(_serverConn) { Database = "postgres" };
        await using (var conn = new NpgsqlConnection(maintBuilder.ToString()))
        {
            await conn.OpenAsync();
            await using var check = new NpgsqlCommand(
                "SELECT 1 FROM pg_database WHERE datname = @db", conn);
            check.Parameters.AddWithValue("db", _dbName);
            var exists = await check.ExecuteScalarAsync();
            if (exists is null)
            {
                await using var create = new NpgsqlCommand($"CREATE DATABASE \"{_dbName}\"", conn);
                await create.ExecuteNonQueryAsync();
            }
        }

        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
        // NOTE: Program.cs prefers ConnectionStrings:ProfileDb (appsettings.Development.json)
        // over DATABASE_URL_PROFILE, so override it via env mapping (double underscore).
        var testConn = new NpgsqlConnectionStringBuilder(_serverConn) { Database = _dbName }.ToString();
        Environment.SetEnvironmentVariable("ConnectionStrings__ProfileDb", testConn);
        Environment.SetEnvironmentVariable("DATABASE_URL_PROFILE", testConn);
        Environment.SetEnvironmentVariable("PROFILE_ENCRYPTION_KEY",
            Environment.GetEnvironmentVariable("PROFILE_ENCRYPTION_KEY")
            ?? "test-profile-encryption-key-32-chars!!");

        // Startup runs EF MigrateAsync — creates the schema.
        _factory = new WebApplicationFactory<Program>();
        Client = _factory.CreateClient();
    }

    public async Task DisposeAsync()
    {
        _factory?.Dispose();
        var maintBuilder = new NpgsqlConnectionStringBuilder(_serverConn) { Database = "postgres" };
        await using var conn = new NpgsqlConnection(maintBuilder.ToString());
        await conn.OpenAsync();
        await using var drop = new NpgsqlCommand(
            $"DROP DATABASE IF EXISTS \"{_dbName}\" WITH (FORCE)", conn);
        await drop.ExecuteNonQueryAsync();
    }
}
