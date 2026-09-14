using System.Net;
using System.Net.Sockets;

namespace Enrichly.JobAutomation.Api.Services;

public sealed class PublicWebhookUrlValidator
{
    public async Task<string?> ValidateAsync(string rawUrl, CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(rawUrl, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps) return "Target URL must be an absolute HTTPS URL.";
        if (uri.UserInfo.Length > 0 || uri.IsLoopback || IsPrivateAddress(uri.Host)) return "Target URL must not resolve to a local or private network address.";
        try
        {
            var addresses = await Dns.GetHostAddressesAsync(uri.DnsSafeHost, cancellationToken);
            return addresses.Length == 0 || addresses.Any(IsPrivateAddress) ? "Target URL must resolve only to public IP addresses." : null;
        }
        catch (SocketException) { return "Target host could not be resolved."; }
    }

    private static bool IsPrivateAddress(string host) => IPAddress.TryParse(host, out var address) && IsPrivateAddress(address);
    private static bool IsPrivateAddress(IPAddress address)
    {
        if (IPAddress.IsLoopback(address) || address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any)) return true;
        var bytes = address.GetAddressBytes();
        return address.AddressFamily == AddressFamily.InterNetwork && (bytes[0] == 10 || bytes[0] == 127 || (bytes[0] == 192 && bytes[1] == 168) || (bytes[0] == 172 && bytes[1] is >= 16 and <= 31) || (bytes[0] == 169 && bytes[1] == 254));
    }
}
