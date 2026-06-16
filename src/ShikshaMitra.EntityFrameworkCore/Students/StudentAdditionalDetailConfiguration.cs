using Microsoft.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.Students;
public class StudentAdditionalDetailConfiguration : IEntityTypeConfiguration<StudentAdditionalDetail>
{
	public void Configure(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<StudentAdditionalDetail> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "StudentAdditionalDetails", ShikshaMitraConsts.DbSchema);
		// Apply default configurations based on ABP conventions.
		builder.ConfigureByConvention();

		builder.Property(x => x.DisabilityDetails).HasMaxLength(500);

		// add navigation properties for inquiry preference
		builder.HasOne(x => x.InquiryPreference)
			.WithMany()
			.HasForeignKey(x => x.InquiryPreferenceId)
			.OnDelete(DeleteBehavior.NoAction);

		builder.Navigation(x => x.InquiryPreference).AutoInclude();
	}
}
