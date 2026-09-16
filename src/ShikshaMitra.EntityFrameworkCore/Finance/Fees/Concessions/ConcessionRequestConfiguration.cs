using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Finance.Fees.ConcessionRequests;
using ShikshaMitra.Finance.Fees.Settings.ConcessionTypes;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.Finance.Fees.ConcessionRequests;

public class ConcessionRequestConfiguration : IEntityTypeConfiguration<ConcessionRequest>
{
	public void Configure(EntityTypeBuilder<ConcessionRequest> builder)
	{
		builder.ToTable("Finance_ConcessionRequests");
		builder.ConfigureByConvention();

		builder.Property(x => x.Remarks).HasMaxLength(2000);
		builder.Property(x => x.RejectionReason).HasMaxLength(2000);
		builder.Property(x => x.ApproverMessage).HasMaxLength(1000);
		builder.Property(x => x.CancellationReason).HasMaxLength(2000);
		builder.Property(x => x.SelectedDueFeeIdsJson).HasColumnType("text").IsRequired();
		builder.Property(x => x.RequestInputSnapshotJson).HasColumnType("text");
		builder.Property(x => x.TypeRequirementsSnapshotJson).HasColumnType("text");
		builder.Property(x => x.CreationIdempotencyKey).HasMaxLength(128);
		builder.Property(x => x.CreationPayloadHash).HasMaxLength(64).IsFixedLength();
		builder.Property(x => x.StudentFeeStructureId).IsRequired();
		builder.Property(x => x.ConcessionPolicyId).IsRequired();
		builder.Property(x => x.ConcessionPolicyVersionId).IsRequired();
		builder.Property(x => x.AcademicYearId).IsRequired();
		builder.Property(x => x.BoardId).IsRequired();
		builder.Property(x => x.EducationMediumId).IsRequired();
		builder.Property(x => x.StandardId).IsRequired();
		builder.Property(x => x.DivisionId).IsRequired();
		builder.HasIndex(x => new { x.AcademicYearId, x.StudentFeeStructureId, x.Status });
		builder.HasIndex(x => new { x.StudentId, x.ConcessionPolicyId, x.Status });
		builder.HasIndex(x => new { x.TenantId, x.StudentId, x.AcademicYearId, x.ConcessionTypeId })
			.IsUnique()
			.AreNullsDistinct(false)
			.HasFilter("\"IsDeleted\" = false AND \"Status\" = 0")
			.HasDatabaseName("UX_ConcessionRequest_PendingStudentYearType");

		builder.HasOne<ConcessionType>()
			.WithMany()
			.HasForeignKey(x => x.ConcessionTypeId)
			.OnDelete(DeleteBehavior.Restrict);

		builder.HasMany(x => x.ConcessionFeeHeadMappings)
			.WithOne()
			.HasForeignKey(x => x.ConcessionRequestId)
			.OnDelete(DeleteBehavior.Cascade);

		builder.HasMany(x => x.ConcessionRequestDocuments)
			.WithOne()
			.HasForeignKey(x => x.ConcessionRequestId)
			.OnDelete(DeleteBehavior.Cascade);

		builder.HasMany(x => x.ConcessionDueFeeAllocations)
			.WithOne()
			.HasForeignKey(x => x.ConcessionRequestId)
			.OnDelete(DeleteBehavior.Cascade);

		builder.HasMany(x => x.ConcessionReversals)
			.WithOne()
			.HasForeignKey(x => x.ConcessionRequestId)
			.OnDelete(DeleteBehavior.Restrict);

		builder.Navigation(x => x.ConcessionFeeHeadMappings).AutoInclude();
		builder.Navigation(x => x.ConcessionRequestDocuments).AutoInclude();
		builder.Navigation(x => x.ConcessionDueFeeAllocations).AutoInclude();
		builder.Navigation(x => x.ConcessionReversals).AutoInclude();
	}
}
