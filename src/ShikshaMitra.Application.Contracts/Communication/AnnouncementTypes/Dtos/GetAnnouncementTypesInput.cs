using System;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Communication.AnnouncementTypes.Dtos
{
    public class GetAnnouncementTypesInput : PagedAndSortedResultRequestDto
    {
        public Guid AcademicMediumId { get; set; }
        public int? Page { get; set; }
        public int? PageSize { get; set; }
    }
}
