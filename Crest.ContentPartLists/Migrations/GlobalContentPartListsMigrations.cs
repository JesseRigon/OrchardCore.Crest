using Crest.Models;
using Crest.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Data.Migration;
using OrchardCore.Environment.Shell.Scope;

namespace Crest.Migrations;

// The shared GLOBAL content part lists: sets that cross module and vendor boundaries
// (country codes, units of measure), installed for every tenant that enables the
// Crest.ContentPartLists feature. Domain-specific sets stay declared in their owning
// module (Fruitful.Accounting's price levels, transaction statuses, ...); these two
// are here precisely because no single domain module owns them.
//
// SeedAsync is idempotent and never overwrites: tenants relabel, hide and re-sort
// freely, and re-running this migration only ever adds missing keys. Categories are
// module-locked here (see SeedGlobalListsAsync) because logic evaluates against them.
public sealed class GlobalContentPartListsMigrations : DataMigration
{
    public const string CountryCodesKey = "global.country-codes";
    public const string UnitsOfMeasureKey = "global.uom";
    public const string PhoneCountryCodesKey = "global.phone-country-codes";
    public const string PostalFormatsKey = "global.postal-formats";
    public const string LanguagesKey = "global.languages";
    public const string UsAreaCodesKey = "global.us-area-codes";
    public const string SubdivisionsKey = "global.subdivisions";
    public const string HonorificsKey = "global.honorifics";
    public const string NameSuffixesKey = "global.name-suffixes";
    public const string UomRec20Key = "global.uom-rec20";

    public Task<int> CreateAsync()
    {
        // Seeding creates CONTENT ITEMS, which fires every enabled feature's content
        // handlers and queries index tables. During first-time tenant SETUP this
        // migration runs while other features' schema may not exist yet (their
        // migrations run after ours), so the seed is deferred to the end of the shell
        // scope - after every migration of the batch has completed. At any later
        // feature-enable the deferral is a harmless no-op delay.
        ShellScope.AddDeferredTask(async scope =>
        {
            var service = scope.ServiceProvider.GetRequiredService<ICrestContentPartListService>();
            await SeedGlobalListsAsync(service);
        });

        return Task.FromResult(6);
    }

    // Tenants that enabled the feature before the data locks and the Mass/Temperature
    // corrections existed: re-seed (adds temperature units, asserts the module locks)
    // and move the mass units out of the physically wrong "Weight" category.
    public Task<int> UpdateFrom1Async()
    {
        ShellScope.AddDeferredTask(async scope =>
        {
            var service = scope.ServiceProvider.GetRequiredService<ICrestContentPartListService>();
            await SeedGlobalListsAsync(service);

            foreach (var unit in UnitsOfMeasure.Where(unit => unit.Category == "Mass"))
            {
                await service.UpdateOptionAsync(UnitsOfMeasureKey, unit.Code, displayText: null, position: null, hidden: null, category: "Mass");
            }

            await BackfillPositionsAsync(service);
        });

        return Task.FromResult(6);
    }

    // Tenants seeded while every option carried Position 0: give the manual order
    // real, sequential positions (countries by name, units grouped by dimension) so
    // the Position index column and drag-and-drop reordering start from something
    // meaningful rather than a wall of zeroes.
    public Task<int> UpdateFrom2Async()
    {
        ShellScope.AddDeferredTask(async scope =>
            await BackfillPositionsAsync(scope.ServiceProvider.GetRequiredService<ICrestContentPartListService>()));

        return Task.FromResult(6);
    }

    // Tenants seeded before units carried plural labels: fill in each unit's plural
    // where the tenant has not set one, leaving any tenant-authored plural alone.
    public Task<int> UpdateFrom3Async()
    {
        ShellScope.AddDeferredTask(async scope =>
        {
            var service = scope.ServiceProvider.GetRequiredService<ICrestContentPartListService>();
            var units = await service.GetAsync(UnitsOfMeasureKey);
            if (units is null)
            {
                return;
            }

            foreach (var unit in UnitsOfMeasure)
            {
                var existing = units.Options.FirstOrDefault(option =>
                    string.Equals(option.Key, unit.Code, StringComparison.OrdinalIgnoreCase));
                if (existing is not null && existing.DisplayTextPlural is null)
                {
                    await service.UpdateOptionAsync(UnitsOfMeasureKey, unit.Code,
                        displayText: null, position: null, hidden: null, category: null,
                        displayTextPlural: unit.Plural);
                }
            }
        });

        return Task.FromResult(6);
    }

