using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using ShikshaMitra.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;
using Xunit;

namespace ShikshaMitra.TenantSettings;

public sealed class SchoolIdentityPostgresFactAttribute : FactAttribute
{
	public SchoolIdentityPostgresFactAttribute()
	{
		if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(SchoolIdentityPostgresFactory.ConnectionVariable)))
			Skip = "Requires a fresh disposable loopback school_identity_tests_* database in SCHOOL_IDENTITY_TEST_POSTGRES.";
	}
}

public class SchoolIdentityPostgresFactory : ShikshaMitraApiTestFactory
{
	public const string ConnectionVariable = "SCHOOL_IDENTITY_TEST_POSTGRES";
	private const string MigrationId = "20261014053153_AddSchoolIdentity";
	private const string OrganizationMigrationId = "20261021055405_AddOrganizationSchoolIdentity";
	private bool _created;
	public string ConnectionString { get; private set; } = "";
	public string? PredecessorId { get; private set; }
	protected override bool InitializeTestData => false;
	protected override void ConfigureWebHost(IWebHostBuilder builder)
	{
		base.ConfigureWebHost(builder);
		builder.ConfigureAppConfiguration(configuration => configuration.AddInMemoryCollection(new Dictionary<string, string?>
		{
			["ConnectionStrings:Default"] = Environment.GetEnvironmentVariable(ConnectionVariable),
			["Logging:LogLevel:Default"] = "Warning"
		}));
	}
	protected override void ConfigureTestDatabase(IServiceCollection services)
	{
		var connection = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable(ConnectionVariable));
		if (connection.Host is not ("127.0.0.1" or "localhost" or "::1") || connection.Database == null ||
			!Regex.IsMatch(connection.Database, "^school_identity_tests_[a-z0-9_]{1,40}$", RegexOptions.CultureInvariant))
			throw new InvalidOperationException("Only a new disposable loopback school_identity_tests_* database is allowed.");
		ConnectionString = connection.ConnectionString;
		using (var administrator = new NpgsqlConnection(new NpgsqlConnectionStringBuilder(ConnectionString) { Database = "postgres" }.ConnectionString))
		{
			administrator.Open();
			using var exists = new NpgsqlCommand("SELECT count(*) FROM pg_database WHERE datname = @name", administrator);
			exists.Parameters.AddWithValue("name", connection.Database);
			if ((long)exists.ExecuteScalar()! != 0) throw new InvalidOperationException("Refusing to reuse or reset an existing database.");
			using var create = new NpgsqlCommand($"CREATE DATABASE \"{connection.Database}\"", administrator);
			create.ExecuteNonQuery(); _created = true;
		}
		services.Configure<AbpDbContextOptions>(options => options.Configure(context => context.DbContextOptions.UseNpgsql(ConnectionString)));
		using var db = new ShikshaMitraDbContext(new DbContextOptionsBuilder<ShikshaMitraDbContext>().UseNpgsql(ConnectionString).Options);
		db.GetService<IRelationalDatabaseCreator>().CreateTables();
		db.Database.ExecuteSqlRaw("DROP TABLE \"AppSchoolIdentityRevisions\", \"AppSchoolIdentityProfiles\", \"AppSchoolIdentityAssets\";");
		foreach (var table in new[] { "AppAcademicTerms", "AppStudentManagementSettings", "AppStudentRollNumberAssignments" })
			db.Database.ExecuteSqlRaw($"CREATE TABLE \"{table}\" (\"Id\" uuid PRIMARY KEY, \"Sentinel\" text NOT NULL); INSERT INTO \"{table}\" VALUES ('11111111-1111-1111-1111-111111111111', 'preserve');");
		var migrations = db.GetService<IMigrationsAssembly>().Migrations.Keys.OrderBy(id => id).ToList();
		PredecessorId = migrations.Last(id => string.CompareOrdinal(id, MigrationId) < 0);
		db.Database.ExecuteSqlRaw("CREATE TABLE \"__EFMigrationsHistory\" (\"MigrationId\" varchar(150) NOT NULL PRIMARY KEY, \"ProductVersion\" varchar(32) NOT NULL);");
		db.Database.ExecuteSqlRaw("INSERT INTO \"__EFMigrationsHistory\" VALUES ({0}, {1})", PredecessorId, "9.0.0");
		var script = db.GetService<IMigrator>().GenerateScript(PredecessorId, MigrationId);
		if (Regex.IsMatch(script, "\\b(DROP|ALTER)\\s+(TABLE|COLUMN)\\b", RegexOptions.IgnoreCase))
			throw new InvalidOperationException("School Identity migration must be additive.");
		db.Database.ExecuteSqlRaw(script);
		var organizationPredecessor = migrations.Last(id => string.CompareOrdinal(id, OrganizationMigrationId) < 0);
		db.Database.ExecuteSqlRaw(db.GetService<IMigrator>().GenerateScript(organizationPredecessor, OrganizationMigrationId));
	}

	public override async ValueTask DisposeAsync()
	{
		await base.DisposeAsync();
		if (!_created) return;
		var connection = new NpgsqlConnectionStringBuilder(ConnectionString);
		if (connection.Host is not ("127.0.0.1" or "localhost" or "::1") || connection.Database == null ||
			!Regex.IsMatch(connection.Database, "^school_identity_tests_[a-z0-9_]{1,40}$", RegexOptions.CultureInvariant))
			throw new InvalidOperationException("Disposable database identity changed; refusing cleanup.");
		NpgsqlConnection.ClearAllPools();
		await using var administrator = new NpgsqlConnection(new NpgsqlConnectionStringBuilder(ConnectionString) { Database = "postgres" }.ConnectionString);
		await administrator.OpenAsync();
		await using var drop = new NpgsqlCommand($"DROP DATABASE \"{connection.Database}\" WITH (FORCE)", administrator);
		await drop.ExecuteNonQueryAsync(); _created = false;
	}

	public async Task VerifyMigrationDownAsync()
	{
		await using var db = new ShikshaMitraDbContext(new DbContextOptionsBuilder<ShikshaMitraDbContext>().UseNpgsql(ConnectionString).Options);
		var organizationPredecessor = db.GetService<IMigrationsAssembly>().Migrations.Keys
			.Where(id => string.CompareOrdinal(id, OrganizationMigrationId) < 0).OrderBy(id => id).Last();
		await db.Database.ExecuteSqlRawAsync(db.GetService<IMigrator>().GenerateScript(OrganizationMigrationId, organizationPredecessor));
		var script = db.GetService<IMigrator>().GenerateScript(MigrationId, PredecessorId);
		await db.Database.ExecuteSqlRawAsync(script);
		await using var sql = new NpgsqlConnection(ConnectionString);
		await sql.OpenAsync();
		foreach (var table in new[] { "AppAcademicTerms", "AppStudentManagementSettings", "AppStudentRollNumberAssignments" })
		{
			await using var retained = new NpgsqlCommand($"SELECT count(*) FROM \"{table}\" WHERE \"Sentinel\" = 'preserve'", sql);
			Assert.Equal(1L, (long)(await retained.ExecuteScalarAsync())!);
		}
		await using var removed = new NpgsqlCommand("SELECT count(*) FROM information_schema.tables WHERE table_name IN ('AppSchoolIdentityProfiles', 'AppSchoolIdentityAssets', 'AppSchoolIdentityRevisions')", sql);
		Assert.Equal(0L, (long)(await removed.ExecuteScalarAsync())!);
	}
}
