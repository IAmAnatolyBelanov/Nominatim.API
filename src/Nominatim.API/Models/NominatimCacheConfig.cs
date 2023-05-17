using System;

namespace Nominatim.API.Models
{
	public class NominatimCacheConfig

	{
		public int SuccessCacheSize { get; set; } = 1000;

		public TimeSpan SuccessCacheEntityLifespan { get; set; } = TimeSpan.FromDays(7);

		public int ErrorsCacheSize { get; set; } = 1000;

		public TimeSpan ErrorsCacheEntityLifespan { get; set; } = TimeSpan.FromDays(7);
	}
}
