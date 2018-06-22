namespace Stardust.Paradox.Data.CodeGeneration.Sample
{
    public interface IClass1:IVertex
    {
        string Id { get; }

        string Name { get; set; }

        long Age { get; set; }

        string Email { get; set; }

        bool ValidatedEmail { get; set; }

        IEdgeCollection<IClass1> Parent { get;}

        IInlineCollection<string> Inline { get;}
    }
}