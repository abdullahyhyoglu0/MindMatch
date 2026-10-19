using MindMatch.Application.Dtos.Match;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MindMatch.Application.Interfaces
{


    public interface IMatchService
    {
        Task<MatchResponse> SendRequestAsync(
            Guid requesterId,
            Guid receiverId,
            CancellationToken cancellationToken = default);

        Task<MatchResponse> AcceptRequestAsync(
            Guid matchId,
            Guid respondingUserId,
            CancellationToken cancellationToken = default);

        Task<MatchResponse> RejectRequestAsync(
            Guid matchId,
            Guid respondingUserId,
            CancellationToken cancellationToken = default);

        Task<MatchResponse> EndMatchAsync(
            Guid matchId,
            Guid requestingUserId,
            CancellationToken cancellationToken = default);

        Task<List<MatchResponse>> GetMyMatchesAsync(
            Guid userId,
            CancellationToken cancellationToken = default);

        Task<List<MatchResponse>> GetIncomingRequestsAsync(Guid userId, CancellationToken cancellationToken = default);
    }

}
