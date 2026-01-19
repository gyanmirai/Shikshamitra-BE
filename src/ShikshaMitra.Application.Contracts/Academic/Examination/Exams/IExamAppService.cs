using System.Threading.Tasks;
using ShikshaMitra.Academic.Examination.Exams.Dtos;
using ShikshaMitra.Common;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Academic.Examination.Exams;

public interface IExamAppService : IApplicationService
{
    Task CreateBasicDetailsAsync(ExamBasicDetailsRequestDto input);
}
