using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.Communication.AnnouncementTypes
{
    public class AnnouncementTypeConfiguration : IEntityTypeConfiguration<AnnouncementType>
    {
        public void Configure(EntityTypeBuilder<AnnouncementType> builder)
        {
            builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "AnnouncementTypes", ShikshaMitraConsts.DbSchema);
            builder.ConfigureByConvention(); 

            builder.Property(x => x.Name).IsRequired().HasMaxLength(256);
            builder.Property(x => x.Description).HasMaxLength(1000); 
            builder.Property(x => x.Color).IsRequired().HasMaxLength(32); 
            
            builder.HasIndex(x => x.Name);
        }
    }
}
