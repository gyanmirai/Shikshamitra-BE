using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Academic.StudentAcademic360;

public interface IStudentAcademic360AppService : IApplicationService
{
    Task<StudentAcademic360Dto> GetAsync(Guid studentId, StudentAcademic360Input input);
}