    // Tenants seeded before the wider reference-data lists existed: seed them.
    // SeedGlobalListsAsync covers everything, adds-only.
    public Task<int> UpdateFrom4Async()
    {
        ShellScope.AddDeferredTask(async scope =>
            await SeedGlobalListsAsync(scope.ServiceProvider.GetRequiredService<ICrestContentPartListService>()));

        return Task.FromResult(6);
    }

    // Tenants seeded before the Rec 20 mapping dataset existed: seed it.
    // SeedGlobalListsAsync covers everything, adds-only.
    public Task<int> UpdateFrom5Async()
    {
        ShellScope.AddDeferredTask(async scope =>
            await SeedGlobalListsAsync(scope.ServiceProvider.GetRequiredService<ICrestContentPartListService>()));

        return Task.FromResult(6);
    }

    private static async Task BackfillPositionsAsync(ICrestContentPartListService contentPartLists)
    {
        var countries = await contentPartLists.GetAsync(CountryCodesKey);
        if (countries is not null && countries.Options.All(option => option.Position == 0))
        {
            await contentPartLists.ReorderOptionsAsync(CountryCodesKey,
                [.. countries.Options
                    .OrderBy(option => option.DisplayText, StringComparer.OrdinalIgnoreCase)
                    .Select(option => option.Key)]);
        }

        var units = await contentPartLists.GetAsync(UnitsOfMeasureKey);
        if (units is not null && units.Options.All(option => option.Position == 0))
        {
            await contentPartLists.ReorderOptionsAsync(UnitsOfMeasureKey,
                [.. units.Options
                    .OrderBy(option => option.Category, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(option => option.DisplayText, StringComparer.OrdinalIgnoreCase)
                    .Select(option => option.Key)]);
        }
    }

    private static async Task SeedGlobalListsAsync(ICrestContentPartListService contentPartLists)
    {
        // ISO 3166-1 alpha-2: the key is the code every other standard builds on
        // (addresses, currencies, phone prefixes); the label is the English short
        // name, freely renamable per tenant. Seed order sets the initial manual
        // positions (by name); how any picker SORTS the list is that instance's
        // SortColumns, not list data.
        //
        // Both global lists carry the MODULE data lock: system logic (formulas,
        // conversions, mapping tables) evaluates against their categories and keys,
        // so the machine surface is frozen for every tenant, super admin included.
        // Labels, visibility, order and category-constrained tenant additions stay
        // open - that is exactly what the data lock (vs the edit lock) means.
        await contentPartLists.SeedAsync(new CrestContentPartListSeed(
            CountryCodesKey,
            "Country codes",
            [.. Countries
                .OrderBy(country => country.Name, StringComparer.OrdinalIgnoreCase)
                .Select(country => new CrestOptionSeed(country.Code, country.Name))],
            DataLock: true));

        // Based on UN/ECE Recommendation 20 CONTENT (deliberately not keyed to its
        // codes - each/pallet/case-style trade codes are a mapping concern; the
        // Rec 20 map is the SEPARATE dataset seeded below as UomRec20Key).
        // Categories are the DIMENSION the unit measures; the initial manual
        // positions group by dimension, and an instance that wants category
        // grouping sorts by the Category column.
        await contentPartLists.SeedAsync(new CrestContentPartListSeed(
            UnitsOfMeasureKey,
            "Units of measure",
            [.. UnitsOfMeasure
                .OrderBy(unit => unit.Category, StringComparer.OrdinalIgnoreCase)
                .ThenBy(unit => unit.Name, StringComparer.OrdinalIgnoreCase)
                .Select(unit => new CrestOptionSeed(unit.Code, unit.Name, Category: unit.Category, DisplayTextPlural: unit.Plural))],
            DataLock: true));

        // The Rec 20 MAPPING dataset: key = the global.uom unit key, Value = the
        // UN/CEFACT Recommendation 20 code (the machine datum EDI/trade documents
        // need). Labels and categories mirror the uom list so the two read
        // consistently. Units with no Rec 20 code (box, case, pack, pallet, roll -
        // package types, Recommendation 21's domain) are simply ABSENT: absence
        // means "no Rec 20 code", never a made-up one.
        var uomNames = UnitsOfMeasure.ToDictionary(unit => unit.Code, unit => (unit.Name, unit.Category), StringComparer.OrdinalIgnoreCase);
        await contentPartLists.SeedAsync(new CrestContentPartListSeed(
            UomRec20Key,
            "UN/CEFACT Rec 20 codes",
            [.. GlobalReferenceData.UomRec20Codes
                .Where(entry => uomNames.ContainsKey(entry.UnitKey))
                .OrderBy(entry => uomNames[entry.UnitKey].Category, StringComparer.OrdinalIgnoreCase)
                .ThenBy(entry => uomNames[entry.UnitKey].Name, StringComparer.OrdinalIgnoreCase)
                .Select(entry => new CrestOptionSeed(
                    entry.UnitKey,
                    uomNames[entry.UnitKey].Name,
                    Category: uomNames[entry.UnitKey].Category,
                    Value: entry.Code))],
            DataLock: true));

        // The wider reference-data lists (see GlobalReferenceData for sourcing and
        // key/Value rationale). Everything code evaluates against - dial codes,
        // postal patterns, language codes, area codes, subdivisions - is
        // module-data-locked; the personal-name lists are not, since no logic reads
        // them and tenants legitimately extend them freely.
        var countryNames = Countries.ToDictionary(country => country.Code, country => country.Name, StringComparer.OrdinalIgnoreCase);
        string CountryName(string code) => countryNames.TryGetValue(code, out var name) ? name : code;

        // Keyed by COUNTRY (dial codes are not unique: +1 spans the NANP); the dial
        // code is the machine Value. Labels reuse the country-code list's names, so
        // the two lists start consistent.
        await contentPartLists.SeedAsync(new CrestContentPartListSeed(
            PhoneCountryCodesKey,
            "Phone country codes",
            [.. GlobalReferenceData.DialCodes
                .OrderBy(entry => CountryName(entry.Code), StringComparer.OrdinalIgnoreCase)
                .Select(entry => new CrestOptionSeed(entry.Code, CountryName(entry.Code), Value: entry.DialCode))],
            DataLock: true));

        // The enum-shaped answer to postal validation: per-country patterns, not
        // postal codes themselves (those are reference DATA, out of scope here).
        await contentPartLists.SeedAsync(new CrestContentPartListSeed(
            PostalFormatsKey,
            "Postal code formats",
            [.. GlobalReferenceData.PostalFormats
                .OrderBy(entry => CountryName(entry.Code), StringComparer.OrdinalIgnoreCase)
                .Select(entry => new CrestOptionSeed(entry.Code, CountryName(entry.Code), Value: entry.Pattern))],
            DataLock: true));

        await contentPartLists.SeedAsync(new CrestContentPartListSeed(
            LanguagesKey,
            "Languages",
            [.. GlobalReferenceData.Languages
                .OrderBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
                .Select(entry => new CrestOptionSeed(entry.Code, entry.Name))],
            DataLock: true));

        // Geographic US area codes, category = state. Overlays get added a few times
        // a year; reseeds (adds-only) are the update path.
        await contentPartLists.SeedAsync(new CrestContentPartListSeed(
            UsAreaCodesKey,
            "US area codes",
            [.. GlobalReferenceData.UsAreaCodes
                .OrderBy(entry => entry.State, StringComparer.OrdinalIgnoreCase)
                .ThenBy(entry => entry.Code, StringComparer.OrdinalIgnoreCase)
                .Select(entry => new CrestOptionSeed(entry.Code, entry.Code, Category: entry.State))],
            DataLock: true));

        // ISO 3166-2, category = owning country's alpha-2 code (the seam a
        // country->subdivision cascade filters on), Value = bare postal abbreviation.
        await contentPartLists.SeedAsync(new CrestContentPartListSeed(
            SubdivisionsKey,
            "Country subdivisions",
            [.. GlobalReferenceData.Subdivisions
                .OrderBy(entry => entry.Country, StringComparer.OrdinalIgnoreCase)
                .ThenBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
                .Select(entry => new CrestOptionSeed(entry.Code, entry.Name, Category: entry.Country, Value: entry.Abbreviation))],
            DataLock: true));

        await contentPartLists.SeedAsync(new CrestContentPartListSeed(
            HonorificsKey,
            "Honorifics",
            [.. GlobalReferenceData.Honorifics
                .Select(entry => new CrestOptionSeed(entry.Key, entry.Label))]));

        await contentPartLists.SeedAsync(new CrestContentPartListSeed(
            NameSuffixesKey,
            "Name suffixes",
            [.. GlobalReferenceData.NameSuffixes
                .Select(entry => new CrestOptionSeed(entry.Key, entry.Label))]));
    }

    internal static readonly (string Code, string Name)[] Countries =
    [
        ("AD", "Andorra"), ("AE", "United Arab Emirates"), ("AF", "Afghanistan"), ("AG", "Antigua and Barbuda"),
        ("AI", "Anguilla"), ("AL", "Albania"), ("AM", "Armenia"), ("AO", "Angola"), ("AQ", "Antarctica"),
        ("AR", "Argentina"), ("AS", "American Samoa"), ("AT", "Austria"), ("AU", "Australia"), ("AW", "Aruba"),
        ("AX", "Åland Islands"), ("AZ", "Azerbaijan"), ("BA", "Bosnia and Herzegovina"), ("BB", "Barbados"),
        ("BD", "Bangladesh"), ("BE", "Belgium"), ("BF", "Burkina Faso"), ("BG", "Bulgaria"), ("BH", "Bahrain"),
        ("BI", "Burundi"), ("BJ", "Benin"), ("BL", "Saint Barthélemy"), ("BM", "Bermuda"),
        ("BN", "Brunei Darussalam"), ("BO", "Bolivia"), ("BQ", "Bonaire, Sint Eustatius and Saba"),
        ("BR", "Brazil"), ("BS", "Bahamas"), ("BT", "Bhutan"), ("BV", "Bouvet Island"), ("BW", "Botswana"),
        ("BY", "Belarus"), ("BZ", "Belize"), ("CA", "Canada"), ("CC", "Cocos (Keeling) Islands"),
        ("CD", "Congo, Democratic Republic of the"), ("CF", "Central African Republic"), ("CG", "Congo"),
        ("CH", "Switzerland"), ("CI", "Côte d'Ivoire"), ("CK", "Cook Islands"), ("CL", "Chile"),
        ("CM", "Cameroon"), ("CN", "China"), ("CO", "Colombia"), ("CR", "Costa Rica"), ("CU", "Cuba"),
        ("CV", "Cabo Verde"), ("CW", "Curaçao"), ("CX", "Christmas Island"), ("CY", "Cyprus"),
        ("CZ", "Czechia"), ("DE", "Germany"), ("DJ", "Djibouti"), ("DK", "Denmark"), ("DM", "Dominica"),
        ("DO", "Dominican Republic"), ("DZ", "Algeria"), ("EC", "Ecuador"), ("EE", "Estonia"), ("EG", "Egypt"),
        ("EH", "Western Sahara"), ("ER", "Eritrea"), ("ES", "Spain"), ("ET", "Ethiopia"), ("FI", "Finland"),
        ("FJ", "Fiji"), ("FK", "Falkland Islands"), ("FM", "Micronesia"), ("FO", "Faroe Islands"),
        ("FR", "France"), ("GA", "Gabon"), ("GB", "United Kingdom"), ("GD", "Grenada"), ("GE", "Georgia"),
        ("GF", "French Guiana"), ("GG", "Guernsey"), ("GH", "Ghana"), ("GI", "Gibraltar"), ("GL", "Greenland"),
        ("GM", "Gambia"), ("GN", "Guinea"), ("GP", "Guadeloupe"), ("GQ", "Equatorial Guinea"), ("GR", "Greece"),
        ("GS", "South Georgia and the South Sandwich Islands"), ("GT", "Guatemala"), ("GU", "Guam"),
        ("GW", "Guinea-Bissau"), ("GY", "Guyana"), ("HK", "Hong Kong"), ("HM", "Heard Island and McDonald Islands"),
        ("HN", "Honduras"), ("HR", "Croatia"), ("HT", "Haiti"), ("HU", "Hungary"), ("ID", "Indonesia"),
        ("IE", "Ireland"), ("IL", "Israel"), ("IM", "Isle of Man"), ("IN", "India"),
        ("IO", "British Indian Ocean Territory"), ("IQ", "Iraq"), ("IR", "Iran"), ("IS", "Iceland"),
        ("IT", "Italy"), ("JE", "Jersey"), ("JM", "Jamaica"), ("JO", "Jordan"), ("JP", "Japan"),
        ("KE", "Kenya"), ("KG", "Kyrgyzstan"), ("KH", "Cambodia"), ("KI", "Kiribati"), ("KM", "Comoros"),
        ("KN", "Saint Kitts and Nevis"), ("KP", "Korea, Democratic People's Republic of"),
        ("KR", "Korea, Republic of"), ("KW", "Kuwait"), ("KY", "Cayman Islands"), ("KZ", "Kazakhstan"),
        ("LA", "Lao People's Democratic Republic"), ("LB", "Lebanon"), ("LC", "Saint Lucia"),
        ("LI", "Liechtenstein"), ("LK", "Sri Lanka"), ("LR", "Liberia"), ("LS", "Lesotho"), ("LT", "Lithuania"),
        ("LU", "Luxembourg"), ("LV", "Latvia"), ("LY", "Libya"), ("MA", "Morocco"), ("MC", "Monaco"),
        ("MD", "Moldova"), ("ME", "Montenegro"), ("MF", "Saint Martin (French part)"), ("MG", "Madagascar"),
        ("MH", "Marshall Islands"), ("MK", "North Macedonia"), ("ML", "Mali"), ("MM", "Myanmar"),
        ("MN", "Mongolia"), ("MO", "Macao"), ("MP", "Northern Mariana Islands"), ("MQ", "Martinique"),
        ("MR", "Mauritania"), ("MS", "Montserrat"), ("MT", "Malta"), ("MU", "Mauritius"), ("MV", "Maldives"),
        ("MW", "Malawi"), ("MX", "Mexico"), ("MY", "Malaysia"), ("MZ", "Mozambique"), ("NA", "Namibia"),
        ("NC", "New Caledonia"), ("NE", "Niger"), ("NF", "Norfolk Island"), ("NG", "Nigeria"),
        ("NI", "Nicaragua"), ("NL", "Netherlands"), ("NO", "Norway"), ("NP", "Nepal"), ("NR", "Nauru"),
        ("NU", "Niue"), ("NZ", "New Zealand"), ("OM", "Oman"), ("PA", "Panama"), ("PE", "Peru"),
        ("PF", "French Polynesia"), ("PG", "Papua New Guinea"), ("PH", "Philippines"), ("PK", "Pakistan"),
        ("PL", "Poland"), ("PM", "Saint Pierre and Miquelon"), ("PN", "Pitcairn"), ("PR", "Puerto Rico"),
        ("PS", "Palestine, State of"), ("PT", "Portugal"), ("PW", "Palau"), ("PY", "Paraguay"), ("QA", "Qatar"),
        ("RE", "Réunion"), ("RO", "Romania"), ("RS", "Serbia"), ("RU", "Russian Federation"), ("RW", "Rwanda"),
        ("SA", "Saudi Arabia"), ("SB", "Solomon Islands"), ("SC", "Seychelles"), ("SD", "Sudan"),
        ("SE", "Sweden"), ("SG", "Singapore"), ("SH", "Saint Helena, Ascension and Tristan da Cunha"),
        ("SI", "Slovenia"), ("SJ", "Svalbard and Jan Mayen"), ("SK", "Slovakia"), ("SL", "Sierra Leone"),
        ("SM", "San Marino"), ("SN", "Senegal"), ("SO", "Somalia"), ("SR", "Suriname"), ("SS", "South Sudan"),
        ("ST", "Sao Tome and Principe"), ("SV", "El Salvador"), ("SX", "Sint Maarten (Dutch part)"),
        ("SY", "Syrian Arab Republic"), ("SZ", "Eswatini"), ("TC", "Turks and Caicos Islands"), ("TD", "Chad"),
        ("TF", "French Southern Territories"), ("TG", "Togo"), ("TH", "Thailand"), ("TJ", "Tajikistan"),
        ("TK", "Tokelau"), ("TL", "Timor-Leste"), ("TM", "Turkmenistan"), ("TN", "Tunisia"), ("TO", "Tonga"),
        ("TR", "Türkiye"), ("TT", "Trinidad and Tobago"), ("TV", "Tuvalu"), ("TW", "Taiwan"),
        ("TZ", "Tanzania"), ("UA", "Ukraine"), ("UG", "Uganda"), ("UM", "United States Minor Outlying Islands"),
        ("US", "United States of America"), ("UY", "Uruguay"), ("UZ", "Uzbekistan"), ("VA", "Holy See"),
        ("VC", "Saint Vincent and the Grenadines"), ("VE", "Venezuela"), ("VG", "Virgin Islands (British)"),
        ("VI", "Virgin Islands (U.S.)"), ("VN", "Viet Nam"), ("VU", "Vanuatu"), ("WF", "Wallis and Futuna"),
        ("WS", "Samoa"), ("YE", "Yemen"), ("YT", "Mayotte"), ("ZA", "South Africa"), ("ZM", "Zambia"),
        ("ZW", "Zimbabwe"),
    ];

    // Name is the singular label; Plural is what a quantity greater than one reads
    // ("3 Boxes"). Invariant-count units (Each, Dozen, Gross) repeat the singular.
    private static readonly (string Code, string Name, string Plural, string Category)[] UnitsOfMeasure =
    [
        // Count
        ("ea", "Each", "Each", "Count"), ("pr", "Pair", "Pairs", "Count"), ("dz", "Dozen", "Dozen", "Count"),
        ("gro", "Gross", "Gross", "Count"), ("bx", "Box", "Boxes", "Count"), ("cs", "Case", "Cases", "Count"),
        ("pk", "Pack", "Packs", "Count"), ("plt", "Pallet", "Pallets", "Count"), ("rl", "Roll", "Rolls", "Count"),
        ("st", "Set", "Sets", "Count"),
        // Mass
        ("mg", "Milligram", "Milligrams", "Mass"), ("g", "Gram", "Grams", "Mass"), ("kg", "Kilogram", "Kilograms", "Mass"),
        ("t", "Metric ton", "Metric tons", "Mass"), ("oz", "Ounce", "Ounces", "Mass"), ("lb", "Pound", "Pounds", "Mass"),
        ("ton", "Short ton", "Short tons", "Mass"),
        // Volume
        ("ml", "Milliliter", "Milliliters", "Volume"), ("cl", "Centiliter", "Centiliters", "Volume"), ("l", "Liter", "Liters", "Volume"),
        ("m3", "Cubic meter", "Cubic meters", "Volume"), ("floz", "Fluid ounce", "Fluid ounces", "Volume"), ("pt", "Pint", "Pints", "Volume"),
        ("qt", "Quart", "Quarts", "Volume"), ("gal", "Gallon", "Gallons", "Volume"), ("ft3", "Cubic foot", "Cubic feet", "Volume"),
        // Length
        ("mm", "Millimeter", "Millimeters", "Length"), ("cm", "Centimeter", "Centimeters", "Length"), ("m", "Meter", "Meters", "Length"),
        ("km", "Kilometer", "Kilometers", "Length"), ("in", "Inch", "Inches", "Length"), ("ft", "Foot", "Feet", "Length"),
        ("yd", "Yard", "Yards", "Length"), ("mi", "Mile", "Miles", "Length"),
        // Area
        ("m2", "Square meter", "Square meters", "Area"), ("ha", "Hectare", "Hectares", "Area"), ("ft2", "Square foot", "Square feet", "Area"),
        ("ac", "Acre", "Acres", "Area"),
        // Time
        ("s", "Second", "Seconds", "Time"), ("min", "Minute", "Minutes", "Time"), ("hr", "Hour", "Hours", "Time"),
        ("day", "Day", "Days", "Time"), ("wk", "Week", "Weeks", "Time"), ("mo", "Month", "Months", "Time"), ("yr", "Year", "Years", "Time"),
        // Temperature
        ("cel", "Degree Celsius", "Degrees Celsius", "Temperature"), ("fah", "Degree Fahrenheit", "Degrees Fahrenheit", "Temperature"),
        ("kel", "Kelvin", "Kelvins", "Temperature"),
    ];
}
