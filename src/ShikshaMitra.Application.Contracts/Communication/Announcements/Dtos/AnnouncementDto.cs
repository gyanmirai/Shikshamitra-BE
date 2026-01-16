using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;
using ShikshaMitra.Communication.Announcements;

namespace ShikshaMitra.Communication.Announcements.Dtos
{
    public class AnnouncementDto : FullAuditedEntityDto<Guid>
    {
        public string Title { get; set; }
        public string Description { get; set; }
        
        public LookupDto Type { get; set; }
        
        public global::ShikshaMitra.ImportanceLevel ImportanceLevel { get; set; }
        public global::ShikshaMitra.AnnouncementStatus Status { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public string Duration { get; set; }
        
        public List<LookupDto> Audience { get; set; }
        public List<string> NotificationPreferences { get; set; }
        
        public List<string> Documents { get; set; }
        public AnnouncedByDto AnnouncedBy { get; set; }
        public bool IsVisible { get; set; }
    }

    public class LookupDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string Color { get; set; } // Optional, based on contract
    }

    public class AnnouncedByDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
    }
}
