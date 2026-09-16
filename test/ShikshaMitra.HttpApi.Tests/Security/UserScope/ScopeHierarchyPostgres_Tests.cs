using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.Infrastructure.Auth;
using Shouldly;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;
using Xunit.Abstractions;

namespace ShikshaMitra.Security.UserScope;

public sealed class SecurityPostgresFactAttribute : FactAttribute
{
    public SecurityPostgresFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SECURITY_TEST_POSTGRES")))
            Skip = "Requires an empty disposable loopback payroll_tests_* PostgreSQL database in SECURITY_TEST_POSTGRES.";
    }
}

public sealed class ScopeQueryCounter : DbCommandInterceptor
{
    private int _count;
    private readonly List<(string Sql, NpgsqlParameter[] Parameters)> _queries = [];
    public int Count => Volatile.Read(ref _count);
    public void Reset() { Interlocked.Exchange(ref _count, 0); _queries.Clear(); }
    private void Capture(DbCommand command)
    {
        _queries.Add((command.CommandText, command.Parameters.Cast<NpgsqlParameter>()
            .Select(parameter => (NpgsqlParameter)parameter.Clone()).ToArray()));
    }

    public async Task ExplainLastRequestAsync(string connectionString, ITestOutputHelper output)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await using (var readOnly = new NpgsqlCommand("SET TRANSACTION READ ONLY", connection, transaction))
            await readOnly.ExecuteNonQueryAsync();
        foreach (var (sql, parameters) in _queries.DistinctBy(query => query.Sql))
        {
            await using var command = new NpgsqlCommand("EXPLAIN (ANALYZE, BUFFERS, FORMAT TEXT) " + sql, connection, transaction);
            command.Parameters.AddRange(parameters);
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                // Plan predicates can contain parameter values. Never export those literals.
                output.WriteLine("PLAN " + Regex.Replace(reader.GetString(0), "'[^']*'", "'<redacted>'"));
            }
        }
        await transaction.RollbackAsync();
    }
    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _count);
        Capture(command);
        return ValueTask.FromResult(result);
    }
    public override InterceptionResult<DbDataReader> ReaderExecuting(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
    {
        Interlocked.Increment(ref _count);
        Capture(command);
        return result;
    }
}

public sealed class SecurityBaselineFactAttribute : FactAttribute
{
    public SecurityBaselineFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SECURITY_BASELINE_POSTGRES")))
            Skip = "Opt-in before-change measurement requires its own empty disposable local database.";
    }
}

public sealed class SecurityPostgresTestFactory(string connectionVariable = "SECURITY_TEST_POSTGRES") : ShikshaMitraApiTestFactory
{
    public ScopeQueryCounter Queries { get; } = new();
    protected override void ConfigureTestDatabase(IServiceCollection services)
    {
        var connection = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable(connectionVariable));
        if (connection.Host is not ("127.0.0.1" or "localhost" or "::1") ||
            connection.Database?.StartsWith("payroll_tests_", StringComparison.Ordinal) != true)
            throw new InvalidOperationException("Security tests require an empty disposable loopback payroll_tests_* database.");
        services.Configure<AbpDbContextOptions>(options => options.Configure(context =>
            context.DbContextOptions.UseNpgsql(connection.ConnectionString).AddInterceptors(Queries)));
        using var database = new ShikshaMitraDbContext(new DbContextOptionsBuilder<ShikshaMitraDbContext>()
            .UseNpgsql(connection.ConnectionString).Options);
        // Never reset a database. A reused database must fail instead of destroying its contents.
        database.GetService<IRelationalDatabaseCreator>().CreateTables();
    }
}

public class ScopeHierarchyPostgres_Tests(ITestOutputHelper output)
{
    [SecurityBaselineFact]
    public Task Profile_before_optimization() => MeasureAsync(enforceAcceptance: false, samples: 5);

    [SecurityPostgresFact]
    public Task Hierarchy_meets_query_and_latency_budget() => MeasureAsync(enforceAcceptance: true, samples: 100);

