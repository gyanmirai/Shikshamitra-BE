using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Students;
using ShikshaMitra.Students.Portal;
using Volo.Abp.EntityFrameworkCore.Modeling;
using Volo.Abp.Identity;

namespace ShikshaMitra.EntityFrameworkCore.Students.Portal;

public class StudentPortalAccountLinkConfiguration : IEntityTypeConfiguration<StudentPortalAccountLink>
{
	public void Configure(EntityTypeBuilder<StudentPortalAccountLink> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "StudentPortalAccountLinks", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
		builder.Property(x => x.AccountType).IsRequired().HasMaxLength(16);
		builder.Property(x => x.Relationship).IsRequired().HasMaxLength(16);
		builder.HasIndex(x => new { x.TenantId, x.StudentId, x.AccountType }).IsUnique().HasFilter("\"IsDeleted\" = false");
		builder.HasIndex(x => new { x.TenantId, x.UserId, x.AccountType });
		builder.HasOne<Student>().WithMany().HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Restrict);
		builder.HasOne<IdentityUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
	}
}
