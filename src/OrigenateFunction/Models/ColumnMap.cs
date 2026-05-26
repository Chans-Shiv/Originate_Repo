namespace OrigenateFunction.Models;

// Single source of truth for table + column names.
public static class ColumnMap
{
    public const string PublisherPrefix = "dmt_";

    // STG_ORIGENATE is named "loanapplication" in Dataverse; HOLDING + EXCEPTIONS keep
    // the stg_origenate_* naming. EntitySet (plural) values are Dataverse's default
    // pluralization and should be verified against the EntityDefinitions metadata.
    public const string StgOrigenateEntityLogical = PublisherPrefix + "loanapplication";
    public const string StgOrigenateEntitySet     = PublisherPrefix + "loanapplications";
    public const string HoldingEntityLogical      = PublisherPrefix + "stg_origenate_holding_table";
    public const string HoldingEntitySet          = PublisherPrefix + "stg_origenate_holding_tables";
    public const string ExceptionsEntityLogical   = PublisherPrefix + "stg_origenate_exceptions";
    public const string ExceptionsEntitySet       = PublisherPrefix + "stg_origenate_exceptionses";

    public const string StgOrigenatePrimaryId = PublisherPrefix + "loanapplicationid";
    public const string HoldingPrimaryId      = PublisherPrefix + "stg_origenate_holding_tableid";
    public const string ExceptionsPrimaryId   = PublisherPrefix + "stg_origenate_exceptionsid";

    public const string ApplicationNumberField       = PublisherPrefix + "applicationnumber";
    public const string PolicyExceptionsField        = PublisherPrefix + "policyexception";
    public const string PolicyExceptionsReasonField  = PublisherPrefix + "policyexceptionreason";

    public static readonly IReadOnlyDictionary<string, string> ExcelHeaderToDataverse =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "AccountNumber", PublisherPrefix + "accountnumber" },
            { "AccountType", PublisherPrefix + "accounttype" },
            { "AnnualIncome", PublisherPrefix + "annualincome" },
            { "ApplicationDecision", PublisherPrefix + "applicationdecision" },
            { "BookingStatus", PublisherPrefix + "bookingstatus" },
            { "ApplicationNumber", ApplicationNumberField },
            { "AppraisalTypeName", PublisherPrefix + "appraisaltype" },
            { "AppraisedValue", PublisherPrefix + "appraisedvalue" },
            { "ApprovingLastOfficerAssociateId", PublisherPrefix + "approvingofficerassociateid" },
            { "ApprovingLastOfficerLocation", PublisherPrefix + "approvingofficerlocation" },
            { "ApprovingLastOfficerName", PublisherPrefix + "approvingofficername" },
            { "AprActualRate", PublisherPrefix + "apractualrate" },
            { "LoanRate", PublisherPrefix + "loanrate" },
            { "AprPromotionalRate", PublisherPrefix + "aprpromotionalrate" },
            { "RateSheetRate", PublisherPrefix + "ratesheetrate" },
            { "B1EmployerCount", PublisherPrefix + "employercountb1" },
            { "B1CreditScore", PublisherPrefix + "creditscoreb1" },
            { "B1EmplStartDate", PublisherPrefix + "employmentstartdateb1" },
            { "B1DateOfBirth", PublisherPrefix + "dateofbirthb1" },
            { "B1EmployerName", PublisherPrefix + "employernameb1" },
            { "B1Income", PublisherPrefix + "incomeb1" },
            { "B1IncVerifFlg", PublisherPrefix + "incomeverificationflagb1" },
            { "B1IncVerifMethod", PublisherPrefix + "incomeverificationmethodb1" },
            { "B1LengthEmployedMonths", PublisherPrefix + "employmentlengthmonthsb1" },
            { "B1LengthEmployedYears", PublisherPrefix + "employmentlengthyearsb1" },
            { "B1Name", PublisherPrefix + "nameb1" },
            { "B1OccupationTitle", PublisherPrefix + "occupationtitleb1" },
            { "B1PrevEmployMonths", PublisherPrefix + "previousemploymentmonthsb1" },
            { "B1PrevEmployYears", PublisherPrefix + "previousemploymentyearsb1" },
            { "B1SelfEmployed", PublisherPrefix + "selfemployedb1" },
            { "B1Ssn", PublisherPrefix + "ssnb1" },
            { "B2EmployerCount", PublisherPrefix + "employercountb2" },
            { "B2CreditScore", PublisherPrefix + "creditscoreb2" },
            { "B2EmplStartDate", PublisherPrefix + "employmentstartdateb2" },
            { "B2DateOfBirth", PublisherPrefix + "dateofbirthb2" },
            { "B2EmployerName", PublisherPrefix + "employernameb2" },
            { "B2Income", PublisherPrefix + "incomeb2" },
            { "B2IncVerifFlg", PublisherPrefix + "incomeverificationflagb2" },
            { "B2IncVerifMethod", PublisherPrefix + "incomeverificationmethodb2" },
            { "B2LengthEmployedMonths", PublisherPrefix + "employmentlengthmonthsb2" },
            { "B2LengthEmployedYears", PublisherPrefix + "employmentlengthyearsb2" },
            { "B2Name", PublisherPrefix + "nameb2" },
            { "B2OccupationTitle", PublisherPrefix + "occupationtitleb2" },
            { "B2PrevEmployMonths", PublisherPrefix + "previousemploymentmonthsb2" },
            { "B2PrevEmployYears", PublisherPrefix + "previousemploymentyearsb2" },
            { "B2SelfEmployed", PublisherPrefix + "selfemployedb2" },
            { "B2Ssn", PublisherPrefix + "ssnb2" },
            { "ClosingOfficerAssociateId", PublisherPrefix + "closingofficerassociateid" },
            { "ClosingOfficerName", PublisherPrefix + "closingofficername" },
            { "LoanToValue", PublisherPrefix + "loantovalueratio" },
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
            { "DateApplication", PublisherPrefix + "applicationdate" },
            { "DateBooked", PublisherPrefix + "bookingdate" },
            { "DateClosed", PublisherPrefix + "closingdate" },
            { "ModificationDate", PublisherPrefix + "modificationdate" },
            { "MonthlyDebt", PublisherPrefix + "monthlydebt" },
            { "Dti", PublisherPrefix + "debttoincomeratio" },
            { "EmployeeLoan", PublisherPrefix + "employeeloanflag" },
            { "LienHolder", PublisherPrefix + "lienholder" },
            { "LienPosition", PublisherPrefix + "lienposition" },
            { "LoanPurpose", PublisherPrefix + "loanpurpose" },
            { "LoanTerm", PublisherPrefix + "loantermmonths" },
            { "FundDate", PublisherPrefix + "funddate" },
            { "AmountRequested", PublisherPrefix + "amountrequested" },
            { "AmountApproved", PublisherPrefix + "amountapproved" },
            { "AmountFinanced", PublisherPrefix + "amountfinanced" },
            { "LoanLineCreditLimit", PublisherPrefix + "loanlinecreditlimit" },
            { "Program", PublisherPrefix + "loanprogram" },
            { "MailingAddress", PublisherPrefix + "mailingaddress" },
            { "MailingCity", PublisherPrefix + "mailingcity" },
            { "MailingState", PublisherPrefix + "mailingstate" },
            { "MailingZip", PublisherPrefix + "mailingzip" },
            { "MdLoan", PublisherPrefix + "mdloanflag" },
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
