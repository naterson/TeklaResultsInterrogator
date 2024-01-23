using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TeklaResultsInterrogator.Core
{
    internal class MenuOption
    {
        public string Name { get; }
        public Action Selected { get; }

        public MenuOption(string name, Action selected)
        {
            this.Name = name;
            this.Selected = selected;
        }
    }
}
