using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Finance.Setting.FeeStructures;

namespace ShikshaMitra.Finance.Setting;

public class FeeStructureMetadataConfiguration : IEntityTypeConfiguration<FeeStructureMetadata>
{
	public void Configure(EntityTypeBuilder<FeeStructureMetadata> builder)
	{
		builder.ToTable("FeeStructureMetadatas", ShikshaMitraConsts.DbSchema);

		builder.HasOne(x => x.AcademicYear).WithMany().HasForeignKey(x => x.AcademicYearId).OnDelete(DeleteBehavior.Restrict);
		builder.HasOne(x => x.AcademicBoard).WithMany().HasForeignKey(x => x.AcademicBoardId).OnDelete(DeleteBehavior.Restrict);
		builder.HasOne(x => x.AcademicMedium).WithMany().HasForeignKey(x => x.AcademicMediumId).OnDelete(DeleteBehavior.Restrict);
		builder.HasOne(x => x.ReceiptBook).WithMany().HasForeignKey(x => x.ReceiptBookId).OnDelete(DeleteBehavior.Cascade);

		builder.Navigation(x => x.AcademicYear).AutoInclude();
		builder.Navigation(x => x.AcademicBoard).AutoInclude();
		builder.Navigation(x => x.AcademicMedium).AutoInclude();
		builder.Navigation(x => x.ReceiptBook).AutoInclude();
	}
}
