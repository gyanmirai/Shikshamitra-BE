using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using ShikshaMitra.Students.Portal;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.Students.Portal;

public class StudentCredentialEmailRequestConfiguration : IEntityTypeConfiguration<StudentCredentialEmailRequest>
{
	public void Configure(EntityTypeBuilder<StudentCredentialEmailRequest> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "StudentCredentialEmailRequests", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
		builder.Property(x => x.AccountType).IsRequired().HasMaxLength(16);
		builder.Property(x => x.Email).IsRequired().HasMaxLength(256);
		builder.Property(x => x.Status).IsRequired().HasMaxLength(16);
		builder.Property(x => x.Message).HasMaxLength(512);
		builder.Property(x => x.EncryptedInitialPassword).HasMaxLength(2048);
		// Legacy Npgsql DateTime mapping can interpret a UTC wall-clock as school-local time.
		// Offset-zero parameters preserve the instant; reads must return UTC for cooldowns/leases.
		var utc = new ValueConverter<DateTime, DateTimeOffset>(
			value => new DateTimeOffset(DateTime.SpecifyKind(value, DateTimeKind.Utc)),
			value => value.UtcDateTime);
		builder.Property(x => x.RequestedAt).HasConversion(utc);
		builder.Property(x => x.NextAttemptAt).HasConversion(utc);
		builder.Property(x => x.SentAt).HasConversion(utc);
		builder.Property(x => x.ClaimedAt).HasConversion(utc);
		builder.HasIndex(x => new { x.TenantId, x.CaseId, x.AccountType, x.LinkId }).IsUnique().HasFilter("\"IsDeleted\" = false");
		builder.HasIndex(x => new { x.Status, x.NextAttemptAt });
	}
}
