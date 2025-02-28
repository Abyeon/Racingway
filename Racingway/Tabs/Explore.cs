using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Racingway.Tabs
{
    internal class Explore : ITab
    {
        public string Name => "Explore";

        private Plugin Plugin { get; set; }
        
        public Explore (Plugin plugin)
        {
            this.Plugin = plugin;
        }

        public void Dispose()
        {

        }

        public void Draw()
        {
            ImGui.Text("Gaming");
        }
    }
}