    private async Task MeasureAsync(bool enforceAcceptance, int samples)
    {
        using var factory = new SecurityPostgresTestFactory(enforceAcceptance ? "SECURITY_TEST_POSTGRES" : "SECURITY_BASELINE_POSTGRES");
        using var client = factory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(30);
        var managerId = Guid.NewGuid();
        client.DefaultRequestHeaders.Add(TestAuthHandler.TestUserIdHeader, managerId.ToString());
        client.DefaultRequestHeaders.Add(TestAuthHandler.TestRolesHeader, "scope-profile");
        int? firstQueryCount = null;
        foreach (var size in new[] { 35, 50, 1000 })
        {
            await AddPopulationAsync(factory, managerId, size);
            var durations = new List<double>();
            var queryCounts = new List<int>();
            double cold = 0;
            for (var request = -5; request < samples; request++)
            {
                factory.Queries.Reset();
                var timer = Stopwatch.StartNew();
                using var response = await client.GetAsync($"/api/app/user-scope/user-scope-hierarchy?userId={managerId}");
                var body = await response.Content.ReadAsStringAsync();
                timer.Stop();
                response.StatusCode.ShouldBe(HttpStatusCode.OK, body);
                if (request == -5) cold = timer.Elapsed.TotalMilliseconds;
                if (request >= 0)
                {
                    durations.Add(timer.Elapsed.TotalMilliseconds);
                    queryCounts.Add(factory.Queries.Count);
                }
            }
            var p95 = durations.Order().ElementAt((int)Math.Ceiling(samples * .95) - 1);
            var maxQueries = queryCounts.Max();
            output.WriteLine($"Employees={size}; samples={samples}; coldMs={cold:F2}; p95Ms={p95:F2}; queriesMin={queryCounts.Min()}; queriesMax={maxQueries}");
            if (enforceAcceptance)
            {
                p95.ShouldBeLessThanOrEqualTo(2000);
                maxQueries.ShouldBeLessThanOrEqualTo(30);
                firstQueryCount ??= maxQueries;
                maxQueries.ShouldBeLessThanOrEqualTo(firstQueryCount.Value);
                if (size == 1000)
                    await factory.Queries.ExplainLastRequestAsync(Environment.GetEnvironmentVariable("SECURITY_TEST_POSTGRES")!, output);
            }
        }
    }

    private static async Task AddPopulationAsync(SecurityPostgresTestFactory factory, Guid managerId, int count)
    {
        using var services = factory.Services.CreateScope();
        using var tenant = services.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(Guid.Parse(TestAuthHandler.TestTenantId));
        using var uow = services.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: true);
        var db = await services.ServiceProvider.GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
        var role = await db.Set<IdentityRole>().FirstAsync();
        var existing = await db.Set<Employee>().Where(x => x.EmployeeCode.StartsWith("SEC-PROFILE-"))
            .Select(x => new { x.Id, x.EmployeeCode }).ToListAsync();
        Guid? parent = existing.OrderBy(x => int.Parse(x.EmployeeCode["SEC-PROFILE-".Length..])).LastOrDefault()?.Id;
        var existingPopulation = await db.Set<Employee>().CountAsync(x => !x.EmployeeCode.StartsWith("SEC-PROFILE-"));
        for (var i = existing.Count; i < count - existingPopulation; i++)
        {
            var userId = i == 0 ? managerId : Guid.NewGuid();
            var user = new IdentityUser(userId, $"scope-profile-{i}", $"scope-profile-{i}@example.invalid", Guid.Parse(TestAuthHandler.TestTenantId));
            db.Set<IdentityUser>().Add(user);
            // Synthetic read-performance fixture only. No compensation, payroll, grants or approvals are created.
            var employee = (Employee)Activator.CreateInstance(typeof(Employee), nonPublic: true)!;
            Set(employee, "Id", Guid.NewGuid());
            Set(employee, "TenantId", Guid.Parse(TestAuthHandler.TestTenantId));
            Set(employee, "UserId", userId);
            Set(employee, "EmployeeCode", $"SEC-PROFILE-{i}");
            Set(employee, "FirstName", "Synthetic");
            Set(employee, "LastName", $"Scope {i}");
            Set(employee, "RoleId", role.Id);
            Set(employee, "PanNo", $"SYNTH{i:D5}");
            Set(employee, "AdharNo", $"SYNTH{i:D7}");
            if (parent.HasValue) employee.AssignReportingManager(parent.Value);
            db.Set<Employee>().Add(employee);
            db.Set<UserScopeAssignment>().Add(new UserScopeAssignment(Guid.NewGuid(), user.TenantId,
                userId: userId, academicYearId: factory.SeededAcademicYearId,
                educationBoardId: factory.SeededBoardId, divisionId: factory.SeededDivisionId,
                description: "Synthetic scope performance fixture."));
            parent = employee.Id;
        }
        await uow.CompleteAsync();
    }

    private static void Set(object target, string property, object value) =>
        target.GetType().GetProperty(property, BindingFlags.Public | BindingFlags.Instance)!.SetValue(target, value);
}
