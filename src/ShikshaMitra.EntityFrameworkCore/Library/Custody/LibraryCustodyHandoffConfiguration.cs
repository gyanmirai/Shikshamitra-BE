using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.Library.Custody;

public sealed class LibraryCustodyHandoffConfiguration : IEntityTypeConfiguration<LibraryCustodyHandoff>
{
	public void Configure(EntityTypeBuilder<LibraryCustodyHandoff> builder)
	{
		builder.ToTable("AppLibraryCustodyHandoffs", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
		builder.Property(row => row.TenantId).IsRequired();
		builder.Property(row => row.Custodian).HasMaxLength(512).IsRequired();
		builder.Property(row => row.ClassName).HasMaxLength(256);
		builder.Property(row => row.RoomName).HasMaxLength(256);
		builder.Property(row => row.Purpose).HasMaxLength(500).IsRequired();
		builder.Property(row => row.IdempotencyKey).HasMaxLength(100).IsRequired();
		builder.Property(row => row.PayloadFingerprint).HasMaxLength(64).IsRequired();
		builder.HasIndex(row => new { row.TenantId, row.ActorId, row.IdempotencyKey }).IsUnique();
		builder.HasIndex(row => new { row.TenantId, row.RecordedAt, row.Id });
		foreach (var property in builder.Metadata.GetProperties()) property.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
	}
}
