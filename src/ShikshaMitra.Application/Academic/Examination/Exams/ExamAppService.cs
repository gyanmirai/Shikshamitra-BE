using System.Threading.Tasks;
using ShikshaMitra.Academic.Examination.Exams.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using System;
using Volo.Abp;

namespace ShikshaMitra.Academic.Examination.Exams;

[RemoteService(false)]
public class ExamAppService(
	IRepository<Exam, Guid> examRepository,
	ExamManager examManager) : ApplicationService, IExamAppService
{
    public async Task CreateBasicDetailsAsync(ExamBasicDetailsRequestDto input)
    {
        var exam = examManager.Create(
            input.ExamTypeId,
            input.ExamName,
            input.StandardId,
            input.DivisionIds,
            input.SubjectIds
        );

		await examRepository.InsertAsync(exam);
	}
}
