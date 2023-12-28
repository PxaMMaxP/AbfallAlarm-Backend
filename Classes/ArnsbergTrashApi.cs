using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Text.Json;
using System.Collections.Generic;
using System.Web;
using NLog;
using System.Threading;
using AbfallAlarm_Backend.Models;
using CsvHelper;
using System.Globalization;
using System.Text;
using CsvHelper.Configuration;

namespace AbfallAlarm_Backend.Classes
{
    public class ArnsbergTrashApi
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();
        private string baseUrl = "https://abfallkalender.arnsberg.de";
        private CollectionDatesManager collectionDatesManager;

        public ArnsbergTrashApi(CollectionDatesManager collectionDatesManager)
        {
            logger.Trace("Initialisiere ArnsbergTrashApi");

            this.collectionDatesManager = collectionDatesManager;

            var realBaseUrl = getRealBaseUrl().Result;
            if (realBaseUrl != null)
            {
                logger.Debug($"Base-URL: {realBaseUrl}");
                baseUrl = realBaseUrl;
            }
            else
            {
                logger.Error("Keine Base-URL ermittelt.");
                throw new Exception("Keine Base-URL ermittelt.");
            }
        }

        private async Task<string?> getRealBaseUrl()
        {
            logger.Trace("Starte getRealBaseUrl");
            using (var client = new HttpClient())
            {
                string url = BuildUrl("/", new Dictionary<string, string>());
                logger.Debug($"Anfrage-URL: {url}");

                try
                {
                    HttpResponseMessage response = await client.GetAsync(url);
                    response.EnsureSuccessStatusCode();
                    string forwardingUrl = response.RequestMessage.RequestUri.ToString();
                    // Entferne das letzte Zeichen, da es ein "/" ist
                    string realBaseUrl = forwardingUrl.Remove(forwardingUrl.Length - 1);

                    logger.Trace("Antwort erfolgreich empfangen und Base-URL ermittelt");
                    return realBaseUrl;
                }
                catch (HttpRequestException e)
                {
                    logger.Error(e, $"HTTP-Anfragefehler bei {url}: {e.Message}");
                    return default;
                }
                catch (JsonException e)
                {
                    logger.Error(e, $"JSON-Verarbeitungsfehler: {e.Message}");
                    return default;
                }
            }
        }

        public async Task<List<Street>> FetchStreetsAsync(string ort = "Arnsberg")
        {
            logger.Trace($"Starte FetchStreetsAsync für Ort: {ort}");
            var parameters = new Dictionary<string, string> { { "ort", ort } };

            var streetsRaw = await FetchJsonDataAsync<string[]>("/Strassen", parameters);
            if (streetsRaw == null)
            {
                logger.Warn("Keine Daten für FetchStreetsAsync erhalten");
                return new List<Street>();
            }

            List<Street> streets = new List<Street>();

            foreach (var street in streetsRaw)
            {
                if (street == null)
                {
                    logger.Debug("Leerer Straßenwert übersprungen");
                    continue;
                }

                streets.Add(new Street(street));
            }

            logger.Debug($"Anzahl der abgerufenen Straßen: {streets.Count}");
            return streets;
        }

        public async Task<List<HouseNumber>> FetchHouseNumbersAsync(string streetName, string ort = "Arnsberg")
        {
            logger.Trace($"Starte FetchHouseNumbersAsync für Straße: {streetName}, Ort: {ort}");
            var parameters = new Dictionary<string, string> { { "ort", ort }, { "strasse", streetName } };

            var houseNumbersRaw = await FetchJsonDataAsync<string[]>("/Hausnummern", parameters);
            if (houseNumbersRaw == null)
            {
                logger.Warn("Keine Daten für FetchHouseNumbersAsync erhalten");
                return [];
            }

            var houseNumbers = new List<HouseNumber>();
            foreach (var houseNumber in houseNumbersRaw)
            {
                houseNumbers.Add(new HouseNumber(houseNumber, [], this.collectionDatesManager));
            }
            logger.Debug($"Anzahl der abgerufenen Hausnummern: {houseNumbers.Count}");
            return houseNumbers;
        }

