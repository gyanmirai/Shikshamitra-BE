using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Onboarding.EducationBoards;

public class EducationBoardStageDto : AuditedEntityDto<Guid>
{
    public Guid EducationBoardId { get; set; }
    public string Name { get; set; }
}
