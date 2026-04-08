using ShikshaMitra.Students.Enrollments.Registrations;
using ShikshaMitra.Students.ValueObjects;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.Students.StudentEnrollments.Helpers;

/// <summary>
/// Transient helper that converts between address/parent-guardian domain value objects and
/// their corresponding DTOs. Centralises the mapping logic so it is shared by
/// <see cref="Registration.StudentFamilyInfoAppService"/> and
/// <see cref="Registration.StudentCommunicationInfoAppService"/> without duplication.
/// </summary>
public class StudentRegistrationMappingHelper : ITransientDependency
{
    public ParentGuardianDto MapParentGuardianDetailsToDto(ParentGuardianDetails details)
    {
        return new ParentGuardianDto
        {
            FullName = details.FullName,
            DateOfBirth = details.DateOfBirth,
            Occupation = details.Occupation,
            Email = details.Email,
            ContactNo = details.ContactNo,
            OfficeNo = details.OfficeNo,
            Qualification = details.Qualification,
            YearOfPassing = details.YearOfPassing,
            Designation = details.Designation,
            Organization = details.Organization,
            AadharNo = details.AadharNo,
            OfficeAddress = details.OfficeAddress != null ? MapAddressDetailsToDto(details.OfficeAddress) : null,
            LocalAddress = details.LocalAddress != null ? MapAddressDetailsToDto(details.LocalAddress) : null
        };
    }

    public AddressDto MapAddressDetailsToDto(AddressDetails address)
    {
        return new AddressDto
        {
            Address1 = address.Address1,
            Address2 = address.Address2,
            Address3 = address.Address3,
            State = address.State,
            City = address.City,
            Pincode = address.Pincode,
            Country = address.Country
        };
    }

    public ParentGuardianDetails MapDtoToParentGuardianDetails(ParentGuardianDto dto)
    {
        var officeAddress = dto.OfficeAddress != null ? MapDtoToAddressDetails(dto.OfficeAddress) : null;
        var localAddress = dto.LocalAddress != null ? MapDtoToAddressDetails(dto.LocalAddress) : null;

        return new ParentGuardianDetails(
            dto.FullName,
            dto.DateOfBirth,
            dto.Occupation,
            dto.Email,
            dto.ContactNo,
            dto.OfficeNo,
            dto.Qualification,
            dto.YearOfPassing,
            dto.Designation,
            dto.Organization,
            dto.AadharNo,
            officeAddress,
            localAddress
        );
    }

    public AddressDetails MapDtoToAddressDetails(AddressDto dto)
    {
        return new AddressDetails(
            dto.Address1,
            dto.Address2,
            dto.Address3,
            dto.City,
            dto.State,
            dto.Country,
            dto.Pincode
        );
    }
}
