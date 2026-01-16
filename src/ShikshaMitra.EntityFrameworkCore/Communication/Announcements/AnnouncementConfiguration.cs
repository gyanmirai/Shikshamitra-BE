using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Communication.Announcements;
using System;
using System.Collections.Generic;
using System.Text.Json;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.Communication.Announcements
{
    public class AnnouncementConfiguration : IEntityTypeConfiguration<Announcement>
    {
        public void Configure(EntityTypeBuilder<Announcement> builder)
        {
            builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "Announcements", ShikshaMitraConsts.DbSchema);
            builder.ConfigureByConvention();

            builder.Property(x => x.Title).IsRequired().HasMaxLength(256);
            builder.Property(x => x.Description).IsRequired(); // Assuming huge text
            builder.Property(x => x.AcademicMediumId).IsRequired();
            builder.Property(x => x.TypeId).IsRequired();
            
            // Value Conversions for Lists
            builder.Property(x => x.Documents)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions)null),
                    v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions)null)
                );

            builder.Property(x => x.Audience)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions)null),
                    v => JsonSerializer.Deserialize<List<Guid>>(v, (JsonSerializerOptions)null)
                );

            builder.Property(x => x.NotificationPreferences)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions)null),
                    v => JsonSerializer.Deserialize<List<global::ShikshaMitra.NotificationPreference>>(v, (JsonSerializerOptions)null)
                );
        }
    }
}
