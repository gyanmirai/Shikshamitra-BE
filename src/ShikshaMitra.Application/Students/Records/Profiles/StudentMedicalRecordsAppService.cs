using ShikshaMitra.Students.Records.Profiles;
using System;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.Records.Profiles;

public class StudentMedicalRecordsAppService(
    IStudentRepository studentRepository,
    IRepository<StudentMedicalRecord, Guid> studentMedicalRecordRepository,
    EntityValidationService validationService,
    StudentScopeAccessService studentScopeAccessService
) : StudentProfileBaseAppService(studentRepository, studentScopeAccessService),
    IStudentMedicalRecordsAppService
{
    public async Task<StudentMedicalRecordsDto> GetMedicalRecordsAsync(Guid studentId)
    {
        var student = await GetScopedStudentAsync(studentId, nameof(GetMedicalRecordsAsync));

        var medicalRecord = await studentMedicalRecordRepository.FirstOrDefaultAsync(x => x.Id == studentId);

        return new StudentMedicalRecordsDto
        {
            StudentId = student.Id,
            Status = student.Status,
            WeightKg = medicalRecord?.WeightKg,
            HeightCm = medicalRecord?.HeightCm,
            BodyMassIndex = medicalRecord?.BodyMassIndex,
            PulseRate = medicalRecord?.PulseRate,
            Hemoglobin = medicalRecord?.Hemoglobin,
            Allergies = medicalRecord?.Allergies,
            ChildImmunization = medicalRecord?.ChildImmunization,
            CovidVaccinationDoses = medicalRecord?.CovidVaccinationDoses,
            ImmunizationRemarks = medicalRecord?.ImmunizationRemarks,
            BloodGroup = medicalRecord?.BloodGroup,
            ChronicConditions = medicalRecord?.ChronicConditions,
            CurrentMedications = medicalRecord?.CurrentMedications,
            HaveDisability = student.AdditionalDetail?.HaveDisability ?? false,
            DisabilityDetails = student.AdditionalDetail?.DisabilityDetails
        };
    }

    public async Task<StudentMedicalRecordsDto> UpdateMedicalRecordsAsync(Guid studentId, UpdateMedicalRecordsDto input)
    {
        var student = await GetScopedStudentAsync(studentId, nameof(UpdateMedicalRecordsAsync));

        var medicalRecord = await studentMedicalRecordRepository.FirstOrDefaultAsync(x => x.Id == studentId);

        if (medicalRecord == null)
        {
            medicalRecord = new StudentMedicalRecord(studentId, CurrentTenant.Id);
            await studentMedicalRecordRepository.InsertAsync(medicalRecord, autoSave: false);
        }

        medicalRecord.UpdateMedicalRecords(
            input.WeightKg,
            input.HeightCm,
            input.BodyMassIndex,
            input.PulseRate,
            input.Hemoglobin,
            input.Allergies,
            input.ChildImmunization,
            input.CovidVaccinationDoses,
            input.ImmunizationRemarks,
            input.BloodGroup,
            input.ChronicConditions,
            input.CurrentMedications
        );

        student.AdditionalDetail?.AdvanceRegistrationState(StudentRegistrationStateEnum.MedicalRecords);
        student.AdditionalDetail?.UpdateDisabilityInfo(input.HaveDisability, input.DisabilityDetails);

        await UnitOfWorkManager.Current!.SaveChangesAsync();

        return await GetMedicalRecordsAsync(studentId);
    }
}
