using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public interface IAdmissionIntakeProfileAppService : IApplicationService
{
	Task<List<AdmissionIntakeProfileDto>> GetListAsync(AdmissionIntakeProfileKind? kind = null);
	Task<List<AdmissionInquiryCounsellorDto>> GetCounsellorsAsync(string? filter = null, bool counsellorOnly = false);
	Task<AdmissionIntakeProfileDto> CreateAsync(SaveAdmissionIntakeProfileDto input);
	Task<AdmissionIntakeProfileDto> UpdateAsync(Guid id, SaveAdmissionIntakeProfileDto input);
	Task<AdmissionIntakeProfileDto> UpdateManagedAsync(Guid setupId, Guid id, SaveAdmissionIntakeProfileDto input);
}
