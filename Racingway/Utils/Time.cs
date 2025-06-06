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

            if (span.TotalMicroseconds < 0)
                sb.Append("-");
            if (span.Days > 0)
                sb.Append($"{span.Days}:");
            if (span.Hours > 0)
                sb.Append($"{span.Hours}:");

            sb.Append($"{Math.Abs(span.Minutes):00}:{Math.Abs(span.Seconds):00}.{Math.Abs(span.Milliseconds):000}");

            return sb.ToString();
        }

        public static string PrettyFormatTimeSpanSigned(TimeSpan span)
        {
            StringBuilder sb = new StringBuilder();

            if (span.TotalMicroseconds < 0)
            {
                sb.Append("-");
            } else
            {
                sb.Append("+");
            }

            if (span.Days > 0)
                sb.Append($"{span.Days}:");
            if (span.Hours > 0)
                sb.Append($"{span.Hours}:");

            sb.Append($"{Math.Abs(span.Minutes):00}:{Math.Abs(span.Seconds):00}.{Math.Abs(span.Milliseconds):000}");

            return sb.ToString();
        }
    }
}
