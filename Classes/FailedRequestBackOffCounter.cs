using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Flexinets.Radius
{
    public class FailedRequestBackOffCounter
    {
        public Int32 FailureCount;
        public DateTime LastFailureUtc;
    }
}
