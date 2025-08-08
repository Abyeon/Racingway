using Racingway.Race.Collision.Triggers;

namespace Racingway.Race
{
    public class TimedCheckpoint
    {
        public Checkpoint? checkpoint { get; set; }
        public long offset { get; set; }

        public TimedCheckpoint(Checkpoint checkpoint, long offset)
        {
            this.checkpoint = checkpoint;
            this.offset = offset;
        }
    }
}
