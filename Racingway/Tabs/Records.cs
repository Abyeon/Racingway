using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Racingway.Tabs
{
    internal class Records : ITab
    {
        public string Name => "Records";

        private Plugin Plugin { get; }

        public Records(Plugin plugin)
        {
            this.Plugin = plugin;
        }

        public void Dispose()
        {

        }

        public void Draw()
        {
            ImGui.Text("Records");
        }
    }
}
