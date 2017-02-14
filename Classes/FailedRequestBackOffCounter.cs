using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Flexinets.Radius
{
    public class FailedRequestBackOffCounter
    {
        public Int32 FailureCount
        {
            get;
            internal set;
        }
        public DateTime LastFailureUtc
        {
            get;
            internal set;
        }
        private const Int32 MaxDelay = 300; // todo maybe inject if needed


        /// <summary>
        /// The current delay in seconds
        /// </summary>
        public Int32 Delay
        {
            get
            {
                var counter = FailureCount - 2;
                var delay = Math.Pow((counter > 0 ? counter : 0), 3);
                return Convert.ToInt32(delay > MaxDelay ? MaxDelay : delay);
            }
        }


        /// <summary>
        /// The datetime when the next request is allowed
        /// </summary>
        public DateTime NextAttempt
        {
            get
            {
                return LastFailureUtc.AddSeconds(Delay > MaxDelay ? MaxDelay : Delay);
            }
        }


        public FailedRequestBackOffCounter(Int32 failureCount, DateTime lastFailureUtc)
        {
            FailureCount = failureCount;
            LastFailureUtc = lastFailureUtc;
        }
    }
}
