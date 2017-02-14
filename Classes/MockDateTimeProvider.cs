using System;

namespace Flexinets.Radius
{
    public class MockDateTimeProvider : IDateTimeProvider
    {
        private  DateTime _currentMockTime;

        public  DateTime UtcNow
        {
            get
            {
                return _currentMockTime;
            }
        }

        public  void SetDateTimeUtcNow(DateTime currentMockTime)
        {
            _currentMockTime = currentMockTime;
        }
    }
}
