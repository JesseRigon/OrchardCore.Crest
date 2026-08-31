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

        return Task.FromResult(3);
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

        return Task.FromResult(3);
    }

    // Tenants seeded while every option carried Position 0: give the manual order
    // real, sequential positions (countries by name, units grouped by dimension) so
    // the Position index column and drag-and-drop reordering start from something
    // meaningful rather than a wall of zeroes.
    public Task<int> UpdateFrom2Async()
    {
        ShellScope.AddDeferredTask(async scope =>
            await BackfillPositionsAsync(scope.ServiceProvider.GetRequiredService<ICrestContentPartListService>()));

        return Task.FromResult(3);
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
        // codes - each/pallet/case-style trade codes are a mapping concern, and the
        // Rec 20 map is a later, separate dataset). Categories are the DIMENSION the
        // unit measures; the initial manual positions group by dimension, and an
        // instance that wants category grouping sorts by the Category column.
        await contentPartLists.SeedAsync(new CrestContentPartListSeed(
            UnitsOfMeasureKey,
            "Units of measure",
            [.. UnitsOfMeasure
                .OrderBy(unit => unit.Category, StringComparer.OrdinalIgnoreCase)
                .ThenBy(unit => unit.Name, StringComparer.OrdinalIgnoreCase)
                .Select(unit => new CrestOptionSeed(unit.Code, unit.Name, Category: unit.Category))],
            DataLock: true));
    }

    private static readonly (string Code, string Name)[] Countries =
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

    private static readonly (string Code, string Name, string Category)[] UnitsOfMeasure =
    [
        // Count
        ("ea", "Each", "Count"), ("pr", "Pair", "Count"), ("dz", "Dozen", "Count"),
        ("gro", "Gross", "Count"), ("bx", "Box", "Count"), ("cs", "Case", "Count"),
        ("pk", "Pack", "Count"), ("plt", "Pallet", "Count"), ("rl", "Roll", "Count"),
        ("st", "Set", "Count"),
        // Mass
        ("mg", "Milligram", "Mass"), ("g", "Gram", "Mass"), ("kg", "Kilogram", "Mass"),
        ("t", "Metric ton", "Mass"), ("oz", "Ounce", "Mass"), ("lb", "Pound", "Mass"),
        ("ton", "Short ton", "Mass"),
        // Volume
        ("ml", "Milliliter", "Volume"), ("cl", "Centiliter", "Volume"), ("l", "Liter", "Volume"),
        ("m3", "Cubic meter", "Volume"), ("floz", "Fluid ounce", "Volume"), ("pt", "Pint", "Volume"),
        ("qt", "Quart", "Volume"), ("gal", "Gallon", "Volume"), ("ft3", "Cubic foot", "Volume"),
        // Length
        ("mm", "Millimeter", "Length"), ("cm", "Centimeter", "Length"), ("m", "Meter", "Length"),
        ("km", "Kilometer", "Length"), ("in", "Inch", "Length"), ("ft", "Foot", "Length"),
        ("yd", "Yard", "Length"), ("mi", "Mile", "Length"),
        // Area
        ("m2", "Square meter", "Area"), ("ha", "Hectare", "Area"), ("ft2", "Square foot", "Area"),
        ("ac", "Acre", "Area"),
        // Time
        ("s", "Second", "Time"), ("min", "Minute", "Time"), ("hr", "Hour", "Time"),
        ("day", "Day", "Time"), ("wk", "Week", "Time"), ("mo", "Month", "Time"), ("yr", "Year", "Time"),
        // Temperature
        ("cel", "Degree Celsius", "Temperature"), ("fah", "Degree Fahrenheit", "Temperature"),
        ("kel", "Kelvin", "Temperature"),
    ];
}
