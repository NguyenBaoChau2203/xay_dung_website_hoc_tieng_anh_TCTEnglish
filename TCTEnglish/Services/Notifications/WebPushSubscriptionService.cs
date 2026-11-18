using Microsoft.EntityFrameworkCore;
using TCTEnglish.Models;
using TCTVocabulary.Models;
using System.Security.Cryptography;
using Microsoft.AspNetCore.WebUtilities;

namespace TCTEnglish.Services.Notifications;

public sealed class PushSubscriptionInput
{
    public string Endpoint { get; init; } = string.Empty;
    public string P256dh { get; init; } = string.Empty;
    public string Auth { get; init; } = string.Empty;
}

public sealed record PushSubscriptionResult(bool Succeeded, string? Error = null, bool FreshSubscriptionRequired = false);

public interface IWebPushSubscriptionService
{
    Task<PushSubscriptionResult> SubscribeAsync(int userId, PushSubscriptionInput input, CancellationToken cancellationToken = default);

    Task<bool> UnsubscribeAsync(int userId, string? endpoint, CancellationToken cancellationToken = default);
}

public sealed class WebPushSubscriptionService : IWebPushSubscriptionService
{
    private readonly DbflashcardContext _context;
    private readonly TimeProvider _timeProvider;

    public WebPushSubscriptionService(DbflashcardContext context, TimeProvider timeProvider)
    {
        _context = context;
        _timeProvider = timeProvider;
    }

    public async Task<PushSubscriptionResult> SubscribeAsync(int userId, PushSubscriptionInput input, CancellationToken cancellationToken = default)
    {
        if (!ValidEncryptionKeys(input.P256dh, input.Auth))
        {
            return new(false, "Khóa đăng ký push không hợp lệ.");
        }

        if (!WebPushEndpointGuard.TryNormalize(input.Endpoint, out var uri, out var error))
        {
            return new(false, error);
        }

        var endpoint = uri.AbsoluteUri;
        var hash = WebPushEndpointGuard.Hash(endpoint);
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var existing = await _context.WebPushSubscriptions.FirstOrDefaultAsync(row => row.EndpointHash == hash, cancellationToken);
        if (existing != null && existing.UserId != userId)
        {
            return new(false, "Thiết bị đang gắn với một tài khoản khác. Hãy tạo đăng ký trình duyệt mới.", true);
        }

        var owned = existing != null && existing.UserId == userId
            ? existing
            : await _context.WebPushSubscriptions.FirstOrDefaultAsync(row => row.UserId == userId && row.EndpointHash == hash, cancellationToken);
        if (owned == null)
        {
            if (await _context.WebPushSubscriptions.CountAsync(row => row.UserId == userId && row.RevokedAtUtc == null, cancellationToken) >= 10)
                return new(false, "Tài khoản đã có tối đa 10 thiết bị nhận thông báo.");
            owned = new WebPushSubscription
            {
                UserId = userId,
                EndpointHash = hash,
                CreatedAtUtc = now
            };
            _context.WebPushSubscriptions.Add(owned);
        }

        owned.Endpoint = endpoint;
        owned.P256dh = input.P256dh;
        owned.Auth = input.Auth;
        owned.RevokedAtUtc = null;
        owned.LastSeenAtUtc = now;
        try { await _context.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException)
        {
            _context.Entry(owned).State = EntityState.Detached;
            return new(false, "Đăng ký đã thay đổi ở một phiên khác. Hãy thử lại với đăng ký trình duyệt mới.", true);
        }
        return new(true);
    }

    private static bool ValidEncryptionKeys(string? p256dh, string? auth)
    {
        if (string.IsNullOrWhiteSpace(p256dh) || string.IsNullOrWhiteSpace(auth) || p256dh.Length > 200 || auth.Length > 200) return false;
        try
        {
            var point = WebEncoders.Base64UrlDecode(p256dh);
            var secret = WebEncoders.Base64UrlDecode(auth);
            if (point.Length != 65 || point[0] != 4 || secret.Length != 16) return false;
            using var key = ECDiffieHellman.Create(new ECParameters
            {
                Curve = ECCurve.NamedCurves.nistP256,
                Q = new ECPoint { X = point[1..33], Y = point[33..65] }
            });
            return true;
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException or CryptographicException) { return false; }
    }

    public async Task<bool> UnsubscribeAsync(int userId, string? endpoint, CancellationToken cancellationToken = default)
    {
        if (!WebPushEndpointGuard.TryNormalize(endpoint, out var uri, out _))
        {
            return false;
        }

        var hash = WebPushEndpointGuard.Hash(uri.AbsoluteUri);
        var row = await _context.WebPushSubscriptions.FirstOrDefaultAsync(
            item => item.UserId == userId && item.EndpointHash == hash && item.RevokedAtUtc == null,
            cancellationToken);
        if (row == null)
        {
            return false;
        }

        row.RevokedAtUtc = _timeProvider.GetUtcNow().UtcDateTime;
        row.Endpoint = "revoked";
        row.P256dh = "revoked";
        row.Auth = "revoked";
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
