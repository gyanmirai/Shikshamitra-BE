using System;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Communication.AnnouncementTypes.Dtos
{
    public class AnnouncementTypeDto : EntityDto<Guid>
    {
        public string AnnouncementType { get; set; }
        public string Description { get; set; }
        public string Color { get; set; }
        public Guid AcademicMediumId { get; set; }
    }
}
