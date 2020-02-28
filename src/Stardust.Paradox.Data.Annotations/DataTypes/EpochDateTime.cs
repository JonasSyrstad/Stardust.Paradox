using System;
using System.Runtime.Serialization;

namespace Stardust.Paradox.Data.Annotations.DataTypes
{
    public partial struct EpochDateTime : IComparable, IComparable<DateTime>, IConvertible, IEquatable<DateTime>,
        IFormattable, ISerializable
    {
        public DateTime Value => _dateTime;

        public int CompareTo(object obj)
        {
            return _dateTime.CompareTo(obj);
        }

        public int CompareTo(DateTime other)
        {
            return _dateTime.CompareTo(other);
        }

        public static implicit operator DateTime(EpochDateTime mdt)
        {
            return mdt._dateTime;
        }

        public static explicit operator EpochDateTime(DateTime mdt)
        {
            return new EpochDateTime(mdt);
        }

        public TypeCode GetTypeCode()
        {
            return _dateTime.GetTypeCode();
        }

        public bool ToBoolean(IFormatProvider provider)
        {
            return ((IConvertible) _dateTime).ToBoolean(provider);
        }

        public byte ToByte(IFormatProvider provider)
        {
            return ((IConvertible) _dateTime).ToByte(provider);
        }

        public char ToChar(IFormatProvider provider)
        {
            return ((IConvertible) _dateTime).ToChar(provider);
        }

        public DateTime ToDateTime(IFormatProvider provider)
        {
            return ((IConvertible) _dateTime).ToDateTime(provider);
        }

        public decimal ToDecimal(IFormatProvider provider)
        {
            return ((IConvertible) _dateTime).ToDecimal(provider);
        }

        public double ToDouble(IFormatProvider provider)
        {
            return ((IConvertible) _dateTime).ToDouble(provider);
        }

        public short ToInt16(IFormatProvider provider)
        {
            return ((IConvertible) _dateTime).ToInt16(provider);
        }

        public int ToInt32(IFormatProvider provider)
        {
            return ((IConvertible) _dateTime).ToInt32(provider);
        }

        public long ToInt64(IFormatProvider provider)
        {
            return ((IConvertible) _dateTime).ToInt64(provider);
        }

        public sbyte ToSByte(IFormatProvider provider)
        {
            return ((IConvertible) _dateTime).ToSByte(provider);
        }

        public float ToSingle(IFormatProvider provider)
        {
            return ((IConvertible) _dateTime).ToSingle(provider);
        }

        public string ToString(IFormatProvider provider)
        {
            return _dateTime.ToString(provider);
        }

        public object ToType(Type conversionType, IFormatProvider provider)
        {
            return ((IConvertible) _dateTime).ToType(conversionType, provider);
        }

        public ushort ToUInt16(IFormatProvider provider)
        {
            return ((IConvertible) _dateTime).ToUInt16(provider);
        }

        public uint ToUInt32(IFormatProvider provider)
        {
            return ((IConvertible) _dateTime).ToUInt32(provider);
        }

        public ulong ToUInt64(IFormatProvider provider)
        {
            return ((IConvertible) _dateTime).ToUInt64(provider);
        }

        public bool Equals(DateTime other)
        {
            return _dateTime.Equals(other);
        }

        public bool Equals(EpochDateTime other)
        {
            return _dateTime.Equals(other.Value);
        }

        public override string ToString()
        {
            return _dateTime.ToString();
        }

        public string ToString(string format, IFormatProvider formatProvider)
        {
            return _dateTime.ToString(format, formatProvider);
        }

        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            ((ISerializable) _dateTime).GetObjectData(info, context);
        }

        public int Epoch
        {
            get
            {
                var t = _dateTime.ToUniversalTime() - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                return (int) t.TotalSeconds;
            }
            set
            {
                var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                _dateTime = epoch.AddSeconds(value);
            }
        }
    }
}