using System.Security.Claims;
using Hermes.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Mvc;

namespace Hermes.Controllers;

public sealed class AccountController : Controller
{
    private readonly IConfiguration _configuration;
    private readonly PortfolioStore _store;

    public AccountController(IConfiguration configuration, PortfolioStore store)
    {
        _configuration = configuration;
        _store = store;
    }

    [HttpGet("login-google")]
    public IActionResult LoginWithGoogle()
    {
        if (!HasGoogleCredentials())
        {
            return Redirect("/?auth=google-not-configured");
        }

        var redirectUrl = Url.Action(nameof(GoogleLoginCallback), "Account");
        var properties = new AuthenticationProperties { RedirectUri = redirectUrl };
        return Challenge(properties, GoogleDefaults.AuthenticationScheme);
    }

    [HttpGet("google-callback")]
    public async Task<IActionResult> GoogleLoginCallback()
    {
        var result = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        if (!result.Succeeded || result.Principal is null)
        {
            return Redirect("/?auth=google-failed");
        }

        var email = result.Principal.FindFirstValue(ClaimTypes.Email);
        if (string.IsNullOrWhiteSpace(email))
        {
            return Redirect("/?auth=google-failed");
        }

        var name = result.Principal.FindFirstValue(ClaimTypes.Name) ?? email;
        _store.SignInWithGoogle(name, email);

        return Redirect("/?auth=google");
    }

    [HttpGet("logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Redirect("/");
    }

    private bool HasGoogleCredentials()
        => !string.IsNullOrWhiteSpace(GetGoogleCredential("ClientId"))
            && !string.IsNullOrWhiteSpace(GetGoogleCredential("ClientSecret"));

    private string? GetGoogleCredential(string key)
        => _configuration[$"Authentication:Google:{key}"] ?? _configuration[$"Google:{key}"];
}
