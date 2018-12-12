using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Conductor_Shared.Utility
{
    public static class Math
    {
        public static double Median<TSource>(this IEnumerable<TSource> source, Func<TSource, double> selector)
        {
            if (source == null)
                throw new ArgumentNullException("source is null");
            if (!source.Any())
                throw new ArgumentNullException("source is empty");

            List<double> values = source.Select(selector).ToList();
            values.Sort();
            if ((values.Count % 2) == 1) // Odd number of values
            {
                return values[values.Count / 2];
            }
            else // Even number of values: find mean of middle two
            {
                return (values[values.Count / 2] + values[values.Count / 2 + 1]) / 2;
            }
        }
    }
}
