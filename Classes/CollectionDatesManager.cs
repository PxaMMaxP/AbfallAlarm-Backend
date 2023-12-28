using AbfallAlarm_Backend.Models;
using System.Linq;
using System.Text.Json;

namespace AbfallAlarm_Backend.Classes
{
    public class CollectionDatesManager
    {
        private Dictionary<HashSet<CollectionDate>, List<CollectionDate>> collectionDatesCache = new Dictionary<HashSet<CollectionDate>, List<CollectionDate>>(new HashSetComparer());
        private Dictionary<HashSet<CollectionDate>, int> collectionDatesIds = new Dictionary<HashSet<CollectionDate>, int>(new HashSetComparer());
        private int nextId = 0;

        public (int Id, List<CollectionDate> CollectionDates) GetOrAddCollectionDates(List<CollectionDate> dates)
        {
            var datesSet = new HashSet<CollectionDate>(dates);
            if (!collectionDatesCache.TryGetValue(datesSet, out var existingDates))
            {
                int id = nextId++;
                collectionDatesCache[datesSet] = dates;
                collectionDatesIds[datesSet] = id;
                return (id, dates);
            }
            else
            {
                int existingId = collectionDatesIds[datesSet];
                return (existingId, existingDates);
            }
        }

        public string ToJson()
        {
            var collectionDatesWithId = collectionDatesCache.Select(entry =>
            {
                var id = collectionDatesIds[entry.Key];
                return new { Id = id, CollectionDates = entry.Value };
            }).ToList();

            return JsonSerializer.Serialize(collectionDatesWithId, new JsonSerializerOptions { WriteIndented = true });
        }
    }


    // Custom comparer für HashSet<CollectionDate>
    class HashSetComparer : IEqualityComparer<HashSet<CollectionDate>>
    {
        public bool Equals(HashSet<CollectionDate>? x, HashSet<CollectionDate>? y)
        {
            if (x is null || y is null)
                return false;

            return x.SetEquals(y);
        }

        public int GetHashCode(HashSet<CollectionDate> obj)
        {
            // Kombiniert HashCodes aller Elemente im HashSet
            return obj.Aggregate(0, (acc, x) => acc ^ x.GetHashCode());
        }
    }
}
