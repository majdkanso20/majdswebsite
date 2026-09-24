// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using MajdsApp.Data;
using System;
using System.Threading.Tasks;
using MajdsApp.Configuration;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MajdsApp.Areas.Identity.Pages.Account.Manage
{
    public class TwoFactorAuthenticationModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly ILogger<TwoFactorAuthenticationModel> _logger;
        private readonly FeatureOptions _featureOptions;

        public TwoFactorAuthenticationModel(
            UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager, ILogger<TwoFactorAuthenticationModel> logger,
            IOptions<FeatureOptions> featureOptions)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _logger = logger;
            _featureOptions = featureOptions.Value;
        }

        /// <summary>
        ///     Whether the site administrator has the two-factor authentication feature turned on.
        /// </summary>
        public bool FeatureEnabled { get; set; }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public bool HasAuthenticator { get; set; }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public int RecoveryCodesLeft { get; set; }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        [BindProperty]
        public bool Is2faEnabled { get; set; }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public bool IsMachineRemembered { get; set; }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        [TempData]
        public string StatusMessage { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");
            }

            HasAuthenticator = await _userManager.GetAuthenticatorKeyAsync(user) != null;
            Is2faEnabled = await _userManager.GetTwoFactorEnabledAsync(user);
            IsMachineRemembered = await _signInManager.IsTwoFactorClientRememberedAsync(user);
            RecoveryCodesLeft = await _userManager.CountRecoveryCodesAsync(user);
            FeatureEnabled = _featureOptions.TwoFactorAuthenticationEnabled;

            if (!FeatureEnabled && !Is2faEnabled)
            {
                StatusMessage = "Error: Two-factor authentication is currently disabled by the site administrator.";
                return RedirectToPage("./Index");
            }

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");
            }

            await _signInManager.ForgetTwoFactorClientAsync();
            StatusMessage = "The current browser has been forgotten. When you login again from this browser you will be prompted for your 2fa code.";
            return RedirectToPage();
        }
    }
}
