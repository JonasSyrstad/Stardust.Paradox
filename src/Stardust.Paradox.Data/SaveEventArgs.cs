using System;
using System.Collections.Generic;
using Stardust.Paradox.Data.Annotations;
using Stardust.Paradox.Data.Internals;

namespace Stardust.Paradox.Data
{
    public class SaveEventArgs:EventArgs
    {
        public ICollection<IGraphEntityInternal> TrackedItems { get; internal set; }
        public Exception Error { get; set; }
        public string FailedUpdateStatement { get; set; }
    }
}