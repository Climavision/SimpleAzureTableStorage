using System.Diagnostics;
using Lab;
using SimpleAzureTableStorage.Core;

var connectionString = "AccountName=devstoreaccount1;AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;DefaultEndpointsProtocol=http;BlobEndpoint=http://127.0.0.1:10000/devstoreaccount1;QueueEndpoint=http://127.0.0.1:10001/devstoreaccount1;TableEndpoint=http://127.0.0.1:10002/devstoreaccount1;";
var strategies = new IKeyStrategy[]
{
    PropertyKeyStrategy.Of<User, string>(x => x.Id, true),
    new PropertyKeyStrategy<User, string>(x => x.Email, true)
};
var stopwatch = new Stopwatch();
stopwatch.Start();
var store = new AzureTableEntityStore(new StoreConfiguration
{
    ConnectionString = connectionString,
    Schema = "Lab"
}, strategies);
Console.WriteLine($"StoreCreated: {stopwatch.ElapsedMilliseconds}");
var session = store.OpenSession();
Console.WriteLine($"SessionOpened: {stopwatch.ElapsedMilliseconds}");

//session.Store(new User { Company = "CompanyABC", Email = "dale@testdomain.com", Id = "fc069198-cf96-4f94-8512-f8f3265402a1", Name = "Dale" });
//await session.SaveChanges();



var byEmail = await session.Load<User, string>(x => x.Email, "dale@testdomain.com");
Console.WriteLine($"LoadByEmail: {stopwatch.ElapsedMilliseconds}");

//var byId = await session.Load<User, string, string>(x => x.Id, "fc069198-cf96-4f94-8512-f8f3265402a1", x => x.Company, "CompanyABC");
//Console.WriteLine($"LoadById: {stopwatch.ElapsedMilliseconds}");

await session.Delete(byEmail);
Console.WriteLine($"Deleted: {stopwatch.ElapsedMilliseconds}");


//stopwatch.Stop();
