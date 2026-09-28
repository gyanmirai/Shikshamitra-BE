using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public interface IAdmissionIntakeProfileAppService : IApplicationService
{
	Task<List<AdmissionIntakeProfileDto>> GetListAsync(AdmissionIntakeProfileKind? kind = null);
	Task<List<AdmissionInquiryCounsellorDto>> GetCounsellorsAsync(string? filter = null);
	Task<AdmissionIntakeProfileDto> CreateAsync(SaveAdmissionIntakeProfileDto input);
	Task<AdmissionIntakeProfileDto> UpdateAsync(Guid id, SaveAdmissionIntakeProfileDto input);
}
