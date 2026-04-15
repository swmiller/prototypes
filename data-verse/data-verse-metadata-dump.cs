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
        Console.WriteLine("Acquiring access token...");
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

        // ===== STEP 2: Setup API Client =====
        var apiClient = new HttpClient();
        apiClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);
        apiClient.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
        apiClient.DefaultRequestHeaders.Add("OData-MaxVersion", "4.0");
        apiClient.DefaultRequestHeaders.Add("OData-Version", "4.0");

        // ===== STEP 3: Get Environment Metadata =====
        Console.WriteLine("=".PadRight(80, '='));
        Console.WriteLine("DATAVERSE ENVIRONMENT METADATA DUMP");
        Console.WriteLine("=".PadRight(80, '='));
        Console.WriteLine();

        // Get WhoAmI Information
        await DumpWhoAmI(apiClient, envUrl);

        // Get Entity Metadata
        await DumpEntityMetadata(apiClient, envUrl);

        // Get Organization Details
        await DumpOrganizationDetails(apiClient, envUrl);

        // Get Solutions
        await DumpSolutions(apiClient, envUrl);

        Console.WriteLine("\n" + "=".PadRight(80, '='));
        Console.WriteLine("METADATA DUMP COMPLETE");
        Console.WriteLine("=".PadRight(80, '='));
    }

    static async Task DumpWhoAmI(HttpClient client, string envUrl)
    {
        Console.WriteLine("\n--- WHO AM I ---");
        try
        {
            var response = await client.GetAsync($"{envUrl}/api/data/v9.2/WhoAmI");
            var json = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                Console.WriteLine(PrettyPrintJson(json));
            }
            else
            {
                Console.WriteLine($"Error: {response.StatusCode}");
                Console.WriteLine(json);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
    }

    static async Task DumpEntityMetadata(HttpClient client, string envUrl)
    {
        Console.WriteLine("\n--- ENTITY DEFINITIONS (Top 10) ---");
        try
        {
            var response = await client.GetAsync(
                $"{envUrl}/api/data/v9.2/EntityDefinitions?$top=10");
            var json = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                Console.WriteLine(PrettyPrintJson(json));
            }
            else
            {
                Console.WriteLine($"Error: {response.StatusCode}");
                Console.WriteLine(json);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
    }

    static async Task DumpOrganizationDetails(HttpClient client, string envUrl)
    {
        Console.WriteLine("\n--- ORGANIZATION DETAILS ---");
        try
        {
            var response = await client.GetAsync(
                $"{envUrl}/api/data/v9.2/organizations");
            var json = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                Console.WriteLine(PrettyPrintJson(json));
            }
            else
            {
                Console.WriteLine($"Error: {response.StatusCode}");
                Console.WriteLine(json);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
    }

    static async Task DumpSolutions(HttpClient client, string envUrl)
    {
        Console.WriteLine("\n--- SOLUTIONS (Top 10) ---");
        try
        {
            var response = await client.GetAsync(
                $"{envUrl}/api/data/v9.2/solutions?$top=10");
            var json = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                Console.WriteLine(PrettyPrintJson(json));
            }
            else
            {
                Console.WriteLine($"Error: {response.StatusCode}");
                Console.WriteLine(json);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
    }

    static string PrettyPrintJson(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            using var stream = new System.IO.MemoryStream();
            using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });
            doc.WriteTo(writer);
            writer.Flush();
            return System.Text.Encoding.UTF8.GetString(stream.ToArray());
        }
        catch
        {
            return json;
        }
    }
}
