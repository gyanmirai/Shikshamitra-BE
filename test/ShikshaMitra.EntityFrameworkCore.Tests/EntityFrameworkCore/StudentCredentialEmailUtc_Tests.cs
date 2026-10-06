using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Npgsql;
using ShikshaMitra.EntityFrameworkCore.Students.Portal;
using ShikshaMitra.Students.Portal;
using Shouldly;
using Xunit;

namespace ShikshaMitra.EntityFrameworkCore;

public class StudentCredentialEmailUtc_Tests
{
	private static ValueConverter Converter(string property)
	{
		var builder = new ModelBuilder(new ConventionSet());
		new StudentCredentialEmailRequestConfiguration().Configure(builder.Entity<StudentCredentialEmailRequest>());
		return builder.Model.FindEntityType(typeof(StudentCredentialEmailRequest))!.FindProperty(property)!.GetValueConverter()!;
	}

	[Fact]
	public void Operational_timestamps_use_offset_zero_parameters_and_UTC_reads()
	{
		var instant = new DateTime(2026, 10, 5, 13, 22, 0, DateTimeKind.Utc);
		foreach (var property in new[] { nameof(StudentCredentialEmailRequest.RequestedAt), nameof(StudentCredentialEmailRequest.NextAttemptAt),
			nameof(StudentCredentialEmailRequest.SentAt), nameof(StudentCredentialEmailRequest.ClaimedAt) })
		{
			var converter = Converter(property);
			converter.ProviderClrType.ShouldBe(typeof(DateTimeOffset));
			var stored = (DateTimeOffset)converter.ConvertToProvider(instant)!;
			stored.Offset.ShouldBe(TimeSpan.Zero);
			stored.UtcDateTime.ShouldBe(instant);
			var loaded = (DateTime)converter.ConvertFromProvider(stored.ToOffset(TimeSpan.FromHours(5.5)))!;
			loaded.Kind.ShouldBe(DateTimeKind.Utc);
			loaded.ShouldBe(instant);
		}
		Converter(nameof(StudentCredentialEmailRequest.SentAt)).ConvertFromProvider(null).ShouldBeNull();
	}

	[StudentCredentialEmailPostgresFact]
	public async Task PostgreSql_roundtrip_preserves_cooldown_and_processing_lease_across_school_timezone()
	{
		AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
		var connection = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("STUDENT_CREDENTIAL_EMAIL_TEST_POSTGRES"));
		if (connection.Host is not ("localhost" or "127.0.0.1" or "::1")) throw new InvalidOperationException("Credential email timestamp verification requires loopback PostgreSQL.");
		connection["Timezone"] = "Asia/Kolkata";
		await using var sql = new NpgsqlConnection(connection.ConnectionString);
		await sql.OpenAsync();
		var converter = Converter(nameof(StudentCredentialEmailRequest.RequestedAt));
		var instant = DateTime.UtcNow;
		await using var command = new NpgsqlCommand("SELECT @instant::timestamptz AT TIME ZONE 'UTC'", sql);
		command.Parameters.AddWithValue("instant", converter.ConvertToProvider(instant)!);
		var wallClock = (DateTime)(await command.ExecuteScalarAsync())!;
		Math.Abs((wallClock - instant).Ticks).ShouldBeLessThan(10L); // PostgreSQL stores microseconds.
		var loaded = (DateTime)converter.ConvertFromProvider(new DateTimeOffset(wallClock, TimeSpan.Zero))!;
		var request = new StudentCredentialEmailRequest(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
			Guid.NewGuid(), Guid.NewGuid(), "student", "synthetic@example.test", Guid.NewGuid(), loaded);
		request.Claim(loaded); request.Sent(loaded);
		request.CanRequest(instant.AddMinutes(1)).ShouldBeFalse();
		request.CanRequest(instant.AddMinutes(2).AddSeconds(1)).ShouldBeTrue();
		request.Requeue(Guid.NewGuid(), loaded.AddMinutes(3)); request.Claim(loaded.AddMinutes(3));
		request.RecoverInterrupted(instant.AddMinutes(12)); request.Status.ShouldBe("Processing");
		request.RecoverInterrupted(instant.AddMinutes(14)); request.Status.ShouldBe("Failed");
	}
}

public sealed class StudentCredentialEmailPostgresFactAttribute : FactAttribute
{
	public StudentCredentialEmailPostgresFactAttribute()
	{
		if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("STUDENT_CREDENTIAL_EMAIL_TEST_POSTGRES")))
			Skip = "Requires a read-only loopback PostgreSQL connection in STUDENT_CREDENTIAL_EMAIL_TEST_POSTGRES. No database rows are changed.";
	}
}
