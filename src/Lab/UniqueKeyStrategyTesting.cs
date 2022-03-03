using Lab.Models;
using SimpleAzureTableStorage.Core;
using SimpleAzureTableStorage.Core.Services;

namespace Lab;

public static class UniqueKeyStrategyTesting
{
    public static async Task Run(string connectionString)
    {
        var strategies = new IKeyStrategy[]
        {
            new PropertyKeyStrategy<Organization, string>(x => x.Id, true),
            new PropertyKeyStrategy<Organization, string>(x => x.ExternalId, true)
        };
        var store = new AzureTableEntityStore(new StoreConfiguration
        {
            ConnectionString = connectionString,
            Schema = "Lab"
        }, strategies);
        var session = store.OpenSession();

        session.Store(new Organization("0b4466cc-4775-4825-9840-dec5733bd359", "Climavision", "climavision.com", "cvision", "c200193c-cafb-4c3c-b485-78bb7b501276"));

        await session.SaveChanges();
        await session.SaveChanges();
        await session.SaveChanges();
    }
}
public static class NonUniqueKeyStrategyTesting
{
    public static async Task Run(string connectionString)
    {
        var strategies = new IKeyStrategy[]
        {
            new PropertyKeyStrategy<Employee, string>(x => x.Id, true),
            new PropertyKeyStrategy<Employee, string>(x => x.Department, false),
            new PropertyKeyStrategy<Employee, string>(x => x.Team, false)
        };
        var store = new AzureTableEntityStore(new StoreConfiguration
        {
            ConnectionString = connectionString,
            Schema = "Lab"
        }, strategies);
        var session = store.OpenSession();

        session.Store(new Employee("1b4466cc-4775-4825-9840-dec5733bd359", "Employee", "Team A", "Department B", DateTime.Now));

        await session.SaveChanges();
        await session.SaveChanges();
        await session.SaveChanges();
    }
}