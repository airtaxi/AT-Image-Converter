using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace ImageConverterAT.Helpers;

public static class HttpHelper
{
    private static readonly HttpClient SharedHttpClient = new();
    public static async Task<string> GetContentFromUrlAsync(string url)
    {
        try
        {
            HttpResponseMessage response = await SharedHttpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }
        catch (HttpRequestException) { return null; }
    }
}
