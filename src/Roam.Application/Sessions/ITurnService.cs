using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Roam.Application.Sessions;

public interface ITurnService
{
    Task<IEnumerable<IceServerDto>> GetIceServersAsync(string userId, CancellationToken cancellationToken = default);
}
