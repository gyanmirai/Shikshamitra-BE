using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using ShikshaMitra.EntityFrameworkCore.Students.StudentEnrollments.Admissions;
using Shouldly;
using Volo.Abp.EntityFrameworkCore;
using Xunit;

namespace ShikshaMitra.EntityFrameworkCore.Tests.Students.Enrollment;

public class AdmissionProviderTimeMapping_Tests
{
	[Theory]
	[InlineData(null, false, false)]
	[InlineData(EfCoreDatabaseProvider.Sqlite, false, false)]
	[InlineData(EfCoreDatabaseProvider.PostgreSql, false, true)]
	[InlineData(null, true, true)]
	[InlineData(EfCoreDatabaseProvider.Sqlite, true, true)]
	public void Provider_detection_preserves_existing_time_storage(EfCoreDatabaseProvider? provider,
		bool hasNpgsqlAnnotation, bool timezoneAware)
	{
		var model = new ModelBuilder(new ConventionSet());
		if (provider.HasValue) model.SetDatabaseProvider(provider.Value);
		if (hasNpgsqlAnnotation) model.Model.SetAnnotation("Npgsql:ValueGenerationStrategy", 0);
		var entity = model.Entity<TimeRow>();
		entity.ConfigureUtcInstants(nameof(TimeRow.OccurredAt));
		var property = entity.Property(x => x.OccurredAt).Metadata;
		property.GetValueConverter().ShouldNotBeNull().ProviderClrType
			.ShouldBe(timezoneAware ? typeof(DateTimeOffset) : typeof(DateTime));
		if (timezoneAware) property.GetColumnType().ShouldBe("timestamp with time zone");
	}

	private sealed class TimeRow
	{
		public DateTime OccurredAt { get; set; }
	}
}
