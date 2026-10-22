using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.Academic.Mobile;

public class MobileSubmissionOperationConfiguration : IEntityTypeConfiguration<MobileSubmissionOperation>
{
	public void Configure(EntityTypeBuilder<MobileSubmissionOperation> b)
	{
		b.ToTable(ShikshaMitraConsts.AcademicDbTablePrefix + "MobileSubmissionOperations", ShikshaMitraConsts.DbSchema);
		b.ConfigureByConvention();
		b.Property(x => x.Operation).HasMaxLength(16).IsRequired();
		b.Property(x => x.PayloadHash).HasMaxLength(64).IsRequired();
		b.Property(x => x.ResponseJson).IsRequired();
		b.HasIndex(x => new { x.TenantId, x.ActorId, x.StudentId, x.AcademicYearId, x.TaskId, x.Operation, x.RequestId }).IsUnique();
	}
}

public class MobileSubmissionSnapshotConfiguration : IEntityTypeConfiguration<MobileSubmissionSnapshot>
{
	public void Configure(EntityTypeBuilder<MobileSubmissionSnapshot> b)
	{
		b.ToTable(ShikshaMitraConsts.AcademicDbTablePrefix + "MobileSubmissionSnapshots", ShikshaMitraConsts.DbSchema);
		b.ConfigureByConvention();
		b.Property(x => x.Revision).HasMaxLength(40).IsRequired();
		b.HasIndex(x => new { x.SubmissionId, x.Revision }).IsUnique();
		b.HasIndex(x => new { x.TenantId, x.StudentId, x.TaskId });
	}
}
