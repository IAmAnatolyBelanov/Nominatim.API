using System;

namespace Nominatim.API.Models
{
	public class NominatimCacheConfig

	{
		public int CacheSize { get; set; } = 1000;

		public TimeSpan CacheEntityLifespan { get; set; } = TimeSpan.FromDays(7);
	}
}
