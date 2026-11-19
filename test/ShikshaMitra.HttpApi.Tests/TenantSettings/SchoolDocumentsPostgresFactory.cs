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

public sealed class SchoolDocumentsPostgresFactAttribute : FactAttribute
{
	public SchoolDocumentsPostgresFactAttribute()
	{
		if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(SchoolDocumentsPostgresFactory.ConnectionVariable)))
			Skip = "Requires a fresh disposable loopback school_documents_tests_* database in SCHOOL_DOCUMENTS_TEST_POSTGRES.";
	}
}

public class SchoolDocumentsPostgresFactory : SchoolDocumentsTestFactory
{
	public const string ConnectionVariable = "SCHOOL_DOCUMENTS_TEST_POSTGRES";
	private const string MigrationId = "20261021052944_AddSchoolDocuments";
	private string _connection = "";
	private string _previous = "";
	private bool _created;

	protected override void ConfigureWebHost(IWebHostBuilder builder)
	{
		base.ConfigureWebHost(builder);
		builder.ConfigureAppConfiguration(config => config.AddInMemoryCollection(new Dictionary<string, string?>
			{ ["ConnectionStrings:Default"] = Environment.GetEnvironmentVariable(ConnectionVariable) }));
	}

	protected override void ConfigureTestDatabase(IServiceCollection services)
	{
		var connection = CheckedConnection(Environment.GetEnvironmentVariable(ConnectionVariable)!);
		_connection = connection.ConnectionString;
		using (var admin = new NpgsqlConnection(new NpgsqlConnectionStringBuilder(_connection) { Database = "postgres" }.ConnectionString))
		{
			admin.Open();
			using var exists = new NpgsqlCommand("SELECT count(*) FROM pg_database WHERE datname = @name", admin);
			exists.Parameters.AddWithValue("name", connection.Database!);
			if ((long)exists.ExecuteScalar()! != 0) throw new InvalidOperationException("Refusing to reuse an existing database.");
			using var create = new NpgsqlCommand($"CREATE DATABASE \"{connection.Database}\"", admin);
			create.ExecuteNonQuery();
			_created = true;
		}
		services.Configure<AbpDbContextOptions>(options => options.Configure(context => context.DbContextOptions.UseNpgsql(_connection)));
		using var db = Context();
		db.GetService<IRelationalDatabaseCreator>().CreateTables();
		db.Database.ExecuteSqlRaw("DROP TABLE \"AppSchoolDocumentFiles\", \"AppSchoolDocuments\";");
		db.Database.ExecuteSqlRaw("CREATE TABLE \"SchoolDocumentMigrationSentinel\" (\"Value\" integer NOT NULL); INSERT INTO \"SchoolDocumentMigrationSentinel\" VALUES (1);");
		_previous = db.GetService<IMigrationsAssembly>().Migrations.Keys.Where(id => string.CompareOrdinal(id, MigrationId) < 0).Max()!;
		db.Database.ExecuteSqlRaw("CREATE TABLE \"__EFMigrationsHistory\" (\"MigrationId\" varchar(150) NOT NULL PRIMARY KEY, \"ProductVersion\" varchar(32) NOT NULL);");
		db.Database.ExecuteSqlRaw("INSERT INTO \"__EFMigrationsHistory\" VALUES ({0}, {1})", _previous, "9.0.0");
		var script = db.GetService<IMigrator>().GenerateScript(_previous, MigrationId);
		if (Regex.IsMatch(script, "\\b(DROP|ALTER)\\s+(TABLE|COLUMN)\\b", RegexOptions.IgnoreCase))
			throw new InvalidOperationException("School Documents migration must be additive.");
		db.Database.ExecuteSqlRaw(script);
	}

	public async Task VerifyDownAsync()
	{
		await using var db = Context();
		await db.Database.ExecuteSqlRawAsync(db.GetService<IMigrator>().GenerateScript(MigrationId, _previous));
		var sentinel = await db.Database.SqlQueryRaw<int>("SELECT \"Value\" FROM \"SchoolDocumentMigrationSentinel\"").SingleAsync();
		if (sentinel != 1) throw new InvalidOperationException("Migration changed unrelated data.");
		var remaining = await db.Database.SqlQueryRaw<long>("SELECT count(*) AS \"Value\" FROM information_schema.tables WHERE table_name IN ('AppSchoolDocuments', 'AppSchoolDocumentFiles')").SingleAsync();
		if (remaining != 0) throw new InvalidOperationException("Down migration left document tables.");
	}

	private ShikshaMitraDbContext Context() => new(new DbContextOptionsBuilder<ShikshaMitraDbContext>().UseNpgsql(_connection).Options);

	private static NpgsqlConnectionStringBuilder CheckedConnection(string value)
	{
		var connection = new NpgsqlConnectionStringBuilder(value);
		if (connection.Host is not ("127.0.0.1" or "localhost" or "::1") || connection.Database == null ||
			!Regex.IsMatch(connection.Database, "^school_documents_tests_[a-z0-9_]{1,40}$", RegexOptions.CultureInvariant))
			throw new InvalidOperationException("Only a fresh disposable loopback school_documents_tests_* database is allowed.");
		return connection;
	}

	public override async ValueTask DisposeAsync()
	{
		await base.DisposeAsync();
		if (!_created) return;
		var connection = CheckedConnection(_connection);
		NpgsqlConnection.ClearAllPools();
		await using var admin = new NpgsqlConnection(new NpgsqlConnectionStringBuilder(_connection) { Database = "postgres" }.ConnectionString);
		await admin.OpenAsync();
		await using var drop = new NpgsqlCommand($"DROP DATABASE \"{connection.Database}\" WITH (FORCE)", admin);
		await drop.ExecuteNonQueryAsync();
		_created = false;
	}
}
