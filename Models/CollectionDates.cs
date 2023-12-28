using CsvHelper.Configuration.Attributes;

namespace AbfallAlarm_Backend.Models
{
    public class CollectionDate
    {
        [Name("Wochentag")]
        public string? Weekday { get; set; }

        [Name("Datum")]
        public string? Date { get; set; }

        [Name("Abfallart")]
        public string? Type { get; set; }

        public override bool Equals(object? obj)
        {
            return obj is CollectionDate other &&
                   Weekday == other.Weekday &&
                   Date == other.Date &&
                   Type == other.Type;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Weekday, Date, Type);
        }
    }
}