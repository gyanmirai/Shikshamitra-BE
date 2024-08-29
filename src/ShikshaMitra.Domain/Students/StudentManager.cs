using System.Threading.Tasks;
using Volo.Abp.Domain.Services;

namespace ShikshaMitra.Students;

public class StudentManager(IStudentRepository studentRepository) : DomainService
{
	
	public async Task CreateAsync()
	{
	}
}