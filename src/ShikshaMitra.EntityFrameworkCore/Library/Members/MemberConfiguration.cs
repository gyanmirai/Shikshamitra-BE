using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ShikshaMitra.Library.Members;

public class MemberConfiguration : IEntityTypeConfiguration<Member>
{
	public void Configure(EntityTypeBuilder<Member> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "Members", ShikshaMitraConsts.DbSchema);

		builder.HasKey(m => m.Id);

		builder.Property(m => m.TenantId)
			.IsRequired(false);

		builder.Property(m => m.Name)
			.IsRequired()
			.HasMaxLength(256);

		builder.Property(m => m.Role)
		   .HasConversion<string>() 
		   .IsRequired()
		   .HasMaxLength(100);
	}
}
