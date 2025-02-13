using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Racingway.Utils
{
    public class Time
    {
        public static string PrettyFormatTimeSpan(TimeSpan span)
        {
            StringBuilder sb = new StringBuilder();

            if (span.Days > 0)
                sb.Append($"{span.Days}:");
            if (span.Hours > 0)
                sb.Append($"{span.Hours}:");

            sb.Append($"{span.Minutes:00}:{span.Seconds:00}.{span.Milliseconds:000}");

            return sb.ToString();
        }
    }
}
