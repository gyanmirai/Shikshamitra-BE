using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Xunit;

namespace ShikshaMitra.EntityFrameworkCore;

public class MigrationHistoryPreservation_Tests
{
	[Fact]
	public void Every_active_source_migration_remains_discoverable()
	{
		var root = FindRepositoryRoot();
		var migrationDirectory = Path.Combine(root, "src", "ShikshaMitra.EntityFrameworkCore", "Migrations");
		var ids = Directory.EnumerateFiles(migrationDirectory, "*.cs")
			.Where(file => Path.GetFileName(file) != "20260909160000_AddFeeStructureRevisions.cs")
			.SelectMany(file => Regex.Matches(File.ReadAllText(file), "\\bMigration\\(\\\"([^\\\"]+)\\\"\\)")
				.Cast<Match>().Select(match => match.Groups[1].Value))
			.OrderBy(id => id, StringComparer.Ordinal).ToArray();
		Assert.NotEmpty(ids);
		Assert.Equal(ids.Length, ids.Distinct(StringComparer.Ordinal).Count());
		using var db = CreateContext();
		var discovered = db.GetService<IMigrationsAssembly>().Migrations.Keys.OrderBy(id => id, StringComparer.Ordinal).ToArray();
		Assert.Equal(ids, discovered);
	}

	[Fact]
	public void Historical_migrations_generate_upgrade_and_downgrade_scripts_offline()
	{
		using var db = CreateContext();
		var migrator = db.GetService<IMigrator>();
		const string from = "20260916050346_PrefixAcademicTables";
		const string to = "20260916080050_NormalizeAcademicTableNames";
		var upgrade = migrator.GenerateScript(from, to);
		var downgrade = migrator.GenerateScript(to, from);
		Assert.Contains("academic_Academic", upgrade);
		Assert.Contains("academic_Academic", downgrade);
		Assert.Contains(to, upgrade);
		Assert.Contains(to, downgrade);
		Assert.Equal(System.Data.ConnectionState.Closed, db.Database.GetDbConnection().State);
	}

	private static ShikshaMitraDbContext CreateContext()
	{
		ShikshaMitraEfCoreEntityExtensionMappings.Configure();
		return new ShikshaMitraDbContext(new DbContextOptionsBuilder<ShikshaMitraDbContext>()
			.UseNpgsql("Host=127.0.0.1;Port=1;Database=migration_history_offline;Username=unused;Password=unused;Timeout=1")
			.Options);
	}

	private static string FindRepositoryRoot()
	{
		for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
			if (File.Exists(Path.Combine(directory.FullName, "ShikshaMitra.sln"))) return directory.FullName;
		throw new DirectoryNotFoundException("Could not find the backend repository root.");
	}
}
