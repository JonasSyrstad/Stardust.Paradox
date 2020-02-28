using System;
using System.Globalization;

namespace Stardust.Paradox.Data.Annotations.DataTypes
{
    public partial struct EpochDateTime
    {
        private DateTime _dateTime;

        public EpochDateTime(long tics)
        {
            _dateTime = new DateTime(tics);
        }

        public EpochDateTime(long ticks, DateTimeKind kind)
        {
            _dateTime = new DateTime(ticks, kind);
        }

        public EpochDateTime(int year, int month, int day)
        {
            _dateTime = new DateTime(year, month, day);
        }

        public EpochDateTime(int year, int month, int day, int hour, int minute, int second)
        {
            _dateTime = new DateTime(year, month, day, hour, minute, second);
        }

        public EpochDateTime(int year, int month, int day, int hour, int minute, int second, DateTimeKind kind)
        {
            _dateTime = new DateTime(year, month, day, hour, minute, second, kind);
        }

        public EpochDateTime(int year, int month, int day, int hour, int minute, int second, Calendar calendar)
        {
            _dateTime = new DateTime(year, month, day, hour, minute, second, calendar);
        }

        public EpochDateTime(int year, int month, int day, int hour, int minute, int second, int millisecond)
        {
            _dateTime = new DateTime(year, month, day, hour, minute, second, millisecond);
        }

        public EpochDateTime(int year, int month, int day, int hour, int minute, int second, int millisecond,
            DateTimeKind kind)
        {
            _dateTime = new DateTime(year, month, day, hour, minute, second, millisecond, kind);
        }

        public EpochDateTime(int year, int month, int day, int hour, int minute, int second, int millisecond,
            Calendar calendar)
        {
            _dateTime = new DateTime(year, month, day, hour, minute, second, millisecond, calendar);
        }

        public EpochDateTime(int year, int month, int day, int hour, int minute, int second, int millisecond,
            Calendar calendar, DateTimeKind kind)
        {
            _dateTime = new DateTime(year, month, day, hour, minute, second, millisecond, calendar, kind);
        }

        public EpochDateTime(int year, int month, int day, Calendar calendar)
        {
            _dateTime = new DateTime(year, month, day, calendar);
        }

        private EpochDateTime(DateTime dateTime)
        {
            _dateTime = dateTime;
        }
    }
}