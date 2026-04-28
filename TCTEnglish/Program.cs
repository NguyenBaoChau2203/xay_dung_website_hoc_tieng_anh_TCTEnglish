using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Facebook;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Mvc.Razor.RuntimeCompilation;
using Microsoft.EntityFrameworkCore;
using TCTEnglish.Hubs;
using TCTEnglish.Services.AI;
using TCTEnglish.Services.AI.Internal;
using TCTEnglish.Services.AI.Internal.Retrievers;
using TCTEnglish.Services.Billing;
using TCTVocabulary.Models;
using TCTVocabulary.Services;
using TCTVocabulary.Workers;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSignalR();
builder.Services.AddRazorPages()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler =
            System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
    });
builder.Services.AddControllersWithViews().AddRazorRuntimeCompilation();
builder.Services.AddDbContext<DbflashcardContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        sqlOptions =>
        {
            sqlOptions.EnableRetryOnFailure(5, TimeSpan.FromSeconds(10), null);
            sqlOptions.CommandTimeout(60);
        }));

builder.Services.AddSingleton<IAppEmailSender, SmtpAppEmailSender>();
builder.Services.AddScoped<IFileStorageService, LocalFileStorageService>();
builder.Services.AddScoped<IAvatarUploadService, AvatarUploadService>();
builder.Services.AddScoped<IClassService, ClassService>();
builder.Services.AddScoped<IStreakService, StreakService>();
builder.Services.AddScoped<IGoalsService, GoalsService>();
builder.Services.AddHttpClient();
builder.Services.AddScoped<IStudyService, StudyService>();
builder.Services.AddScoped<IWritingService, WritingService>();
builder.Services.AddScoped<IWritingAiEvaluationService, WritingAiEvaluationService>();
builder.Services.AddSingleton<IWritingRequestRateLimiter, WritingRequestRateLimiter>();
builder.Services.AddScoped<TCTEnglish.Services.IListeningService, TCTEnglish.Services.ListeningService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IYoutubeTranscriptService, YoutubeTranscriptService>();
builder.Services.AddScoped<IVocabSuggestService, VocabSuggestService>();
builder.Services.AddScoped<IPremiumAccessService, PremiumAccessService>();
builder.Services.AddScoped<ISubscriptionService, SubscriptionService>();
builder.Services.AddScoped<IBillingService, BillingService>();
builder.Services.AddOptions<TCTEnglish.Services.Billing.VnPay.VnPayOptions>()
    .BindConfiguration(TCTEnglish.Services.Billing.VnPay.VnPayOptions.SectionName)
    .ValidateOnStart();
builder.Services.AddOptions<TCTEnglish.Services.Billing.MoMo.MoMoOptions>()
    .BindConfiguration(TCTEnglish.Services.Billing.MoMo.MoMoOptions.SectionName)
    .ValidateOnStart();
builder.Services.AddScoped<IPaymentGateway, TCTEnglish.Services.Billing.VnPay.VnPayGateway>();
builder.Services.AddHttpClient<TCTEnglish.Services.Billing.MoMo.MoMoGateway>();
builder.Services.AddScoped<IPaymentGateway>(sp =>
    sp.GetRequiredService<TCTEnglish.Services.Billing.MoMo.MoMoGateway>());
builder.Services.AddScoped<IIpnService, IpnService>();
builder.Services.AddSingleton<IPaymentProviderHealthService, PaymentProviderHealthService>();
builder.Services.AddScoped<IPaymentAuditService, PaymentAuditService>();
// Reconciliation
builder.Services.AddScoped<IPaymentReconciliationService, PaymentReconciliationService>();
builder.Services.AddHttpClient<IVnPayQueryClient, TCTEnglish.Services.Billing.VnPay.VnPayQueryClient>();
builder.Services.AddOptions<TCTEnglish.Workers.ReconciliationWorkerOptions>()
    .BindConfiguration("ReconciliationWorker");
builder.Services.AddHostedService<TCTEnglish.Workers.PaymentReconciliationWorker>();
builder.Services.AddHostedService<TCTEnglish.Workers.PendingPaymentCleanupWorker>();
builder.Services.AddHostedService<TCTEnglish.Workers.PremiumExpiryWorker>();
builder.Services.AddOptions<AiOptions>()
    .Configure<IConfiguration>((options, configuration) =>
    {
        configuration.GetSection("AI").Bind(options);
    });
builder.Services.AddScoped<IAiConversationService, AiConversationService>();
builder.Services.AddScoped<IAiChatService, AiChatService>();
builder.Services.AddScoped<IAiObservabilityService, AiObservabilityService>();
builder.Services.AddScoped<IAiContextBuilder, AiContextBuilder>();
builder.Services.AddSingleton<DeterministicIntentClassifier>();
builder.Services.AddSingleton<IAiQueryClassifier, MlNetAiQueryClassifier>();
// Also register the concrete type so AiManagementController can inject it directly
// for the InvalidateModel() hot-reload call. Both resolve the same singleton instance.
builder.Services.AddSingleton<MlNetAiQueryClassifier>(
    sp => (MlNetAiQueryClassifier)sp.GetRequiredService<IAiQueryClassifier>());
