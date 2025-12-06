using System.Threading;
using System.Threading.Tasks;
using MailCore.Models;

// Interface defining OAuth authentication service
namespace MailCore.Interfaces
{
    public interface IOAuthService
    {
        Task<OAuthResult?> LoginAsync(CancellationToken ct = default);
    }
}
