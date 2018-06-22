using System.Collections.Generic;

namespace Stardust.Paradox.Data
{
    public interface IDualDictionary<T1, T2> : IEnumerable<Pair<T1, T2>> where T1 : class where T2 : class
    {
        T2 this[T1 key] { get; }
        T1 this[T2 key] { get; }

        Pair<T1, T2> this[int index] { get; }

        void Add(T1 item1, T2 item2);

        void Add(Pair<T1, T2> pair);

        void Add(Pair<T2, T1> pair);
        void Add(T2 item2, T1 item1);


    }
}