namespace Lab.Models;

public record Organization(string Id, string Name, string EmailDomain, string ExternalId, string ImageLinkId);

public record Employee(string Id, string Name, string Team, string Department, DateTime StartDate);

public record Assignment(string Id, string OrganizationId, string EmployeeId, DateTime Expiration);
