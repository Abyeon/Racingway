using LiteDB;
using Racingway.Race.Collision.Triggers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Racingway.Race
{
    public class TimedCheckpoint
    {
        public Checkpoint checkpoint { get; set; }
        public long offset { get; set; }

        public TimedCheckpoint(Checkpoint checkpoint, long offset)
        {
            this.checkpoint = checkpoint;
            this.offset = offset;
        }
    }
}
