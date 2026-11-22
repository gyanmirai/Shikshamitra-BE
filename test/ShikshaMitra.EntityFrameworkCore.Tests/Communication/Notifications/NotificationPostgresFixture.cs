using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace ShikshaMitra.Communication.Notifications;

public sealed class NotificationPostgresFactAttribute : FactAttribute
{
	public NotificationPostgresFactAttribute()
	{
		if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(NotificationPostgresFixture.VariableName)))
			Skip = "Set SHIKSHAMITRA_NOTIFICATION_TEST_POSTGRES to an isolated local test cluster; no school connection is used.";
	}
}

public sealed class NotificationPostgresFixture : IAsyncLifetime
{
	public const string VariableName = "SHIKSHAMITRA_NOTIFICATION_TEST_POSTGRES";
	private string _database = string.Empty;
	private string _adminConnection = string.Empty;
	private string _testConnection = string.Empty;

	public async Task InitializeAsync()
	{
		var configured = Environment.GetEnvironmentVariable(VariableName);
		if (string.IsNullOrWhiteSpace(configured)) return;
		var settings = new NpgsqlConnectionStringBuilder(configured);
		if (settings.Host is not ("127.0.0.1" or "localhost" or "::1") ||
			settings.Database?.StartsWith("notification_tests", StringComparison.Ordinal) != true)
			throw new InvalidOperationException("Notification persistence tests require an explicit local notification_tests database target.");
		_database = "notification_tests_" + Guid.NewGuid().ToString("N");
		settings.Database = "postgres";
		settings.Pooling = false;
		_adminConnection = settings.ConnectionString;
		await using (var connection = new NpgsqlConnection(_adminConnection))
		{
			await connection.OpenAsync();
			await using var command = new NpgsqlCommand($"CREATE DATABASE \"{_database}\"", connection);
			await command.ExecuteNonQueryAsync();
		}
		settings.Database = _database;
		_testConnection = settings.ConnectionString;
		await using var context = CreateContext();
		await context.Database.EnsureCreatedAsync();
	}

	public NotificationPersistenceTestDbContext CreateContext()
	{
		if (string.IsNullOrEmpty(_testConnection)) throw new InvalidOperationException("The isolated PostgreSQL fixture is not configured.");
		return new(new DbContextOptionsBuilder<NotificationPersistenceTestDbContext>().UseNpgsql(_testConnection).Options);
	}

	public NpgsqlConnection CreateConnection() => new(_testConnection);

	public async Task DisposeAsync()
	{
		if (string.IsNullOrEmpty(_database)) return;
		await using var connection = new NpgsqlConnection(_adminConnection);
		await connection.OpenAsync();
		await using var command = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{_database}\" WITH (FORCE)", connection);
		await command.ExecuteNonQueryAsync();
	}
}
