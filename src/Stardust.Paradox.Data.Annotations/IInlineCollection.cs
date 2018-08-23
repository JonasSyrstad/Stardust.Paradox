using System.Collections;
using System.Collections.Generic;

namespace Stardust.Paradox.Data.Annotations
{
    public interface IInlineCollection : IEnumerable
    {
        string ToTransferData();

        IInlineCollection LoadFromTransferData(string data);
    }

    public interface IInlineCollection<T> : IEnumerable<T>, IInlineCollection
    {
        void AddRange(IEnumerable<T> items);
    }
}