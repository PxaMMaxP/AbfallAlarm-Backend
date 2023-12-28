using System.Text.Json;
using AbfallAlarm_Backend.Models;
using System.Collections.Generic;
using System.Linq;

namespace AbfallAlarm_Backend.Classes
{
    public static class JsonHelper
    {
        public static string SerializeStreets(List<Street> streets)
        {
            var streetsData = streets.Select(street => new
            {
                street.StreetName,
                HouseNumbers = street.HouseNumbers.Select(hn => new { hn.Number, hn.CollectionDatesId }).ToList()
            }).ToList();

            return JsonSerializer.Serialize(streetsData, new JsonSerializerOptions { WriteIndented = true });
        }
    }
}