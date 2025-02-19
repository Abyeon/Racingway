using LiteDB;
using Racingway.Collision;
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
        [BsonId]
        public ObjectId Id { get; set; }
        public string Name { get; set; }
        public string Address { get; set; }
        public List<Trigger> Triggers { get; set; }


        public Route(string Name, string address, List<Trigger> triggers) 
        {
            this.Name = Name;
            this.Address = address;
            this.Triggers = triggers;
        }
    }
}
