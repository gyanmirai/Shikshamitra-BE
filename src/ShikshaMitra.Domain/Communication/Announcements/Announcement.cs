using System;
using System.Collections.Generic;
using Volo.Abp.Domain.Entities.Auditing;

namespace ShikshaMitra.Communication.Announcements
{
    public class Announcement : FullAuditedAggregateRoot<Guid>
    {
        public string Title { get; set; }
        public string Description { get; set; }
        public Guid AcademicMediumId { get; set; }
        public Guid TypeId { get; set; }
        public global::ShikshaMitra.ImportanceLevel ImportanceLevel { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public int Duration { get; set; }
        public global::ShikshaMitra.DurationUnit DurationUnit { get; set; }
        public List<string> Documents { get; set; }
        public List<Guid> Audience { get; set; }
        public List<global::ShikshaMitra.NotificationPreference> NotificationPreferences { get; set; }
        public global::ShikshaMitra.AnnouncementStatus Status { get; set; }
        public bool IsVisible { get; set; }

        protected Announcement()
        {
        }

        public Announcement(
            Guid id,
            string title,
            string description,
            Guid academicMediumId,
            Guid typeId,
            ImportanceLevel importanceLevel,
            DateTime startDate,
            DateTime? endDate,
            int duration,
            DurationUnit durationUnit,
            List<string> documents,
            List<Guid> audience,
            List<NotificationPreference> notificationPreferences,
            AnnouncementStatus status,
            bool isVisible)
            : base(id)
        {
            Title = title;
            Description = description;
            AcademicMediumId = academicMediumId;
            TypeId = typeId;
            ImportanceLevel = importanceLevel;
            StartDate = startDate;
            EndDate = endDate;
            Duration = duration;
            DurationUnit = durationUnit;
            Documents = documents ?? new List<string>();
            Audience = audience ?? new List<Guid>();
            NotificationPreferences = notificationPreferences ?? new List<NotificationPreference>();
            Status = status;
            IsVisible = isVisible;
        }
    }
}
