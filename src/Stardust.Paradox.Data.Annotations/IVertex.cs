namespace Stardust.Paradox.Data.Annotations
{
    public interface IVertex
    {
        string Label { get;  }
        event PropertyChangedHandler PropertyChanged;
        event PropertyChangingHandler PropertyChanging;
    }
}