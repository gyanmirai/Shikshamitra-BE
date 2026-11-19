using System.Threading.Tasks;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public interface IAdmissionApplicationNumberingRepository
{
	Task LockAsync();
	Task<AdmissionApplicationNumberingRule?> GetCurrentAsync();
	Task<bool> HasIssuedAsync();
	Task InsertRuleAsync(AdmissionApplicationNumberingRule rule);
	Task<long> TakeNextAsync(AdmissionApplicationNumberingRule rule, string scopeKey, string yearText);
	Task<long> PeekNextAsync(AdmissionApplicationNumberingRule rule, string scopeKey, string yearText);
	Task<bool> NumberExistsAsync(string number);
}
