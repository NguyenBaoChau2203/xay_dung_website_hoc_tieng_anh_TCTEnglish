using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace TCTEnglish.Tests.Infrastructure;

public static class IntegrationTestClientHelper
{
    public static HttpClient CreateAnonymousClient(TestWebApplicationFactory factory)
    {
        return factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });
    }

    public static HttpClient CreateAuthenticatedClient(
        TestWebApplicationFactory factory,
        int userId,
        string role)
    {
        var client = CreateAnonymousClient(factory);

        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.UserIdHeaderName, userId.ToString());
        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.RoleHeaderName, role);
        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.EmailHeaderName, $"user{userId}@test.local");
        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.NameHeaderName, $"User {userId}");

        return client;
    }

    public static async Task<string> GetAntiForgeryTokenAsync(HttpClient client, string route)
    {
        using var response = await client.GetAsync(route);
        var body = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Request to '{route}' failed with {(int)response.StatusCode} {response.StatusCode}.{Environment.NewLine}{body}");
        }

        var match = Regex.Match(
            body,
            "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"|value=\"([^\"]+)\"[^>]*name=\"__RequestVerificationToken\"",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);

        var token = match.Groups[1].Value;
        if (string.IsNullOrWhiteSpace(token))
        {
            token = match.Groups[2].Value;
        }

        Assert.False(string.IsNullOrWhiteSpace(token), $"Missing antiforgery token on route {route}.{Environment.NewLine}{body}");
        return token;
    }

    public static MultipartFormDataContent CreatePngUploadForm(string fieldName, string fileName, string formKey, string formValue)
    {
        var validPngBytes = new byte[]
        {
            0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
            0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52,
            0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01,
            0x08, 0x06, 0x00, 0x00, 0x00, 0x1F, 0x15, 0xC4,
            0x89, 0x00, 0x00, 0x00, 0x0A, 0x49, 0x44, 0x41,
            0x54, 0x78, 0x9C, 0x63, 0x00, 0x01, 0x00, 0x00,
            0x05, 0x00, 0x01, 0x0D, 0x0A, 0x2D, 0xB4, 0x00,
            0x00, 0x00, 0x00, 0x49, 0x45, 0x4E, 0x44, 0xAE,
            0x42, 0x60, 0x82
        };

        var imageContent = new ByteArrayContent(validPngBytes);
        imageContent.Headers.ContentType = MediaTypeHeaderValue.Parse("image/png");

        return new MultipartFormDataContent
        {
            { imageContent, fieldName, fileName },
            { new StringContent(formValue), formKey }
        };
    }
}
