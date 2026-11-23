using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Students;
using ShikshaMitra.Students.Portal;
using Volo.Abp.EntityFrameworkCore.Modeling;
using Volo.Abp.Identity;

namespace ShikshaMitra.EntityFrameworkCore.Students.Portal;

public class StudentAppAccessRestrictionConfiguration : IEntityTypeConfiguration<StudentAppAccessRestriction>
{
	public void Configure(EntityTypeBuilder<StudentAppAccessRestriction> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "StudentAppAccessRestrictions", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
		builder.Property(x => x.TenantId).IsRequired();
		builder.HasIndex(x => new { x.TenantId, x.StudentId }).IsUnique().HasFilter("\"UserId\" IS NULL");
		builder.HasIndex(x => new { x.TenantId, x.StudentId, x.UserId }).IsUnique().HasFilter("\"UserId\" IS NOT NULL");
		builder.HasIndex(x => new { x.TenantId, x.UserId });
		builder.HasOne<Student>().WithMany().HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Restrict);
		builder.HasOne<IdentityUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
	}
}

public class StudentAppAccessEventConfiguration : IEntityTypeConfiguration<StudentAppAccessEvent>
{
	public void Configure(EntityTypeBuilder<StudentAppAccessEvent> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "StudentAppAccessEvents", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
		builder.Property(x => x.TenantId).IsRequired();
		builder.Property(x => x.Relationship).IsRequired().HasMaxLength(16);
		builder.Property(x => x.DisplayName).HasMaxLength(512);
		builder.Property(x => x.Reason).IsRequired().HasMaxLength(400);
		builder.Property(x => x.ChangedByName).HasMaxLength(256);
		builder.HasIndex(x => new { x.TenantId, x.StudentId, x.ChangedAt });
		builder.HasOne<Student>().WithMany().HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Restrict);
	}
}
