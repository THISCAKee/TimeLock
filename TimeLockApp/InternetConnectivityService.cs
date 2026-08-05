using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace TimeLockApp;

public sealed class InternetConnectivityService
{
    private const string ConnectivityCheckUrl =
        "http://www.msftconnecttest.com/connecttest.txt";

    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(5)
    };

    public async Task<bool> HasInternetAccessAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            using HttpRequestMessage request = new(
                HttpMethod.Get,
                ConnectivityCheckUrl);

            using HttpResponseMessage response =
                await HttpClient.SendAsync(
                    request,
                    HttpCompletionOption.ResponseContentRead,
                    cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return false;
            }

            string content = await response.Content.ReadAsStringAsync(
                cancellationToken);

            return content.Trim().Equals(
                "Microsoft Connect Test",
                StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }
}