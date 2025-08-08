using System;

namespace Racingway.Tabs
{
    public interface ITab : IDisposable
    {
        public string Name { get; }
        public void Draw();
    }
}
