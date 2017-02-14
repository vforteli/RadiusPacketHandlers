using System;

namespace Flexinets.Radius
{
    public interface IDateTimeProvider
    {
        DateTime UtcNow { get; }
    }
}