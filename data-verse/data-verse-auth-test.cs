using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;

class Program
{
    static async Task Main()
    {
        // ===== CONFIGURATION =====
        string tenantId = "YOUR_TENANT_ID";
        string clientId = "YOUR_CLIENT_ID";
        string clientSecret = "YOUR_CLIENT_SECRET";
        string envUrl = "https://YOUR_ORG.crm.dynamics.com"; // Your Dataverse URL

        // ===== STEP 1: Get OAuth Token =====
        var tokenClient = new HttpClient();

        var tokenRequest = new HttpRequestMessage(HttpMethod.Post,
            $"https://login.microsoftonline.com/{tenantId}/oauth2/v2.0/token");

        tokenRequest.Content = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string,string>("client_id", clientId),
            new KeyValuePair<string,string>("client_secret", clientSecret),
            new KeyValuePair<string,string>("grant_type", "client_credentials"),
            new KeyValuePair<string,string>("scope", $"{envUrl}/.default")
        });

        var tokenResponse = await tokenClient.SendAsync(tokenRequest);
        var tokenJson = await tokenResponse.Content.ReadAsStringAsync();

        if (!tokenResponse.IsSuccessStatusCode)
        {
            Console.WriteLine("Token request failed:");
            Console.WriteLine(tokenJson);
            return;
        }

        using var tokenDoc = JsonDocument.Parse(tokenJson);
        string accessToken = tokenDoc.RootElement.GetProperty("access_token").GetString();

        Console.WriteLine("Access token acquired successfully.\n");

        // ===== STEP 2: Call Dataverse WhoAmI =====
        var apiClient = new HttpClient();
        apiClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);
        apiClient.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));

        var whoAmIResponse = await apiClient.GetAsync($"{envUrl}/api/data/v9.2/WhoAmI");
        var whoAmIJson = await whoAmIResponse.Content.ReadAsStringAsync();

        Console.WriteLine("Dataverse WhoAmI Response:");
        Console.WriteLine(whoAmIJson);
    }
}
