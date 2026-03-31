using System.Net.Http;

namespace TCTEnglish.Tests.TestHelpers;

public sealed class StubHttpClientFactory : IHttpClientFactory
{
    private readonly HttpClient _httpClient;

    public StubHttpClientFactory(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public HttpClient CreateClient(string name)
    {
        return _httpClient;
    }
}