builder.Services.AddScoped<IAnswerComposer, TemplateAnswerComposer>();
builder.Services.AddOptions<MlNetIntentClassifierOptions>()
    .BindConfiguration(MlNetIntentClassifierOptions.SectionName);
builder.Services.AddSingleton<MlNetIntentClassifierAssetResolver>();
builder.Services.AddScoped<MlNetIntentDatasetLoader>();
builder.Services.AddScoped<IMlNetTrainerService, MlNetTrainerService>();
builder.Services.AddSingleton<IKnowledgeRetriever, WebsiteGuideRetriever>();
builder.Services.AddScoped<IKnowledgeRetriever, UserVocabularyRetriever>();
builder.Services.AddScoped<IKnowledgeRetriever, LearningProgressRetriever>();
builder.Services.AddScoped<IKnowledgeRetriever, CardLookupRetriever>();
builder.Services.AddScoped<IKnowledgeRetriever, SpeakingRetriever>();
builder.Services.AddScoped<IKnowledgeRetriever, ClassRetriever>();
builder.Services.AddScoped<IKnowledgeRetriever, StudyRecommendationRetriever>();
builder.Services.AddScoped<InternalKnowledgeProvider>();
builder.Services.AddScoped<IAiProviderClient, GeminiProviderClient>();
builder.Services.AddSingleton<IAiTokenCounter, SimpleAiTokenCounter>();
builder.Services.AddSingleton<IAiRequestRateLimiter, AiRequestRateLimiter>();
builder.Services.AddSingleton<IAiConversationExecutionGuard, AiConversationExecutionGuard>();
builder.Services.AddSingleton<IAiStreamingService, AiStreamingService>();
builder.Services.AddHostedService<AutoUnlockWorker>();
builder.Services.AddHostedService<NotificationWorker>();

builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultSignInScheme = "ExternalCookie";
})
.AddCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.ExpireTimeSpan = TimeSpan.FromDays(30);
})
.AddCookie("ExternalCookie")
.AddGoogle(options =>
{
    options.ClientId = string.IsNullOrWhiteSpace(builder.Configuration["Authentication:Google:ClientId"])
        ? "dummy-client-id"
        : builder.Configuration["Authentication:Google:ClientId"]!;
    options.ClientSecret = string.IsNullOrWhiteSpace(builder.Configuration["Authentication:Google:ClientSecret"])
        ? "dummy-client-secret"
        : builder.Configuration["Authentication:Google:ClientSecret"]!;
    options.Scope.Add("profile");
    options.Events.OnCreatingTicket = ctx =>
    {
        var pictureUrl = ctx.User.GetProperty("picture").GetString();
        if (!string.IsNullOrEmpty(pictureUrl))
        {
            ctx.Identity?.AddClaim(new System.Security.Claims.Claim("picture", pictureUrl));
        }

        return System.Threading.Tasks.Task.CompletedTask;
    };
    options.Events.OnRemoteFailure = ctx =>
    {
        ctx.Response.Redirect("/Account/Login");
        ctx.HandleResponse();
        return System.Threading.Tasks.Task.CompletedTask;
    };
})
.AddFacebook(options =>
{
    options.AppId = string.IsNullOrWhiteSpace(builder.Configuration["Authentication:Facebook:AppId"])
        ? "dummy-app-id"
        : builder.Configuration["Authentication:Facebook:AppId"]!;
    options.AppSecret = string.IsNullOrWhiteSpace(builder.Configuration["Authentication:Facebook:AppSecret"])
        ? "dummy-app-secret"
        : builder.Configuration["Authentication:Facebook:AppSecret"]!;
    options.Events.OnRemoteFailure = ctx =>
    {
        ctx.Response.Redirect("/Account/Login");
        ctx.HandleResponse();
        return System.Threading.Tasks.Task.CompletedTask;
    };
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
    dbContext.Database.Migrate();
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapHub<ClassChatHub>("/classChatHub");

app.MapControllerRoute(
    name: "admin-speaking-management-legacy",
    pattern: "Admin/SpeakingManagement/{action=Index}/{id?}",
    defaults: new { area = "Admin", controller = "SpeakingVideoManagement" });

app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Dashboard}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Landing}/{id?}");

try
{
    await TCTVocabulary.Models.JsonVocabularySeeder.SeedFromJsonAsync(app.Services);
}
catch (Exception ex)
{
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    logger.LogError(ex, "JsonVocabularySeeder: Lỗi không mong đợi khi seed từ JSON.");
}

try
{
    await TCTVocabulary.Models.ListeningLessonSeedData.SeedAsync(app.Services);
}
catch (Exception ex)
{
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    logger.LogError(ex, "ListeningLessonSeedData: Lỗi không mong đợi khi seed dữ liệu luyện nghe.");
}

try
{
    await TCTEnglish.Models.BillingSeedData.SeedAsync(app.Services);
}
catch (Exception ex)
{
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    logger.LogError(ex, "BillingSeedData: Loi khi seed goi Premium.");
}

app.Run();
