using Stardust.Paradox.Data.Annotations;

namespace Stardust.Paradox.Data.Internals
{
    public interface IEdgeEntityInternal : IEdgeEntity
    {
        void SetInVertex(IVertex inVertex);
        void SetOutVertex(IVertex outVertex);
	    void Reset(bool b);
    }
}