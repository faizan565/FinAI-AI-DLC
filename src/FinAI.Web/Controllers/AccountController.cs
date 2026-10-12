using FinAI.Web.Models;
using FinAI.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace FinAI.Web.Controllers;

public sealed class AccountController(FinAiApiClient api) : Controller
{
    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        ViewData["Title"] = "Sign in";
        ViewData["ReturnUrl"] = returnUrl;
        return View(new LoginViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl, CancellationToken cancellationToken)
    {
        ViewData["Title"] = "Sign in";
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await api.LoginAsync(model.UserName.Trim(), model.Password, cancellationToken);
        if (!result.IsSuccess)
        {
            AddApiError(result.Error!, "Invalid username or password.");
            return View(model);
        }

        return LocalRedirect(IsSafeLocalUrl(returnUrl) ? returnUrl! : Url.Action("Index", "Dashboard")!);
    }

    [HttpGet]
    public IActionResult Register()
    {
        ViewData["Title"] = "Create account";
        return View(new RegisterViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model, CancellationToken cancellationToken)
    {
        ViewData["Title"] = "Create account";
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await api.RegisterAsync(model.UserName.Trim(), model.Password, cancellationToken);
        if (!result.IsSuccess)
        {
            AddApiError(result.Error!, "Registration failed. Please try again.");
            return View(model);
        }

        var login = await api.LoginAsync(model.UserName.Trim(), model.Password, cancellationToken);
        if (!login.IsSuccess)
        {
            TempData["Message"] = "Account created. Please sign in.";
            return RedirectToAction(nameof(Login));
        }

        return RedirectToAction("Index", "Dashboard");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        await api.LogoutAsync(cancellationToken);
        api.ForgetSession();
        return RedirectToAction(nameof(Login));
    }

    private void AddApiError(ApiError error, string fallbackMessage)
    {
        foreach (var (field, messages) in error.Errors)
        {
            foreach (var message in messages)
            {
                ModelState.AddModelError(field, message);
            }
        }

        if (ModelState.ErrorCount == 0)
        {
            ModelState.AddModelError(string.Empty, error.IsUnauthorized ? fallbackMessage : error.Message);
        }
    }

    private bool IsSafeLocalUrl(string? url) => !string.IsNullOrEmpty(url) && Url.IsLocalUrl(url);
}
