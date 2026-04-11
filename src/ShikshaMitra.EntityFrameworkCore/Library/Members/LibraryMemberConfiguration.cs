using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.Library.Members;

public class LibraryMemberConfiguration : IEntityTypeConfiguration<LibraryMember>
{
    public void Configure(EntityTypeBuilder<LibraryMember> builder)
    {
        builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "LibraryMembers", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();

	}
}
