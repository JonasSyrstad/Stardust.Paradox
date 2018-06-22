using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Stardust.Paradox.Data.Internals
{
    public class DualDictionary<T1, T2> : IDualDictionary<T1, T2> where T2 : class where T1 : class
    {
        private readonly Dictionary<T1, int> _lookup1;
        private readonly Dictionary<T2, int> _lookup2;
        private readonly List<Pair<T1, T2>> _container;

        private readonly ReaderWriterLockSlim _synclock = new ReaderWriterLockSlim();
        #region constructors
        public DualDictionary()
        {
            _lookup1 = new Dictionary<T1, int>();
            _lookup2 = new Dictionary<T2, int>();
            _container = new List<Pair<T1, T2>>();
        }

        public DualDictionary([NotNull]IEnumerable<Pair<T1, T2>> pairs)
        {
            if (pairs == null) throw new ArgumentNullException(nameof(pairs));
            _lookup1 = new Dictionary<T1, int>();
            _lookup2 = new Dictionary<T2, int>();
            var i = 0;
            var enumerable = pairs as Pair<T1, T2>[] ?? pairs.ToArray();
            foreach (var pair in enumerable)
            {
                _lookup1.Add(pair.Item1, i);
                _lookup2.Add(pair.Item2, i);
                i++;
            }
            _container = enumerable.ToList();
        }

        public DualDictionary([NotNull]IEnumerable<Pair<T2, T1>> pairs)
        {
            if (pairs == null) throw new ArgumentNullException(nameof(pairs));
            _lookup1 = new Dictionary<T1, int>();
            _lookup2 = new Dictionary<T2, int>();
            var i = 0;
            var enumerable = pairs as Pair<T2, T1>[] ?? pairs.ToArray();
            foreach (var pair in enumerable)
            {
                _lookup1.Add(pair.Item2, i);
                _lookup2.Add(pair.Item1, i);
                i++;
            }
            _container = enumerable.Select(pair => (Pair<T1, T2>)pair).ToList();
        }
        #endregion

        public IEnumerator<Pair<T1, T2>> GetEnumerator()
        {
            _synclock.TryEnterReadLock(2);
            try
            {
                return _container.Where(p => !p.IsDeleted()).GetEnumerator();
            }
            finally
            {
                _synclock.ExitReadLock();
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public T2 this[T1 key]
        {
            get
            {
                _synclock.TryEnterReadLock(2);
                try
                {
                    return _container[_lookup1[key]];
                }
                finally
                {
                    _synclock.ExitReadLock();
                }
            }
        }

        public T1 this[T2 key]
        {
            get
            {
                _synclock.TryEnterReadLock(2);
                try
                {
                    return _container[_lookup2[key]];
                }
                finally
                {
                    _synclock.ExitReadLock();
                }

            }
        }

        Pair<T1, T2> IDualDictionary<T1, T2>.this[int index]
        {
            get
            {
                _synclock.TryEnterReadLock(2);
                try
                {
                    return _container[index];
                }
                finally
                {
                    _synclock.ExitReadLock();
                }

            }
        }

        public void Add(T1 item1, T2 item2)
        {
            Add(new Pair<T2, T1>(item1, item2));
        }

        public void Add(Pair<T1, T2> pair)
        {
            _synclock.TryEnterWriteLock(10);
            try
            {
                var i = _container.Count;
                _container.Add(pair);
                _lookup1.Add(pair.Item1, i);
                _lookup2.Add(pair.Item2, i);
            }
            finally
            {
                _synclock.ExitWriteLock();
            }
        }

        public void Remove(T1 key)
        {
            _synclock.TryEnterWriteLock(10);
            try
            {
                var other = this[key];
                var i = _lookup1[key];
                _container[i].Delete();
                _lookup1.Remove(key);
                _lookup2.Remove(other);
            }
            finally
            {
                _synclock.ExitWriteLock();
            }
        }

        public void Remove(T2 key)
        {
           Remove(this[key]);
        }

        public void Add(Pair<T2, T1> pair)
        {
            Add((Pair<T1, T2>)pair);
        }

        public void Add(T2 item2, T1 item1)
        {
            Add(new Pair<T2, T1>(item1, item2));
        }

        public bool ContainsKey(T1 key)
        {
            return _lookup1.ContainsKey(key);
        }

        public bool ContainsKey(T2 key)
        {
            return _lookup2.ContainsKey(key);
        }
    }
}