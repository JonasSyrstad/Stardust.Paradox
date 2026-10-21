using CommunityToolkit.Mvvm.ComponentModel;
using Stardust.Paradox.GremlinStudio.Core.Snippets;

namespace Stardust.Paradox.GremlinStudio.ViewModels;

public sealed partial class SnippetItemViewModel : ObservableObject
{
    public SnippetItemViewModel(QuerySnippet snippet)
    {
        Snippet = snippet;
    }

    public QuerySnippet Snippet { get; private set; }

    public string Id => Snippet.Id;

    public string Name => Snippet.Name;

    public string Query => Snippet.Query;

    public string Tags => string.Join(", ", Snippet.Tags ?? Array.Empty<string>());

    public void Update(QuerySnippet snippet)
    {
        Snippet = snippet;
        OnPropertyChanged(nameof(Snippet));
        OnPropertyChanged(nameof(Id));
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(Query));
        OnPropertyChanged(nameof(Tags));
    }
}
