using ShikshaMitra.Common;
using ShikshaMitra.TenantSettings.AcademicYears;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Domain.Services;
using Volo.Abp.Guids;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.HR.Employees;

public class EmployeeManager(
    IEmployeeRepository employeeRepository,
    IGuidGenerator guidGenerator,
    IIdentityUserRepository userRepository,
    IIdentityRoleRepository roleRepository) : DomainService
{
    // Private helper method to get an employee by ID
    private async Task<Employee> GetEmployeeAsync(Guid employeeId)
    {
        var employee = await employeeRepository.GetAsync(employeeId);
        if (employee == null)
        {
            throw new BusinessException($"Employee with ID {employeeId} does not exist.");
        }
        return employee;
    }

    // Private helper method to get a user by ID
    private async Task<IdentityUser> GetUserAsync(Guid userId)
    {
        var user = await userRepository.GetAsync(userId);
        if (user == null)
        {
            throw new BusinessException($"User with ID {userId} does not exist.");
        }
        return user;
    }

    // Private helper method to get a role by ID
    private async Task<IdentityRole> GetRoleAsync(Guid roleId)
    {
        var role = await roleRepository.GetAsync(roleId);
        if (role == null)
        {
            throw new BusinessException($"Role with ID {roleId} does not exist.");
        }
        return role;
    }

    public async Task<Employee> CreateEmployeeAsync(
        Guid userId,
        Guid roleId,
        string employeeCode,
        string firstName,
        string lastName,
        string? middleName,
        string shortName,
        EmployeeStatusType employeeStatusType,
        DateOnly? dateOfBirth,
        byte[]? profilePicture = null,
        string? familyCast = null,
        EmployeeCastType? castType = null,
        GenderType? gender = null,
        string? religion = null,
        MaritalStatusType? maritalStatus = null,
        string? bloodGroup = null,
        string? mobileNo = null,
        string? email = null,
        string? address = null,
        string? department = null,
        string? designation = null,
        DateOnly? dateOfJoining = null,
        string? employeeGroup = null,
        string? qualification = null,
        string? identificationMark1 = null,
        string? identificationMark2 = null,
        string? identificationMark3 = null)
    {
        // Validate user existence
        var user = await GetUserAsync(userId);

        // Validate role existence
        var role = await GetRoleAsync(roleId);

        // Create employee entity
        var employee = new Employee(
            id: GuidGenerator.Create(),
            tenantId: CurrentTenant.Id,
            userId: userId,
            profilePicture: profilePicture,
            employeeCode: employeeCode,
            lastName: lastName,
            firstName: firstName,
            middleName: middleName,
            shortName: shortName,
            employeeStatusType: employeeStatusType,
            dateOfBirth: dateOfBirth,
            familyCast: familyCast,
            castType: castType,
            genderType: gender,
            religion: religion,
            maritalStatus: maritalStatus,
            bloodGroup: bloodGroup,
            mobileNo: mobileNo,
            email: email,
            address: address,
            department: department,
            designation: designation,
            dateOfJoining: dateOfJoining,
            employeeGroup: employeeGroup,
            qualification: qualification,
            roleId: roleId,
            identificationMark1: identificationMark1,
            identificationMark2: identificationMark2,
            identificationMark3: identificationMark3,
            fatherDetails: null,
            motherDetails: null,
            spouseDetails: null,
            bankDetails: null,
            workExperiences: null
        );

        return employee;
    }

    public async Task<Employee> UpdateFamilyInfoByEmployeeIdAsync(Guid employeeId, ContactDetails fatherDetails,
        ContactDetails motherDetails, ContactDetails spouseDetails)
    {
        // Get the employee
        var employee = await GetEmployeeAsync(employeeId);

        // Update the family details
        employee.UpdateFamilyDetails(fatherDetails, motherDetails, spouseDetails);

        return employee;
    }

    public async Task<Employee> UpdateExperienceInfoByEmployeeIdAsync(Guid employeeId,
        List<WorkExperience> workExperiences)
    {
        // Get the employee
        var employee = await GetEmployeeAsync(employeeId);

        // Update the work experiences
        employee.UpdateWorkExperiences(workExperiences);

        return employee;
    }

    public async Task<Employee> UpdateBankDetailsByEmployeeIdAsync(Guid employeeId, BankDetails bankDetails)
    {
        // Get the employee
        var employee = await GetEmployeeAsync(employeeId);

        // Update the bank details
        employee.UpdateBankDetails(bankDetails);

        return employee;
    }
}
