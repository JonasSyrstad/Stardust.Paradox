using System;

namespace Stardust.Paradox.Data.Annotations.DataTypes
{
	public static class EpochDateTimeExtensions
	{
		public static EpochDateTime ToEpoch(this DateTime dateTime)
		{
			return (EpochDateTime) dateTime;
		}
	}
}