namespace Tsikhanau.Monolith.Shared.Time;

public static class TimeSpanExtensions
{
    extension(TimeSpan timeSpan)
    {
        public Int32 TotalSecondsRoundedUp => (Int32)Math.Ceiling(timeSpan.TotalSeconds);
    }
}
