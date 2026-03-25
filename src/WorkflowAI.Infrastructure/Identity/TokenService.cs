using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using WorkflowAI.Application.Common.Interfaces;

namespace WorkflowAI.Infrastructure.Identity;

public sealed class TokenService(string signingKey) : ITokenService
{
    public string GenerateApprovalToken(Guid approvalRequestId, string action, DateTime expiresAt)
    {
        var payload = new ApprovalTokenPayload(approvalRequestId, action, expiresAt);
        var json = JsonSerializer.Serialize(payload);
        var data = Encoding.UTF8.GetBytes(json);
        var signature = ComputeHmac(data);

        var combined = new byte[data.Length + signature.Length];
        Buffer.BlockCopy(data, 0, combined, 0, data.Length);
        Buffer.BlockCopy(signature, 0, combined, data.Length, signature.Length);

        return Convert.ToBase64String(combined);
    }

    public ApprovalTokenPayload? ValidateApprovalToken(string token)
    {
        try
        {
            var combined = Convert.FromBase64String(token);
            if (combined.Length <= 32) return null;

            var data = combined[..^32];
            var providedSignature = combined[^32..];
            var expectedSignature = ComputeHmac(data);

            if (!CryptographicOperations.FixedTimeEquals(providedSignature, expectedSignature))
                return null;

            var json = Encoding.UTF8.GetString(data);
            var payload = JsonSerializer.Deserialize<ApprovalTokenPayload>(json);

            if (payload is null || payload.ExpiresAt < DateTime.UtcNow)
                return null;

            return payload;
        }
        catch
        {
            return null;
        }
    }

    private byte[] ComputeHmac(byte[] data)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(signingKey));
        return hmac.ComputeHash(data);
    }
}
