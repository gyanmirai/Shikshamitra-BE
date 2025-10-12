using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Academic.Curriculum.Setting;

public interface ISubjectAppService : IApplicationService
{
	Task<SubjectDto> CreateSubjectAsync(CreateSubjectDto createSubjectDto);
	Task<SubjectDto> UpdateSubjectAsync(Guid id, UpdateSubjectDto updateSubjectDto);
    Task<bool> DeleteSubjectAsync(Guid id);
	Task<List<SubjectDto>> GetListAsync(Guid educationMediumId);
}
