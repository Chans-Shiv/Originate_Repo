namespace OrigenateFunction.Models;

// Single source of truth for table + column names.
// TODO(user): replace PublisherPrefix "new_" with the real prefix via project-wide
// search/replace once known. All logical names below are formed from PublisherPrefix.
public static class ColumnMap
{
    public const string PublisherPrefix = "dmt_";

    public const string StgOrigenateEntityLogical = PublisherPrefix + "stg_origenate";
    public const string StgOrigenateEntitySet = PublisherPrefix + "stg_origenates";
    public const string HoldingEntityLogical = PublisherPrefix + "stg_origenate_holding";
    public const string HoldingEntitySet = PublisherPrefix + "stg_origenate_holdings";
    public const string ExceptionsEntityLogical = PublisherPrefix + "stg_origenate_exception";
    public const string ExceptionsEntitySet = PublisherPrefix + "stg_origenate_exceptions";

    public const string StgOrigenatePrimaryId = PublisherPrefix + "stg_origenateid";
    public const string HoldingPrimaryId = PublisherPrefix + "stg_origenate_holdingid";
    public const string ExceptionsPrimaryId = PublisherPrefix + "stg_origenate_exceptionid";

    public const string ApplicationNumberField = PublisherPrefix + "applicationnumber";
    public const string PolicyExceptionsField = PublisherPrefix + "policyexceptions";
    public const string PolicyExceptionsReasonField = PublisherPrefix + "policyexceptionsreason";

