using AbfallAlarm_Backend.Classes;
using System.Text.Json;

namespace AbfallAlarm_Backend.Models
{
    public class Street
    {
        public string StreetName { get; set; }

        public List<HouseNumber> HouseNumbers { get; set; } = [];

        public Street(string streetName)
        {
            StreetName = streetName;
        }

        public string ToJson()
        {
            var streetData = new
            {
                StreetName,
                HouseNumbers = HouseNumbers.Select(hn => new { hn.Number, hn.CollectionDatesId }).ToList()
            };

            return JsonSerializer.Serialize(streetData, new JsonSerializerOptions { WriteIndented = true });
        }
    }

}
