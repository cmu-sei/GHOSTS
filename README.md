<div>

# GHOSTS NPC Framework

**Realistic User Behavior Modeling and Simulation for Cyber/Cognitive Training, Exercises, and Research**

[![Version](https://img.shields.io/badge/version-9.0-green.svg)](https://github.com/cmu-sei/GHOSTS/releases)
[![License](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE.md)
[![.NET](https://img.shields.io/badge/.NET-10.0-purple.svg)](https://dotnet.microsoft.com/)
[![Angular](https://img.shields.io/badge/Angular-20-red.svg)](https://angular.dev/)
[![PRs Welcome](https://img.shields.io/badge/PRs-welcome-brightgreen.svg)](https://github.com/cmu-sei/GHOSTS/pulls)

[Quick Start](https://cmu-sei.github.io/GHOSTS/quickstart/) • [Documentation](https://cmu-sei.github.io/GHOSTS/) • [Issues](https://github.com/cmu-sei/GHOSTS/issues) • [Demo Video](https://www.youtube.com/watch?v=EkwK-cqwjjA)

</div>

---

## Overview

GHOSTS is an NPC (or agent) orchestration framework that models and simulates realistic users on all types of computer systems, generating human-like activity across applications, networks, and workflows. Beyond simple automation, it can dynamically reason, chat, and create content via integrated LLMs, enabling adaptive, context-aware behavior. Designed for cyber training, research, and simulation, it produces realistic network traffic, supports complex multi-agent scenarios, and leaves behind realistic artifacts. Its modular architecture allows the addition of new agents, behaviors, and lightweight clients, making it a flexible platform for high-fidelity simulations.

**Watch a quick demo:** [3-minute introduction on YouTube](https://www.youtube.com/watch?v=EkwK-cqwjjA)

---

### ⭐ Show Your Support

If you find GHOSTS useful for your cyber training, research, or simulation needs, please consider **starring this repository**! Your stars help:
- 🌟 Increase visibility for others in the cybersecurity community
- 💪 Motivate continued development and new features
- 📈 Show that realistic NPC simulation is valuable for cyber operations

[**⭐ Star this repo**](https://github.com/cmu-sei/GHOSTS) • [Watch for updates](https://github.com/cmu-sei/GHOSTS/subscription) • [Share with colleagues](https://github.com/cmu-sei/GHOSTS)

## Quick Start

For detailed installation instructions, platform-specific builds, and configuration options, see the [Quick Start Guide](https://cmu-sei.github.io/GHOSTS/quickstart/).

### Basic Usage

1. **Deploy the API Server** - Use Docker Compose or deploy to your infrastructure
2. **Install Clients** - Deploy GHOSTS clients on Windows or Linux machines
3. **Configure Timelines** - Define activities through the UI or API
4. **Monitor Activity** - View real-time NPC behavior through Grafana dashboards

See the [full documentation](https://cmu-sei.github.io/GHOSTS/) for detailed configuration and usage examples.

---

## Architecture

GHOSTS consists of several integrated components that work together to create a realistic simulation environment:

### Core Components

| Component | Description | Documentation |
|-----------|-------------|---------------|
| **GHOSTS Client** | Cross-platform agent (Windows/Linux) that executes simulated user activities | [Client Docs](https://cmu-sei.github.io/GHOSTS/core/client/) |
| **GHOSTS API** | Central server (.NET 10) managing clients, timelines, and activity orchestration via REST and WebSocket | [API Docs](https://cmu-sei.github.io/GHOSTS/core/api/) |
| **GHOSTS Frontend** | **New!** Modern Angular 20 web interface for managing machines, groups, timelines, NPCs, and scenarios | [NG Docs](src/Ghosts.Frontend/) |
| **GHOSTS UI** | (Deprecated) Next.js-based web interface for managing machines, groups, and deploying timelines | [UI Docs](https://cmu-sei.github.io/GHOSTS/core/ui/) |
| **GHOSTS Lite** | Lightweight client version for resource-constrained environments | [Lite Docs](https://cmu-sei.github.io/GHOSTS/core/lite/) |

### Supporting Services

| Service | Description | Documentation |
|---------|-------------|---------------|
| **Animator** | Generates realistic NPC personas with attributes, relationships, and social networks | [Animator Docs](https://cmu-sei.github.io/GHOSTS/animator/) |
| **Pandora** | Content generation server providing dynamic web content and responses | [Pandora Docs](https://cmu-sei.github.io/GHOSTS/content/pandora/) |
| **Socializer** | Simulated social media platform for realistic social interactions | [Socializer Docs](https://cmu-sei.github.io/GHOSTS/content/social/) |
| **Grafana Integration** | Real-time monitoring and visualization of NPC activities | [Grafana Docs](https://cmu-sei.github.io/GHOSTS/core/grafana/) |

## Use Cases

GHOSTS is designed for various cybersecurity and training scenarios:

- **Cyber Training & Exercises** - Populate training environments with realistic user activity
- **Red Team Operations** - Generate believable background noise during security assessments
- **Blue Team Training** - Create realistic network traffic for detection and analysis practice
- **Research & Development** - Test security tools and detection algorithms with realistic data
- **Cyber Range Development** - Build immersive environments with autonomous NPCs
- **Simulation & Modeling** - Generate realistic network behavior patterns for analysis

## What's New

### Version 9.0 (Currently in Development)

- **🆕 GHOSTS Frontend** - Modern [Angular 19 web interface](src/Ghosts.Frontend/) with enhanced UX and comprehensive search functionality
- **⚡ .NET 10 Upgrade** - Core API and services upgraded to .NET 10 for improved performance and latest features
- **🎯 Enhanced Scenarios** - New scenario planning and tracking capabilities with timeline management
- **🔍 Advanced Search** - Client-side search across machines, groups, timelines, NPCs, and scenarios
- **🎨 Improved UI/UX** - Material Design components, responsive layouts, and streamlined workflows

### Version 8.2

- **New UI** - [Next.js web interface](src/ghosts.ui) for managing machines, groups, and timelines
- **GHOSTS Lite** - [Lightweight client](src/Ghosts.Client.Lite) for resource-constrained environments
- **LLM Integration** - AI-powered content generation (migrate to [RangerAI](https://github.com/cmu-sei/rangerai) for latest AI features)
- **Bug Fixes** - Resolved GUID issues (#385), client path bugs (#384), and animation cancellation issues
- **Documentation Updates** - Enhanced animation documentation

### Version 8.0 Major Changes

> **⚠️ Breaking Changes**: Version 8.0 introduced breaking changes requiring a fresh installation. No upgrade path from previous versions.

**Key Updates:**
- Merged ANIMATOR and SPECTRE into core platform (both now archived)
- Migrated from MongoDB to PostgreSQL for better performance
- WebSocket support for real-time NPC connectivity
- Simplified Docker Compose deployment
- Reorganized API endpoints
- Enhanced timeline configuration with random delays

<details>
<summary>View Version 8.1 Changes</summary>

- GHOSTS LITE beta release
- API cleanup for machine updates and groups
- Simplified JSON object structures
- Improved machine group management
- Enhanced timeline delivery system

</details>

For complete version history, see the [releases page](https://github.com/cmu-sei/GHOSTS/releases).

## Documentation

Comprehensive documentation is available at [cmu-sei.github.io/GHOSTS](https://cmu-sei.github.io/GHOSTS/)

## Contributing

We welcome contributions from the community! Whether it's bug reports, feature requests, documentation improvements, or code contributions, your input helps make GHOSTS better.

### How to Contribute

1. **Report Issues** - Use the [GitHub issue tracker](https://github.com/cmu-sei/GHOSTS/issues) for bugs and feature requests
2. **Submit Pull Requests** - Fork the repository, create a feature branch, and submit a PR
3. **Improve Documentation** - Help enhance guides, examples, and API documentation
4. **Share Use Cases** - Tell us how you're using GHOSTS in your environment

Please ensure your contributions align with our project goals and maintain code quality standards.

## Related Projects

- **[RangerAI](https://github.com/cmu-sei/rangerai)** - Advanced AI integration for GHOSTS (successor to Shadows)
- **ANIMATOR** - Now integrated into GHOSTS core (archived)
- **SPECTRE** - Now integrated into GHOSTS core (archived)

## License

This project is licensed under the MIT License. See [LICENSE.md](LICENSE.md) for full details.

**Distribution Statement**: [DISTRIBUTION STATEMENT A] This material has been approved for public release and unlimited distribution.

Copyright 2017-2025 Carnegie Mellon University. All Rights Reserved.

---

<div>

[Docs](https://cmu-sei.github.io/GHOSTS/) • [GitHub](https://github.com/cmu-sei/GHOSTS) • [Issues](https://github.com/cmu-sei/GHOSTS/issues) • [Releases](https://github.com/cmu-sei/GHOSTS/releases)

</div>
