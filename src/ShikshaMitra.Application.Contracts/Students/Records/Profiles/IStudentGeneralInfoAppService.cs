using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.Records.Profiles;

/// <summary>
/// Manages the general-information section of an existing student record.
/// </summary>
public interface IStudentGeneralInfoAppService : IApplicationService
{
    Task<StudentGeneralDetailsDto> GetGeneralDetailsByIdAsync(Guid studentId);

    Task<StudentGeneralDetailsDto> UpdateGeneralInfoAsync(Guid studentId, UpdateGeneralInfoDto input);

}
