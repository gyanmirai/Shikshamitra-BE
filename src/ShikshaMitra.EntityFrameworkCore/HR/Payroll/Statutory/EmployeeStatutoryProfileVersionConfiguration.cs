using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.HR.Payroll.Statutory;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.HR.Payroll.Statutory;

public class EmployeeStatutoryProfileVersionConfiguration : IEntityTypeConfiguration<EmployeeStatutoryProfileVersion>
{
	public void Configure(EntityTypeBuilder<EmployeeStatutoryProfileVersion> builder)
	{
		builder.ToTable("HR_EmployeeStatutoryProfileVersions");
		builder.ConfigureByConvention();
		builder.Property(x => x.PfNumber).HasMaxLength(50);
		builder.Property(x => x.UanNumber).HasMaxLength(50);
		builder.Property(x => x.EsicInsuranceNumber).HasMaxLength(100);
		builder.Property(x => x.PfDisabledReason).HasMaxLength(500);
		builder.Property(x => x.EsicDisabledReason).HasMaxLength(500);
		builder.Property(x => x.RequestFingerprint).HasMaxLength(64).IsRequired();
		builder.HasIndex(x => new { x.TenantId, x.EmployeeId, x.EffectiveMonth }).IsUnique()
			.HasFilter("\"IsDeleted\" = false AND \"IsSuperseded\" = false").AreNullsDistinct(false);
		builder.HasIndex(x => new { x.TenantId, x.EmployeeId, x.RequestId }).IsUnique().AreNullsDistinct(false);
	}
}
