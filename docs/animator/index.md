# GHOSTS ANIMATOR Overview

???+ info "Animator Integration"
    As of GHOSTS v8.0, Animator functionality has been integrated directly into the main GHOSTS API. The standalone [GHOSTS ANIMATOR repository](https://github.com/cmu-sei/GHOSTS-ANIMATOR) has been archived.

    All Animator features are now accessible through the GHOSTS API and UI. No separate installation is required.

## What is Animator?

Animator is GHOSTS' NPC (Non-Player Character) generation and animation system. It creates realistic user profiles with extensive personal details and can dynamically update NPC behaviors during exercises and training events.

Animator brings NPCs to life in two ways:

1. **Initial Creation**

    Animator creates the initial NPC profile, including details such as name, address, career, finances, and family members. Based on configuration, it can place users in a multi-level organizational structure, and establish relationships between users.

2. **Animation Jobs**

    Via jobs that can be run during training and exercise events, Animator can update the NPC's preferences, beliefs, and relationships. This enables dynamic NPCs that change over time.

At its core, Animator is a realistic user detail generator. It creates sufficiently realistic identities with verbose portfolios of personal information. Each NPC has numerous categories of details and metadata that define who they are. Information is generated using sourced datasets to distribute characteristics realistically. As we like to say, it creates "NPCs so real, they sell for a premium on the dark web."[^1]

## Accessing Animator Features

Since Animator is now integrated into the GHOSTS API, you access it through:

**1. GHOSTS API** (Port 5000)

   - API endpoints for NPC generation and management
   - Programmatic access to all Animator functions
   - See API documentation at `http://localhost:5000/swagger`

**2. GHOSTS UI** (Port 8080)

   - Visual interface for generating and managing NPCs
   - Click "Generate Random NPCs" to create new characters
   - View and edit NPC details, organizational structure, and relationships
   - See the [UI documentation](../core/ui.md#npcs) for details

**3. Animator Jobs** (via UI or API)

   - Schedule jobs to update NPC behaviors dynamically
   - See the [Jobs documentation](jobs.md) for configuration details

## Prerequisites

Animator is included when you install the GHOSTS API. If you haven't already:

1. Follow the [API installation guide](../core/api.md)
2. Ensure all containers are running: `docker ps -a`
3. Access the UI at `http://localhost:8080`

## Use Cases

Animator-generated NPCs serve multiple purposes in training, research, and security contexts:

### 1. Training Machine Learning Algorithms

Animator creates large sets of hyper-realistic user data ideal for training ML models. The 100+ data points per NPC enable rapid training of anthropology-related algorithms without privacy concerns or data acquisition costs.[^2]

**Benefits:**

- Unlimited synthetic training data
- Realistic demographic distributions
- No privacy or compliance issues
- Configurable characteristics

### 2. Honeypot and Deception Operations

NPC profiles are designed to be convincingly real while completely fabricated, making them perfect for deception operations.

**Applications:**

- Honeypot user accounts that appear legitimate
- Decoy documents with realistic author metadata
- Fake employee directories
- Realistic email conversations

### 3. Insider Threat Modeling

Each NPC receives an Insider Threat Profile based on CDSE (Center for Development of Security Excellence) Insider Threat Potential Indicators.

**Profile Factors:**

- Financial stress indicators
- Foreign contacts and travel
- Criminal history
- Mental health considerations
- Social engineering vulnerability

This enables realistic insider threat scenarios and training for security teams.

### 4. Social Network and Relationship Modeling

Animator generates interconnected NPCs with realistic social relationships:

**Features:**

- Family relationships (spouses, children, parents)
- Professional networks (colleagues, supervisors)
- Multi-level organizational structures
- Friendship and social connections

Use this for social network analysis research, organizational simulations, or realistic communication patterns.

## How NPC Generation Works

**Step 1: Profile Creation**

- Animator creates an empty NPC profile template

**Step 2: Data Point Generation**

- Iterates through 100+ data points including:
  - Personal: Name, age, gender, ethnicity
  - Location: Address, city, state, country
  - Professional: Career, employer, salary, job history
  - Financial: Bank accounts, credit score, assets
  - Social: Family members, relationships, social media
  - Background: Education, criminal history, military service
  - Health: Medical conditions, mental health status

**Step 3: Weighted Randomization**

- Uses verified datasets to ensure realistic distributions
- For example, name frequency matches real-world demographics
- Geographic data reflects actual population distributions
- Career choices align with education and location

**Step 4: Relationship Establishment**

- Creates family structures (if configured)
- Establishes organizational relationships
- Generates social connections

**Step 5: Storage**

- NPCs are stored in the Postgres database
- Accessible via API and UI
- Can be exported in various formats

## Generating NPCs via UI

1. Navigate to `http://localhost:8080`
2. Click on the "NPCs" section
3. Click "Generate Random NPCs"
4. Configure generation parameters:

   - Number of NPCs to create
   - Campaign/Exercise name
   - Organizational structure
   - Enclave/Team assignments
5. Click "Generate"
6. NPCs appear in the list and can be assigned to machines

## Generating NPCs via API

**Generate NPCs programmatically:**

```bash
curl -X POST http://localhost:5000/api/npcs/generate \
  -H "Content-Type: application/json" \
  -d '{
    "count": 50,
    "campaign": "Exercise 2024",
    "enclave": "Brigade HQ",
    "team": "Operations"
  }'
```

**Retrieve NPCs:**

```bash
curl http://localhost:5000/api/npcs
```

See the Swagger documentation at `http://localhost:5000/swagger` for complete API details.

## NPC Data Fields

Animator generates extensive data for each NPC. Key fields include:

| Category | Fields |
|----------|--------|
| **Identity** | First name, last name, middle name, gender, date of birth, SSN |
| **Contact** | Email, phone, address, city, state, ZIP, country |
| **Employment** | Employer, job title, department, salary, hire date, employee ID |
| **Financial** | Bank name, account number, routing number, credit score, assets |
| **Education** | Degrees, institutions, graduation dates, major/minor |
| **Family** | Spouse, children, parents, siblings (with details for each) |
| **Background** | Military service, criminal record, security clearance level |
| **Digital** | Social media accounts, online presence, browsing habits |
| **Psychological** | Personality traits, insider threat indicators, vulnerabilities |
| **Metadata** | Campaign, enclave, team, assigned machine |

[^1]: The GHOSTS development team highly recommends Nick Bilton's book *American Kingpin* for insight into the early days of the dark web.
[^2]: A key developer from the Animator team went on to a position in the SEI's AI division. AI models need data. You connect the dots.
