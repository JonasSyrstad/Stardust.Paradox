namespace Stardust.Paradox.Data.Annotations
{
    public interface IGraphEntity
    {
        string Label { get; }
        event PropertyChangedHandler PropertyChanged;
        event PropertyChangingHandler PropertyChanging;
    }


    public interface IVertex : IGraphEntity
    {
    }

    public interface IDynamicGraphEntity
    {
        string[] DynamicPropertyNames { get; }
        object GetProperty(string propertyName);

        void SetProperty(string propertyName, object value);
    }
}