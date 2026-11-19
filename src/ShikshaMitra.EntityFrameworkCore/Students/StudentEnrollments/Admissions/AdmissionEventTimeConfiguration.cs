using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using ShikshaMitra.Students.StudentEnrollments.Admissions;

namespace ShikshaMitra.EntityFrameworkCore.Students.StudentEnrollments.Admissions;

internal static class AdmissionEventTimeConfiguration
{
	private static readonly ValueConverter<DateTime, DateTimeOffset> UtcInstant = new(
		value => new DateTimeOffset(AdmissionEventUtcTime.Normalize(value)), value => value.UtcDateTime);
	private static readonly ValueConverter<DateTime, DateTime> UtcTestInstant = new(
		value => AdmissionEventUtcTime.Normalize(value), value => AdmissionEventUtcTime.Normalize(value));

	internal static void Configure<TEntity>(EntityTypeBuilder<TEntity> builder, params string[] properties) where TEntity : class
	{
		if (builder.Metadata.Model.FindAnnotation("Npgsql:ValueGenerationStrategy") == null)
		{
			foreach (var property in properties) builder.Property(property).HasConversion(UtcTestInstant);
			return;
		}
		// Keep legacy Npgsql parameters timezone-aware when writing the existing timestamptz columns.
		foreach (var property in properties)
			builder.Property(property).HasConversion(UtcInstant).HasColumnType("timestamp with time zone");
	}
}
