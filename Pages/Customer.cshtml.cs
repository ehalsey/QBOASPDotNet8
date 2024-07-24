using System.Net.Http.Headers;
using System.Text.Json;
using AspNet.Security.OAuth.QuickBooks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace WebIdentAuth.Pages;

[Authorize(AuthenticationSchemes = "QuickBooks,Identity.Application")]
public class CustomerModel : PageModel
{
    private readonly IAuthenticationService _authenticationService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthenticationSchemeProvider _schemeProvider;
    private readonly ILogger<IndexModel> _logger;

    public string StatusMessage { get; set; } = "none";


    public CustomerModel(IAuthenticationSchemeProvider schemeProvider, IAuthenticationService authenticationService, IHttpContextAccessor httpContextAccessor, ILogger<IndexModel> logger)
    {
        _schemeProvider = schemeProvider;
        _authenticationService = authenticationService;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public async Task OnGet()
    {
        //if user identity doesn't contain Quickbooks, then challenge the user
        if (!_httpContextAccessor.HttpContext.User.Identities.Any(i => i.AuthenticationType == "https://oauth.platform.intuit.com/op/v1"))
        {
            await _httpContextAccessor.HttpContext.ChallengeAsync("QuickBooks");
            return;
        }

        var authResult = await _authenticationService.AuthenticateAsync(_httpContextAccessor.HttpContext, QuickBooksAuthenticationDefaults.AuthenticationScheme);

        if (authResult?.Succeeded == true)
        {
            // included her for illustration purposes only.  Tokens should be cached using a TokenService that includes getting new tokens when they expire
            var accessToken = authResult.Properties.GetTokenValue("access_token");
            var refreshToken = authResult.Properties.GetTokenValue("refresh_token");
            var idToken = authResult.Properties.GetTokenValue("id_token");
            _logger.LogInformation("Access token: {AccessToken}", accessToken);
            _logger.LogInformation("Refresh token: {RefreshToken}", refreshToken);
            _logger.LogInformation("ID token: {IdToken}", idToken);

            var baseUrl = "https://sandbox-quickbooks.api.intuit.com/v3/company";   // should be in appsettings.json or env variables
            var realmId = _httpContextAccessor.HttpContext.User.FindFirst("realmId")?.Value;
            var query = "select * from Customer MAXRESULTS 1";
            
            // call QuickBooks api and get the list of customers using httpClient
            // included her for illustration purposes only.  Should be in a QuickBooksService and not in your controller or page
            string fullUrl = $"{baseUrl}/{realmId}/query?query={query}&minorversion=73";
            
            HttpClient client = new();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            client.DefaultRequestHeaders.Add("User-Agent", "MemberMan");
            client.DefaultRequestHeaders.Add("Accept", "application/json");
            var response = await client.GetAsync(fullUrl);
            var customersString = await response.Content.ReadAsStringAsync();

            //parse the customersString to get the customer name
            var customer = JsonDocument.Parse(customersString).RootElement.GetProperty("QueryResponse").GetProperty("Customer")[0];
            var customerName = customer.GetProperty("DisplayName").GetString();

            StatusMessage = customerName ?? "none";
        }
        else
        {
            StatusMessage = "Error: " + authResult.Failure?.Message;
        }

    }
}
