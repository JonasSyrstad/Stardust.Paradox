using System.Collections.Generic;

namespace Stardust.Paradox.Data
{
    public struct Pair<T1, T2> where T1 : class where T2 : class
    {
        public Pair(T1 item1, T2 item2)
        {
            Item1 = item1;
            Item2 = item2;
        }

        public Pair(T2 item2, T1 item1)
        {
            Item1 = item1;
            Item2 = item2;
        }

        public T1 Item1 { get; set; }
        public T2 Item2 { get; set; }

        internal bool IsDeleted()
        {
            return Item1 == null && Item2 == null;
        }

        internal void Delete()
        {
            Item2 = null;
            Item1 = null;
        }

        public override bool Equals(object obj)
        {
            if (!(obj is Pair<T1, T2>) && !(obj is Pair<T2, T1>)) return false;

            var pair = (Pair<T1, T2>) obj;
            return EqualityComparer<T1>.Default.Equals(Item1, pair.Item1) &&
                   EqualityComparer<T2>.Default.Equals(Item2, pair.Item2);
        }

        public bool Equals(Pair<T1, T2> other)
        {
            return EqualityComparer<T1>.Default.Equals(Item1, other.Item1) &&
                   EqualityComparer<T2>.Default.Equals(Item2, other.Item2);
        }

        public bool Equals(Pair<T2, T1> other)
        {
            return EqualityComparer<T1>.Default.Equals(Item1, other.Item2) &&
                   EqualityComparer<T2>.Default.Equals(Item2, other.Item1);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (EqualityComparer<T1>.Default.GetHashCode(Item1) * 397) ^
                       EqualityComparer<T2>.Default.GetHashCode(Item2);
            }
        }

        public static implicit operator Pair<T2, T1>(Pair<T1, T2> pair)
        {
            return new Pair<T2, T1>(pair.Item2, pair.Item1);
        }

        public static implicit operator Pair<T1, T2>(KeyValuePair<T1, T2> pair)
        {
            return new Pair<T2, T1>(pair.Key, pair.Value);
        }

        public static implicit operator T2(Pair<T1, T2> pair)
        {
            return pair.Item2;
        }

        public static implicit operator T1(Pair<T1, T2> pair)
        {
            return pair.Item1;
        }
    }
}