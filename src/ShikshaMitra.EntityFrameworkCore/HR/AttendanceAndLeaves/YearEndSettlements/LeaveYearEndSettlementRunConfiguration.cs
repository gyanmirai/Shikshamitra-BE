using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.HR.AttendanceAndLeaves.YearEndSettlements;

public class LeaveYearEndSettlementRunConfiguration : IEntityTypeConfiguration<LeaveYearEndSettlementRun>
{
	public void Configure(EntityTypeBuilder<LeaveYearEndSettlementRun> builder)
	{
		builder.ToTable("HR_LeaveYearEndSettlementRuns");
		builder.ConfigureByConvention();
		builder.HasKey(x => x.Id);

		builder.Property(x => x.Status).IsRequired();
		builder.Property(x => x.PreviewedAt).IsRequired();
		builder.Property(x => x.TotalCarriedDays).HasPrecision(18, 2);
		builder.Property(x => x.TotalEncashedDays).HasPrecision(18, 2);
		builder.Property(x => x.TotalForfeitedDays).HasPrecision(18, 2);
		builder.Property(x => x.TotalEncashmentAmount).HasPrecision(18, 2);

		builder.HasIndex(x => new { x.TenantId, x.AcademicYearId })
			.IsUnique()
			.HasDatabaseName("IX_HR_LeaveYearEndRuns_Tenant_SourceYear");

		builder.HasIndex(x => new { x.TenantId, x.Status })
			.HasDatabaseName("IX_HR_LeaveYearEndRuns_Tenant_Status");
	}
}
