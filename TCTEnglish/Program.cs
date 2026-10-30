using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Facebook;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Mvc.Razor.RuntimeCompilation;
using Microsoft.EntityFrameworkCore;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure;
using TCTEnglish.Hubs;
using TCTEnglish.Infrastructure;
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
var mvc = builder.Services.AddControllersWithViews();
if (builder.Environment.IsDevelopment()
    && !builder.Configuration.GetValue<bool>("Testing:DisableRuntimeCompilation"))
{
    mvc.AddRazorRuntimeCompilation();
}
builder.Services.AddDbContext<DbflashcardContext>(options =>
{
    var settings = DatabaseSettings.Read(builder.Configuration, builder.Environment.IsDevelopment());
    options.UseMySql(settings.ConnectionString, settings.ServerVersion);
});

builder.Services.AddSingleton<IAppEmailSender, SmtpAppEmailSender>();
builder.Services.AddScoped<IFileStorageService, LocalFileStorageService>();
builder.Services.AddScoped<IAvatarUploadService, AvatarUploadService>();
builder.Services.AddScoped<IClassService, ClassService>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<TCTEnglish.Services.Notifications.LearningBusinessClock>();
builder.Services.AddScoped<TCTEnglish.Services.Learning.DirectNotificationGate>();
builder.Services.AddScoped<TCTEnglish.Services.Learning.ILearningNotificationIntentWriter, TCTEnglish.Services.Learning.LearningNotificationIntentWriter>();
builder.Services.AddScoped<IStreakService, StreakService>();
builder.Services.AddScoped<IGoalsService, GoalsService>();
builder.Services.AddHttpClient();
builder.Services.AddScoped<IStudyService, StudyService>();
builder.Services.AddOptions<TCTEnglish.Services.Writing.WritingEvaluationOptions>()
    .BindConfiguration(TCTEnglish.Services.Writing.WritingEvaluationOptions.SectionName);
builder.Services.AddOptions<TCTEnglish.Services.Speaking.SpeechProcessingOptions>()
    .BindConfiguration(TCTEnglish.Services.Speaking.SpeechProcessingOptions.SectionName);
builder.Services.AddSingleton<TCTEnglish.Services.Speaking.ISpeechInferenceLimiter, TCTEnglish.Services.Speaking.SpeechInferenceLimiter>();
builder.Services.AddHttpClient<TCTEnglish.Services.Speaking.ISpeechTranscriber, TCTEnglish.Services.Speaking.WhisperHttpTranscriber>((sp, client) =>
{
    var speechOptions = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<TCTEnglish.Services.Speaking.SpeechProcessingOptions>>().Value;
    if (speechOptions.IsConfigured
        && Uri.TryCreate(speechOptions.WhisperHttpBaseUrl.TrimEnd('/') + "/", UriKind.Absolute, out var baseAddress))
    {
        client.BaseAddress = baseAddress;
    }

    client.Timeout = TimeSpan.FromSeconds(Math.Clamp(speechOptions.InferenceTimeoutSeconds, 1, 60) + 2);
});
builder.Services.AddScoped<TCTEnglish.Services.Speaking.ISpeakingAssessmentService, TCTEnglish.Services.Speaking.SpeakingAssessmentService>();
builder.Services.AddScoped<TCTEnglish.Services.Speaking.ISpeakingImportJobService, TCTEnglish.Services.Speaking.SpeakingImportJobService>();
builder.Services.AddScoped<TCTEnglish.Services.Learning.ICompletionCreditService, TCTEnglish.Services.Learning.CompletionCreditService>();
builder.Services.AddScoped<TCTEnglish.Services.Notifications.ILearningOutboxDispatcher, TCTEnglish.Services.Notifications.LearningOutboxDispatcher>();
builder.Services.AddOptions<TCTEnglish.Services.Writing.LocalWritingEngineOptions>()
    .BindConfiguration(TCTEnglish.Services.Writing.LocalWritingEngineOptions.SectionName);
