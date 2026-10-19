using MindMatch.Application.Dtos.Interest;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MindMatch.Application.Interfaces
{
    public interface IInterestService
    {
        Task<List<InterestResponse>> GetAllAsync(
CancellationToken cancellationToken = default);
    }
}
