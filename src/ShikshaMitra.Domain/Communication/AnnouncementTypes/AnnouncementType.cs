using System;
using Volo.Abp.Domain.Entities.Auditing;

namespace ShikshaMitra.Communication.AnnouncementTypes
{
    public class AnnouncementType : FullAuditedAggregateRoot<Guid>
    {
        public Guid AcademicMediumId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Color { get; set; }

        protected AnnouncementType()
        {
        }

        public AnnouncementType(Guid id, Guid academicMediumId, string name, string description, string color)
            : base(id)
        {
            AcademicMediumId = academicMediumId;
            Name = name;
            Description = description;
            Color = color;
        }
    }
}
