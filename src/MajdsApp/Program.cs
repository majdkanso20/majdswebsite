using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MajdsApp.Configuration;
using MajdsApp.Data;
using MajdsApp.Services;
using MajdsApp.SharedKernel.Data;
using MajdsApp.SharedKernel.Security;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpContextCurrentUser>();
builder.Services.AddScoped<AuditSaveChangesInterceptor>();
builder.Services.AddDbContext<ApplicationDbContext>((sp, options) =>
    options.UseSqlite(connectionString)
        .AddInterceptors(sp.GetRequiredService<AuditSaveChangesInterceptor>()));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.Configure<FeatureOptions>(builder.Configuration.GetSection("Features"));
builder.Services.Configure<SmtpOptions>(builder.Configuration.GetSection("Email:Smtp"));

builder.Services.AddDefaultIdentity<ApplicationUser>(options => options.SignIn.RequireConfirmedAccount = true)
    .AddRoles<ApplicationRole>()
    .AddSignInManager<ApplicationSignInManager>()
    .AddEntityFrameworkStores<ApplicationDbContext>();
builder.Services.AddRazorPages();

// Sends real confirmation / password-reset emails via SMTP once credentials are configured (see appsettings.json
// "Email:Smtp" and `dotnet user-secrets`). Without credentials, Identity's built-in no-op sender stays in place
// and RegisterConfirmation falls back to showing the on-page confirmation link, so the app still runs.
var smtpUsername = builder.Configuration["Email:Smtp:Username"];
var smtpPassword = builder.Configuration["Email:Smtp:Password"];
var smtpConfigured = !string.IsNullOrEmpty(smtpUsername) && !string.IsNullOrEmpty(smtpPassword);
if (smtpConfigured)
{
    builder.Services.AddTransient<IEmailSender<ApplicationUser>, SmtpEmailSender>();
}

// External logins (Google / Microsoft) are only wired up once credentials are configured,
// via appsettings or `dotnet user-secrets`, so the app still runs fine without them.
var authenticationBuilder = builder.Services.AddAuthentication();

var googleClientId = builder.Configuration["Authentication:Google:ClientId"];
var googleClientSecret = builder.Configuration["Authentication:Google:ClientSecret"];
if (!string.IsNullOrEmpty(googleClientId) && !string.IsNullOrEmpty(googleClientSecret))
{
    authenticationBuilder.AddGoogle(options =>
    {
        options.ClientId = googleClientId;
        options.ClientSecret = googleClientSecret;
    });
}

var microsoftClientId = builder.Configuration["Authentication:Microsoft:ClientId"];
var microsoftClientSecret = builder.Configuration["Authentication:Microsoft:ClientSecret"];
if (!string.IsNullOrEmpty(microsoftClientId) && !string.IsNullOrEmpty(microsoftClientSecret))
{
    authenticationBuilder.AddMicrosoftAccount(options =>
    {
        options.ClientId = microsoftClientId;
        options.ClientSecret = microsoftClientSecret;
    });
}

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorPages()
   .WithStaticAssets();

app.Run();
