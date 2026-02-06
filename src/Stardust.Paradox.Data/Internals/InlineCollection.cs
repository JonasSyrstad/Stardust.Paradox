using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using Newtonsoft.Json;
using Stardust.Paradox.Data.Annotations;
using Stardust.Particles;

namespace Stardust.Paradox.Data.Internals
{
    internal class InlineCollection<T> : IInlineCollection<T>
    {
        private static readonly ConcurrentDictionary<string, SerializationType> _serializationTypes =
            new ConcurrentDictionary<string, SerializationType>();

        private readonly string _name;
        private readonly IGraphEntityInternal _parent;
        private List<T> _internal;

        public InlineCollection()
        {
            _internal = new List<T>();
        }

        public InlineCollection(IGraphEntityInternal parent, string name) : this()
        {
            _parent = parent;
            _name = name;
        }

        public InlineCollection(string inlineString, GraphDataEntity parent, string name) : this(parent, name)
        {
            _internal = JsonConvert.DeserializeObject<List<T>>(inlineString);
        }

        private SerializationType Serialization =>
            !_serializationTypes.TryGetValue($"{_parent.GetType().FullName}.{_name}", out var i)
                ? SerializationType.ClearText
                : i;

        public IEnumerator<T> GetEnumerator()
        {
            return _internal.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public void Add(T item)
        {
            _internal.Add(item);
            WriteUpdateStatement();
        }

        public void Clear()
        {
            _internal.Clear();
            _parent.OnPropertyChanged(this, _name);
        }

        public bool Contains(T item)
        {
            return _internal.Contains(item);
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            _internal.CopyTo(array, arrayIndex);
        }

        public bool Remove(T item)
        {
            var result = _internal.Remove(item);
            if (result)
                _parent.OnPropertyChanged(this, _name);
            return result;
        }

        public int Count => _internal.Count;
        public bool IsReadOnly => false;

        public void AddRange(IEnumerable<T> items)
        {
            _internal.AddRange(items);
            _parent.OnPropertyChanged(this, _name);
        }

        string IInlineCollection.ToTransferData()
        {
            return ToString();
        }

        IInlineCollection IInlineCollection.LoadFromTransferData(string data)
        {
            if (data.IsNullOrWhiteSpace()) return this;
            if (Serialization == SerializationType.Base64)
                data = Encoding.UTF8.GetString(Convert.FromBase64String(data));
            if (Serialization == SerializationType.Compressed)
                data = Encoding.UTF8.GetString(Decompress(Convert.FromBase64String(data)));
            if (data.IsNullOrWhiteSpace()) return this;
            _internal = JsonConvert.DeserializeObject<List<T>>(data);
            return this;
        }

        internal static void SetSerializationType(string name, SerializationType type)
        {
            _serializationTypes.TryAdd(name, type);
        }

        private void WriteUpdateStatement()
        {
            _parent.OnPropertyChanged(this, _name);
        }

        public override string ToString()
        {
            switch (Serialization)
            {
                case SerializationType.ClearText:
                    return JsonConvert.SerializeObject(this);
                case SerializationType.Base64:
                    return Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(this)));
                case SerializationType.Compressed:
                default:
                    return Convert.ToBase64String(Compress(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(this))));
            }
        }

        private static byte[] Compress(byte[] data)
        {
            using (var compressedStream = new MemoryStream())
            using (var zipStream = new GZipStream(compressedStream, CompressionMode.Compress))
            {
                zipStream.Write(data, 0, data.Length);
                zipStream.Close();
                return compressedStream.ToArray();
            }
        }

        private static byte[] Decompress(byte[] data)
        {
            using (var compressedStream = new MemoryStream(data))
            using (var zipStream = new GZipStream(compressedStream, CompressionMode.Decompress))
            using (var resultStream = new MemoryStream())
            {
                zipStream.CopyTo(resultStream);
                return resultStream.ToArray();
            }
        }
    }
}