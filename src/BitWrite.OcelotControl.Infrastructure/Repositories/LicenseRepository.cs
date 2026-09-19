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
            });

        var license = License.Create(
            GetEntry(entries, "Name"),
            GetEntry(entries, "ProductCode"),
            DateTimeOffset.Parse(GetEntry(entries, "ExpirationDate")),
            int.Parse(GetEntry(entries, "MaxGateways")),
            int.Parse(GetEntry(entries, "MaxRoutes")),
            features,
            string.Empty
        );

        // Use reflection to set internal fields since License.Create sets them
        var id = LicenseId.From(Guid.Parse(GetEntry(entries, "Id")));
        var status = LicenseStatus.From(GetEntry(entries, "Status"));
        var createdAt = DateTimeOffset.Parse(GetEntry(entries, "CreatedAt"));
        var updatedAt = DateTimeOffset.Parse(GetEntry(entries, "UpdatedAt"));
        var activatedAt = DateTimeOffset.TryParse(GetEntry(entries, "ActivatedAt"), out var act) ? act : (DateTimeOffset?)null;
        var revokedAt = DateTimeOffset.TryParse(GetEntry(entries, "RevokedAt"), out var rev) ? rev : (DateTimeOffset?)null;
        var revocationReason = GetEntry(entries, "RevocationReason");

        // Use reflection to set private fields
        typeof(License).GetProperty(nameof(License.Id))!.SetValue(license, id);
        typeof(License).GetProperty(nameof(License.Status))!.SetValue(license, LicenseStatus.From(GetEntry(entries, "Status")));
        typeof(License).GetProperty(nameof(License.CreatedAt))!.SetValue(license, createdAt);
        typeof(License).GetProperty(nameof(License.UpdatedAt))!.SetValue(license, updatedAt);
        typeof(License).GetProperty(nameof(License.ActivatedAt))!.SetValue(license, activatedAt);
        typeof(License).GetProperty(nameof(License.RevokedAt))!.SetValue(license, revokedAt);
        typeof(License).GetProperty(nameof(License.RevocationReason))!.SetValue(license, revocationReason);

        return license;
    }

    private static double ToUnixTimestamp(DateTimeOffset dateTime)
    {
        return dateTime.ToUnixTimeSeconds();
    }
}