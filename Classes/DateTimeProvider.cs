using System;

namespace Flexinets.Radius
{
    public class DateTimeProvider : IDateTimeProvider
    {
        public DateTime UtcNow
        {
            get
            {
                return DateTime.UtcNow;
            }
        }
    }
}
