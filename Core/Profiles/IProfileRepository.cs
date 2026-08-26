namespace PIT.Core.Profiles;

public interface IProfileRepository
{
    Task<IReadOnlyList<AutomationProfile>> LoadAllAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(AutomationProfile profile, CancellationToken cancellationToken = default);

    Task DeleteAsync(AutomationProfile profile, CancellationToken cancellationToken = default);
}
