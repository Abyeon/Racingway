using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Racingway.Tabs
{
    public interface ITab : IDisposable
    {
        public string Name { get; }
        public void Draw();
    }
}
