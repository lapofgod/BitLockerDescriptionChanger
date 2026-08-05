namespace BitLockerDescriptionChanger.Models;

public sealed record DiskExtent(int DiskNumber, long StartingOffset, long ExtentLength);
