namespace NexoSferaApi.Models.Dto;

/// <summary>
/// Employee (Pracownik) DTO
/// </summary>
public class EmployeeDto
{
    public int Id { get; set; }
    public string? Symbol { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? FullName { get; set; }
    public string? Pesel { get; set; }
    public DateTime? BirthDate { get; set; }
    public string? Gender { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public bool IsActive { get; set; }
    public EmployeeAddressDto? Address { get; set; }
    public EmployeeAddressDto? CorrespondenceAddress { get; set; }
    public List<EmployeeContactDto>? Contacts { get; set; }
}

/// <summary>
/// Employee address DTO
/// </summary>
public class EmployeeAddressDto
{
    public string? Name { get; set; }
    public string? Street { get; set; }
    public string? BuildingNumber { get; set; }
    public string? ApartmentNumber { get; set; }
    public string? PostalCode { get; set; }
    public string? City { get; set; }
    public string? PostOffice { get; set; }
    public string? Country { get; set; }
    public string? Province { get; set; }
    public string? District { get; set; }
    public string? Municipality { get; set; }
}

/// <summary>
/// Employee contact DTO
/// </summary>
public class EmployeeContactDto
{
    public int Id { get; set; }
    public string? Type { get; set; }
    public string? Value { get; set; }
    public bool IsPrimary { get; set; }
    public string? Comment { get; set; }
}

/// <summary>
/// Employee summary DTO for listing
/// </summary>
public class EmployeeSummaryDto
{
    public int Id { get; set; }
    public string? Symbol { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? FullName { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public bool IsActive { get; set; }
}
