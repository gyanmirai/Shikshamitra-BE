using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.HR.Payroll.Compensation;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.HR.Payroll.Compensation;

public class EmployeeCompensationRevisionConfiguration : IEntityTypeConfiguration<EmployeeCompensationRevision>
{
	public void Configure(EntityTypeBuilder<EmployeeCompensationRevision> builder)
	{
		builder.ToTable("HR_EmployeeCompensationRevisions");
		builder.ConfigureByConvention();
		builder.Property(x => x.QuotedMonthlyAmount).HasPrecision(18, 2);
		builder.Property(x => x.ProtectedMonthlyAmount).HasPrecision(18, 2);
		builder.Property(x => x.GrossSalary).HasPrecision(18, 2);
		builder.Property(x => x.NetSalary).HasPrecision(18, 2);
		builder.Property(x => x.EmployerCost).HasPrecision(18, 2);
		builder.Property(x => x.StatutoryProfile).IsRequired().HasMaxLength(16);
		builder.Property(x => x.CalculationSnapshotJson).IsRequired().HasColumnType("jsonb");
		builder.Property(x => x.RejectionReason).HasMaxLength(500);
		builder.HasIndex(x => new { x.TenantId, x.EmployeeId, x.RevisionNumber }).IsUnique().HasFilter("\"IsDeleted\" = false");
		builder.HasIndex(x => new { x.TenantId, x.EmployeeId, x.EffectiveMonth, x.Status });
	}
}

public class EmployeeCompensationRevisionLineConfiguration : IEntityTypeConfiguration<EmployeeCompensationRevisionLine>
{
	public void Configure(EntityTypeBuilder<EmployeeCompensationRevisionLine> builder)
	{
		builder.ToTable("HR_EmployeeCompensationRevisionLines");
		builder.ConfigureByConvention();
		builder.Property(x => x.Name).IsRequired().HasMaxLength(100);
		builder.Property(x => x.Description).HasMaxLength(255);
		builder.Property(x => x.Amount).HasPrecision(18, 2);
		builder.HasOne<EmployeeCompensationRevision>()
			.WithMany()
			.HasForeignKey(x => x.RevisionId)
			.OnDelete(DeleteBehavior.Cascade);
		builder.HasIndex(x => new { x.TenantId, x.RevisionId, x.DisplayOrder });
		builder.HasIndex(x => new { x.TenantId, x.EmployeeId });
	}
}
