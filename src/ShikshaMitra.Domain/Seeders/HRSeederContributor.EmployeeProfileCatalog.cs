using Microsoft.AspNetCore.Identity;
using ShikshaMitra.Common;
using ShikshaMitra.Groups;
using ShikshaMitra.HR.Calendar.Holidays;
using ShikshaMitra.HR.Calendar.HolidayTypes;
using ShikshaMitra.HR.AttendanceAndLeaves;
using ShikshaMitra.HR.AttendanceAndLeaves.Settings;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Employees.Settings;
using ShikshaMitra.HR.Employees.Payroll;
using ShikshaMitra.HR.Policies;
using ShikshaMitra.HR.Payroll.RunPayroll;
using ShikshaMitra.HR.Payroll.Settings;
using ShikshaMitra.HR.ProfessionalManagement;
using ShikshaMitra.TenantSettings;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.EducationBoards;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Timing;
using Volo.Abp.Uow;

namespace ShikshaMitra.Seeders;

public partial class HRSeederContributor
{
	internal static byte[] BuildSeedPdfBytes(string documentName, SuratSeedEmployeeProfile profile)
	{
		var body = $"Seed document: {documentName}\nEmployee: {profile.FirstName} {profile.LastName}\nSchool: Demo School, Surat\n";
		return Encoding.ASCII.GetBytes($"%PDF-1.4\n% Seeded demo PDF\n1 0 obj <<>> endobj\nstream\n{body}endstream\n%%EOF");
	}

	private static string SanitizeFileToken(string value) =>
		new(value.Select(ch => char.IsLetterOrDigit(ch) ? char.ToLowerInvariant(ch) : '-').ToArray());

	private static string BuildDemoMobile(string baseMobile, int suffix)
	{
		var digits = new string(baseMobile.Where(char.IsDigit).ToArray());
		if (digits.Length < 9)
		{
			return $"98250000{suffix:00}";
		}

		return $"{digits[..^1]}{suffix % 10}";
	}

	private static string BuildSeedPanNo(string mobile, string email)
	{
		var digits = new string(mobile.Where(char.IsDigit).ToArray()).PadLeft(4, '0');
		var serial = digits[^4..];
		var check = (char)('A' + (StableSeedHash(email) % 26));
		return $"DDSMS{serial}{check}";
	}

	private static string BuildSeedAdharNo(string mobile)
	{
		var digits = new string(mobile.Where(char.IsDigit).ToArray()).PadLeft(9, '0');
		return $"900{digits[^9..]}";
	}