builder.Services.AddHttpClient<TCTEnglish.Services.Writing.ILocalGrammarChecker, TCTEnglish.Services.Writing.LanguageToolGrammarChecker>((sp, client) =>
{
    var opts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<TCTEnglish.Services.Writing.LocalWritingEngineOptions>>().Value;
    if (!string.IsNullOrWhiteSpace(opts.LanguageToolBaseUrl)
        && Uri.TryCreate(opts.LanguageToolBaseUrl.TrimEnd('/') + "/", UriKind.Absolute, out var baseAddress))
    {
        client.BaseAddress = baseAddress;
    }
});
builder.Services.AddHttpClient<TCTEnglish.Services.Writing.ILocalWritingModel, TCTEnglish.Services.Writing.OllamaWritingModel>((sp, client) =>
{
    var opts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<TCTEnglish.Services.Writing.LocalWritingEngineOptions>>().Value;
    if (!string.IsNullOrWhiteSpace(opts.OllamaBaseUrl)
        && Uri.TryCreate(opts.OllamaBaseUrl.TrimEnd('/') + "/", UriKind.Absolute, out var baseAddress))
    {
        client.BaseAddress = baseAddress;
    }
});
builder.Services.AddHostedService<SpeakingImportWorker>();
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
builder.Services.AddScoped<IKnowledgeRetriever, LearningAreaProgressRetriever>();
builder.Services.AddScoped<IKnowledgeRetriever, GoalsRetriever>();
builder.Services.AddScoped<IKnowledgeRetriever, NotificationRetriever>();
builder.Services.AddScoped<IKnowledgeRetriever, BillingRetriever>();
builder.Services.AddSingleton<IKnowledgeRetriever, SecurityPolicyRetriever>();
builder.Services.AddScoped<InternalKnowledgeProvider>();
builder.Services.AddScoped<IAiProviderClient, GeminiProviderClient>();
builder.Services.AddSingleton<IAiTokenCounter, SimpleAiTokenCounter>();
builder.Services.AddSingleton<IAiRequestRateLimiter, AiRequestRateLimiter>();
builder.Services.AddSingleton<IAiConversationExecutionGuard, AiConversationExecutionGuard>();
builder.Services.AddSingleton<IAiStreamingService, AiStreamingService>();
builder.Services.AddHostedService<AutoUnlockWorker>();
builder.Services.AddHostedService<NotificationWorker>();
builder.Services.AddScoped<IReadingTranslationService, ReadingTranslationService>();

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

if (app.Environment.IsDevelopment()
    && builder.Configuration.GetValue<bool>("Database:ApplyMigrationsOnStartup"))
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
    await dbContext.Database.MigrateAsync();
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = static context =>
    {
        if (!string.Equals(context.File.Name, "tct-release.json", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        context.Context.Response.ContentType = "application/json; charset=utf-8";
        context.Context.Response.Headers.CacheControl = "no-store, no-cache, max-age=0";
        context.Context.Response.Headers.Pragma = "no-cache";
    }
});
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapHub<ClassChatHub>("/classChatHub");
app.MapGet("/health/ready", async (DbflashcardContext db, CancellationToken cancellationToken) =>
{
    try
    {
        if (!await db.Database.CanConnectAsync(cancellationToken))
            return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
        var pending = await db.Database.GetPendingMigrationsAsync(cancellationToken);
        return pending.Any()
            ? Results.StatusCode(StatusCodes.Status503ServiceUnavailable)
            : Results.Ok(new { status = "ready" });
    }
    catch (Exception)
    {
        return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
    }
});

// Narrow JSON marker endpoint when the wwwroot file is absent or static files skipped.
app.MapGet("/tct-release.json", async (HttpContext http, IWebHostEnvironment env) =>
{
    var markerPath = Path.GetFullPath(Path.Combine(env.WebRootPath, "tct-release.json"));
    var webRoot = Path.GetFullPath(env.WebRootPath)
        .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
        + Path.DirectorySeparatorChar;
    if (!markerPath.StartsWith(webRoot, StringComparison.OrdinalIgnoreCase)
        || !System.IO.File.Exists(markerPath))
    {
        return Results.NotFound();
    }

    var bytes = await System.IO.File.ReadAllBytesAsync(markerPath);
    http.Response.Headers.CacheControl = "no-store, no-cache, max-age=0";
    http.Response.Headers.Pragma = "no-cache";
    return Results.Bytes(bytes, "application/json; charset=utf-8");
});

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

if (builder.Configuration.GetValue<bool>("Database:SeedOnStartup"))
{
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
}

app.Run();
