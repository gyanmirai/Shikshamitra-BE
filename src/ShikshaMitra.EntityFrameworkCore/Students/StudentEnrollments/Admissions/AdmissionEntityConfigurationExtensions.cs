using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Volo.Abp.EntityFrameworkCore;

namespace ShikshaMitra.EntityFrameworkCore.Students.StudentEnrollments.Admissions;

internal static class AdmissionEntityConfigurationExtensions
{
	private static readonly ValueConverter<DateTime, DateTimeOffset> UtcInstant = new(
		value => new DateTimeOffset(value.Kind == DateTimeKind.Local
			? value.ToUniversalTime() : DateTime.SpecifyKind(value, DateTimeKind.Utc)),
		value => value.UtcDateTime);

	internal static void ConfigureUtcInstants<TEntity>(this EntityTypeBuilder<TEntity> builder,
		params string[] properties) where TEntity : class
	{
		// ABP 9's GetDatabaseProvider reads this model annotation. Read it through
		// public metadata instead of EF's infrastructure-only ModelBuilder constructor.
		var provider = (EfCoreDatabaseProvider?)builder.Metadata.Model["_Abp_DatabaseProvider"];
		if (provider != EfCoreDatabaseProvider.PostgreSql)
		{
			AdmissionEventTimeConfiguration.Configure(builder, properties);
			return;
		}
		// Preserve the existing PostgreSQL storage and parameter mapping.
		foreach (var property in properties)
			builder.Property(property).HasColumnType("timestamp with time zone").HasConversion(UtcInstant);
	}

	internal static void ConfigureSoftDeleteDefault<TEntity>(this EntityTypeBuilder<TEntity> builder)
		where TEntity : class
	{
		builder.Property<bool>("IsDeleted")
			.IsRequired()
			.ValueGeneratedNever()
			.HasDefaultValue(false);
	}
}
