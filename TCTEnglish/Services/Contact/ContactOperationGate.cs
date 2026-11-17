using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.DataProtection;

namespace TCTEnglish.Services.Contact;

public enum ContactSubmissionStatus
{
    Accepted, ConfigurationUnavailable, Failed, Unknown, Throttled, Duplicate, InvalidOperation, InvalidInput
}

/// <summary>
/// Per-singleton, bounded admission/replay guard. No form content or raw client address is cached.
/// This deliberately does not promise deduplication across processes or restarts.
/// </summary>
public sealed class ContactOperationGate
{
    public static readonly TimeSpan NonceLifetime = TimeSpan.FromMinutes(15);
    public static readonly TimeSpan RateWindow = TimeSpan.FromMinutes(10);
    public const int AttemptsPerWindow = 3;
    public const int OperationCapacity = 4096;
    public const int ClientCapacity = 2048;
    private readonly object _sync = new();
    private readonly IDataProtector _protector;
    private readonly TimeProvider _time;
    private readonly byte[] _partitionKey = RandomNumberGenerator.GetBytes(32);
    private readonly Dictionary<string, Operation> _operations = new();
    private readonly Dictionary<string, ClientWindow> _clients = new();

    private sealed record Operation(DateTimeOffset Expires, bool Pending);
    private sealed record ClientWindow(DateTimeOffset Expires, int Attempts);

    public ContactOperationGate(IDataProtectionProvider provider, TimeProvider? time = null)
    {
        _protector = provider.CreateProtector("TCTEnglish.Contact.Operation.v1");
        _time = time ?? TimeProvider.System;
    }

    private string Partition(string client) => Convert.ToHexString(HMACSHA256.HashData(_partitionKey, Encoding.UTF8.GetBytes(client)));

    public string Issue(string client)
    {
        var issued = _time.GetUtcNow().ToUnixTimeSeconds();
        return _protector.Protect($"{Guid.NewGuid():N}|{issued}|{Partition(client)}");
    }

    public ContactSubmissionStatus? TryBegin(string nonce, string client, out string operationId)
    {
        operationId = string.Empty;
        if (string.IsNullOrEmpty(nonce) || nonce.Length > 2048) return ContactSubmissionStatus.InvalidOperation;
        string[] parts;
        try { parts = _protector.Unprotect(nonce).Split('|'); }
        catch (CryptographicException) { return ContactSubmissionStatus.InvalidOperation; }
        var now = _time.GetUtcNow();
        if (parts.Length != 3 || !Guid.TryParseExact(parts[0], "N", out _)
            || !long.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var issuedSeconds)
            || issuedSeconds < DateTimeOffset.MinValue.ToUnixTimeSeconds()
            || issuedSeconds > DateTimeOffset.MaxValue.ToUnixTimeSeconds())
            return ContactSubmissionStatus.InvalidOperation;
        var issued = DateTimeOffset.FromUnixTimeSeconds(issuedSeconds);
        var partition = Partition(client);
        if (issued > now || now - issued >= NonceLifetime || parts[2] != partition)
            return ContactSubmissionStatus.InvalidOperation;

        lock (_sync)
        {
            // Never evict an active dispatch, even if a transport fails to honor its deadline.
            foreach (var key in _operations.Where(p => !p.Value.Pending && p.Value.Expires <= now).Select(p => p.Key).ToArray())
                _operations.Remove(key);
            foreach (var key in _clients.Where(p => p.Value.Expires <= now).Select(p => p.Key).ToArray())
                _clients.Remove(key);
            if (_operations.ContainsKey(parts[0])) return ContactSubmissionStatus.Duplicate;
            if (_operations.Count >= OperationCapacity) return ContactSubmissionStatus.Throttled;
            if (_clients.TryGetValue(partition, out var window))
            {
                if (window.Attempts >= AttemptsPerWindow) return ContactSubmissionStatus.Throttled;
            }
            else
            {
                if (_clients.Count >= ClientCapacity) return ContactSubmissionStatus.Throttled;
                window = new ClientWindow(now + RateWindow, 0);
            }
            _clients[partition] = window with { Attempts = window.Attempts + 1 };
            // Retain through the entire nonce lifetime plus the maximum transport deadline.
            _operations.Add(parts[0], new Operation(issued + NonceLifetime + TimeSpan.FromSeconds(60), true));
            operationId = parts[0];
            return null;
        }
    }

    public void Complete(string operationId)
    {
        lock (_sync)
        {
            if (_operations.TryGetValue(operationId, out var operation))
                _operations[operationId] = operation with { Pending = false };
        }
    }
}
