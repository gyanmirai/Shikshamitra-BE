using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.HR.AttendanceAndLeaves.Settings;

public class AttendancePolicyEvaluationResultConfiguration : IEntityTypeConfiguration<AttendancePolicyEvaluationResult>
{
	public void Configure(EntityTypeBuilder<AttendancePolicyEvaluationResult> builder)
	{
		builder.ToTable("HR_AttendancePolicyEvaluationResults");
		builder.ConfigureByConvention();
		builder.HasKey(x => x.Id);

		builder.Property(x => x.BaselinePayableUnits).HasPrecision(18, 2);
		builder.Property(x => x.EvaluatedPayableUnits).HasPrecision(18, 2);
		builder.Property(x => x.WarningsJson).HasColumnType("jsonb");

		builder.HasIndex(x => new { x.TenantId, x.AttendancePolicyEvaluationRunId, x.EmployeeId, x.AttendanceDate })
			.HasDatabaseName("IX_AttendancePolicyEvaluationResults_Run_Employee_Date");
	}
}