	internal static IReadOnlyDictionary<string, SuratSeedEmployeeProfile> BuildSuratSeedEmployeeProfiles()
	{
		var state = new State("Gujarat", "GJ");
		var country = new Country("India", "IN");
		var profiles = new[]
		{
			new SuratSeedEmployeeProfile("Jayesh", "Bhagat", null, "jayesh.bhagat@ddvs.com", "9825012001", "9825012002", "Director", "Director", "Management", new DateOnly(1978, 3, 12), new DateOnly(2008, 6, 1), GenderType.Male, MaritalStatusType.Married, "Bhagat", CastType.General, "Hindu", "O+", "M.Com, Education Leadership", "A-101, Royal Residency", "City Light Road", null, "Surat", state, country, "395007", "Mole on right cheek", EmployeeGroupType.NonTeaching),
			new SuratSeedEmployeeProfile("Dasharthbhai", "Patel", null, "dasharthbhai.patel@ddvs.com", "9825012003", "9825012004", "Director", "Director", "Management", new DateOnly(1975, 8, 22), new DateOnly(2007, 6, 1), GenderType.Male, MaritalStatusType.Married, "Patel", CastType.General, "Hindu", "B+", "B.Com, Education Administration", "B-202, Shree Heights", "Adajan Road", null, "Surat", state, country, "395009", "Scar on left hand", EmployeeGroupType.NonTeaching),
			new SuratSeedEmployeeProfile("Tushar", "Patel", null, "tushar.patel@ddvs.com", "9825012005", "9825012006", "Director", "Director", "Management", new DateOnly(1980, 1, 18), new DateOnly(2009, 6, 1), GenderType.Male, MaritalStatusType.Married, "Patel", CastType.General, "Hindu", "A+", "MBA, School Management", "C-303, Green Avenue", "Vesu Main Road", null, "Surat", state, country, "395007", "Birthmark on neck", EmployeeGroupType.NonTeaching),
			new SuratSeedEmployeeProfile("Admin", "DDVS", null, "admin-ddvs@ddvs.com", "9825012040", "9825012041", "admin", "System Administrator", "Administration", new DateOnly(1985, 4, 10), new DateOnly(2012, 6, 1), GenderType.Male, MaritalStatusType.Married, "DDVS", CastType.General, "Hindu", "O+", "MBA, School Administration", "A-401, Admin Residency", "Parvat Patiya", null, "Surat", state, country, "395010", "Seeded DDVS admin profile", EmployeeGroupType.NonTeaching),
			new SuratSeedEmployeeProfile("Admin", "Operations", null, "admin-ops@ddvs.com", "9825012042", "9825012043", "admin", "Operations Administrator", "Administration", new DateOnly(1987, 9, 18), new DateOnly(2014, 6, 1), GenderType.Female, MaritalStatusType.Married, "Operations", CastType.General, "Hindu", "A+", "MBA, Operations Management", "A-402, Admin Residency", "Parvat Patiya", null, "Surat", state, country, "395010", "Seeded operations admin profile", EmployeeGroupType.NonTeaching),
			new SuratSeedEmployeeProfile("Principal", "Demo", "GSEB", "principal-demo-gseb@ddvs.com", "9825012044", "9825012045", "Principal", "Principal - Demo GSEB", "Administration", new DateOnly(1981, 2, 9), new DateOnly(2010, 6, 1), GenderType.Male, MaritalStatusType.Married, "Demo", CastType.General, "Hindu", "B+", "M.Ed, School Leadership", "B-402, Academic Residency", "City Light Road", null, "Surat", state, country, "395007", "Seeded demo GSEB principal profile", EmployeeGroupType.Teaching),
			new SuratSeedEmployeeProfile("Principal", "GSEB", null, "principal-gseb@ddvs.com", "9825012046", "9825012047", "Principal", "Principal - GSEB", "Administration", new DateOnly(1980, 11, 19), new DateOnly(2009, 6, 1), GenderType.Male, MaritalStatusType.Married, "GSEB", CastType.General, "Hindu", "O+", "M.Ed, GSEB School Leadership", "B-403, Academic Residency", "City Light Road", null, "Surat", state, country, "395007", "Seeded GSEB principal profile", EmployeeGroupType.Teaching),
			new SuratSeedEmployeeProfile("Principal", "CBSE", null, "principal-cbse@ddvs.com", "9825012007", "9825012008", "Principal", "Principal - CBSE", "Administration", new DateOnly(1982, 6, 12), new DateOnly(2011, 6, 1), GenderType.Male, MaritalStatusType.Married, "CBSE", CastType.General, "Hindu", "A+", "M.Ed, CBSE School Leadership", "B-401, Academic Residency", "City Light Road", null, "Surat", state, country, "395007", "Seeded CBSE principal profile", EmployeeGroupType.Teaching),
			new SuratSeedEmployeeProfile("Admissions", "Reception", null, "admissions-reception@ddvs.com", "9825012057", null, "Admissions Receptionist", "Admissions Receptionist", "Admissions", new DateOnly(1993, 4, 12), new DateOnly(2020, 6, 1), GenderType.Female, MaritalStatusType.Married, "Reception", CastType.General, "Hindu", "B+", "B.Com, Front Office Administration", "A-105, Staff Residency", "City Light Road", null, "Surat", state, country, "395007", "Seeded admissions receptionist profile", EmployeeGroupType.NonTeaching),
			new SuratSeedEmployeeProfile("Admissions", "Counselor", null, "admissions-counselor@ddvs.com", "9825012058", null, "Admissions Counselor", "Admissions Counselor", "Admissions", new DateOnly(1990, 8, 21), new DateOnly(2017, 6, 1), GenderType.Female, MaritalStatusType.Married, "Counselor", CastType.General, "Hindu", "O+", "M.A Psychology, Admissions Counseling", "A-106, Staff Residency", "City Light Road", null, "Surat", state, country, "395007", "Seeded admissions counselor profile", EmployeeGroupType.NonTeaching),
			new SuratSeedEmployeeProfile("Admissions", "Approver", null, "admissions-approver@ddvs.com", "9825012059", null, "Admissions Approver", "Admissions Approver", "Admissions", new DateOnly(1985, 10, 7), new DateOnly(2012, 6, 1), GenderType.Male, MaritalStatusType.Married, "Approver", CastType.General, "Hindu", "A+", "M.Ed, School Admissions", "A-107, Staff Residency", "City Light Road", null, "Surat", state, country, "395007", "Seeded admissions approver profile", EmployeeGroupType.NonTeaching),
			new SuratSeedEmployeeProfile("Teacher", "Demo1", null, "teacher-demo-1@ddvs.com", "9825012048", null, "Teacher", "Teacher - Demo Primary", "Primary", new DateOnly(1992, 3, 7), new DateOnly(2017, 7, 1), GenderType.Female, MaritalStatusType.Married, "Demo1", CastType.General, "Hindu", "O+", "B.Sc, B.Ed", "D-101, Teacher Residency", "Athwa Gate", null, "Surat", state, country, "395001", "Seeded teacher demo 1 profile", EmployeeGroupType.Teaching),
			new SuratSeedEmployeeProfile("Teacher", "Demo2", null, "teacher-demo-2@ddvs.com", "9825012049", null, "Teacher", "Teacher - Demo Secondary", "Secondary", new DateOnly(1991, 8, 23), new DateOnly(2016, 7, 1), GenderType.Male, MaritalStatusType.Married, "Demo2", CastType.General, "Hindu", "A-", "M.Sc, B.Ed", "D-102, Teacher Residency", "Athwa Gate", null, "Surat", state, country, "395001", "Seeded teacher demo 2 profile", EmployeeGroupType.Teaching),
			new SuratSeedEmployeeProfile("Teacher", "GSEB", null, "teacher-gseb@ddvs.com", "9825012050", null, "Teacher", "Teacher - GSEB", "Secondary", new DateOnly(1990, 5, 25), new DateOnly(2015, 7, 1), GenderType.Female, MaritalStatusType.Married, "GSEB", CastType.General, "Hindu", "B+", "M.A, B.Ed", "D-103, Teacher Residency", "Athwa Gate", null, "Surat", state, country, "395001", "Seeded GSEB teacher login profile", EmployeeGroupType.Teaching),
			new SuratSeedEmployeeProfile("Hiren", "Vadher", null, "hiren.vadher@ddvs.com", "9825012010", "9825012011", "Principal", "Principal", "Administration", new DateOnly(1984, 1, 1), new DateOnly(2010, 6, 1), GenderType.Male, MaritalStatusType.Married, "Vadher", CastType.General, "Hindu", "B+", "M.Ed, PhD in Education Management", "B-301, Tapi Heights", "Adajan Road", "Near Pal RTO", "Surat", state, country, "395009", "Mole on left cheek", EmployeeGroupType.Teaching),
			new SuratSeedEmployeeProfile("Jignesh", "Jinjala", null, "jignesh.jinjala@ddvs.com", "9825012012", "9825012013", "Vice Principal", "Vice Principal", "Administration", new DateOnly(1986, 4, 16), new DateOnly(2012, 6, 1), GenderType.Male, MaritalStatusType.Married, "Jinjala", CastType.General, "Hindu", "A+", "M.Sc, M.Ed", "A-204, Shree Residency", "City Light Road", null, "Surat", state, country, "395007", "Scar on right hand", EmployeeGroupType.Teaching),
			new SuratSeedEmployeeProfile("Akshay", "Patel", null, "akshay.patel@ddvs.com", "9825012014", null, "HOD", "Head of Science Department", "Science", new DateOnly(1988, 1, 1), new DateOnly(2013, 6, 1), GenderType.Male, MaritalStatusType.Married, "Patel", CastType.OBC, "Hindu", "O+", "M.Sc Physics, B.Ed", "15, Shantiniketan Society", "Vesu Main Road", null, "Surat", state, country, "395007", "Birthmark on neck", EmployeeGroupType.Teaching),
			new SuratSeedEmployeeProfile("Parth", "Ghusar", null, "parth.ghusar@ddvs.com", "9825012015", null, "HOD", "Head of Mathematics Department", "Mathematics", new DateOnly(1990, 5, 14), new DateOnly(2016, 6, 1), GenderType.Male, MaritalStatusType.Married, "Ghusar", CastType.General, "Hindu", "AB+", "M.Sc Mathematics, B.Ed", "C-101, Siddhi Vinayak Enclave", "Pal Gam Road", null, "Surat", state, country, "395009", "Mole on forehead", EmployeeGroupType.Teaching),
			new SuratSeedEmployeeProfile("Sweta", "Badgujar", null, "sweta.badgujar@ddvs.com", "9825012016", "9825012017", "Teacher", "Senior Teacher - English", "Languages", new DateOnly(1993, 7, 2), new DateOnly(2018, 7, 1), GenderType.Female, MaritalStatusType.Married, "Badgujar", CastType.General, "Hindu", "B-", "M.A English, B.Ed", "D-502, Royal Residency", "Athwa Gate", null, "Surat", state, country, "395001", "Scar on left eyebrow", EmployeeGroupType.Teaching),
			new SuratSeedEmployeeProfile("Jeet", "Ram", null, "jeet.ram@ddvs.com", "9825012018", null, "Teacher", "Teacher - Chemistry", "Science", new DateOnly(1992, 1, 1), new DateOnly(2016, 7, 1), GenderType.Male, MaritalStatusType.Married, "Ram", CastType.General, "Hindu", "O-", "M.Sc Chemistry, B.Ed", "E-301, Green Park", "Katargam Road", null, "Surat", state, country, "395004", "Dimple on chin", EmployeeGroupType.Teaching),
			new SuratSeedEmployeeProfile("Dharmik", "Jinjala", null, "dharmik.jinjala@ddvs.com", "9825012019", null, "Teacher", "Teacher - Biology", "Science", new DateOnly(1995, 11, 27), new DateOnly(2020, 12, 1), GenderType.Male, MaritalStatusType.Unmarried, "Jinjala", CastType.OBC, "Hindu", "A-", "M.Sc Biology, B.Ed", "F-102, Palm Gardens", "Sarthana Jakat Naka", null, "Surat", state, country, "395006", "Mole on right shoulder", EmployeeGroupType.Teaching),
			new SuratSeedEmployeeProfile("Jay", "Dalia", null, "jay.dalia@ddvs.com", "9825012020", "9825012021", "Teacher", "Teacher - History", "Social Studies", new DateOnly(1991, 10, 12), new DateOnly(2015, 6, 1), GenderType.Male, MaritalStatusType.Married, "Dalia", CastType.General, "Hindu", "B+", "M.A History, B.Ed", "G-201, Heritage Complex", "Rander Road", null, "Surat", state, country, "395005", "Beauty mark on neck", EmployeeGroupType.Teaching),
			new SuratSeedEmployeeProfile("Hiren", "Akbari", null, "hiren.akbari@ddvs.com", "9825012022", null, "Teacher", "Teacher - Computer Science", "Computer Science", new DateOnly(1994, 1, 7), new DateOnly(2018, 7, 1), GenderType.Male, MaritalStatusType.Unmarried, "Akbari", CastType.General, "Jain", "AB-", "B.Tech CS, B.Ed", "H-405, Tech Plaza", "Udhna Magdalla Road", null, "Surat", state, country, "395017", "Scar on left hand", EmployeeGroupType.Teaching),
			new SuratSeedEmployeeProfile("Prerna", "Atre", null, "prerna.atre@ddvs.com", "9825012023", null, "Teacher", "Teacher - Hindi", "Languages", new DateOnly(1996, 12, 1), new DateOnly(2021, 12, 1), GenderType.Female, MaritalStatusType.Unmarried, "Atre", CastType.General, "Hindu", "O+", "M.A Hindi, B.Ed", "I-304, Sai Darshan", "Varachha Main Road", null, "Surat", state, country, "395006", "Mole on left ear", EmployeeGroupType.Teaching),
			new SuratSeedEmployeeProfile("Vishal", "Makwana", null, "vishal.makwana@ddvs.com", "9825012024", "9825012025", "Administrative Manager", "Administrative Manager", "Administration", new DateOnly(1989, 5, 10), new DateOnly(2015, 6, 1), GenderType.Male, MaritalStatusType.Married, "Makwana", CastType.General, "Hindu", "A+", "MBA in Administration", "J-501, Vista Towers", "Ghod Dod Road", null, "Surat", state, country, "395007", "Mole on right cheek", EmployeeGroupType.NonTeaching),
			new SuratSeedEmployeeProfile("Mayur", "Solanki", null, "mayur.solanki@ddvs.com", "9825012026", null, "HR Manager", "HR Manager", "Human Resources", new DateOnly(1991, 3, 31), new DateOnly(2017, 6, 1), GenderType.Male, MaritalStatusType.Married, "Solanki", CastType.OBC, "Hindu", "B+", "MBA in HR Management", "K-201, University Campus", "Piplod", null, "Surat", state, country, "395007", "Birthmark on left arm", EmployeeGroupType.NonTeaching),
			new SuratSeedEmployeeProfile("HR", "Manager", null, "hr-manager@ddvs.com", "9825012054", null, "HR Manager", "HR Manager - Demo", "Human Resources", new DateOnly(1989, 12, 11), new DateOnly(2015, 6, 1), GenderType.Female, MaritalStatusType.Married, "Manager", CastType.General, "Hindu", "AB+", "MBA in HR Management", "K-301, University Campus", "Piplod", null, "Surat", state, country, "395007", "Seeded HR manager login profile", EmployeeGroupType.NonTeaching),
			new SuratSeedEmployeeProfile("Punam", "Badgujar", null, "punam.badgujar@ddvs.com", "9825012027", "9825012028", "Finance Officer", "Finance Officer", "Finance", new DateOnly(1990, 11, 15), new DateOnly(2016, 6, 1), GenderType.Female, MaritalStatusType.Married, "Badgujar", CastType.OBC, "Hindu", "O+", "M.Com, CA Inter", "L-102, Finance Plaza", "Ring Road", null, "Surat", state, country, "395002", "Scar on chin", EmployeeGroupType.NonTeaching),
			new SuratSeedEmployeeProfile("Finance", "Officer", null, "finance-officer@ddvs.com", "9825012055", null, "Finance Officer", "Finance Officer - Demo", "Finance", new DateOnly(1988, 1, 14), new DateOnly(2014, 6, 1), GenderType.Female, MaritalStatusType.Married, "Officer", CastType.General, "Hindu", "O+", "M.Com, CA Inter", "L-202, Finance Plaza", "Ring Road", null, "Surat", state, country, "395002", "Seeded finance officer login profile", EmployeeGroupType.NonTeaching),
			new SuratSeedEmployeeProfile("Milind", "Kumar", null, "milind.kumar@ddvs.com", "9825012029", null, "IT Administrator", "IT Administrator", "Information Technology", new DateOnly(1995, 2, 12), new DateOnly(2018, 6, 1), GenderType.Male, MaritalStatusType.Married, "Kumar", CastType.General, "Hindu", "AB+", "B.Tech IT", "M-403, Silicon Heights", "Udhna", null, "Surat", state, country, "394210", "Mole on left wrist", EmployeeGroupType.NonTeaching),
			new SuratSeedEmployeeProfile("Nikhil", "Baku", null, "nikhil.baku@ddvs.com", "9825012030", null, "Office Assistant", "Office Assistant", "Administration", new DateOnly(1999, 10, 21), new DateOnly(2021, 11, 1), GenderType.Male, MaritalStatusType.Unmarried, "Baku", CastType.General, "Hindu", "A-", "B.Com", "N-502, Shyam Residency", "Mota Varachha", null, "Surat", state, country, "394101", "Dimple on right cheek", EmployeeGroupType.NonTeaching),
			new SuratSeedEmployeeProfile("Raj", "Baku", null, "raj.baku@ddvs.com", "9825012031", null, "Clerk", "Clerk", "Administration", new DateOnly(1998, 7, 10), new DateOnly(2020, 7, 15), GenderType.Male, MaritalStatusType.Unmarried, "Baku", CastType.General, "Hindu", "B+", "B.A.", "O-108, Admin Arcade", "Sarthana", null, "Surat", state, country, "395006", "Scar on right thumb", EmployeeGroupType.NonTeaching),
			new SuratSeedEmployeeProfile("Clerk", "Office", null, "clerk-office@ddvs.com", "9825012056", null, "Clerk", "Office Clerk - Demo", "Administration", new DateOnly(1996, 4, 18), new DateOnly(2019, 7, 1), GenderType.Male, MaritalStatusType.Unmarried, "Office", CastType.General, "Hindu", "B+", "B.Com", "O-208, Admin Arcade", "Sarthana", null, "Surat", state, country, "395006", "Seeded office clerk login profile", EmployeeGroupType.NonTeaching)
		};
		return profiles.ToDictionary(x => x.Email, StringComparer.OrdinalIgnoreCase);
	}

	internal sealed record SuratSeedEmployeeProfile(
		string FirstName,
		string LastName,
		string? MiddleName,
		string Email,
		string Mobile,
		string? SecondaryMobile,
		string RoleName,
		string Designation,
		string Department,
		DateOnly DateOfBirth,
		DateOnly DateOfJoining,
		GenderType Gender,
		MaritalStatusType MaritalStatus,
		string FamilyCast,
		CastType CastType,
		string Religion,
		string BloodGroup,
		string Qualification,
		string Address1,
		string? Address2,
		string? Address3,
		string City,
		State State,
		Country Country,
		string PinCode,
		string IdentificationMark,
		EmployeeGroupType EmployeeGroup);

	private static uint StableSeedHash(string value)
	{
		unchecked
		{
			var hash = 2166136261u;
			foreach (var character in value.ToUpperInvariant())
			{
				hash ^= character;
				hash *= 16777619u;
			}

			return hash;
		}
	}

	/// <summary>
	/// Demo payroll lines + attendance for active employees. Skips employees that already have payroll components.
	/// Attendance: rolling 12 months, weekdays only; idempotent per employee per day.
	/// </summary>
}
