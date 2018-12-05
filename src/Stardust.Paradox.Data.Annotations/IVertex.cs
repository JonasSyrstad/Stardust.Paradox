using System.Collections;

namespace Stardust.Paradox.Data.Annotations
{
    public interface IGraphEntity
    {
        string Label { get;  }
        event PropertyChangedHandler PropertyChanged;
        event PropertyChangingHandler PropertyChanging;
    }
   

    public interface IVertex : IGraphEntity
    {

    }

    //public interface IEdge: IGraphEntity
    //{ }
}