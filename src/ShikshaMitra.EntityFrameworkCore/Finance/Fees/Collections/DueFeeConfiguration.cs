using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra;
using ShikshaMitra.Finance.Fees.FeeCollection;
using Volo.Abp.EntityFrameworkCore.Modeling;

public class DueFeeConfiguration : IEntityTypeConfiguration<DueFee>
{
	public void Configure(EntityTypeBuilder<DueFee> builder)
	{
		builder.ToTable("Finance_DueFees", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();

		// Finance-critical: prevents duplicate due fee records at the database level
		// regardless of application-layer bugs or concurrent requests.
		// HasFilter scopes the uniqueness to non-deleted rows so that soft-deleted
		// DueFees do not block new inserts (and the seeder's ExistsForStudentAsync
		// check, which uses ABP's soft-delete filter, cannot see them).
		builder.HasIndex(x => new { x.TenantId, x.StudentId, x.FeeHeadId, x.Month, x.Year })
			.IsUnique()
			.HasFilter("\"IsDeleted\" = false")
			.HasDatabaseName("IX_DueFees_TenantId_StudentId_FeeHeadId_Month_Year");
	}
}
