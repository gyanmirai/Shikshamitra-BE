using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using ShikshaMitra.Communication.Announcements;

namespace ShikshaMitra.Communication.Announcements.Dtos
{
    public class AnnouncementCreateDto
    {
        [Required]
        public string Title { get; set; }

        public string Description { get; set; }

        [Required]
        public List<Guid> Audience { get; set; }

        [Required]
        public Guid Type { get; set; }

        [Required]
        public global::ShikshaMitra.ImportanceLevel ImportanceLevel { get; set; }

        [Required]
        public DateTime StartDate { get; set; }

        public DateTime? EndDate { get; set; }

        public int Duration { get; set; }
        public global::ShikshaMitra.DurationUnit DurationUnit { get; set; }

        public List<string>? Documents { get; set; }

        [Required]
        public List<global::ShikshaMitra.NotificationPreference> NotificationPreferences { get; set; }
    }
}
