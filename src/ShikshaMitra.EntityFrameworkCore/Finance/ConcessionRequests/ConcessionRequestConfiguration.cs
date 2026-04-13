using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.Finance.ConcessionRequests;

public class ConcessionRequestConfiguration : IEntityTypeConfiguration<ConcessionRequest>
{
    public void Configure(EntityTypeBuilder<ConcessionRequest> builder)
    {
        builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "ConcessionRequests", ShikshaMitraConsts.DbSchema);
        builder.ConfigureByConvention();

        builder.Property(x => x.StudentId)
            .IsRequired();

        builder.Property(x => x.ConcessionTypeId)
            .IsRequired();

        builder.Property(x => x.Status)
            .IsRequired();

        builder.Property(x => x.RejectionReason)
            .HasMaxLength(2000);

        builder.Property(x => x.Remarks)
            .HasMaxLength(2000);

        // One-to-many: ConcessionRequest → ConcessionFeeHeadMappings
        builder.HasMany(x => x.ConcessionFeeHeadMappings)
            .WithOne()
            .HasForeignKey(m => m.ConcessionRequestId)
            .OnDelete(DeleteBehavior.Cascade);

        // One-to-many: ConcessionRequest → ConcessionRequestDocuments
        builder.HasMany(x => x.ConcessionRequestDocuments)
            .WithOne()
            .HasForeignKey(d => d.ConcessionRequestId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes
        builder.HasIndex(x => x.StudentId)
            .HasDatabaseName("IX_ConcessionRequests_StudentId");

        builder.HasIndex(x => x.ConcessionTypeId)
            .HasDatabaseName("IX_ConcessionRequests_ConcessionTypeId");

        builder.HasIndex(x => new { x.StudentId, x.Status })
            .HasDatabaseName("IX_ConcessionRequests_StudentId_Status");
    }
}

