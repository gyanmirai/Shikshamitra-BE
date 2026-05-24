using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.HR.Policies;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.HR.Policies;

public class WorkCalendarHolidayConfiguration : IEntityTypeConfiguration<WorkCalendarHoliday>
{
	public void Configure(EntityTypeBuilder<WorkCalendarHoliday> builder)
	{
		builder.ToTable("HR_WorkCalendarHolidays");
		builder.ConfigureByConvention();

		builder.Property(x => x.AcademicYearId).IsRequired();
		builder.Property(x => x.WorkCalendarPolicyId).IsRequired();
		builder.Property(x => x.Date).IsRequired();
		builder.Property(x => x.FromDate).IsRequired();
		builder.Property(x => x.ToDate).IsRequired();
		builder.Property(x => x.HolidayName).IsRequired().HasMaxLength(256);
		builder.Property(x => x.Source).IsRequired().HasConversion<int>();
		builder.Property(x => x.IsIncluded).IsRequired();
		builder.Property(x => x.Notes).HasMaxLength(500);

		builder.HasIndex(x => new { x.TenantId, x.WorkCalendarPolicyId, x.Date, x.HolidayName })
			.HasDatabaseName("IX_HR_WorkCalendarHolidays_Policy_Date_Name");

		builder.HasIndex(x => new { x.TenantId, x.AcademicYearId, x.Date })
			.HasDatabaseName("IX_HR_WorkCalendarHolidays_AcademicYear_Date");

		builder.HasIndex(x => new { x.TenantId, x.AcademicYearId, x.FromDate, x.ToDate })
			.HasDatabaseName("IX_HR_WorkCalendarHolidays_AcademicYear_Range");
	}
}