    // TODO(user): once Dataverse column types are confirmed, add per-column coercion
    //             in OrigenateRow.ToDataverseEntity(). Today every value is sent as
    //             a string; Dataverse will accept ISO-formatted dates and numeric strings,
    //             but will reject OptionSet / Boolean / Lookup columns sent as text.
    // Likely typed columns (review when schema is known):
    //   Dates:    B1DateOfBirth, B2DateOfBirth, B1EmplStartDate, B2EmplStartDate,
    //             DateApplication, DateBooked, DateClosed, ModificationDate, FundDate,
    //             DecisionDate
    //   Numeric:  AnnualIncome, AppraisedValue, AprActualRate, LoanRate,
    //             AprPromotionalRate, RateSheetRate, B1CreditScore, B2CreditScore,
    //             B1Income, B2Income, CollateralValue, MonthlyDebt, Dti, LoanTerm,
    //             AmountRequested, AmountApproved, AmountFinanced, LoanLineCreditLimit,
    //             TotalSalePrice, TotalApplicants, ExistingLienBalances, LoanToValue,
    //             DecisionedCltv, ContractCltv,
    //             B1/B2 LengthEmployedMonths/Years, B1/B2 PrevEmployMonths/Years,
    //             B1/B2 EmployerCount
    //   Boolean:  EmployeeLoan, MdLoan, B1IncVerifFlg, B2IncVerifFlg,
    //             B1SelfEmployed, B2SelfEmployed, PricingOverride
    //   OptionSet (probably): AccountType, BookingStatus, ApplicationDecision,
    //                          OccupancyCode, ResidenceType, PropertyType, LienPosition,
    //                          LoanPurpose, Program, ClientStatus, AppraisalTypeName,
    //                          B1IncVerifMethod, B2IncVerifMethod
    public static readonly IReadOnlyDictionary<string, string> ExcelHeaderToDataverse =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "AccountNumber", PublisherPrefix + "accountnumber" },
            { "AccountType", PublisherPrefix + "accounttype" },
            { "AnnualIncome", PublisherPrefix + "annualincome" },
            { "ApplicationDecision", PublisherPrefix + "applicationdecision" },
            { "BookingStatus", PublisherPrefix + "bookingstatus" },
            { "ApplicationNumber", ApplicationNumberField },
            { "AppraisalTypeName", PublisherPrefix + "appraisaltypename" },
            { "AppraisedValue", PublisherPrefix + "appraisedvalue" },
            { "ApprovingLastOfficerAssociateId", PublisherPrefix + "approvinglastofficerassociateid" },
            { "ApprovingLastOfficerLocation", PublisherPrefix + "approvinglastofficerlocation" },
            { "ApprovingLastOfficerName", PublisherPrefix + "approvinglastofficername" },
            { "AprActualRate", PublisherPrefix + "apractualrate" },
            { "LoanRate", PublisherPrefix + "loanrate" },
            { "AprPromotionalRate", PublisherPrefix + "aprpromotionalrate" },
            { "RateSheetRate", PublisherPrefix + "ratesheetrate" },
            { "B1EmployerCount", PublisherPrefix + "b1employercount" },
            { "B1CreditScore", PublisherPrefix + "b1creditscore" },
            { "B1EmplStartDate", PublisherPrefix + "b1emplstartdate" },
            { "B1DateOfBirth", PublisherPrefix + "b1dateofbirth" },
            { "B1EmployerName", PublisherPrefix + "b1employername" },
            { "B1Income", PublisherPrefix + "b1income" },
            { "B1IncVerifFlg", PublisherPrefix + "b1incverifflg" },
            { "B1IncVerifMethod", PublisherPrefix + "b1incverifmethod" },
            { "B1LengthEmployedMonths", PublisherPrefix + "b1lengthemployedmonths" },
            { "B1LengthEmployedYears", PublisherPrefix + "b1lengthemployedyears" },
            { "B1Name", PublisherPrefix + "b1name" },
            { "B1OccupationTitle", PublisherPrefix + "b1occupationtitle" },
            { "B1PrevEmployMonths", PublisherPrefix + "b1prevemploymonths" },
            { "B1PrevEmployYears", PublisherPrefix + "b1prevemployyears" },
            { "B1SelfEmployed", PublisherPrefix + "b1selfemployed" },
            { "B1Ssn", PublisherPrefix + "b1ssn" },
            { "B2EmployerCount", PublisherPrefix + "b2employercount" },
            { "B2CreditScore", PublisherPrefix + "b2creditscore" },
            { "B2EmplStartDate", PublisherPrefix + "b2emplstartdate" },
            { "B2DateOfBirth", PublisherPrefix + "b2dateofbirth" },
            { "B2EmployerName", PublisherPrefix + "b2employername" },
            { "B2Income", PublisherPrefix + "b2income" },
            { "B2IncVerifFlg", PublisherPrefix + "b2incverifflg" },
            { "B2IncVerifMethod", PublisherPrefix + "b2incverifmethod" },
            { "B2LengthEmployedMonths", PublisherPrefix + "b2lengthemployedmonths" },
            { "B2LengthEmployedYears", PublisherPrefix + "b2lengthemployedyears" },
            { "B2Name", PublisherPrefix + "b2name" },
            { "B2OccupationTitle", PublisherPrefix + "b2occupationtitle" },
            { "B2PrevEmployMonths", PublisherPrefix + "b2prevemploymonths" },
            { "B2PrevEmployYears", PublisherPrefix + "b2prevemployyears" },
            { "B2SelfEmployed", PublisherPrefix + "b2selfemployed" },
            { "B2Ssn", PublisherPrefix + "b2ssn" },
            { "ClosingOfficerAssociateId", PublisherPrefix + "closingofficerassociateid" },
            { "ClosingOfficerName", PublisherPrefix + "closingofficername" },
            { "LoanToValue", PublisherPrefix + "loantovalue" },
            { "DecisionedCltv", PublisherPrefix + "decisionedcltv" },
            { "ContractCltv", PublisherPrefix + "contractcltv" },
            { "CollateralDescription", PublisherPrefix + "collateraldescription" },
            { "CollateralValue", PublisherPrefix + "collateralvalue" },
            { "CollateralCity", PublisherPrefix + "collateralcity" },
            { "CollateralCounty", PublisherPrefix + "collateralcounty" },
            { "CollateralState", PublisherPrefix + "collateralstate" },
            { "CollateralStreetAddress", PublisherPrefix + "collateralstreetaddress" },
            { "CollateralZip", PublisherPrefix + "collateralzip" },
            { "CostCenterNumber", PublisherPrefix + "costcenternumber" },
            { "DateApplication", PublisherPrefix + "dateapplication" },
            { "DateBooked", PublisherPrefix + "datebooked" },
            { "DateClosed", PublisherPrefix + "dateclosed" },
            { "ModificationDate", PublisherPrefix + "modificationdate" },
            { "MonthlyDebt", PublisherPrefix + "monthlydebt" },
            { "Dti", PublisherPrefix + "dti" },
            { "EmployeeLoan", PublisherPrefix + "employeeloan" },
            { "LienHolder", PublisherPrefix + "lienholder" },
            { "LienPosition", PublisherPrefix + "lienposition" },
            { "LoanPurpose", PublisherPrefix + "loanpurpose" },
            { "LoanTerm", PublisherPrefix + "loanterm" },
            { "FundDate", PublisherPrefix + "funddate" },
            { "AmountRequested", PublisherPrefix + "amountrequested" },
            { "AmountApproved", PublisherPrefix + "amountapproved" },
            { "AmountFinanced", PublisherPrefix + "amountfinanced" },
            { "LoanLineCreditLimit", PublisherPrefix + "loanlinecreditlimit" },
            { "Program", PublisherPrefix + "program" },
            { "MailingAddress", PublisherPrefix + "mailingaddress" },
            { "MailingCity", PublisherPrefix + "mailingcity" },
            { "MailingState", PublisherPrefix + "mailingstate" },
            { "MailingZip", PublisherPrefix + "mailingzip" },
            { "MdLoan", PublisherPrefix + "mdloan" },
            { "MonthKey", PublisherPrefix + "monthkey" },
            { "YearKey", PublisherPrefix + "yearkey" },
            { "OccupancyCode", PublisherPrefix + "occupancycode" },
            { "OriginatingMarketName", PublisherPrefix + "originatingmarketname" },
            { "LoanOriginatorNumber", PublisherPrefix + "loanoriginatornumber" },
            { "LoanOriginatorName", PublisherPrefix + "loanoriginatorname" },
            { "SalesReferenceName", PublisherPrefix + "salesreferencename" },
            { "PricingOverride", PublisherPrefix + "pricingoverride" },
            { "PricingOverrideReason", PublisherPrefix + "pricingoverridereason" },
            { "ProcessorName", PublisherPrefix + "processorname" },
            { "ProductDescription", PublisherPrefix + "productdescription" },
            { "ProductNumber", PublisherPrefix + "productnumber" },
            { "PropertyType", PublisherPrefix + "propertytype" },
            { "RegionName", PublisherPrefix + "regionname" },
            { "ReportMarket", PublisherPrefix + "reportmarket" },
            { "ResidenceType", PublisherPrefix + "residencetype" },
            { "TotalSalePrice", PublisherPrefix + "totalsaleprice" },
            { "TotalApplicants", PublisherPrefix + "totalapplicants" },
            { "UnderwriterAssociateId", PublisherPrefix + "underwriterassociateid" },
            { "UnderwriterName", PublisherPrefix + "underwritername" },
            { "DecisionDate", PublisherPrefix + "decisiondate" },
            { "ExistingLienBalances", PublisherPrefix + "existinglienbalances" },
            { "ClientStatus", PublisherPrefix + "clientstatus" },
            { "PolicyExceptions", PolicyExceptionsField },
            { "PolicyExceptionsReason", PolicyExceptionsReasonField },
        };

    // Fields read into row.Fields but NOT inserted into STG_ORIGENATE.
    // PolicyExceptions / PolicyExceptionsReason go to STG_ORIGENATE_EXCEPTIONS only.
    public static readonly IReadOnlySet<string> ExcludedFromStg =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            PolicyExceptionsField,
            PolicyExceptionsReasonField,
        };

    // Business fields copied between STG and HOLDING (exception columns excluded).
    public static readonly IReadOnlyList<string> StgBusinessFields =
        ExcelHeaderToDataverse.Values
            .Where(v => !ExcludedFromStg.Contains(v))
            .Distinct()
            .ToArray();
}
