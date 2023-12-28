using AbfallAlarm_Backend.Classes;

namespace AbfallAlarm_Backend.Models
{
    public class HouseNumber
    {
        public string Number { get; set; }

        // Referenz auf eine Liste von CollectionDate-Objekten
        public List<CollectionDate>? CollectionDates { get; private set; }

        // Eigenschaft für die ID
        public int? CollectionDatesId { get; private set; }

        public HouseNumber(string number, List<CollectionDate>? collectionDates, CollectionDatesManager manager)
        {
            Number = number;
            if (collectionDates != null)
            {
                SetCollectionDates(collectionDates, manager);
            }
        }

        public void SetCollectionDates(List<CollectionDate> collectionDates, CollectionDatesManager manager)
        {
            var result = manager.GetOrAddCollectionDates(collectionDates);
            CollectionDatesId = result.Id;
            CollectionDates = result.CollectionDates;
        }
    }

}
