using AbfallAlarm_Backend.Classes;

namespace AbfallAlarm_Backend;

class Program
{
    private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

    static async Task Main(string[] args)
    {
        var startTimestamp = DateTime.Now;
        logger.Info($"Started AbfallAlarm_Backend at {startTimestamp.ToString("dd.MM.yyyy HH:mm:ss")}");

        var collectionDatesManager = new CollectionDatesManager();

        // Erstelle eine neue Instanz der ArnsbergTrashApi-Klasse
        ArnsbergTrashApi arnsbergTrashApi = new ArnsbergTrashApi(collectionDatesManager);

        // Rufe die Methode FetchStreetsAsync auf
        var streets = await arnsbergTrashApi.FetchStreetsAsync();
        //streets = streets.Slice(0, 2);

        var timestamp = DateTime.Now;
        streets = await arnsbergTrashApi.FetchHouseNumbersInParallel(streets, 5);
        logger.Info($"Fetched house numbers for {streets.Count} streets, total house numbers of {streets.CalculateTotalNumberOfHouseNumbers()}, in {DateTime.Now - timestamp:hh\\:mm\\:ss}");
        timestamp = DateTime.Now;
        streets = await arnsbergTrashApi.FetchCollectionDatesInParallel(streets, 5);
        logger.Info($"Fetched Collection Plans for {streets.CalculateTotalNumberOfHouseNumbers()} house numbers of {streets.Count} streets in {DateTime.Now - timestamp:hh\\:mm\\:ss}");

        /**foreach (var street in streets)
        {
            street.HouseNumbers = await arnsbergTrashApi.FetchHouseNumbersAsync(street.StreetName);
            Console.WriteLine($"Straße: {street.StreetName}");
            foreach (var houseNumber in street.HouseNumbers)
            {
                var collectionDate = await arnsbergTrashApi.FetchCsvAsync(street.StreetName, houseNumber.Number);
                houseNumber.SetCollectionDates(collectionDate, collectionDatesManager);
                Console.WriteLine($"Hausnummer: {houseNumber.Number}");
            }
        }**/

        var pathPrefix = Path.Combine("..", "..", "..", "docs");

        var collectionDatesJSON = collectionDatesManager.ToJson();
        File.WriteAllText(pathPrefix + "/collectionDates.json", collectionDatesJSON);

        var streetsJSON = JsonHelper.SerializeStreets(streets);
        File.WriteAllText(pathPrefix + "/streets.json", streetsJSON);

        var endTimestamp = DateTime.Now;
        logger.Info($"Ended AbfallAlarm_Backend at {endTimestamp.ToString("dd.MM.yyyy HH:mm:ss")}");
        logger.Info($"Runtime: {endTimestamp - startTimestamp:hh\\:mm\\:ss}");
    }
}
