using BulkMessaging.Jobs;
using BulkMessaging.Models;
using BulkMessaging.Services;
using Microsoft.Extensions.FileProviders;
using Quartz;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();

// Add JSON database services
builder.Services.AddSingleton<JsonDatabaseService<User>>(_ => new JsonDatabaseService<User>("users.json"));
builder.Services.AddSingleton<JsonDatabaseService<AuditLog>>(_ => new JsonDatabaseService<AuditLog>("auditlogs.json"));

// Add application services
builder.Services.AddSingleton<AuditLogService>();
builder.Services.AddSingleton<UserService>();
builder.Services.AddSingleton<AuthService>();
builder.Services.AddSingleton<TemplateService>();
builder.Services.AddSingleton<CampaignService>();
builder.Services.AddSingleton<MessageService>();
builder.Services.AddHostedService<ScheduledMessageDispatcher>();
builder.Services.AddHostedService<ScheduledMessageReconciliationService>();
builder.Services.AddScoped<ContactCleanerService>();

builder.Services.AddQuartz();
builder.Services.AddQuartzHostedService(opt =>
{
    opt.WaitForJobsToComplete = true; // finish in-flight sends on shutdown
});

builder.Services.AddScoped<CampaignMessageSender>();

// Add authentication
builder.Services.AddAuthentication("CustomAuth")
    .AddCookie("CustomAuth", options =>
    {
        options.LoginPath = "/Auth/Login";
        options.LogoutPath = "/Auth/Logout";
        options.AccessDeniedPath = "/Auth/Login";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
        options.Cookie.Name = "BulkMessaging.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    });

builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "RequestVerificationToken";
});


builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("SuperAdmin", "Admin"));
    options.AddPolicy("SuperAdminOnly", policy => policy.RequireRole("SuperAdmin"));
});

var app = builder.Build();

// Initialize default admin
using (var scope = app.Services.CreateScope())
{
    var authService = scope.ServiceProvider.GetRequiredService<AuthService>();
    await authService.InitializeDefaultAdminAsync();
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
var uploadsPath = @"C:\BulkMessager\Templates\Uploads";
Directory.CreateDirectory(uploadsPath); // make sure it exists at startup
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(uploadsPath),
    RequestPath = "/uploads"
});

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Auth}/{action=Login}/{id?}");

// Redirect root to login page
app.MapGet("/", context =>
{
    context.Response.Redirect("/Auth/Login");
    return Task.CompletedTask;
});

app.Run();
