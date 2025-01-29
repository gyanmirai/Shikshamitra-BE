using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.Students.StudentEnrollments.FollowUps;

public class FollowUpMessageConfiguration : IEntityTypeConfiguration<FollowUpMessage>
{
	public void Configure(EntityTypeBuilder<FollowUpMessage> builder)
	{
		builder.ToTable(
			ShikshaMitraConsts.DbTablePrefix + "FollowUpMessages",
			ShikshaMitraConsts.DbSchema
		);

		builder.ConfigureByConvention();

		builder.Property(x => x.Message)
			   .IsRequired()
			   .HasMaxLength(1000);

	}
}
