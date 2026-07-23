# NPC Persona Reference

Every NPC that Animator generates is backed by a single rich object called an **`NpcProfile`**. This is the persona: the full portfolio of who the NPC is — their name and face, where they live, who's in their family, where they went to school, where they work, what they're paid, what motivates them, what they're worried about, and the digital accounts they leave behind.

This page is the field-by-field map of that object. Use it when you are authoring a scenario and want to know **exactly which values you can pull from an NPC** — to seed content, drive relationships, build an insider-threat storyline, or template realistic documents and messages.

???+ tip "How to read this page"
    - Every value below is a **real property on the `NpcProfile` model** ([`src/Ghosts.Animator/Models/NpcProfile.cs`](https://github.com/cmu-sei/GHOSTS/blob/master/src/Ghosts.Animator/Models/NpcProfile.cs)). If it's documented here, you can reference it.
    - The persona is stored as JSON (`jsonb` in Postgres) and returned as JSON from the API, so the field names below are the JSON keys you'll actually see.
    - Nested objects (like `Name`, `Employment`, `InsiderThreat`) are expanded in their own sections.
    - Fields that are **enums** serialize as their string name (e.g. `"Male"`, `"Bachelors"`), not their number.

---

## The persona at a glance

An `NpcProfile` groups its data into these areas. Jump to any section for the full field list.

| Area | What it holds | Section |
|---|---|---|
| :material-account: **Identity** | Name, sex, birthdate, email, phones, password, photo, ID card | [Identity & contact](#identity-contact) |
| :material-map-marker: **Location** | One or more home/mailing addresses | [Addresses](#addresses) |
| :material-human-male-height: **Physical & health** | Height, weight, blood type, meals, medical conditions, prescriptions | [Health](#health) |
| :material-account-group: **Family & relationships** | Household members and links to other NPCs | [Family & relationships](#family-relationships) |
| :material-school: **Education** | Degrees, majors, and schools | [Education](#education) |
| :material-briefcase: **Employment** | Full job history — companies, titles, salary, managers | [Employment](#employment) |
| :material-medal: **Military** | Branch, rank, pay grade, MOS, and unit hierarchy | [Military rank & unit](#military-rank-unit) |
| :material-cash: **Finances** | Net worth, debt, credit cards | [Finances](#finances) |
| :material-brain: **Mind & motivation** | Performance/personality scores and Reiss-style motivations | [Mental health](#mental-health) · [Motivations](#motivations) |
| :material-airplane: **Foreign travel** | Trips abroad with dates and destinations | [Foreign travel](#foreign-travel) |
| :material-shield-alert: **Insider threat** | CDSE-aligned risk indicators and related events | [Insider threat](#insider-threat) |
| :material-account-key: **Digital footprint** | Online accounts, credentials, and the workstation they use | [Accounts](#accounts) · [Workstation](#workstation) |
| :material-tune: **Preferences & attributes** | Scored preferences and free-form key/value metadata | [Preferences & attributes](#preferences-attributes) |

---

## Identity & contact

The core "who is this person" fields live at the top level of the profile.

| Field | Type | Example | Notes |
|---|---|---|---|
| `Id` | GUID | `"e6f1…"` | Unique NPC id. Referenced by `Relationships` and `Manager` fields on other NPCs. |
| `Name` | [`NameProfile`](#name) | see below | Structured name. |
| `Email` | string | `"jane.doe@brigade.mil"` | Primary email. Employment records can carry their own work email too. |
| `Password` | string | `"Sp1derM@n!"` | Generated primary password. |
| `HomePhone` | string | `"(703) 555-0143"` | |
| `CellPhone` | string | `"(703) 555-0197"` | |
| `BiologicalSex` | enum | `"Male"` | `Female` or `Male`. See [enums](#enum-reference). |
| `Birthdate` | date-time | `"1989-04-12T00:00:00"` | Drives age; the content engine will even post a birthday tweet on the day. |
| `CAC` | string | `"1234567890"` | Common Access Card identifier (military ID). |
| `PhotoLink` | string (URL) | `"https://…/face.jpg"` | Generated headshot for the NPC. |
| `Created` | date-time | `"2026-07-23T14:02:00Z"` | When the persona was generated (UTC). |

### Name

`NpcProfile.Name` — a structured `NameProfile`. Used everywhere a person's name appears.

| Field | Type | Example |
|---|---|---|
| `Prefix` | string | `"Ms."` |
| `First` | string | `"Jane"` |
| `Middle` | string | `"Marie"` |
| `Last` | string | `"Doe"` |
| `Suffix` | string | `"Jr."` |

!!! note
    `NameProfile.ToString()` renders a clean display name from whatever parts are present (e.g. `"Jane Marie Doe"`), so you can use the whole object where a single name string is expected.

---

## Addresses

`NpcProfile.Address` is a **list** of `AddressProfile` — an NPC can have more than one (home, mailing, etc.).

| Field | Type | Example |
|---|---|---|
| `AddressType` | string | `"Home"` |
| `Name` | string | `"Jane Doe"` |
| `Address1` | string | `"1600 Defense Blvd"` |
| `Address2` | string | `"Apt 4B"` |
| `City` | string | `"Arlington"` |
| `State` | string | `"VA"` |
| `PostalCode` | string | `"22202"` |

!!! info "International addresses"
    Foreign travel and military-unit locations use a separate `InternationalAddressProfile` shape with lowercase JSON keys: `country`, `geonameid`, `name` (the city), and `subcountry`.

---

## Health

`NpcProfile.Health` — a `HealthProfile` covering physical characteristics and medical background.

| Field | Type | Example | Notes |
|---|---|---|---|
| `Height` | int | `68` | Inches. |
| `Weight` | int | `160` | Pounds. |
| `BloodType` | string | `"O+"` | Free-form string. |
| `PreferredMeal` | string | `"Pad Thai"` | |
| `MedicalConditions` | list of `MedicalCondition` | see below | |

**`MedicalCondition`**

| Field | Type | Example |
|---|---|---|
| `Name` | string | `"Hypertension"` |
| `Prescriptions` | list of `Prescription` | |

**`Prescription`**

| Field | Type | Example |
|---|---|---|
| `Name` | string | `"Lisinopril"` |

---

## Family & relationships

There are two distinct concepts here:

**`NpcProfile.Family`** — the household. A `FamilyProfile` with a `Members` list.

Each member (`FamilyProfile.Person`):

| Field | Type | Example |
|---|---|---|
| `Name` | [`NameProfile`](#name) | `"John Doe"` |
| `Relationship` | string | `"Father"`, `"Spouse"`, `"Daughter"` |

**`NpcProfile.Relationships`** — links to **other NPCs** in the same cohort (used by the social graph). Each `RelationshipProfile`:

| Field | Type | Example | Notes |
|---|---|---|---|
| `Id` | int | `3` | |
| `With` | GUID | `"e6f1…"` | The `Id` of another NPC. |
| `Type` | string | `"Colleague"` | |

!!! tip "Building social scenarios"
    Use `Family.Members` for offline, personal storylines (a spouse, a sick parent) and `Relationships[].With` to traverse the professional/social network between NPCs.

---

## Education

`NpcProfile.Education` — an `EducationProfile` with a `Degrees` list. Each `Degree`:

| Field | Type | Example | Notes |
|---|---|---|---|
| `Level` | enum | `"Bachelors"` | See [`DegreeLevel`](#enum-reference). |
| `DegreeType` | string | `"BS"` | |
| `Major` | string | `"Computer Science"` | |
| `School` | object | see below | |

**`School`**

| Field | Type | Example |
|---|---|---|
| `Name` | string | `"Virginia Tech"` |
| `Location` | string | `"Blacksburg, VA"` |

---

## Employment

`NpcProfile.Employment` — an `EmploymentProfile` holding an `EmploymentRecords` list. This is a **full job history**, so an NPC can have several records ordered over time.

Each `EmploymentRecord`:

| Field | Type | Example | Notes |
|---|---|---|---|
| `Company` | string | `"Acme Defense"` | |
| `StartDate` | date-time | `"2018-06-01T00:00:00"` | |
| `EndDate` | date-time? | `null` | `null` means current job. |
| `Department` | string | `"IT"` | |
| `Organization` | string | `"Network Operations"` | |
| `JobTitle` | string | `"Systems Administrator"` | |
| `Level` | int | `3` | Seniority level. |
| `Salary` | double | `92000` | |
| `Manager` | GUID | `"e6f1…"` | `Id` of the NPC who is this person's manager. |
| `EmailSuffix` | string | `"@acme.com"` | |
| `Email` | string | `"jdoe@acme.com"` | Work email. |
| `Address` | [`AddressProfile`](#addresses) | | Work address. |
| `Phone` | string | `"(703) 555-0110"` | Work phone. |
| `EmploymentStatus` | enum | `"FullTime"` | See [`EmploymentStatuses`](#enum-reference). |

---

## Military rank & unit

For military personas, rank and unit are populated. `NpcProfile.Rank` is a `Rank` object:

| Field | Type | Example | Notes |
|---|---|---|---|
| `Branch` | enum | `"USARMY"` | See [`MilitaryBranch`](#enum-reference). |
| `Pay` | string | `"E-5"` | Pay grade. |
| `Name` | string | `"Sergeant"` | |
| `Abbr` | string | `"SGT"` | |
| `Classification` | string | `"Enlisted"` | |
| `Billet` | string | `"Squad Leader"` | |
| `MOS` | string | `"25B"` | Military Occupational Specialty. |
| `MOSID` | string | `"Information Technology Specialist"` | |
| `Probability` | double | `0.14` | Weighting used during generation. |

`NpcProfile.Unit` is a `MilitaryUnit` that can nest arbitrarily deep:

| Field | Type | Notes |
|---|---|---|
| `Country` | string | |
| `Address` | [`AddressProfile`](#addresses) | Unit location. |
| `Sub` | list of `Unit` | Sub-units, each with `Name`, `Type`, `Nick`, `HQ`, and its own nested `Sub`. |

---

## Finances

`NpcProfile.Finances` — a `FinancialProfile`.

| Field | Type | Example |
|---|---|---|
| `NetWorth` | double | `145000` |
| `TotalDebt` | double | `38000` |
| `CreditCards` | list of `CreditCard` | see below |

**`CreditCard`**

| Field | Type | Example |
|---|---|---|
| `Number` | string | `"4111 1111 1111 1111"` |
| `Type` | string | `"Visa"` |

!!! tip "Financial stress"
    A high `TotalDebt` relative to `NetWorth` pairs naturally with the [insider-threat](#insider-threat) `FinancialConsiderations` indicators for building a financially-motivated risk storyline.

---

## Mental health

`NpcProfile.MentalHealth` — a `MentalHealthProfile`. Every field is an integer **score** you can threshold on. These drive personality, performance, and how "on edge" an NPC is.

| Field | Type | Meaning |
|---|---|---|
| `InterpersonalSkills` | int | Emotional intelligence / getting along with others. |
| `AdherenceToPolicy` | int | How closely they follow the rules. |
| `EnthusiasmAndAttitude` | int | Positivity and drive. |
| `OpenToFeedback` | int | Receptiveness to criticism. |
| `GeneralPerformance` | int | Day-to-day performance. |
| `OverallPerformance` | int | Aggregate performance. |
| `IQ` | int | Intelligence score. |
| `SpideySense` | int | Instinct for detecting something off. |
| `SenseSomethingIsWrongQuotient` | int | Suspicion level. |
| `HappyQuotient` | int | Baseline happiness. |
| `MelancholyQuotient` | int | Baseline sadness. |

---

## Motivations

`NpcProfile.MotivationalProfile` implements the [Reiss Motivational Profile](https://www.reissmotivationprofile.com/) — 16 basic desires. Each is a **double** (roughly `-2` to `2`) expressing how strongly, relative to the population, the NPC is driven by that desire. This is the primary input to the [animation decision-making framework](jobs.md#decision-making-framework).

| Desire | Desire | Desire | Desire |
|---|---|---|---|
| `Acceptance` | `Beauty` | `Curiosity` | `Eating` |
| `Family` | `Honor` | `Idealism` | `Independence` |
| `Order` | `PhysicalActivity` | `Power` | `Saving` |
| `SocialContact` | `Status` | `Tranquility` | `Vengeance` |

---

## Foreign travel

`NpcProfile.ForeignTravel` — a `ForeignTravelProfile` with a `Trips` list. Each `Trip`:

| Field | Type | Example |
|---|---|---|
| `Code` | string | `"DEU"` |
| `Country` | string | `"Germany"` |
| `Destination` | string | `"Berlin"` |
| `ArriveDestination` | date-time | `"2024-08-02T00:00:00"` |
| `DepartDestination` | date-time | `"2024-08-16T00:00:00"` |

---

## Insider threat

`NpcProfile.InsiderThreat` is an `InsiderThreatProfile` structured around **CDSE (Center for Development of Security Excellence) Insider Threat Potential Indicators**. It's the richest area for security-training scenarios.

| Field | Type | Notes |
|---|---|---|
| `IsBackgroundCheckStatusClear` | bool | Did their background check come back clean? |
| `Access` | `AccessProfile` | Access-related indicators (expanded below). |
| `CriminalViolentOrAbusiveConduct` | indicator profile | |
| `FinancialConsiderations` | indicator profile | Pairs with [Finances](#finances). |
| `ForeignConsiderations` | indicator profile | Pairs with [Foreign travel](#foreign-travel). |
| `JudgementCharacterAndPsychologicalConditions` | indicator profile | |
| `ProfessionalLifecycleAndPerformance` | indicator profile | Pairs with [Employment](#employment). |
| `SecurityAndComplianceIncidents` | indicator profile | |
| `SubstanceAbuseAndAddictiveBehaviors` | indicator profile | |
| `TechnicalActivity` | indicator profile | |

**Every indicator profile** shares the same base shape (`InsiderThreatBaseProfile`):

| Field | Type | Notes |
|---|---|---|
| `Id` | int | |
| `RelatedEvents` | list of `RelatedEvent` | The concrete incidents behind the indicator. |

**`RelatedEvent`** — a single observable event:

| Field | Type | Example |
|---|---|---|
| `Id` | int | `1` |
| `Description` | string | `"Attempted access to a restricted share"` |
| `CorrectiveAction` | string | `"Access revoked; counseled"` |
| `ReportedBy` | string | `"SOC Analyst"` |
| `Reported` | date-time | `"2025-02-10T00:00:00"` |

**`Access`** additionally carries:

| Field | Type | Example |
|---|---|---|
| `SecurityClearance` | string | `"Secret"` |
| `PhysicalAccess` | string | `"Building A, Floors 1–3"` |
| `SystemsAccess` | string | `"Domain admin"` |
| `IsDoDSystemsPrivilegedUser` | bool? | `true` |
| `ExplosivesAccess` | string | |
| `CBRNAccess` | string | Chemical/Biological/Radiological/Nuclear. |

---

## Accounts

`NpcProfile.Accounts` is a list of online accounts (social media, services, etc.). Each `Account`:

| Field | Type | Example |
|---|---|---|
| `Id` | int | `1` |
| `Url` | string | `"https://twitter.com/janedoe"` |
| `Username` | string | `"janedoe"` |
| `Password` | string | `"hunter2"` |

---

## Workstation

`NpcProfile.Workstation` — the `MachineProfile` for the computer this NPC uses.

| Field | Type | Example |
|---|---|---|
| `Name` | string | `"WKS-OPS-014"` |
| `Domain` | string | `"BRIGADE"` |
| `Username` | string | `"jdoe"` |
| `Password` | string | `"P@ssw0rd!"` |
| `IPAddress` | string | `"10.1.4.14"` |

---

## Preferences & attributes

**`NpcProfile.Preferences`** — a list of scored, named preferences you define and the animation engine can evolve. Each `Preference`:

| Field | Type | Example |
|---|---|---|
| `Id` | int | `1` |
| `Name` | string | `"Coffee"` |
| `Score` | int | `78` |
| `Meta` | string | `"prefers cold brew"` |

**`NpcProfile.Attributes`** — a free-form `string → string` dictionary for any extra metadata you want to carry on the persona.

---

## Enum reference

Enums serialize to their **string name** in JSON.

| Enum | Values |
|---|---|
| `BiologicalSex` | `Female`, `Male` |
| `DegreeLevel` | `GED`, `HSDiploma`, `Associates`, `Bachelors`, `Masters`, `Doctorate`, `Professional`, `None` |
| `EmploymentStatuses` | `FullTime`, `PartTime`, `Suspended`, `Temporary`, `Resigned`, `Terminated` |
| `MilitaryBranch` | `USAF`, `USARMY`, `USCG`, `USMC`, `USN` |

---

## Full example persona (JSON)

A trimmed but representative persona as returned by the API. Optional/empty collections are omitted for brevity.

```json
{
  "Id": "e6f1a3c2-9d4b-4f2a-8b1e-2c7d9f0a1b23",
  "Name": { "Prefix": "Ms.", "First": "Jane", "Middle": "Marie", "Last": "Doe", "Suffix": "" },
  "Email": "jane.doe@brigade.mil",
  "Password": "Sp1derM@n!",
  "HomePhone": "(703) 555-0143",
  "CellPhone": "(703) 555-0197",
  "BiologicalSex": "Female",
  "Birthdate": "1989-04-12T00:00:00",
  "CAC": "1234567890",
  "PhotoLink": "https://ghosts.example/faces/e6f1a3c2.jpg",
  "Created": "2026-07-23T14:02:00Z",

  "Address": [
    { "AddressType": "Home", "Name": "Jane Doe", "Address1": "1600 Defense Blvd",
      "Address2": "Apt 4B", "City": "Arlington", "State": "VA", "PostalCode": "22202" }
  ],

  "Health": {
    "Height": 66, "Weight": 150, "BloodType": "O+", "PreferredMeal": "Pad Thai",
    "MedicalConditions": [
      { "Name": "Hypertension", "Prescriptions": [ { "Name": "Lisinopril" } ] }
    ]
  },

  "Family": {
    "Members": [
      { "Name": { "First": "John", "Last": "Doe" }, "Relationship": "Spouse" },
      { "Name": { "First": "Emma", "Last": "Doe" }, "Relationship": "Daughter" }
    ]
  },
  "Relationships": [
    { "Id": 1, "With": "a1b2c3d4-0000-0000-0000-000000000001", "Type": "Colleague" }
  ],

  "Education": {
    "Degrees": [
      { "Level": "Bachelors", "DegreeType": "BS", "Major": "Computer Science",
        "School": { "Name": "Virginia Tech", "Location": "Blacksburg, VA" } }
    ]
  },

  "Employment": {
    "EmploymentRecords": [
      { "Company": "Acme Defense", "StartDate": "2018-06-01T00:00:00", "EndDate": null,
        "Department": "IT", "Organization": "Network Operations",
        "JobTitle": "Systems Administrator", "Level": 3, "Salary": 92000,
        "Manager": "a1b2c3d4-0000-0000-0000-000000000009",
        "Email": "jdoe@acme.com", "EmailSuffix": "@acme.com",
        "Phone": "(703) 555-0110", "EmploymentStatus": "FullTime" }
    ]
  },

  "Rank": { "Branch": "USARMY", "Pay": "E-5", "Name": "Sergeant", "Abbr": "SGT",
            "Classification": "Enlisted", "Billet": "Squad Leader",
            "MOS": "25B", "MOSID": "Information Technology Specialist", "Probability": 0.14 },

  "Finances": {
    "NetWorth": 145000, "TotalDebt": 38000,
    "CreditCards": [ { "Number": "4111 1111 1111 1111", "Type": "Visa" } ]
  },

  "MentalHealth": {
    "InterpersonalSkills": 72, "AdherenceToPolicy": 60, "EnthusiasmAndAttitude": 80,
    "OpenToFeedback": 55, "GeneralPerformance": 78, "OverallPerformance": 76,
    "IQ": 118, "SpideySense": 40, "SenseSomethingIsWrongQuotient": 35,
    "HappyQuotient": 65, "MelancholyQuotient": 30
  },

  "MotivationalProfile": {
    "Acceptance": 0.3, "Beauty": -0.1, "Curiosity": 1.2, "Eating": 0.0,
    "Family": 1.5, "Honor": 0.8, "Idealism": 0.4, "Independence": 0.9,
    "Order": 1.1, "PhysicalActivity": -0.2, "Power": 0.1, "Saving": 0.6,
    "SocialContact": 0.7, "Status": 0.2, "Tranquility": -0.4, "Vengeance": -0.9
  },

  "ForeignTravel": {
    "Trips": [
      { "Code": "DEU", "Country": "Germany", "Destination": "Berlin",
        "ArriveDestination": "2024-08-02T00:00:00", "DepartDestination": "2024-08-16T00:00:00" }
    ]
  },

  "InsiderThreat": {
    "IsBackgroundCheckStatusClear": true,
    "Access": {
      "Id": 1, "SecurityClearance": "Secret", "PhysicalAccess": "Building A, Floors 1-3",
      "SystemsAccess": "Domain admin", "IsDoDSystemsPrivilegedUser": true,
      "RelatedEvents": []
    },
    "FinancialConsiderations": {
      "Id": 2,
      "RelatedEvents": [
        { "Id": 1, "Description": "Reported gambling debt", "CorrectiveAction": "Referred to EAP",
          "ReportedBy": "Supervisor", "Reported": "2025-02-10T00:00:00" }
      ]
    }
  },

  "Accounts": [
    { "Id": 1, "Url": "https://twitter.com/janedoe", "Username": "janedoe", "Password": "hunter2" }
  ],

  "Workstation": { "Name": "WKS-OPS-014", "Domain": "BRIGADE", "Username": "jdoe",
                   "Password": "P@ssw0rd!", "IPAddress": "10.1.4.14" },

  "Preferences": [ { "Id": 1, "Name": "Coffee", "Score": 78, "Meta": "prefers cold brew" } ],
  "Attributes": { "Hobby": "Rock climbing", "FavoriteColor": "green" }
}
```

---

## Using persona values in scenarios

Persona values aren't just for display — they actively drive what an NPC does and says.

### Content generation draws from the profile

When the API generates social content for an NPC (the built-in native formatter, or an LLM formatter), it reaches into the profile for realistic, self-consistent details. For example, the native formatter can produce a post about the NPC's **address**, **family** member, **employment**, **education**, or an **account** — and on the NPC's `Birthdate`, it posts a birthday message automatically.

This is exactly the leakage the [animation model](jobs.md) is designed to explore: an NPC discloses a true detail about themselves, and other agents — or an adversary — can observe and infer from it.

!!! warning "Sensitive fields are stripped for some content flows"
    Certain generation paths deliberately null out sensitive fields (e.g. `Rank`, `CAC`, `Unit`) before handing the profile to a content engine, so those values don't leak into generated text. Keep this in mind when deciding which fields to build a scenario around.

### Retrieving personas via the API

Fetch generated NPCs (and their full profiles) to template documents, seed emails, or wire up a scenario:

```bash
# List NPCs
curl http://localhost:5000/api/npcs

# Generate NPCs, then read back their personas
curl -X POST http://localhost:5000/api/npcsgenerate/one
```

See the [Animator overview](index.md) for generation options (campaign, enclave, team, rank distribution) and the Swagger docs at `http://localhost:5000/swagger` for the complete API surface.
