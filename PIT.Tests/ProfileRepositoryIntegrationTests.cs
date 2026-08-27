using PIT.Core.Automation;
using PIT.Core.Profiles;
using PIT.Infrastructure.Profiles;
using System.Collections.ObjectModel;

namespace PIT.Tests;

public sealed class ProfileRepositoryIntegrationTests
{
    [Fact]
    public async Task Profile_with_macro_round_trips_through_json_storage()
    {
        var directory = Path.Combine(Path.GetTempPath(), "pit-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var previous = Environment.GetEnvironmentVariable("PIT_DATA_DIRECTORY");
        try
        {
            Environment.SetEnvironmentVariable("PIT_DATA_DIRECTORY", directory);
            var repository = new JsonProfileRepository();
            var profile = new AutomationProfile
            {
                Name = "Demo profile",
                Macros = new List<MacroDefinition>
                {
                    new()
                    {
                        Name = "Wait and continue",
                        Steps = new ObservableCollection<MacroStep>
                        {
                            new() { Order = 1, Action = new ActionDefinition { Kind = ActionKind.Delay } }
                        }
                    }
                }
            };

            await repository.SaveAsync(profile);
            var loaded = Assert.Single(await repository.LoadAllAsync());

            Assert.Equal(profile.Id, loaded.Id);
            Assert.Equal("Demo profile", loaded.Name);
            Assert.Equal("Wait and continue", Assert.Single(loaded.Macros).Name);

            await repository.DeleteAsync(loaded);
            Assert.Empty(await repository.LoadAllAsync());
        }
        finally
        {
            Environment.SetEnvironmentVariable("PIT_DATA_DIRECTORY", previous);
            Directory.Delete(directory, recursive: true);
        }
    }
}
