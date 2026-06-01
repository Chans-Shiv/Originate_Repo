namespace Fhn.Originate.FtbanknewSync.Domain.Entities;

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

    // ── Error table ──
    // The auto-number / primary key column (dmt_ID) is populated by Dataverse; we
    // never write to it. Row diagnostics that don't have a dedicated column (source
    // blob name, row number, enqueue timestamp) are prepended to the ErrorMessage
    // body by DataverseErrorTableService so a single column carries enough context
    // for triage without jumping to the queue/archive blob.
    public const string ErrorTableEntityLogical    = PublisherPrefix + "stg_origenate_errortable";
    public const string ErrorTable_AccountNumber     = PublisherPrefix + "accountnumber";
    public const string ErrorTable_ApplicationNumber = PublisherPrefix + "applicationnumber";
    public const string ErrorTable_ErrorMessage      = PublisherPrefix + "errormessage";
    public const string ErrorTable_InvocationId      = PublisherPrefix + "invocationid";
    public const string ErrorTable_Process           = PublisherPrefix + "process";

    // Keys = exact Excel header strings (with spaces). Lookup is case-insensitive,
    // so "ACCOUNT NUMBER" and "Account Number" both match — but spacing matters.
    // For the income-verification-flag column we accept both "Flg" and "F1g" spellings
    // because we've seen the source workbook use the latter (digit-1) in some files.
    // STG mapping: Excel header → STG_ORIGENATE (dmt_loanapplication) logical name.
    // HOLDING has the SAME display headers but a subset of DIFFERENT logical names —
    // see HoldingHeaderOverrides + HoldingExcelHeaderToDataverse below.
    public static readonly IReadOnlyDictionary<string, string> StgExcelHeaderToDataverse =
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
            { "DTI", PublisherPrefix + "debttoincomeratio" },
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

    // ── HOLDING logical-name overrides (keyed by Excel header) ──
    // STG and HOLDING use the SAME Excel display headers, but a subset of columns
    // have DIFFERENT Dataverse logical names on HOLDING. This lists ONLY the
    // divergences; any header not here uses the same logical name on both tables.
    //   - Borrower 1/2 → primary/secondary applicant
    //   - CLTV variants → long "combinedloantovalue" form
    //   - Approving Last Officer Associate → "associated" suffix
    private static readonly IReadOnlyDictionary<string, string> HoldingHeaderOverrides =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            // Borrower 1 → primary applicant
            { "B1 Credit Score",            PublisherPrefix + "primarycreditscore" },
            { "B1 Date Of Birth",           PublisherPrefix + "primarydateofbirth" },
            { "B1 Empl Start Date",         PublisherPrefix + "primaryemploymentstartdate" },
            { "B1 Employer Count",          PublisherPrefix + "primaryemployercount" },
            { "B1 Employer Name",           PublisherPrefix + "primaryemployername" },
            { "B1 Inc Verif Flg",           PublisherPrefix + "primaryincomeverifiedflag" },
            { "B1 Inc Verif F1g",           PublisherPrefix + "primaryincomeverifiedflag" },
            { "B1 Inc Verif Method",        PublisherPrefix + "primaryincomeverificationmethod" },
            { "B1 Income",                  PublisherPrefix + "primaryincome" },
            { "B1 Length Employed Months",  PublisherPrefix + "primaryemploymentlengthmonths" },
            { "B1 Length Employed Years",   PublisherPrefix + "primaryemploymentlengthyears" },
            { "B1 Name",                    PublisherPrefix + "primaryapplicantname" },
            { "B1 Occupation Title",        PublisherPrefix + "primaryoccupationtitle" },
            { "B1 Prev Employ Months",      PublisherPrefix + "primarypreviousemploymentmonths" },
            { "B1 Prev Employ Years",       PublisherPrefix + "primarypreviousemploymentyears" },
            { "B1 Self Employed",           PublisherPrefix + "primaryselfemployedflag" },
            { "B1 SSN",                     PublisherPrefix + "primaryssn" },

            // Borrower 2 → secondary applicant
            { "B2 Credit Score",            PublisherPrefix + "secondarycreditscore" },
            { "B2 Date Of Birth",           PublisherPrefix + "secondarydateofbirth" },
            { "B2 Empl Start Date",         PublisherPrefix + "secondaryemploymentstartdate" },
            { "B2 Employer Count",          PublisherPrefix + "secondaryemployercount" },
            { "B2 Employer Name",           PublisherPrefix + "secondaryemployername" },
            { "B2 Inc Verif Flg",           PublisherPrefix + "secondaryincomeverifiedflag" },
            { "B2 Inc Verif F1g",           PublisherPrefix + "secondaryincomeverifiedflag" },
            { "B2 Inc Verif Method",        PublisherPrefix + "secondaryincomeverificationmethod" },
            { "B2 Income",                  PublisherPrefix + "secondaryincome" },
            { "B2 Length Employed Months",  PublisherPrefix + "secondaryemploymentlengthmonths" },
            { "B2 Length Employed Years",   PublisherPrefix + "secondaryemploymentlengthyears" },
            { "B2 Name",                    PublisherPrefix + "secondaryapplicantname" },
            { "B2 Occupation Title",        PublisherPrefix + "secondaryoccupationtitle" },
            { "B2 Prev Employ Months",      PublisherPrefix + "secondarypreviousemploymentmonths" },
            { "B2 Prev Employ Years",       PublisherPrefix + "secondarypreviousemploymentyears" },
            { "B2 Self Employed",           PublisherPrefix + "secondaryselfemployedflag" },
            { "B2 SSN",                     PublisherPrefix + "secondaryssn" },

            // CLTV variants use the long "combinedloantovalue" suffix on HOLDING
            { "DecisionedCLTV",             PublisherPrefix + "decisionedcombinedloantovalue" },
            { "ContractCLTV",               PublisherPrefix + "contractcombinedloantovalue" },

            // NOTE: "Approving Last Officer Associate" is NOT overridden — despite the
            // HOLDING display name reading "...Associated", its logical name is the same
            // as STG (dmt_approvingofficerassociateid). Leaving it out keeps both equal.

            // Collateral Zip: STG uses dmt_collateralzip, HOLDING uses dmt_collateralzipcode.
            { "Collateral Zip", PublisherPrefix + "collateralzipcode" },

            // Mailing Zip: STG uses dmt_mailingzip, HOLDING uses dmt_mailingzipcode.
            { "Mailing Zip", PublisherPrefix + "mailingzipcode" },
        };

    // HOLDING mapping: Excel header → HOLDING (dmt_stg_origenate_holding_table)
    // logical name. Same headers as STG, with HoldingHeaderOverrides applied. This is
    // the SECOND of the two separate mappings — display headers collide with STG but
    // logical names differ, so the two maps must be kept distinct.
    public static readonly IReadOnlyDictionary<string, string> HoldingExcelHeaderToDataverse =
        StgExcelHeaderToDataverse.ToDictionary(
            kv => kv.Key,
            kv => HoldingHeaderOverrides.TryGetValue(kv.Key, out var holding) ? holding : kv.Value,
            StringComparer.OrdinalIgnoreCase);

    // Business fields (exception columns excluded) in each table's OWN logical names.
    // StreamAsync against STG uses StgBusinessFields; against HOLDING uses
    // HoldingBusinessFields — never cross them, or the renamed columns won't read.
    public static readonly IReadOnlyList<string> StgBusinessFields =
        StgExcelHeaderToDataverse
            .Where(kv => !ExcludedFromStg.Contains(kv.Value))
            .Select(kv => kv.Value)
            .Distinct()
            .ToArray();

    public static readonly IReadOnlyList<string> HoldingBusinessFields =
        StgExcelHeaderToDataverse
            .Where(kv => !ExcludedFromStg.Contains(kv.Value))
            .Select(kv => HoldingHeaderOverrides.TryGetValue(kv.Key, out var holding) ? holding : kv.Value)
            .Distinct()
            .ToArray();

    // Field-name translation maps, derived by joining the two header maps on the
    // common Excel header. Only entries where STG and HOLDING logical names actually
    // differ are included. EntityProjector consults these when copying rows:
    //   - StgToHoldingField : BackupOldRowsStep   (STG → HOLDING)
    //   - HoldingToStgField : ReconcileStep        (HOLDING → STG)
    public static readonly IReadOnlyDictionary<string, string> StgToHoldingField = BuildRename(toHolding: true);
    public static readonly IReadOnlyDictionary<string, string> HoldingToStgField = BuildRename(toHolding: false);

    private static IReadOnlyDictionary<string, string> BuildRename(bool toHolding)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in StgExcelHeaderToDataverse)
        {
            if (ExcludedFromStg.Contains(kv.Value)) continue;
            var stg = kv.Value;
            var holding = HoldingHeaderOverrides.TryGetValue(kv.Key, out var h) ? h : kv.Value;
            if (string.Equals(stg, holding, StringComparison.OrdinalIgnoreCase)) continue;
            if (toHolding) map[stg] = holding;
            else map[holding] = stg;
        }
        return map;
    }
}
