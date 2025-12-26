using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.Records;

public interface IStudentManagementAppService : IApplicationService
{
	Task<List<StudentRecordDto>> GetStudentRecords(Guid divisionId);
}
