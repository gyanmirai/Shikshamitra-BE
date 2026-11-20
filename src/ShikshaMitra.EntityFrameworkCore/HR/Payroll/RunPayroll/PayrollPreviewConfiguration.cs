using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.HR.Payroll.RunPayroll;

public class PayrollPreviewRunConfiguration : IEntityTypeConfiguration<PayrollPreviewRun>
{
	public void Configure(EntityTypeBuilder<PayrollPreviewRun> b)
	{
		b.ToTable("HR_PayrollPreviewRuns"); b.ConfigureByConvention();
		b.Ignore(x => x.IsTerminal);
		b.Property(x => x.Month).HasMaxLength(7).IsRequired();
		b.Property(x => x.ScopeFingerprint).HasMaxLength(64).IsRequired();
		b.Property(x => x.Status).HasMaxLength(16).IsRequired();
		b.Property(x => x.ErrorCode).HasMaxLength(128);
		b.HasIndex(x => new { x.TenantId, x.Month, x.AcademicYearId, x.ScopeFingerprint, x.CreatedAtUtc });
	}
}

public class PayrollPreviewEmployeeConfiguration : IEntityTypeConfiguration<PayrollPreviewEmployee>
{
	public void Configure(EntityTypeBuilder<PayrollPreviewEmployee> b)
	{
		b.ToTable("HR_PayrollPreviewEmployees"); b.ConfigureByConvention();
		b.Property(x => x.EmployeeName).HasMaxLength(256).IsRequired();
		b.Property(x => x.EmployeeCode).HasMaxLength(64).IsRequired();
		b.Property(x => x.ItemJson).IsRequired();
		b.Property(x => x.PayableDays).HasPrecision(18, 2);
		b.Property(x => x.NetPay).HasPrecision(18, 2);
		b.Property(x => x.NetPayDelta).HasPrecision(18, 2);
		b.Property(x => x.RegisterIssueCodes).IsRequired().HasDefaultValueSql("ARRAY[]::text[]");
		b.HasIndex(x => new { x.RunId, x.EmployeeId }).IsUnique();
		b.HasIndex(x => new { x.RunId, x.EmployeeName, x.EmployeeId });
		b.HasIndex(x => new { x.RunId, x.PayableDays, x.EmployeeId });
		b.HasIndex(x => new { x.RunId, x.NetPay, x.EmployeeId });
		b.HasIndex(x => new { x.RunId, x.NetPayDelta, x.EmployeeId });
		b.HasOne<PayrollPreviewRun>().WithMany().HasForeignKey(x => x.RunId).OnDelete(DeleteBehavior.Cascade);
	}
}

public class PayrollInputRevisionConfiguration : IEntityTypeConfiguration<PayrollInputRevision>
{
	public void Configure(EntityTypeBuilder<PayrollInputRevision> b)
	{
		b.ToTable("HR_PayrollInputRevisions"); b.ConfigureByConvention();
	}
}
