using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShikshaMitra.Finance.FeeCollection;

public interface IFeeCollectionAppService
{
	Task<StudentFeeCollectionListDto> GetStudentsAsync(GetStudentsInput input);
	Task<CollectFeeOutput> CollectFeeAsync(CollectFeeInput input);
	Task<StudentFeeDetailsDto> GetStudentFeeDetailsAsync(GetStudentFeeDetailsInput input);
	Task<ComprehensiveFeeDetailsDto> GetComprehensiveFeeDetailsAsync(GetComprehensiveFeeDetailsInput input);
	Task<FeeCollectionDetailsDto> GetFeeCollectionDetailsAsync(Guid studentFeeId);
}
