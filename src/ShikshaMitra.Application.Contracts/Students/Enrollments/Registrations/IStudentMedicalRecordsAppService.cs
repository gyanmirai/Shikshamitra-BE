using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.Enrollments.Registrations;

/// <summary>
/// Manages the Medical Records section of the student registration form.
/// Covers physical measurements, immunisation records, allergies, and chronic conditions.
/// </summary>
public interface IStudentMedicalRecordsAppService : IApplicationService
{
    Task<StudentMedicalRecordsDto> GetMedicalRecordsAsync(Guid studentId);

    Task<StudentMedicalRecordsDto> UpdateMedicalRecordsAsync(Guid studentId, UpdateMedicalRecordsDto input);
}
