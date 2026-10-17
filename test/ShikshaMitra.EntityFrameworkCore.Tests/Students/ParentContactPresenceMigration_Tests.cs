using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Npgsql;
using ShikshaMitra.Migrations;
using Shouldly;
using Xunit;

namespace ShikshaMitra.EntityFrameworkCore.Tests.Students;

public sealed class ParentContactPostgresFactAttribute : FactAttribute
{
	public ParentContactPostgresFactAttribute()
	{
		if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("STUDENT_PROFILE_TEST_POSTGRES")))
			Skip = "Requires a separate loopback student_profile_tests_* database.";
	}
}

public class ParentContactPresenceMigration_Tests
{
	[ParentContactPostgresFact]
	public async Task Repair_restores_contact_only_profiles_without_changing_names_emails_or_empty_profiles()
	{
		var settings = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("STUDENT_PROFILE_TEST_POSTGRES"));
		if (settings.Host is not ("localhost" or "127.0.0.1" or "::1") ||
			settings.Database?.StartsWith("student_profile_tests_", StringComparison.Ordinal) != true)
			throw new InvalidOperationException("Use a separate disposable student_profile_tests_* database.");
		await using var connection = new NpgsqlConnection(settings.ConnectionString);
		await connection.OpenAsync();
		await using var transaction = await connection.BeginTransactionAsync();
		await using var command = connection.CreateCommand();
		command.Transaction = transaction;
		command.CommandText = """
			CREATE TEMP TABLE "StudentFamilyDetails" (
			  "Id" integer PRIMARY KEY,
			  "FatherDetails_FullName" text, "FatherDetails_Email" text, "FatherDetails_ContactNo" text,
			  "MotherDetails_FullName" text, "MotherDetails_Email" text, "MotherDetails_ContactNo" text,
			  "GuardianDetails_FullName" text, "GuardianDetails_Email" text, "GuardianDetails_ContactNo" text
			) ON COMMIT DROP;
			INSERT INTO "StudentFamilyDetails" VALUES
			(1, NULL, 'father@example.test', NULL, NULL, NULL, '9000000001', NULL, 'guardian@example.test', NULL),
			(2, 'Reviewed Father', 'current@example.test', NULL, '', 'mother@example.test', NULL, NULL, NULL, NULL),
			(3, NULL, NULL, NULL, NULL, '   ', NULL, NULL, NULL, NULL);
			""";
		await command.ExecuteNonQueryAsync();
		command.CommandText = new MigrationAccess().RepairSql();
		await command.ExecuteNonQueryAsync();
		await command.ExecuteNonQueryAsync(); // Safe on a repeated repair.
		command.CommandText = """
			SELECT "FatherDetails_FullName", "FatherDetails_Email", "MotherDetails_FullName",
			       "MotherDetails_ContactNo", "GuardianDetails_FullName", "GuardianDetails_Email"
			FROM "StudentFamilyDetails" WHERE "Id" = 1;
			""";
		await using (var reader = await command.ExecuteReaderAsync())
		{
			(await reader.ReadAsync()).ShouldBeTrue();
			reader.GetString(0).ShouldBeEmpty(); reader.GetString(1).ShouldBe("father@example.test");
			reader.GetString(2).ShouldBeEmpty(); reader.GetString(3).ShouldBe("9000000001");
			reader.GetString(4).ShouldBeEmpty(); reader.GetString(5).ShouldBe("guardian@example.test");
		}
		command.CommandText = """
			SELECT "Id", "FatherDetails_FullName", "FatherDetails_Email", "MotherDetails_FullName", "GuardianDetails_FullName"
			FROM "StudentFamilyDetails" WHERE "Id" IN (2, 3) ORDER BY "Id";
			""";
		await using (var reader = await command.ExecuteReaderAsync())
		{
			(await reader.ReadAsync()).ShouldBeTrue();
			reader.GetString(1).ShouldBe("Reviewed Father"); reader.GetString(2).ShouldBe("current@example.test");
			reader.GetString(3).ShouldBeEmpty(); reader.IsDBNull(4).ShouldBeTrue();
			(await reader.ReadAsync()).ShouldBeTrue();
			reader.IsDBNull(1).ShouldBeTrue(); reader.IsDBNull(3).ShouldBeTrue(); reader.IsDBNull(4).ShouldBeTrue();
		}
		await transaction.RollbackAsync();
	}

	private sealed class MigrationAccess : PreserveContactOnlyParentProfiles
	{
		public string RepairSql()
		{
			var builder = new MigrationBuilder("Npgsql.EntityFrameworkCore.PostgreSQL");
			Up(builder);
			return builder.Operations.OfType<SqlOperation>().Single().Sql;
		}
	}
}
