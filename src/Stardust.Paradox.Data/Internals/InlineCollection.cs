using Newtonsoft.Json;
using Stardust.Paradox.Data.Annotations;
using Stardust.Particles;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;

namespace Stardust.Paradox.Data.Internals
{

	internal class InlineCollection<T> : IInlineCollection<T>
	{
		internal static void SetSerializationType(string name, SerializationType type)
		{
			_serializationTypes.TryAdd(name, type);
		}

		private static ConcurrentDictionary<string, SerializationType> _serializationTypes = new ConcurrentDictionary<string, SerializationType>();
		private readonly IGraphEntityInternal _parent;
		private readonly string _name;
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

		private void WriteUpdateStatement()
		{
			_parent.OnPropertyChanged(this, _name);
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

		private SerializationType Serialization => !_serializationTypes.TryGetValue($"{_parent.GetType().FullName}.{_name}", out var i) ? SerializationType.ClearText : i;

		public override string ToString()
		{
			if (Serialization == SerializationType.ClearText)
				return JsonConvert.SerializeObject(this);
			return Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(this)));
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
			if (data.IsNullOrWhiteSpace()) return this;
			_internal = JsonConvert.DeserializeObject<List<T>>(data);
			return this;
		}
	}
}