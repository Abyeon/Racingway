using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Racingway.Utils
{
    public class Record
    {
        public DateTime Date {  get; set; }
        public string Name { get; set; }
        public string World {  get; set; }
        public TimeSpan Time { get; set; }


        public Record(DateTime date, string name, string world, TimeSpan time)
        {
            this.Date = date;
            this.Name = name;
            this.World = world;
            this.Time = time;
        }
    }
}
