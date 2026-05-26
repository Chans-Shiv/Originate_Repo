namespace OrigenateFunction.Models;

// Single source of truth for table + column names.
public static class ColumnMap
{
    public const string PublisherPrefix = "dmt_";

    // STG_ORIGENATE is named "loanapplication" in Dataverse; HOLDING + EXCEPTIONS keep
    // the stg_origenate_* naming.
    public const string StgOrigenateEntityLogical = PublisherPrefix + "loanapplication";
    public const string HoldingEntityLogical      = PublisherPrefix + "stg_origenate_holding_table";
    public const string ExceptionsEntityLogical   = PublisherPrefix + "stg_origenate_exceptions";

    public const string StgOrigenatePrimaryId = PublisherPrefix + "loanapplicationid";
    public const string HoldingPrimaryId      = PublisherPrefix + "stg_origenate_holding_tableid";
    public const string ExceptionsPrimaryId   = PublisherPrefix + "stg_origenate_exceptionsid";

    public const string ApplicationNumberField       = PublisherPrefix + "applicationnumber";
    public const string PolicyExceptionsField        = PublisherPrefix + "policyexception";
    public const string PolicyExceptionsReasonField  = PublisherPrefix + "policyexceptionreason";

    // Keys = exact Excel header strings (with spaces). Lookup is case-insensitive,
    // so "ACCOUNT NUMBER" and "Account Number" both match — but spacing matters.
    // For the income-verification-flag column we accept both "Flg" and "F1g" spellings
    // because we've seen the source workbook use the latter (digit-1) in some files.
    public static readonly IReadOnlyDictionary<string, string> ExcelHeaderToDataverse =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "Account Number", PublisherPrefix + "accountnumber" },
            { "Account Type", PublisherPrefix + "accounttype" },
            { "Annual Income", PublisherPrefix + "annualincome" },
            { "Application Decision", PublisherPrefix + "applicationdecision" },
            { "Booking Status", PublisherPrefix + "bookingstatus" },
            { "Application Number", ApplicationNumberField },
            { "Appraisal Type Name", PublisherPrefix + "appraisaltype" },
            { "Appraised Value", PublisherPrefix + "appraisedvalue" },
            { "Approving Last Officer Associate ID", PublisherPrefix + "approvingofficerassociateid" },
            { "Approving Last Officer Location", PublisherPrefix + "approvingofficerlocation" },
            { "Approving Last Officer Name", PublisherPrefix + "approvingofficername" },
            { "APR Actual Rate", PublisherPrefix + "apractualrate" },
            { "Loan Rate", PublisherPrefix + "loanrate" },
            { "APR Promotional Rate", PublisherPrefix + "aprpromotionalrate" },
            { "Rate Sheet Rate", PublisherPrefix + "ratesheetrate" },

            { "B1 Employer Count", PublisherPrefix + "employercountb1" },
            { "B1 Credit Score", PublisherPrefix + "creditscoreb1" },
            { "B1 Empl Start Date", PublisherPrefix + "employmentstartdateb1" },
            { "B1 Date Of Birth", PublisherPrefix + "dateofbirthb1" },
            { "B1 Employer Name", PublisherPrefix + "employernameb1" },
            { "B1 Income", PublisherPrefix + "incomeb1" },
            { "B1 Inc Verif Flg", PublisherPrefix + "incomeverificationflagb1" },
            { "B1 Inc Verif F1g", PublisherPrefix + "incomeverificationflagb1" },
            { "B1 Inc Verif Method", PublisherPrefix + "incomeverificationmethodb1" },
            { "B1 Length Employed Months", PublisherPrefix + "employmentlengthmonthsb1" },
            { "B1 Length Employed Years", PublisherPrefix + "employmentlengthyearsb1" },
            { "B1 Name", PublisherPrefix + "nameb1" },
            { "B1 Occupation Title", PublisherPrefix + "occupationtitleb1" },
            { "B1 Prev Employ Months", PublisherPrefix + "previousemploymentmonthsb1" },
            { "B1 Prev Employ Years", PublisherPrefix + "previousemploymentyearsb1" },
            { "B1 Self Employed", PublisherPrefix + "selfemployedb1" },
            { "B1 SSN", PublisherPrefix + "ssnb1" },

            { "B2 Employer Count", PublisherPrefix + "employercountb2" },
            { "B2 Credit Score", PublisherPrefix + "creditscoreb2" },
            { "B2 Empl Start Date", PublisherPrefix + "employmentstartdateb2" },
            { "B2 Date Of Birth", PublisherPrefix + "dateofbirthb2" },
            { "B2 Employer Name", PublisherPrefix + "employernameb2" },
            { "B2 Income", PublisherPrefix + "incomeb2" },
            { "B2 Inc Verif Flg", PublisherPrefix + "incomeverificationflagb2" },
            { "B2 Inc Verif F1g", PublisherPrefix + "incomeverificationflagb2" },
            { "B2 Inc Verif Method", PublisherPrefix + "incomeverificationmethodb2" },
            { "B2 Length Employed Months", PublisherPrefix + "employmentlengthmonthsb2" },
            { "B2 Length Employed Years", PublisherPrefix + "employmentlengthyearsb2" },
            { "B2 Name", PublisherPrefix + "nameb2" },
            { "B2 Occupation Title", PublisherPrefix + "occupationtitleb2" },
            { "B2 Prev Employ Months", PublisherPrefix + "previousemploymentmonthsb2" },
            { "B2 Prev Employ Years", PublisherPrefix + "previousemploymentyearsb2" },
            { "B2 Self Employed", PublisherPrefix + "selfemployedb2" },
            { "B2 SSN", PublisherPrefix + "ssnb2" },

            { "Closing Officer Associate ID", PublisherPrefix + "closingofficerassociateid" },
            { "Closing Officer Name", PublisherPrefix + "closingofficername" },
            { "LOAN TO VALUE", PublisherPrefix + "loantovalueratio" },
            { "DecisionedCLTV", PublisherPrefix + "decisionedcltv" },
            { "ContractCLTV", PublisherPrefix + "contractcltv" },
            { "DTT", PublisherPrefix + "debttoincomeratio" },
            { "Program", PublisherPrefix + "loanprogram" },
            { "Collateral Description", PublisherPrefix + "collateraldescription" },
            { "Collateral Value", PublisherPrefix + "collateralvalue" },
            { "Collateral City", PublisherPrefix + "collateralcity" },
            { "Collateral County", PublisherPrefix + "collateralcounty" },
            { "Collateral State", PublisherPrefix + "collateralstate" },
            { "Collateral Street Address", PublisherPrefix + "collateralstreetaddress" },
            { "Collateral Zip", PublisherPrefix + "collateralzip" },
            { "Cost Center Number", PublisherPrefix + "costcenternumber" },
            { "Date Application", PublisherPrefix + "applicationdate" },
            { "Date Booked", PublisherPrefix + "bookingdate" },
            { "Date Closed", PublisherPrefix + "closingdate" },
            { "Modification Date", PublisherPrefix + "modificationdate" },
            { "Monthly Debt", PublisherPrefix + "monthlydebt" },
            { "Employee Loan", PublisherPrefix + "employeeloanflag" },
            { "Lien Holder", PublisherPrefix + "lienholder" },
            { "Lien Position", PublisherPrefix + "lienposition" },
            { "Loan Purpose", PublisherPrefix + "loanpurpose" },
            { "Loan Term", PublisherPrefix + "loantermmonths" },
            { "Fund Date", PublisherPrefix + "funddate" },
            { "Amount Requested", PublisherPrefix + "amountrequested" },
            { "Amount Approved", PublisherPrefix + "amountapproved" },
            { "Amount Financed", PublisherPrefix + "amountfinanced" },
            { "Loan Line Credit Limit", PublisherPrefix + "loanlinecreditlimit" },
            { "Mailing Address", PublisherPrefix + "mailingaddress" },
            { "Mailing City", PublisherPrefix + "mailingcity" },
            { "Mailing State", PublisherPrefix + "mailingstate" },
            { "Mailing Zip", PublisherPrefix + "mailingzip" },
            { "MD Loan", PublisherPrefix + "mdloanflag" },
            { "Month Key", PublisherPrefix + "monthkey" },
            { "Year Key", PublisherPrefix + "yearkey" },
            { "Occupancy Code", PublisherPrefix + "occupancycode" },
            { "Originating Market Name", PublisherPrefix + "originatingmarketname" },
            { "Loan Originator Number", PublisherPrefix + "loanoriginatornumber" },
            { "Loan Originator Name", PublisherPrefix + "loanoriginatorname" },
            { "Sales Reference Name", PublisherPrefix + "salesreferencename" },
            { "Policy Exceptions", PolicyExceptionsField },
            { "Policy Exceptions Reason", PolicyExceptionsReasonField },
            { "Pricing Override", PublisherPrefix + "pricingoverride" },
            { "Pricing Override Reason", PublisherPrefix + "pricingoverridereason" },
            { "Processor Name", PublisherPrefix + "processorname" },
            { "Product Description", PublisherPrefix + "productdescription" },
            { "Product Number", PublisherPrefix + "productnumber" },
            { "Property Type", PublisherPrefix + "propertytype" },
            { "Region Name", PublisherPrefix + "regionname" },
            { "Report Market", PublisherPrefix + "reportmarket" },
            { "Residence Type", PublisherPrefix + "residencetype" },
            { "Total Sale Price", PublisherPrefix + "totalsaleprice" },
            { "Total Applicants", PublisherPrefix + "totalapplicants" },
            { "Underwriter Associate ID", PublisherPrefix + "underwriterassociateid" },
            { "Underwriter Name", PublisherPrefix + "underwritername" },
            { "Decision Date", PublisherPrefix + "decisiondate" },
            { "Existing Lien Balances", PublisherPrefix + "existinglienbalances" },
            { "Client Status", PublisherPrefix + "clientstatus" },
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
