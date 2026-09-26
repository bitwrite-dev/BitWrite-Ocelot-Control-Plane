using BitWrite.OcelotControl.Application.Interfaces;
using BitWrite.OcelotControl.Domain.Aggregates.License;
using BitWrite.OcelotControl.Domain.ValueObjects.Identity;
using BitWrite.OcelotControl.Domain.ValueObjects.Status;
using BitWrite.OcelotControl.Infrastructure.Redis;
using StackExchange.Redis;
using System.Text.Json;

namespace BitWrite.OcelotControl.Infrastructure.Repositories;

public class RedisLicenseRepository : RedisRepositoryBase, ILicenseRepository
{
    private const string LicensesIndexKey = "ocelot:index:licenses";
    private const string LicenseProductCodesIndexKey = "ocelot:index:license-product-codes";

    public RedisLicenseRepository(IConnectionMultiplexer connectionMultiplexer) 
        : base(connectionMultiplexer)
    {
    }

    public async Task<License?> GetAsync(LicenseId id, CancellationToken cancellationToken = default)
    {
        var key = RedisKeyHelper.License(id);
        var entries = await GetHashAsync(key);
        
        if (entries.Length == 0)
            return null;

        var license = DeserializeLicense(entries);
        return license;
    }

    public async Task<License?> GetByProductCodeAsync(string productCode, CancellationToken cancellationToken = default)
    {
        var licenseId = await Database.HashGetAsync(LicenseProductCodesIndexKey, productCode.ToUpperInvariant());
        if (licenseId.IsNullOrEmpty)
            return null;

        var id = LicenseId.From(Guid.Parse(licenseId!));
        return await GetAsync(id, cancellationToken);
    }

    public async Task<List<License>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var licenseIds = await Database.SortedSetRangeByScoreAsync(
            LicensesIndexKey, 
            double.NegativeInfinity, 
            double.PositiveInfinity, 
            Exclude.None, 
            Order.Descending);

        var licenses = new List<License>();

        foreach (var id in licenseIds)
        {
            try
            {
                var licenseId = LicenseId.From(Guid.Parse(id.ToString()));
                var license = await GetAsync(licenseId, cancellationToken);
                if (license != null)
                    licenses.Add(license);
            }
            catch
            {
                // Skip invalid IDs
            }
        }

        return licenses;
    }

    public async Task<List<License>> GetActiveAsync(CancellationToken cancellationToken = default)
    {
        var allLicenses = await GetAllAsync(cancellationToken);
        return allLicenses.Where(l => l.IsValid).ToList();
    }

    public async Task<List<License>> GetExpiredAsync(CancellationToken cancellationToken = default)
    {
        var allLicenses = await GetAllAsync(cancellationToken);
        return allLicenses.Where(l => l.IsExpired).ToList();
    }

    public async Task AddAsync(License license, CancellationToken cancellationToken = default)
    {
        var key = RedisKeyHelper.License(license.Id);
        var entries = SerializeLicense(license);

        var transaction = Database.CreateTransaction();
        transaction.HashSetAsync(key, entries);
        transaction.SortedSetAddAsync(LicensesIndexKey, license.Id.Value.ToString(), ToUnixTimestamp(license.CreatedAt));
        transaction.HashSetAsync(LicenseProductCodesIndexKey, license.ProductCode.ToUpperInvariant(), license.Id.Value.ToString());
        await transaction.ExecuteAsync();
    }

    public async Task UpdateAsync(License license, CancellationToken cancellationToken = default)
    {
        var key = RedisKeyHelper.License(license.Id);
        var entries = SerializeLicense(license);

        var transaction = Database.CreateTransaction();
        transaction.HashSetAsync(key, entries);
        transaction.SortedSetAddAsync(LicensesIndexKey, license.Id.Value.ToString(), ToUnixTimestamp(license.UpdatedAt));
        transaction.HashSetAsync(LicenseProductCodesIndexKey, license.ProductCode.ToUpperInvariant(), license.Id.Value.ToString());
        await transaction.ExecuteAsync();
    }

    public async Task DeleteAsync(LicenseId id, CancellationToken cancellationToken = default)
    {
        var key = RedisKeyHelper.License(id);
        
        var transaction = Database.CreateTransaction();
        transaction.KeyDeleteAsync(key);
        transaction.SortedSetRemoveAsync(LicensesIndexKey, id.Value.ToString());
        await transaction.ExecuteAsync();
    }

    private HashEntry[] SerializeLicense(License license)
    {
        var featuresJson = JsonSerializer.Serialize(license.Features, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        return new HashEntry[]
        {
            new("Id", license.Id.Value.ToString()),
            new("Name", license.Name),
            new("ProductCode", license.ProductCode),
            new("Status", license.Status.Value),
            new("ExpirationDate", license.ExpirationDate.ToString("O")),
            new("CreatedAt", license.CreatedAt.ToString("O")),
            new("UpdatedAt", license.UpdatedAt.ToString("O")),
            new("ActivatedAt", license.ActivatedAt?.ToString("O") ?? ""),
            new("RevokedAt", license.RevokedAt?.ToString("O") ?? ""),
            new("RevocationReason", license.RevocationReason ?? ""),
            new("MaxGateways", license.MaxGateways.ToString()),
            new("MaxRoutes", license.MaxRoutes.ToString()),
            new("Features", featuresJson)
        };
    }

    private License DeserializeLicense(HashEntry[] entries)
    {
        var featuresJson = GetEntry(entries, "Features");
        var features = string.IsNullOrEmpty(featuresJson)
            ? new List<LicenseFeature>()
            : JsonSerializer.Deserialize<List<LicenseFeature>>(featuresJson, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            }) ?? new List<LicenseFeature>();

        // Reconstitute, not Create. Create mints a fresh LicenseId, resets the
        // lifecycle state, and rejects an expiration date in the past — so this
        // used to rewrite seven properties through reflection just to undo what
        // Create had done, and listing licenses threw once one expired.
        return License.Reconstitute(
            LicenseId.From(Guid.Parse(GetEntry(entries, "Id"))),
            GetEntry(entries, "Name"),
            GetEntry(entries, "ProductCode"),
            LicenseStatus.From(GetEntry(entries, "Status")),
            DateTimeOffset.Parse(GetEntry(entries, "ExpirationDate")),
            int.Parse(GetEntry(entries, "MaxGateways")),
            int.Parse(GetEntry(entries, "MaxRoutes")),
            DateTimeOffset.Parse(GetEntry(entries, "CreatedAt")),
            DateTimeOffset.Parse(GetEntry(entries, "UpdatedAt")),
            DateTimeOffset.TryParse(GetEntry(entries, "ActivatedAt"), out var act) ? act : null,
            DateTimeOffset.TryParse(GetEntry(entries, "RevokedAt"), out var rev) ? rev : null,
            NullIfEmpty(GetEntry(entries, "RevocationReason")));
    }

    private static string? NullIfEmpty(string value) =>
        string.IsNullOrEmpty(value) ? null : value;

    private static double ToUnixTimestamp(DateTimeOffset dateTime)
    {
        return dateTime.ToUnixTimeSeconds();
    }
}