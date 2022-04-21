using System.Diagnostics;
using Azure.Data.Tables;
using Lab;
using SimpleAzureTableStorage.Core;

var connectionString = "AccountName=devstoreaccount1;AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;DefaultEndpointsProtocol=http;BlobEndpoint=http://127.0.0.1:10000/devstoreaccount1;QueueEndpoint=http://127.0.0.1:10001/devstoreaccount1;TableEndpoint=http://127.0.0.1:10002/devstoreaccount1;";
var shouldClose = false;

while (!shouldClose)
{
    Console.WriteLine("What test do you want to run?");
    Console.WriteLine();
    Console.WriteLine("1: Dual unique keys");
    Console.WriteLine("2: Dual non-unique keys");
    Console.WriteLine("3: Single unique and single non-unique keys");
    Console.WriteLine("4: Query test");
    Console.WriteLine();
    Console.Write("choice: ");
    var choice = Console.ReadLine();

    switch (choice)
    {
        case "0":
            var _client = new TableServiceClient(connectionString);
            var tables = _client.Query("TableName ge 'Lab' and TableName le 'Labzzzzzzzzz'");

            foreach (var table in tables)
            {
                Console.WriteLine($"Deleting table: {table.Name}");
                await _client.DeleteTableAsync(table.Name);
            }

            break;
        case "1":
            await UniqueKeyStrategyTesting.Run(connectionString);

            break;
        case "2":
            await NonUniqueKeyStrategyTesting.Run(connectionString);

            break;
        case "3":
            await SingleUniqueSingleNonUnique.Run(connectionString);

            break;
        case "4":
            await QueryPerformanceTesting.Run(connectionString);

            break;
        default:
            shouldClose = true;
            break;
    }
}