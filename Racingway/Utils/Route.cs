using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Racingway.Utils
{
    /// <summary>
    /// Container for triggers making up a race.
    /// To be used to differentiate races.
    /// </summary>

    public class Route
    {
        public uint Territory { get; init; }
        public uint? Ward { get; init; }
        public uint? Plot { get; init; }
        public uint? World { get; init; }


        public Route() 
        {
        }
    }
}
