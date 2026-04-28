namespace SalesCRM.Core.Enums;

public enum AllowanceScope
{
    Global,
    Region,
    Zone,
    User,
    Role  // ScopeId holds (int)UserRole — enables per-role rates (e.g. FO=8/km, ZH=10/km)
}
