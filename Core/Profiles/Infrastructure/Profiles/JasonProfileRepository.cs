using PIT.Core.Profiles;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PIT.Infrastructure.Profiles;

public sealed class JsonProfileRepository : IProfileRepository
{
    private readonly string _profilesDirectory;

    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        Converters =
        {
            new JsonStringEnumConverter()
        }
    };

    public JsonProfileRepository()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        _profilesDirectory = Path.Combine(appData, "PIT", "Profiles");

        Directory.CreateDirectory(_profilesDirectory);
    }

    public async Task<IReadOnlyList<AutomationProfile>> LoadAllAsync(CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_profilesDirectory);

        var files = Directory.GetFiles(_profilesDirectory, "*.json");
        var profiles = new List<AutomationProfile>();

        foreach (var file in files)
        {
            try
            {
                await using var stream = File.OpenRead(file);

                var profile = await JsonSerializer.DeserializeAsync<AutomationProfile>(
                    stream,
                    _jsonOptions,
                    cancellationToken);

                if (profile is null)
                {
                    continue;
                }

                profile.Macros ??= new();
                profile.Schemes ??= new();
                profile.TriggerBindings ??= new();

                profiles.Add(profile);
            }
            catch
            {
                // Uszkodzony/nieaktualny profil pomijamy, żeby aplikacja dalej startowała.
            }
        }

        return profiles
            .GroupBy(x => x.Id)
            .Select(x => x.First())
            .OrderBy(x => x.Name)
            .ToList();
    }

    public async Task SaveAsync(AutomationProfile profile, CancellationToken cancellationToken = default)
    {
        profile.UpdatedAt = DateTime.Now;
        profile.Macros ??= new();
        profile.Schemes ??= new();
        profile.TriggerBindings ??= new();

        Directory.CreateDirectory(_profilesDirectory);
        DeleteExistingFilesForProfile(profile.Id);

        var safeName = MakeSafeFileName(profile.Name);
        var filePath = Path.Combine(_profilesDirectory, $"{safeName}_{profile.Id}.json");

        await using var stream = File.Create(filePath);

        await JsonSerializer.SerializeAsync(
            stream,
            profile,
            _jsonOptions,
            cancellationToken);
    }

    public Task DeleteAsync(AutomationProfile profile, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_profilesDirectory);
        DeleteExistingFilesForProfile(profile.Id);

        return Task.CompletedTask;
    }

    private void DeleteExistingFilesForProfile(Guid profileId)
    {
        var files = Directory.GetFiles(_profilesDirectory, $"*_{profileId}.json");

        foreach (var file in files)
        {
            try
            {
                File.Delete(file);
            }
            catch
            {
                // Ignorujemy zablokowany plik; kolejny zapis spróbuje ponownie.
            }
        }
    }

    private static string MakeSafeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();

        var safe = new string(value
            .Select(ch => invalid.Contains(ch) ? '_' : ch)
            .ToArray());

        return string.IsNullOrWhiteSpace(safe)
            ? "Profile"
            : safe;
    }
}
