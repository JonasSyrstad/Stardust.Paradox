using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using Stardust.Paradox.GremlinStudio.Core.Variables;

namespace Stardust.Paradox.GremlinStudio.ViewModels;

public sealed partial class VariableSetItemViewModel : ObservableObject
{
    public VariableSetItemViewModel(QueryVariableSet set)
    {
        Set = set;
    }

    public QueryVariableSet Set { get; private set; }

    public string Id => Set.Id;

    public string Name => Set.Name;

    public string Json => Set.Json;

    /// <summary>
    /// Short summary of the variables for display in the list.
    /// </summary>
    public string VariableSummary
    {
        get
        {
            try
            {
                using var doc = JsonDocument.Parse(Set.Json);
                var keys = doc.RootElement.EnumerateObject().Select(p => p.Name).ToList();
                var summary = string.Join(", ", keys.Take(4));
                if (keys.Count > 4) summary += ", …";

                var connCount = Set.ConnectionVariables?.Count ?? 0;
                if (connCount > 0)
                    summary += $" (+{connCount} conn)";

                return string.IsNullOrEmpty(summary) ? "(empty)" : summary;
            }
            catch
            {
                return "(invalid JSON)";
            }
        }
    }

    public void Update(QueryVariableSet set)
    {
        Set = set;
        OnPropertyChanged(nameof(Set));
        OnPropertyChanged(nameof(Id));
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(Json));
        OnPropertyChanged(nameof(VariableSummary));
    }
}
