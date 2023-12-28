using AbfallAlarm_Backend.Models;

namespace AbfallAlarm_Backend.Classes
{

    public static class StreetExtensions
    {
        public static int CalculateTotalNumberOfHouseNumbers(this List<Street> streets)
        {
            int totalHouseNumbers = 0;

            foreach (var street in streets)
            {
                if (street.HouseNumbers != null)
                {
                    totalHouseNumbers += street.HouseNumbers.Count;
                }
            }

            return totalHouseNumbers;
        }
    }
}