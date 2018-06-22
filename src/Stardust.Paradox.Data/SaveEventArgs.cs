using System;
using System.Collections.Generic;

namespace Stardust.Paradox.Data
{
    public class SaveEventArgs:EventArgs
    {
        public IEnumerable<IVertex> TrackedItems { get; internal set; }
        public Exception Error { get; set; }
    }
}