        public async Task<List<Street>> FetchHouseNumbersInParallel(List<Street> streets, int maxDegreeOfParallelism)
        {
            logger.Debug($"Starte paralleles Abrufen von Hausnummern mit einer maximalen Parallelität von {maxDegreeOfParallelism}");

            var semaphore = new SemaphoreSlim(maxDegreeOfParallelism);
            var tasks = new List<Task>();

            foreach (var street in streets)
            {
                await semaphore.WaitAsync();
                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        logger.Trace($"Beginne mit dem Abrufen der Hausnummern für die Straße: {street.StreetName}");
                        var houseNumbers = await this.FetchHouseNumbersAsync(street.StreetName);
                        street.HouseNumbers = houseNumbers;
                        logger.Debug($"Abrufen der Hausnummern für {street.StreetName} abgeschlossen");
                    }
                    catch (Exception ex)
                    {
                        logger.Error(ex, $"Fehler beim Abrufen der Hausnummern für {street.StreetName}");
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }));
            }

            await Task.WhenAll(tasks);
            logger.Debug("Paralleles Abrufen aller Hausnummern abgeschlossen");
            return streets;
        }

        public async Task<List<Street>> FetchCollectionDatesInParallel(List<Street> streets, int maxDegreeOfParallelism)
        {
            logger.Debug($"Starte paralleles Abrufen von CollectionDates mit einer maximalen Parallelität von {maxDegreeOfParallelism}");

            var semaphore = new SemaphoreSlim(maxDegreeOfParallelism);
            var tasks = new List<Task>();

            foreach (var street in streets)
            {
                if (street.HouseNumbers == null) continue; // Keine Aktion, wenn keine Hausnummern vorhanden sind

                foreach (var houseNumber in street.HouseNumbers)
                {
                    await semaphore.WaitAsync();
                    tasks.Add(Task.Run(async () =>
                    {
                        try
                        {
                            logger.Trace($"Beginne mit dem Abrufen der CollectionDates für die Straße: {street.StreetName}, Hausnummer: {houseNumber.Number}");
                            var collectionDates = await FetchCsvAsync(street.StreetName, houseNumber.Number);
                            houseNumber.SetCollectionDates(collectionDates, this.collectionDatesManager);
                            logger.Debug($"Abrufen der CollectionDates für {street.StreetName} {houseNumber.Number} abgeschlossen");
                        }
                        catch (Exception ex)
                        {
                            logger.Error(ex, $"Fehler beim Abrufen der CollectionDates für {street.StreetName} {houseNumber.Number}");
                        }
                        finally
                        {
                            semaphore.Release();
                        }
                    }));
                }
            }

            await Task.WhenAll(tasks);
            logger.Debug("Paralleles Abrufen aller CollectionDates abgeschlossen");
            return streets;
        }


        public async Task<List<CollectionDate>> FetchCsvAsync(string streetName, string houseNumber, string ort = "Arnsberg")
        {
            logger.Trace($"Starte FetchCsvAsync für Straße: {streetName}, Hausnummer: {houseNumber}, Ort: {ort}");
            var parameters = new Dictionary<string, string> { { "ort", ort }, { "strasse", streetName }, { "hausnr", houseNumber } };

            var csvRaw = await FetchDataAsync<string>("/abfallkalender/csv", parameters, Encoding.Latin1);
            if (csvRaw == null)
            {
                logger.Warn("Keine Daten für FetchCsvAsync erhalten");
                return null;
            }
            else
            {
                logger.Debug($"Anzahl der abgerufenen Zeichen: {csvRaw.Length}");
                using var reader = new StringReader(csvRaw);
                var config = new CsvConfiguration(CultureInfo.InvariantCulture)
                {
                    BadDataFound = null,
                    Delimiter = ";",
                    Quote = '"',

                };
                using var csv = new CsvReader(reader, config);
                var records = csv.GetRecords<CollectionDate>().ToList();
                logger.Debug($"Abrufen der CSV-Daten für {streetName} {houseNumber} abgeschlossen");

                return records;
            }
        }

        private async Task<T?> FetchJsonDataAsync<T>(string endpoint, IDictionary<string, string> parameters)
        {
            var raw = await this.FetchDataAsync<T>(endpoint, parameters);
            try
            {
                logger.Trace("Antwort erfolgreich empfangen und deserialisiert");
                return JsonSerializer.Deserialize<T>(raw);
            }
            catch (JsonException e)
            {
                logger.Error(e, $"JSON-Verarbeitungsfehler: {e.Message}");
                return default;
            }
        }

        private async Task<string?> FetchDataAsync<T>(string endpoint, IDictionary<string, string> parameters, Encoding? serverEncoding = null)
        {
            logger.Trace($"Starte FetchDataAsync für Endpoint: {endpoint}");
            if (serverEncoding == null)
            {
                serverEncoding = Encoding.UTF8;
            }

            using (var client = new HttpClient())
            {
                string url = BuildUrl(endpoint, parameters);
                logger.Debug($"Anfrage-URL: {url}");

                try
                {
                    HttpResponseMessage response = await client.GetAsync(url);
                    response.EnsureSuccessStatusCode();
                    var responseBodyBytes = await response.Content.ReadAsByteArrayAsync();

                    string responseBodyInOriginalEncoding = serverEncoding.GetString(responseBodyBytes);
                    byte[] bytesInDefaultEncoding = Encoding.Default.GetBytes(responseBodyInOriginalEncoding);
                    string responseBodyInDefaultEncoding = Encoding.Default.GetString(bytesInDefaultEncoding);

                    logger.Trace("Antwort erfolgreich empfangen und deserialisiert");
                    return responseBodyInDefaultEncoding;
                }
                catch (HttpRequestException e)
                {
                    logger.Error(e, $"HTTP-Anfragefehler bei {url}: {e.Message}");
                    return default;
                }
                catch (JsonException e)
                {
                    logger.Error(e, $"JSON-Verarbeitungsfehler: {e.Message}");
                    return default;
                }
            }
        }

        private string BuildUrl(string endpoint, IDictionary<string, string> parameters)
        {
            var builder = new UriBuilder(baseUrl + endpoint);
            var query = HttpUtility.ParseQueryString(builder.Query);

            foreach (var param in parameters)
            {
                query[param.Key] = param.Value;
            }

            builder.Query = query.ToString();
            logger.Debug($"Erstellte URL: {builder}");
            return builder.ToString();
        }
    }
}
