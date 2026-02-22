using ClampDown.Core.HelperIpc;

namespace ClampDown.Core.Abstractions;

public interface IHelperCommandClient
{
    Task<HelperResponse> SendAsync(HelperRequest request, TimeSpan timeout, CancellationToken cancellationToken = default);
}
