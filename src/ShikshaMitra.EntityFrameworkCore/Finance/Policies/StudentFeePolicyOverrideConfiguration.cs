using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra;
using ShikshaMitra.Domain.Finance.Policies;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.Finance.Policies;

public class StudentFeePolicyOverrideConfiguration : IEntityTypeConfiguration<StudentFeePolicyOverride>
{
	public void Configure(EntityTypeBuilder<StudentFeePolicyOverride> builder)
	{
		builder.ToTable("Finance_StudentFeePolicyOverrides", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();

		builder.Property(x => x.StudentId).IsRequired();
		builder.Property(x => x.FeeStructureId).IsRequired();
		builder.Property(x => x.WaiverPreApprovalReason).HasMaxLength(500);

		builder.HasIndex(x => new { x.TenantId, x.StudentId, x.FeeStructureId })
			.IsUnique()
			.HasDatabaseName("IX_Finance_StudentFeePolicyOverrides_Tenant_Student_FeeStructure");
	}
}
