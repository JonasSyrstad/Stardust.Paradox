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

    public void Update(QueryVariableSet set)
    {
        Set = set;
        OnPropertyChanged(nameof(Set));
        OnPropertyChanged(nameof(Id));
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(Json));
    }
}
